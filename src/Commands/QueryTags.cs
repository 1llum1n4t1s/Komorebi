using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.Commands;

/// <summary>
///     タグ一覧を取得するクラス。
///     git tag -l を使用して、名前、タイプ、SHA、作成者、日時、メッセージを取得する。
///     レコード区切りにはランダムなバウンダリ文字列を使用する。
/// </summary>
public class QueryTags : Command
{
    /// <summary>
    ///     コンストラクタ。タグ一覧を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    public QueryTags(string repo)
    {
        // タグ間の区切りとして一意なバウンダリ文字列を生成
        _boundary = $"----- BOUNDARY OF TAGS {Guid.NewGuid()} -----";

        Context = repo;
        WorkingDirectory = repo;
        Args = $"tag -l --format=\"{_boundary}%(refname)%00%(objecttype)%00%(objectname)%00%(*objectname)%00%(taggername)±%(taggeremail)%00%(creatordate:unix)%00%(contents:subject)%0a%0a%(contents:body)\"";
    }

    /// <summary>
    ///     コマンドを非同期で実行し、タグのリストを返す。
    /// </summary>
    /// <returns>タグモデルのリスト</returns>
    public async Task<List<Models.Tag>> GetResultAsync()
    {
        var tags = new List<Models.Tag>();
        var rs = await ReadToEndAsync().ConfigureAwait(false);
        if (!rs.IsSuccess)
            return tags;

        // バウンダリ文字列でレコードを分割
        var records = rs.StdOut.Split(_boundary, StringSplitOptions.RemoveEmptyEntries);
        foreach (var record in records)
        {
            // NULLデリミタでフィールドを分割（refname, objecttype, objectname, *objectname, tagger, date, message）
            var subs = record.Split('\0');
            if (subs.Length != 7)
                continue;

            // "refs/tags/" プレフィックス（10文字）を除去
            var name = subs[0][10..];
            // メッセージがタグ名と同一の場合はnullに設定（軽量タグの場合）
            var message = subs[6].Trim();
            if (!string.IsNullOrEmpty(message) && message.Equals(name, StringComparison.Ordinal))
                message = null;

            ulong.TryParse(subs[5], out var creatorDate);

            tags.Add(new Models.Tag()
            {
                Name = name,
                IsAnnotated = subs[1].Equals("tag", StringComparison.Ordinal),
                // 注釈付きタグは*objectname（デリファレンスSHA）を使用、軽量タグはobjectnameを使用
                SHA = string.IsNullOrEmpty(subs[3]) ? subs[2] : subs[3],
                Creator = Models.User.FindOrAdd(subs[4]),
                CreatorDate = creatorDate,
                Message = message,
            });
        }

        return tags;
    }

    /// <summary>タグレコード間の区切り文字列</summary>
    private readonly string _boundary;
}
