using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Komorebi.Commands
{
    /// <summary>
    ///     2つのリビジョン間の変更ファイル一覧を取得するgitコマンド。
    ///     git diff --name-status を実行し、変更の種類（追加、削除、変更等）とファイルパスを返す。
    /// </summary>
    public partial class CompareRevisions : Command
    {
        /// <summary>
        ///     変更状態（M, A, D, C）とファイルパスにマッチする正規表現。
        /// </summary>
        [GeneratedRegex(@"^([MADC])\s+(.+)$")]
        private static partial Regex REG_FORMAT();

        /// <summary>
        ///     リネーム状態（R + 類似度）とファイルパスにマッチする正規表現。
        /// </summary>
        [GeneratedRegex(@"^R[0-9]{0,4}\s+(.+)$")]
        private static partial Regex REG_RENAME_FORMAT();

        /// <summary>
        ///     2つのリビジョン間の変更を比較するコンストラクタ。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="start">比較開始リビジョン。空の場合はインデックスとの比較（-R）。</param>
        /// <param name="end">比較終了リビジョン。</param>
        public CompareRevisions(string repo, string start, string end)
        {
            WorkingDirectory = repo;
            Context = repo;

            // startが空の場合は-Rオプションでインデックスとの逆方向比較を行う
            var based = string.IsNullOrEmpty(start) ? "-R" : start;

            // git diff --name-status: 変更の種類とファイル名のみを表示する
            Args = $"diff --name-status {based} {end}";
        }

        /// <summary>
        ///     2つのリビジョン間の特定パス配下の変更を比較するコンストラクタ。
        /// </summary>
        /// <param name="repo">リポジトリの作業ディレクトリパス。</param>
        /// <param name="start">比較開始リビジョン。空の場合はインデックスとの比較（-R）。</param>
        /// <param name="end">比較終了リビジョン。</param>
        /// <param name="path">比較対象のパス（ディレクトリまたはファイル）。</param>
        public CompareRevisions(string repo, string start, string end, string path)
        {
            WorkingDirectory = repo;
            Context = repo;

            var based = string.IsNullOrEmpty(start) ? "-R" : start;

            // -- <path>: 特定パス配下の変更のみに限定する
            Args = $"diff --name-status {based} {end} -- {path.Quoted()}";
        }

        /// <summary>
        ///     比較結果を非同期で読み取り、変更リストとして返す。
        /// </summary>
        /// <returns>変更情報のリスト。</returns>
        public async Task<List<Models.Change>> ReadAsync()
        {
            var changes = new List<Models.Change>();
            try
            {
                // gitプロセスを作成して起動する
                using var proc = new Process();
                proc.StartInfo = CreateGitStartInfo(true);
                proc.Start();

                // 出力を1行ずつ読み取り、変更情報をパースする
                while (await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    // 通常の変更パターン（M, A, D, C）にマッチするか確認する
                    var match = REG_FORMAT().Match(line);
                    if (!match.Success)
                    {
                        // リネームパターン（R + 類似度）にマッチするか確認する
                        match = REG_RENAME_FORMAT().Match(line);
                        if (match.Success)
                        {
                            var renamed = new Models.Change() { Path = match.Groups[1].Value };
                            renamed.Set(Models.ChangeState.Renamed);
                            changes.Add(renamed);
                        }

                        continue;
                    }

                    var change = new Models.Change() { Path = match.Groups[2].Value };
                    var status = match.Groups[1].Value;

                    // ステータス文字に応じて変更の種類を設定する
                    switch (status[0])
                    {
                        case 'M':
                            change.Set(Models.ChangeState.Modified);
                            changes.Add(change);
                            break;
                        case 'A':
                            change.Set(Models.ChangeState.Added);
                            changes.Add(change);
                            break;
                        case 'D':
                            change.Set(Models.ChangeState.Deleted);
                            changes.Add(change);
                            break;
                        case 'C':
                            change.Set(Models.ChangeState.Copied);
                            changes.Add(change);
                            break;
                    }
                }

                await proc.WaitForExitAsync().ConfigureAwait(false);

                // 変更リストをパスの数値ソートで並べ替える
                changes.Sort((l, r) => Models.NumericSort.Compare(l.Path, r.Path));
            }
            catch
            {
                //ignore changes;
            }

            return changes;
        }
    }
}
