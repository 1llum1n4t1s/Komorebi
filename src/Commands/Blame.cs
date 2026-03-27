using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     git blameコマンドを実行し、ファイルの各行の変更履歴（著者・コミット情報）を取得するクラス。
/// </summary>
public partial class Blame : Command
{
    /// <summary>
    ///     blame出力行を解析するための正規表現パターン。
    ///     コミットSHA、ファイル名、著者名、タイムスタンプ、行内容を抽出する。
    /// </summary>
    [GeneratedRegex(@"^\^?([0-9a-f]+)\s+(.*)\s+\((.*)\s+(\d+)\s+[\-\+]?\d+\s+\d+\) (.*)")]
    private static partial Regex REG_FORMAT();

    /// <summary>
    ///     Blameコマンドのコンストラクタ。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="file">対象ファイルのパス</param>
    /// <param name="revision">対象リビジョン</param>
    /// <param name="ignoreWhitespace">空白の変更を無視するかどうか</param>
    public Blame(string repo, string file, string revision, bool ignoreWhitespace)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;

        var builder = new StringBuilder();
        // -f: ファイル名を表示、-t: タイムスタンプをUnix形式で表示
        builder.Append("blame -f -t ");
        if (ignoreWhitespace)
            builder.Append("-w ");
        builder.Append(revision).Append(" -- ").Append(file.Quoted());

        Args = builder.ToString();
    }

    /// <summary>
    ///     blameコマンドを非同期で実行し、結果を解析して返す。
    /// </summary>
    /// <returns>blame解析結果データ</returns>
    public async Task<Models.BlameData> ReadAsync()
    {
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return new Models.BlameData();

        return ParseBlameOutput(rs.StdOut);
    }

    /// <summary>
    ///     blame出力文字列を解析して、BlameDataモデルに変換する。
    /// </summary>
    /// <param name="output">git blameの標準出力</param>
    /// <returns>解析されたblameデータ</returns>
    internal static Models.BlameData ParseBlameOutput(string output)
    {
        var result = new Models.BlameData();
        var content = new StringBuilder();
        var lastSHA = string.Empty;
        var needUnifyCommitSHA = false;
        var minSHALen = 64;

        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // ヌル文字を含む場合はバイナリファイルと判定
            if (line.Contains('\0'))
            {
                result.IsBinary = true;
                result.LineInfos.Clear();
                break;
            }

            var match = REG_FORMAT().Match(line);
            if (!match.Success)
                continue;

            // 行の内容部分を蓄積
            content.AppendLine(match.Groups[5].Value);

            // 正規表現グループからblame情報を抽出
            var commit = match.Groups[1].Value;
            var file = match.Groups[2].Value.Trim();
            var author = match.Groups[3].Value;
            var timestamp = ulong.Parse(match.Groups[4].Value);

            var info = new Models.BlameLineInfo()
            {
                IsFirstInGroup = commit != lastSHA,
                CommitSHA = commit,
                File = file,
                Author = author,
                Timestamp = timestamp,
            };

            result.LineInfos.Add(info);
            lastSHA = commit;

            // '^'で始まる行は境界コミット。SHA長を統一する必要がある
            if (line[0] == '^')
            {
                needUnifyCommitSHA = true;
                minSHALen = Math.Min(minSHALen, commit.Length);
            }
        }

        // 境界コミットが存在する場合、すべてのSHAを最短長に揃える
        if (needUnifyCommitSHA)
        {
            foreach (var line in result.LineInfos)
            {
                if (line.CommitSHA.Length > minSHALen)
                    line.CommitSHA = line.CommitSHA[..minSHALen];
            }
        }

        result.Content = content.ToString();
        return result;
    }
}
