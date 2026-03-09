using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Komorebi.Commands
{
    /// <summary>
    ///     amend（修正コミット）時のステージング済み変更を取得するクラス。
    ///     git diff-index --cached を使用して、親コミットとの差分を取得する。
    /// </summary>
    public partial class QueryStagedChangesWithAmend : Command
    {
        /// <summary>通常の変更（追加・コピー・削除・変更・タイプ変更）を解析する正規表現</summary>
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{40}) [0-9a-f]{40} ([ACDMT])\d{0,6}\t(.*)$")]
        private static partial Regex REG_FORMAT1();
        /// <summary>リネーム変更を解析する正規表現</summary>
        [GeneratedRegex(@"^:[\d]{6} ([\d]{6}) ([0-9a-f]{40}) [0-9a-f]{40} R\d{0,6}\t(.*\t.*)$")]
        private static partial Regex REG_FORMAT2();

        /// <summary>
        ///     コンストラクタ。親コミットとのステージング差分を取得するコマンドを設定する。
        /// </summary>
        /// <param name="repo">リポジトリのパス</param>
        /// <param name="parent">親コミットのSHA</param>
        public QueryStagedChangesWithAmend(string repo, string parent)
        {
            WorkingDirectory = repo;
            Context = repo;
            // --cached: ステージング済みの変更、-M: リネーム検出
            Args = $"diff-index --cached -M {parent}";
            _parent = parent;
        }

        /// <summary>
        ///     コマンドを同期的に実行し、ステージング済み変更のリストを返す。
        /// </summary>
        /// <returns>変更モデルのリスト</returns>
        public List<Models.Change> GetResult()
        {
            var rs = ReadToEnd();
            if (!rs.IsSuccess)
                return [];

            var changes = new List<Models.Change>();
            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var match = REG_FORMAT2().Match(line);
                if (match.Success)
                {
                    var change = new Models.Change()
                    {
                        Path = match.Groups[3].Value,
                        DataForAmend = new Models.ChangeDataForAmend()
                        {
                            FileMode = match.Groups[1].Value,
                            ObjectHash = match.Groups[2].Value,
                            ParentSHA = _parent,
                        },
                    };
                    change.Set(Models.ChangeState.Renamed);
                    changes.Add(change);
                    continue;
                }

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

                    var type = match.Groups[3].Value;
                    switch (type)
                    {
                        case "A":
                            change.Set(Models.ChangeState.Added);
                            break;
                        case "C":
                            change.Set(Models.ChangeState.Copied);
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

        /// <summary>親コミットのSHA</summary>
        private readonly string _parent;
    }
}
