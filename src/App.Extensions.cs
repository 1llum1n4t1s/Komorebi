using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Avalonia.Media;

namespace Komorebi;

/// <summary>
/// <see cref="string"/>型に対するユーティリティ拡張メソッド群。
/// git CLIコマンドへの引数渡し等でエスケープ・クォート処理が必要な場面で使用する。
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 文字列をダブルクォートで囲む。内部のダブルクォートはエスケープされる。
    /// git CLIにパス引数を安全に渡すために使用する。
    /// </summary>
    /// <param name="value">クォート対象の文字列</param>
    /// <returns>エスケープ済みダブルクォート付き文字列（例: "foo\"bar"）</returns>
    public static string Quoted(this string value)
    {
        return $"\"{Escaped(value)}\"";
    }

    /// <summary>
    /// 文字列中のダブルクォートをバックスラッシュでエスケープする。
    /// </summary>
    /// <param name="value">エスケープ対象の文字列</param>
    /// <returns>ダブルクォートが \"に置換された文字列</returns>
    public static string Escaped(this string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// ユーザー入力のフォントファミリー名を正規化・検証する。
    /// カンマ区切りの各フォント名について以下の処理を行う:
    /// 1. 前後の空白をトリムし、連続する空白を1つに圧縮する
    /// 2. システムフォントとしてパースを試み、タイプフェースが存在するか確認する
    /// 無効なフォント名は静かに除外される。
    /// </summary>
    /// <param name="input">ユーザーが入力したフォントファミリー名（カンマ区切りで複数指定可）</param>
    /// <returns>検証済みフォント名のカンマ区切り文字列。有効なフォントがない場合は空文字列</returns>
    public static string FormatFontNames(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        // カンマで分割して各フォント名を個別に処理する
        var parts = input.Split(',');
        List<string> trimmed = [];

        foreach (var part in parts)
        {
            // 前後の空白をトリムし、空の要素はスキップする
            var t = part.Trim();
            if (string.IsNullOrEmpty(t))
                continue;

            // 連続する空白文字を1つに圧縮する（例: "Noto  Sans" → "Noto Sans"）
            var sb = new StringBuilder();
            var prevChar = '\0';

            foreach (var c in t)
            {
                if (c == ' ' && prevChar == ' ')
                    continue;  // 連続空白の2文字目以降をスキップする
                sb.Append(c);
                prevChar = c;
            }

            var name = sb.ToString();

            // システムフォントとしてパースを試みる
            try
            {
                var fontFamily = FontFamily.Parse(name);
                // タイプフェース（Regular, Bold等）が1つ以上あれば有効なフォントと判定する
                if (fontFamily.FamilyTypefaces.Count > 0)
                    trimmed.Add(name);
            }
            catch
            {
                // フォントパースの例外は無視する（無効なフォント名として扱う）
            }
        }

        // 有効なフォントが1つ以上あればカンマ区切りで結合して返す
        return trimmed.Count > 0 ? string.Join(',', trimmed) : string.Empty;
    }
}

/// <summary>
/// 一時ファイルのライフサイクルを RAII で管理するスコープ。
/// <see cref="Path.GetTempFileName"/> で取得した一時ファイルを Dispose 時に必ず削除する。
/// 例外発生時のリークを防ぐため、生 <c>try/finally</c> の代わりに使用する:
/// <code>
/// using var temp = new TempFileScope();
/// await DoSomethingWith(temp.Path);
/// // Dispose 時に File.Delete が走る（例外パスでも保証）
/// </code>
/// </summary>
public sealed class TempFileScope : IDisposable
{
    /// <summary>一時ファイルのフルパス。</summary>
    public string Path { get; }

    private bool _disposed;

    /// <summary>新しい一時ファイルを作成する。</summary>
    public TempFileScope()
    {
        Path = System.IO.Path.GetTempFileName();
    }

    /// <summary>一時ファイルを削除する。多重 Dispose は安全に無視される。</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            File.Delete(Path);
        }
        catch
        {
            // 削除失敗は無視する（既に消えている / ロック中 / 権限なし等のケースで例外を伝播させない）
        }
    }
}

/// <summary>
/// <see cref="Commands.Command"/>に対する拡張メソッド群。
/// コマンド実行時のログ出力先を流暢に設定するために使用する。
/// </summary>
public static class CommandExtensions
{
    /// <summary>
    /// コマンドにログ出力先を設定し、メソッドチェーンのためにコマンド自身を返す。
    /// 例: <c>new Fetch(repo).Use(log).ExecAsync()</c>
    /// </summary>
    /// <typeparam name="T">Commandの派生型</typeparam>
    /// <param name="cmd">ログを設定するコマンドインスタンス</param>
    /// <param name="log">出力先のコマンドログインターフェース</param>
    /// <returns>ログが設定されたコマンドインスタンス（引数と同一参照）</returns>
    public static T Use<T>(this T cmd, Models.ICommandLog log) where T : Commands.Command
    {
        cmd.Log = log;
        return cmd;
    }
}

/// <summary>
/// <see cref="DirectoryInfo"/>に対する拡張メソッド群。
/// ディレクトリツリーの再帰的なファイル走査に使用する。
/// </summary>
public static class DirectoryInfoExtension
{
    /// <summary>
    /// ディレクトリ配下のファイルを再帰的に走査し、各ファイルパスに対してコールバックを実行する。
    /// 隠しディレクトリ（.で始まる）とnode_modulesはスキップされる。
    /// アクセス不能なファイル/ディレクトリは無視される。
    /// </summary>
    /// <param name="dir">走査開始ディレクトリ</param>
    /// <param name="onFile">各ファイルのフルパスを受け取るコールバック</param>
    /// <param name="maxDepth">再帰の最大深度（デフォルト: 4）。0でサブディレクトリに入らない</param>
    public static void WalkFiles(this DirectoryInfo dir, Action<string> onFile, int maxDepth = 4)
    {
        try
        {
            // アクセス不能ファイルを無視し、手動で再帰制御するためサブディレクトリ探索はオフ
            var options = new EnumerationOptions()
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };

            // 現在のディレクトリ直下の全ファイルに対してコールバックを実行
            foreach (var file in dir.GetFiles("*", options))
                onFile(file.FullName);

            // 深度が残っていればサブディレクトリへ再帰
            if (maxDepth > 0)
            {
                foreach (var subDir in dir.GetDirectories("*", options))
                {
                    // .git等の隠しディレクトリとnode_modulesは走査対象外
                    if (subDir.Name.StartsWith(".", StringComparison.Ordinal) ||
                        subDir.Name.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                        continue;

                    WalkFiles(subDir, onFile, maxDepth - 1);
                }
            }
        }
        catch
        {
            // Ignore exceptions.
        }
    }
}
