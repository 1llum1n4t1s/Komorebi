using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// 現在のブランチを特定のコミットにリセットするダイアログのViewModel。
    /// リセットモード（Soft/Mixed/Hard等）の選択に対応する。
    /// </summary>
    public class Reset : Popup
    {
        /// <summary>
        /// リセット対象の現在のブランチ。
        /// </summary>
        public Models.Branch Current
        {
            get;
        }

        /// <summary>
        /// リセット先のコミット。
        /// </summary>
        public Models.Commit To
        {
            get;
        }

        /// <summary>
        /// 選択されたリセットモード（Soft/Mixed/Hard等）。
        /// </summary>
        public Models.ResetMode SelectedMode
        {
            get;
            set;
        }

        /// <summary>
        /// リポジトリ、現在のブランチ、リセット先コミットを指定してダイアログを初期化する。
        /// デフォルトのリセットモードとしてMixed（インデックス1）を選択する。
        /// </summary>
        public Reset(Repository repo, Models.Branch current, Models.Commit to)
        {
            _repo = repo;
            Current = current;
            To = to;
            SelectedMode = Models.ResetMode.Supported[1];
        }

        /// <summary>
        /// ブランチのリセットを実行する。
        /// 選択されたモードで指定コミットにリセットし、サブモジュールも自動更新する。
        /// </summary>
        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = App.Text("Progress.Resetting", To.SHA);

            var log = _repo.CreateLog($"Reset HEAD to '{To.SHA}'");
            Use(log);

            // git reset コマンドを選択されたモードで実行
            var succ = await new Commands.Reset(_repo.FullPath, To.SHA, SelectedMode.Arg)
                .Use(log)
                .ExecAsync();

            // リセット後にサブモジュールを自動更新
            await _repo.AutoUpdateSubmodulesAsync(log);

            log.Complete();
            return succ;
        }

        /// <summary>対象リポジトリ</summary>
        private readonly Repository _repo = null;
    }
}
