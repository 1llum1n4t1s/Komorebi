using System;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     サブモジュールのトラッキングブランチを設定するポップアップダイアログのViewModel。
    /// </summary>
    public class SetSubmoduleBranch : Popup
    {
        /// <summary>
        ///     設定対象のサブモジュール。
        /// </summary>
        public Models.Submodule Submodule
        {
            get;
        }

        /// <summary>
        ///     変更先のブランチ名。
        /// </summary>
        public string ChangeTo
        {
            get => _changeTo;
            set => SetProperty(ref _changeTo, value);
        }

        /// <summary>
        ///     コンストラクタ。リポジトリとサブモジュールを受け取り、現在のブランチで初期化する。
        /// </summary>
        public SetSubmoduleBranch(Repository repo, Models.Submodule submodule)
        {
            _repo = repo;
            _changeTo = submodule.Branch;
            Submodule = submodule;
        }

        /// <summary>
        ///     ブランチ変更操作を実行する。変更がない場合はスキップする。
        /// </summary>
        public override async Task<bool> Sure()
        {
            ProgressDescription = App.Text("Progress.SetSubmoduleBranch");

            if (_changeTo.Equals(Submodule.Branch, StringComparison.Ordinal))
                return true;

            using var lockWatcher = _repo.LockWatcher();
            var log = _repo.CreateLog("Set Submodule's Branch");
            Use(log);

            var succ = await new Commands.Submodule(_repo.FullPath)
                .Use(log)
                .SetBranchAsync(Submodule.Path, _changeTo);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo;
        private string _changeTo;
    }
}
