using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Komorebi.Commands;

/// <summary>
/// amend時にステージング済み変更のファイルモード・オブジェクトハッシュを含む詳細情報を取得するクラス。
/// git diff-index --cached -M を使用する。取得した情報はUpdateIndexInfoで復元に使用される。
/// </summary>
public partial class QueryStagedChangesWithAmend : Command
{
    /// <summary>追加・削除・変更・タイプ変更（A/D/M/T）のdiff-index出力行を解析する正規表現</summary>
    [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{40}) [0-9a-f]{40} ([ADMT])\d{0,6}\t(.*)$")]
    private static partial Regex REG_FORMAT1();
    /// <summary>リネーム・コピー（R/C）のdiff-index出力行を解析する正規表現（タブ区切りで旧パスと新パスを含む）</summary>
    [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{40}) [0-9a-f]{40} ([RC])\d{0,6}\t(.*\t.*)$")]
    private static partial Regex REG_FORMAT2();

    /// <summary>
    /// コンストラクタ。親コミットとの差分からステージング済み変更の詳細を取得するコマンドを設定する。
    /// </summary>
    /// <param name="repo">リポジトリのパス</param>
    /// <param name="parent">比較対象の親コミットSHA</param>
    public QueryStagedChangesWithAmend(string repo, string parent)
    {
        WorkingDirectory = repo;
        Context = repo;
        Args = $"diff-index --cached -M {parent}";
        _parent = parent;
    }

    /// <summary>
    /// コマンドを同期的に実行し、amend用データ付きの変更リストを返す。
    /// </summary>
    /// <returns>ファイルモード・オブジェクトハッシュ付きの変更モデルリスト</returns>
    public List<Models.Change> GetResult()
    {
        var rs = ReadToEnd();
        if (!rs.IsSuccess)
            return [];

        var changes = new List<Models.Change>();
        var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            // まずリネーム/コピー用の正規表現で試行（タブ区切りパスを含む）
            var match = REG_FORMAT2().Match(line);
            if (match.Success)
            {
                var change = new Models.Change()
                {
                    Path = match.Groups[4].Value,
                    DataForAmend = new Models.ChangeDataForAmend()
                    {
                        FileMode = match.Groups[1].Value,
                        ObjectHash = match.Groups[2].Value,
                        ParentSHA = _parent,
                    },
                };
                var type = match.Groups[3].Value;
                change.Set(type == "R" ? Models.ChangeState.Renamed : Models.ChangeState.Copied);
                changes.Add(change);
                continue;
            }

            // 通常の変更（A/D/M/T）用の正規表現で試行
            match = REG_FORMAT1().Match(line);
            if (match.Success)
            {
                var change = new Models.Change()
                {
                    Path = match.Groups[4].Value,
                    DataForAmend = new Models.ChangeDataForAmend()
                    {
                        FileMode = match.Groups[1].Value,
                        ObjectHash = match.Groups[2].Value,
                        ParentSHA = _parent,
                    },
                };

                // 変更種別に応じてChangeStateを設定
                var type = match.Groups[3].Value;
                switch (type)
                {
                    case "A":
                        change.Set(Models.ChangeState.Added);
                        break;
                    case "D":
                        change.Set(Models.ChangeState.Deleted);
                        break;
                    case "M":
                        change.Set(Models.ChangeState.Modified);
                        break;
                    case "T":
                        change.Set(Models.ChangeState.TypeChanged);
                        break;
                }
                changes.Add(change);
            }
        }

        return changes;
    }

    /// <summary>比較対象の親コミットSHA</summary>
    private readonly string _parent;
}
