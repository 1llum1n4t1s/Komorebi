using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;

namespace Komorebi.Models;

/// <summary>
/// 「SSH 秘密鍵のパーミッションが緩すぎる」エラー通知の自動修正（所有者のみ読み書き = 600）を担う。
///
/// 旧実装は stderr から抜き出したパスをそのまま外部 chmod コマンドへ渡していたため、
/// (a) 細工されたエラー文で任意のファイル・ディレクトリの権限を変えうる、(b) chmod の終了コードを見ずに成功通知していた。
/// ここでは対象を「通常ファイル（ディレクトリ・シンボリックリンクでない）で、~/.ssh 配下または設定済みの鍵、
/// かつ基準ディレクトリから鍵までの途中にリンク・ジャンクションが無い」ものに限定し、
/// 判定で確定したフルパスに対して .NET の File.SetUnixFileMode で直接設定して結果を返す。
///
/// 残存リスク（TOCTOU）: 判定してから chmod するまでの間に、同じ利用者権限でファイルやディレクトリを
/// 差し替えられると別のファイルの権限を変えうる。.NET には openat/O_NOFOLLOW 相当で各階層を辿る API が無く、
/// この窓は完全には閉じられない。ハンドル経由の設定（fchmod）で「開いた後」の差し替えだけは防ぐが、
/// 判定から open までの窓は残る。対象は細工された stderr 文字列による誘導の防止であり、
/// ファイルシステムを同時に書き換えられる同一権限の攻撃者は対象外とする。
/// </summary>
public static class SSHKeyPermissionFixer
{
    /// <summary>
    /// 自動修正してよい鍵ファイルか判定する（OS 非依存の純粋判定。テスト可能にするため設定値は引数で受ける）。
    /// </summary>
    public static bool IsFixableKeyFile(string keyPath, string sshDirectory, IEnumerable<string> configuredKeys)
        => TryGetFixableKeyFile(keyPath, sshDirectory, configuredKeys, out _);

    /// <summary>
    /// 自動修正してよい鍵ファイルか判定し、chmod に使う検証済みのフルパスを返す。
    /// 呼び出し側はエラー文の元のパスではなく、この verifiedPath を <see cref="TrySetOwnerReadWriteOnly"/> に渡す。
    /// </summary>
    /// <param name="keyPath">エラー文から抽出した鍵のパス</param>
    /// <param name="sshDirectory">利用者の ~/.ssh（空なら判定に使わない）</param>
    /// <param name="configuredKeys">Komorebi に設定済みの鍵パス（グローバル・リポジトリローカルの remote.*.sshkey）</param>
    /// <param name="verifiedPath">判定で確定したフルパス（false のときは空）</param>
    public static bool TryGetFixableKeyFile(string keyPath, string sshDirectory, IEnumerable<string> configuredKeys, out string verifiedPath)
    {
        verifiedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(keyPath))
            return false;

        string full;
        try
        {
            full = Path.GetFullPath(keyPath);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        // ネットワーク・デバイスパスは対象外（存在確認でも外部へ接続するため、ファイルシステムに触れる前に除外する）
        if (PathContainment.IsUncOrDevicePath(full))
            return false;

        // 通常ファイルに限る。シンボリックリンクはリンク先（任意のファイル）の権限を変えてしまうので対象外。
        var info = new FileInfo(full);
        if (!info.Exists || info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            return false;

        // (1) ~/.ssh 配下: ~/.ssh（含まない）から鍵までの途中にリンク・ジャンクションが無いこと
        if (!string.IsNullOrWhiteSpace(sshDirectory) && TryGetUnder(full, sshDirectory, out var underSsh))
        {
            verifiedPath = underSsh;
            return true;
        }

        // (2) 設定済みの鍵と一致: 利用者のホーム（鍵がホーム配下の場合）またはドライブ・ファイルシステムのルート
        // （含まない）から鍵までの途中にリンク・ジャンクションが無いこと。リポジトリローカルの remote.*.sshkey は
        // リポジトリ側から設定され得るため、一致したというだけでリンク経由の誘導を許さない。
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var configured in configuredKeys)
        {
            if (string.IsNullOrWhiteSpace(configured))
                continue;

            try
            {
                if (!string.Equals(Path.GetFullPath(configured), full, comparison))
                    continue;
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                // 不正な設定値は一致しないものとして扱う
                continue;
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var boundary = !string.IsNullOrEmpty(home) && PathContainment.IsSameOrUnder(full, Path.TrimEndingDirectorySeparator(Path.GetFullPath(home)))
                ? home
                : Path.GetPathRoot(full);
            if (!string.IsNullOrEmpty(boundary) && PathContainment.HasNoReparsePointBelow(boundary, full))
            {
                verifiedPath = full;
                return true;
            }

            return false;
        }

        return false;
    }

    /// <summary>
    /// full が sshDirectory の配下（文字列）で、途中にリンク・ジャンクションが無ければ、chmod に使うパスを返す。
    /// ~/.ssh 自体が dotfiles 管理などでリンクになっている場合は、その実体の配下として同じ相対パスのファイルを返す
    /// （途中にリンクが無いことは確認済みなので、実体側のパスは常に実体の ~/.ssh 配下になる。旧実装の再判定は冗長だった）。
    /// </summary>
    private static bool TryGetUnder(string full, string sshDirectory, out string verifiedPath)
    {
        verifiedPath = string.Empty;
        try
        {
            var sshFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sshDirectory));
            if (PathContainment.IsUncOrDevicePath(sshFull) ||
                !PathContainment.IsSameOrUnder(full, sshFull) ||
                !PathContainment.HasNoReparsePointBelow(sshFull, full))
                return false;

            var sshInfo = new DirectoryInfo(sshFull);
            if (sshInfo.LinkTarget is null)
            {
                verifiedPath = full;
                return true;
            }

            var realSsh = sshInfo.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            if (string.IsNullOrEmpty(realSsh))
                return false;

            var realFull = Path.Combine(realSsh, Path.GetRelativePath(sshFull, full));
            var realInfo = new FileInfo(realFull);
            if (!realInfo.Exists || realInfo.LinkTarget is not null || realInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return false;

            verifiedPath = realInfo.FullName;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 判定できないものは修正対象にしない
            return false;
        }
    }

    /// <summary>
    /// 鍵ファイルを所有者のみ読み書き可能（0600）にする。<see cref="TryGetFixableKeyFile"/> の verifiedPath を渡すこと。
    /// ファイルを開いたハンドル経由（fchmod）で設定し、開いた後の差し替えの影響を受けないようにする
    /// （判定から open までの窓は残る。クラス説明の残存リスクを参照）。
    /// Windows では Unix ファイルモードが無いため呼べない（CA1416 を呼び出し側の OS ガードと属性で明示する）。
    /// </summary>
    /// <returns>成功なら null、失敗なら例外メッセージ</returns>
    [UnsupportedOSPlatform("windows")]
    public static string? TrySetOwnerReadWriteOnly(string verifiedPath)
    {
        try
        {
            using var handle = File.OpenHandle(verifiedPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            File.SetUnixFileMode(handle, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return null;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }
}
