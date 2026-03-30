using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// 指定リビジョンのツリーオブジェクト（ファイル・ディレクトリ）を取得するクラス。
/// git ls-tree を使用する。
/// </summary>
public partial class QueryRevisionObjects : Command
{
    /// <summary>
    /// ls-tree出力からオブジェクトタイプ、SHA、パスを抽出する正規表現。
    /// </summary>
    [GeneratedRegex(@"^\d+\s+(\w+)\s+([0-9a-f]+)\s+(.*)$")]
    private static partial Regex REG_FORMAT();

    /// <summary>
    /// コンストラクタ。指定リビジョンのツリーオブジェクトを取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="sha">対象リビジョンのSHA</param>
    /// <param name="parentFolder">フィルタする親フォルダ（空の場合はルート）</param>
    public QueryRevisionObjects(string repo, string sha, string parentFolder)
    {
        WorkingDirectory = repo;
        Context = repo;

        var builder = new StringBuilder(1024);
        builder.Append("ls-tree ").Append(sha);
        if (!string.IsNullOrEmpty(parentFolder))
            builder.Append(" -- ").Append(parentFolder.Quoted());

        Args = builder.ToString();
    }

    /// <summary>
    /// コマンドを非同期で実行し、ツリーオブジェクトのリストを返す。
    /// </summary>
    /// <returns>オブジェクトモデル（blob/tree/tag/commit）のリスト</returns>
    public async Task<List<Models.Object>> GetResultAsync()
    {
        var outs = new List<Models.Object>();

        try
        {
            using var proc = new Process();
            proc.StartInfo = CreateGitStartInfo(true);
            proc.Start();

            while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                var match = REG_FORMAT().Match(line);
                if (!match.Success)
                    continue;

                var obj = new Models.Object();
                obj.SHA = match.Groups[2].Value;
                obj.Type = Models.ObjectType.Blob;
                obj.Path = match.Groups[3].Value;

                obj.Type = match.Groups[1].Value switch
                {
                    "blob" => Models.ObjectType.Blob,
                    "tree" => Models.ObjectType.Tree,
                    "tag" => Models.ObjectType.Tag,
                    "commit" => Models.ObjectType.Commit,
                    _ => obj.Type,
                };

                outs.Add(obj);
            }

            await proc.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            // Ignore exceptions.
        }

        return outs;
    }
}
