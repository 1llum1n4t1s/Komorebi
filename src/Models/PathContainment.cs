using System;
using System.IO;

namespace Komorebi.Models;

/// <summary>
/// 「あるパスが、信頼できる基準ディレクトリの配下にあるか」を判定する共通処理。
/// safe.directory の自動追加（<see cref="SafeDirectories"/>）と SSH 鍵パーミッションの自動修正
/// （<see cref="SSHKeyPermissionFixer"/>）の両方で使う。
///
/// どちらも git の stderr（リポジトリ名などで細工できる文字列）から得たパスを扱うため、次の順で判定する。
/// <list type="number">
/// <item>文字列だけの判定（UNC・デバイスパスの拒否、配下かどうか）を先に済ませ、ファイルシステムに触れない。
/// Windows で <c>\\host\share</c> 形式のパスに Directory.Exists 等を呼ぶと、context 外の SMB サーバーへ
/// 接続しにいき（NTLM 資格情報の送出・タイムアウトによる UI フリーズ）かねないため。</item>
/// <item>文字列で配下と判定できたものだけ、基準ディレクトリから対象までの各階層に reparse point
/// （シンボリックリンク・ジャンクション）が無いことを実ファイルシステムで確かめる。文字列上は配下でも、
/// 途中のリンクを経由すると実体は基準の外にあり得るため。</item>
/// </list>
/// 判定と、その後の操作（git config・chmod）の間にパスが差し替えられる窓（TOCTOU）は .NET の API では
/// 完全には閉じられない（openat/O_NOFOLLOW 相当で各階層を辿る手段が無い）。この処理は、細工された
/// 文字列による誘導を防ぐためのもので、同じ利用者権限でファイルシステムを同時に書き換えられる攻撃者は対象外。
/// </summary>
public static class PathContainment
{
    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// UNC（<c>\\host\share</c>、<c>//host/share</c>）またはデバイスパス（<c>\\?\</c>、<c>\\.\</c>）か。
    /// 文字列だけで判定し、ファイルシステムには触れない。区切り文字は '\' と '/' の両方を受け付ける。
    /// </summary>
    public static bool IsUncOrDevicePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (path.StartsWith("%(prefix)/", StringComparison.Ordinal))
            path = path[10..];

        return path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]);
    }

    /// <summary>
    /// 正規化済み（フルパス・末尾区切りなし）の path が root と同一か root 配下かを文字列だけで判定する。
    /// 区切りは '\' と '/' のどちらでもよい。名前の前方一致（/repo と /repo-other）は配下とみなさない。
    /// </summary>
    public static bool IsSameOrUnder(string path, string root)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(root))
            return false;

        var p = path.Replace('\\', '/');
        var r = root.Replace('\\', '/');
        if (p.Equals(r, Comparison))
            return true;

        var prefix = r.EndsWith('/') ? r : r + "/";
        return p.StartsWith(prefix, Comparison);
    }

    /// <summary>
    /// root（含まない）から path（含む）までの各要素が実在し、reparse point（シンボリックリンク・ジャンクション）で
    /// ないことを確かめる。path が root と同一なら確認する要素は無いので true。
    /// 呼び出し前に <see cref="IsSameOrUnder"/> で文字列上の包含を確認しておくこと（ここでは配下でなければ false）。
    /// </summary>
    public static bool HasNoReparsePointBelow(string root, string path)
    {
        try
        {
            var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            if (!IsSameOrUnder(current, fullRoot))
                return false;

            while (!IsSameOrUnder(fullRoot, current))
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (!info.Exists || info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    return false;

                var parent = Path.GetDirectoryName(current);
                // 基準に到達せずにルートまで来た（文字列判定と食い違う異常系）
                if (string.IsNullOrEmpty(parent) || parent.Length >= current.Length)
                    return false;
                current = Path.TrimEndingDirectorySeparator(parent);
            }

            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // 判定できないものは配下とみなさない
            return false;
        }
    }

    private static bool IsSeparator(char c) => c is '\\' or '/';
}
