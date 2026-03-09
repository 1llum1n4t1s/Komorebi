using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     サブモジュールを更新するポップアップダイアログのViewModel。
    ///     全サブモジュールまたは選択したサブモジュールの更新、初期化、リモート追跡の設定を行う。
    /// </summary>
    public class UpdateSubmodules : Popup
    {
        /// <summary>
        ///     事前に特定のサブモジュールが選択されているかどうか。
        /// </summary>
        public bool HasPreSelectedSubmodule
        {
            get;
        }

        /// <summary>
        ///     リポジトリ内の全サブモジュールリスト。
        /// </summary>
        public List<Models.Submodule> Submodules
        {
            get => _repo.Submodules;
        }

        /// <summary>
        ///     更新対象として選択されたサブモジュール。
        /// </summary>
        public Models.Submodule SelectedSubmodule
        {
            get;
            set;
        }

        /// <summary>
        ///     全サブモジュールを一括更新するかどうか。
        /// </summary>
        public bool UpdateAll
        {
            get => _updateAll;
            set => SetProperty(ref _updateAll, value);
        }

        /// <summary>
        ///     初期化オプションの表示切り替え。
        /// </summary>
        public bool IsEnableInitVisible
        {
            get;
            set;
        } = true;

        /// <summary>
        ///     未初期化サブモジュールを初期化するかどうか（--init）。
        /// </summary>
        public bool EnableInit
        {
            get;
            set;
        } = true;

        /// <summary>
        ///     リモート追跡を有効にするかどうか（--remote）。
        /// </summary>
        public bool EnableRemote
        {
            get;
            set;
        } = false;

        /// <summary>
        ///     コンストラクタ。選択されたサブモジュールに応じて初期設定を行う。
        /// </summary>
        public UpdateSubmodules(Repository repo, Models.Submodule selected)
        {
            _repo = repo;

            if (selected != null)
            {
                // 特定のサブモジュールが指定された場合
                _updateAll = false;
                SelectedSubmodule = selected;
                IsEnableInitVisible = selected.Status == Models.SubmoduleStatus.NotInited;
                EnableInit = selected.Status == Models.SubmoduleStatus.NotInited;
                HasPreSelectedSubmodule = true;
            }
            else if (repo.Submodules.Count > 0)
            {
                // 未指定の場合は最初のサブモジュールを選択
                SelectedSubmodule = repo.Submodules[0];
                IsEnableInitVisible = true;
                HasPreSelectedSubmodule = false;
            }
        }

        /// <summary>
        ///     サブモジュール更新を実行する。全更新または選択サブモジュールのみ更新。
        /// </summary>
        public override async Task<bool> Sure()
        {
            // 更新対象のパスリストを構築
            var targets = new List<string>();
            if (_updateAll)
            {
                foreach (var submodule in Submodules)
                    targets.Add(submodule.Path);
            }
            else if (SelectedSubmodule != null)
            {
                targets.Add(SelectedSubmodule.Path);
            }

            if (targets.Count == 0)
                return true;

            var log = _repo.CreateLog("Update Submodule");
            using var lockWatcher = _repo.LockWatcher();
            Use(log);

            await new Commands.Submodule(_repo.FullPath)
                .Use(log)
                .UpdateAsync(targets, EnableInit, EnableRemote);

            log.Complete();
            _repo.MarkSubmodulesDirtyManually();
            return true;
        }

        private readonly Repository _repo = null;
        private bool _updateAll = true;
    }
}
