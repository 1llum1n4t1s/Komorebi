using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
/// スタッシュ一覧を取得するクラス。
/// git stash list を使用して、SHA、親、タイムスタンプ、名前、メッセージを取得する。
/// </summary>
public class QueryStashes : Command
{
    /// <summary>
    /// コンストラクタ。スタッシュ一覧を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryStashes(string repo)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = "stash list -z --no-show-signature --format=\"%H%n%P%n%ct%n%gd%n%B\"";
    }

    /// <summary>
    /// コマンドを非同期で実行し、スタッシュのリストを返す。
    /// </summary>
    /// <returns>スタッシュモデルのリスト</returns>
    public async Task<List<Models.Stash>> GetResultAsync()
    {
        var outs = new List<Models.Stash>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return outs;

        // NULLデリミタでレコードを分割
        var items = rs.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in items)
        {
            var current = new Models.Stash();

            // 改行で区切られた各フィールドを順番に解析
            var nextPartIdx = 0;
            var start = 0;
            var end = item.IndexOf('\n', start);
            while (end > 0 && nextPartIdx < 4)
            {
                var line = item[start..end];

                switch (nextPartIdx)
                {
                    case 0: // SHA
                        current.SHA = line;
                        break;
                    case 1: // 親コミット（スペース区切り）
                        if (line.Length > 6)
                            current.Parents.AddRange(line.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        break;
                    case 2: // コミッタータイムスタンプ
                        current.Time = ulong.Parse(line);
                        break;
                    case 3: // スタッシュ名（例: stash@{0}）
                        current.Name = line;
                        break;
                }

                nextPartIdx++;

                start = end + 1;
                if (start >= item.Length - 1)
                    break;

                end = item.IndexOf('\n', start);
            }

            // 残りの部分はメッセージ本文
            if (start < item.Length)
                current.Message = item[start..];

            outs.Add(current);
        }
        return outs;
    }
}
