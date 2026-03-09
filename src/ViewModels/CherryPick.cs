using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     チェリーピックダイアログのViewModel。
    ///     git cherry-pickコマンドで指定コミットの変更を現在のブランチに適用する。
    ///     マージコミットのチェリーピックにも対応する。
    /// </summary>
    public class CherryPick : Popup
    {
        /// <summary>
        ///     チェリーピック対象のコミットリスト。
        /// </summary>
        public List<Models.Commit> Targets
        {
            get;
            private set;
        }

        /// <summary>
        ///     対象がマージコミットかどうかのフラグ。
        /// </summary>
        public bool IsMergeCommit
        {
            get;
            private set;
        }

        /// <summary>
        ///     マージコミットの親コミットリスト。マージコミットの場合にメインラインを選択するために使用する。
        /// </summary>
        public List<Models.Commit> ParentsForMergeCommit
        {
            get;
            private set;
        }

        /// <summary>
        ///     マージコミットのメインライン番号（0始まり）。
        /// </summary>
        public int MainlineForMergeCommit
        {
            get;
            set;
        }

        /// <summary>
        ///     コミットメッセージにソース情報を追記するかどうか。
        /// </summary>
        public bool AppendSourceToMessage
        {
            get;
            set;
        }

        /// <summary>
        ///     チェリーピック後に自動コミットするかどうか。
        /// </summary>
        public bool AutoCommit
        {
            get;
            set;
        }

        /// <summary>
        ///     通常コミットのチェリーピック用コンストラクタ。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="targets">チェリーピック対象のコミットリスト</param>
        public CherryPick(Repository repo, List<Models.Commit> targets)
        {
            _repo = repo;
            Targets = targets;
            IsMergeCommit = false;
            ParentsForMergeCommit = [];
            MainlineForMergeCommit = 0;
            AppendSourceToMessage = true;
            AutoCommit = true;
        }

        /// <summary>
        ///     マージコミットのチェリーピック用コンストラクタ。
        /// </summary>
        /// <param name="repo">対象のリポジトリViewModel</param>
        /// <param name="merge">マージコミット</param>
        /// <param name="parents">マージコミットの親コミットリスト</param>
        public CherryPick(Repository repo, Models.Commit merge, List<Models.Commit> parents)
        {
            _repo = repo;
            Targets = [merge];
            IsMergeCommit = true;
            ParentsForMergeCommit = parents;
            MainlineForMergeCommit = 0;
            AppendSourceToMessage = true;
            AutoCommit = true;
        }

        /// <summary>
        ///     確定処理。git cherry-pickコマンドを実行する。
        ///     マージコミットの場合は-mオプション付きで実行する。
        /// </summary>
        /// <returns>常にtrue</returns>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            // コミットメッセージをクリアする（チェリーピック後のコンフリクト解決用）
            _repo.ClearCommitMessage();
            ProgressDescription = "Cherry-Pick commit(s) ...";

            var log = _repo.CreateLog("Cherry-Pick");
            Use(log);

            if (IsMergeCommit)
            {
                // マージコミットの場合は-mオプションでメインラインを指定してチェリーピックする
                await new Commands.CherryPick(
                    _repo.FullPath,
                    Targets[0].SHA,
                    !AutoCommit,
                    AppendSourceToMessage,
                    $"-m {MainlineForMergeCommit + 1}")
                    .Use(log)
                    .ExecAsync();
            }
            else
            {
                // 通常コミットの場合は全対象SHAを結合してチェリーピックする
                await new Commands.CherryPick(
                    _repo.FullPath,
                    string.Join(' ', Targets.ConvertAll(c => c.SHA)),
                    !AutoCommit,
                    AppendSourceToMessage,
                    string.Empty)
                    .Use(log)
                    .ExecAsync();
            }

            log.Complete();
            return true;
        }

        /// <summary>対象リポジトリへの参照</summary>
        private readonly Repository _repo = null;
    }
}
