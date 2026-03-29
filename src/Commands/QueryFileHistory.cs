using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定ファイルの変更履歴（バージョン一覧）を取得するクラス。
/// git log --follow --name-status を使用して、リネームを追跡しながら履歴を取得する。
/// </summary>
public class QueryFileHistory : Command
{
    /// <summary>
    /// コンストラクタ。指定ファイルの変更履歴を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="path">対象ファイルのパス</param>
    /// <param name="head">検索開始のリビジョン（空の場合はHEADから）</param>
    public QueryFileHistory(string repo, string path, string head)
    {
        WorkingDirectory = repo;
        Context = repo;
        RaiseError = false;

        // --follow: リネームを追跡する
        // --name-status: 各コミットでの変更種別（M/A/D/R）とパスを出力する
        // -n 10000: 最大10000件まで取得する
        var builder = new StringBuilder();
        builder.Append("log --no-show-signature --date-order -n 10000 --decorate=no --format=\"@%H%x00%P%x00%aN±%aE%x00%at%x00%s\" --follow --name-status ");
        if (!string.IsNullOrEmpty(head))
            builder.Append(head).Append(" ");
        builder.Append("-- ").Append(path.Quoted());

        Args = builder.ToString();
    }

    /// <summary>
    /// コマンドを非同期で実行し、ファイルバージョンのリストを返す。
    /// </summary>
    /// <returns>ファイルバージョンモデルのリスト</returns>
    public async Task<List<Models.FileVersion>> GetResultAsync()
    {
        var versions = new List<Models.FileVersion>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return versions;

        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return versions;

        Models.FileVersion last = null;
        foreach (var line in lines)
        {
            // '@' で始まる行はコミット情報行（SHA、親、著者、タイムスタンプ、件名）
            if (line.StartsWith('@'))
            {
                var parts = line.Split('\0');
                if (parts.Length != 5)
                    continue;

                last = new Models.FileVersion();
                last.SHA = parts[0][1..];
                last.HasParent = !string.IsNullOrEmpty(parts[1]);
                last.Author = Models.User.FindOrAdd(parts[2]);
                last.AuthorTime = ulong.Parse(parts[3]);
                last.Subject = parts[4];
                versions.Add(last);
            }
            else if (last is not null)
            {
                // name-status行: 基底クラスの共通パーサーを使用（旧: 25行の重複ロジック）
                var parsed = ParseNameStatusLine(line);
                if (parsed is null)
                    continue;

                last.Change.Path = parsed.Value.path;
                last.Change.Set(parsed.Value.state);
            }
        }

        return versions;
    }
}
