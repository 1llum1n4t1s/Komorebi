using System;
using System.IO;

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
