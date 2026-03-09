using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// 複数のブランチまたはコミットを一度にマージするダイアログのViewModel。
    /// Octopusマージなどのマルチヘッドマージ戦略に対応する。
    /// </summary>
    public class MergeMultiple : Popup
    {
        /// <summary>
        /// マージ対象のリスト（ブランチまたはコミット）。
        /// </summary>
        public List<object> Targets
        {
            get;
        } = [];

        /// <summary>
        /// マージ後に自動コミットするかどうか。
        /// </summary>
        public bool AutoCommit
        {
            get;
            set;
        }

        /// <summary>
        /// 使用するマージ戦略（Octopus等）。
        /// </summary>
        public Models.MergeStrategy Strategy
        {
            get;
            set;
        }

        /// <summary>
        /// 複数コミットをマージ対象として初期化するコンストラクタ。
        /// </summary>
        public MergeMultiple(Repository repo, List<Models.Commit> commits)
        {
            _repo = repo;
            Targets.AddRange(commits);
            AutoCommit = true;
            Strategy = Models.MergeStrategy.ForMultiple[0];
        }

        /// <summary>
        /// 複数ブランチをマージ対象として初期化するコンストラクタ。
        /// </summary>
        public MergeMultiple(Repository repo, List<Models.Branch> branches)
        {
            _repo = repo;
            Targets.AddRange(branches);
            AutoCommit = true;
            Strategy = Models.MergeStrategy.ForMultiple[0];
        }

        /// <summary>
        /// 複数ヘッドのマージを実行する。
        /// マージ対象を文字列リストに変換し、指定された戦略でgit mergeを実行する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            _repo.ClearCommitMessage();
            ProgressDescription = "Merge head(s) ...";

            var log = _repo.CreateLog("Merge Multiple Heads");
            Use(log);

            // マージ対象を文字列リストに変換してマージコマンドを実行
            await new Commands.Merge(
                _repo.FullPath,
                ConvertTargetToMergeSources(),
                AutoCommit,
                Strategy.Arg)
                .Use(log)
                .ExecAsync();

            log.Complete();
            return true;
        }

        /// <summary>
        /// マージ対象オブジェクトをgitコマンド用の文字列リストに変換する。
        /// ブランチはフレンドリー名、コミットはデコレータ名またはSHAを使用する。
        /// </summary>
        private List<string> ConvertTargetToMergeSources()
        {
            var ret = new List<string>();
            foreach (var t in Targets)
            {
                if (t is Models.Branch branch)
                {
                    // ブランチの場合はフレンドリー名を使用
                    ret.Add(branch.FriendlyName);
                }
                else if (t is Models.Commit commit)
                {
                    // コミットの場合、ブランチヘッドやタグのデコレータがあればその名前を使用
                    var d = commit.Decorators.Find(x => x.Type is
                        Models.DecoratorType.LocalBranchHead or
                        Models.DecoratorType.RemoteBranchHead or
                        Models.DecoratorType.Tag);

                    if (d != null)
                        ret.Add(d.Name);
                    else
                        ret.Add(commit.SHA);
                }
            }

            return ret;
        }

        /// <summary>対象リポジトリ</summary>
        private readonly Repository _repo = null;
    }
}
