using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     ローカルブランチの上流（upstream）ブランチを設定するポップアップダイアログのViewModel。
    ///     リモートブランチの選択または上流の解除を行う。
    /// </summary>
    public class SetUpstream : Popup
    {
        /// <summary>
        ///     上流を設定するローカルブランチ。
        /// </summary>
        public Models.Branch Local
        {
            get;
        }

        /// <summary>
        ///     選択可能なリモートブランチのリスト。
        /// </summary>
        public List<Models.Branch> RemoteBranches
        {
            get;
            private set;
        }

        /// <summary>
        ///     選択されたリモートブランチ。
        /// </summary>
        public Models.Branch SelectedRemoteBranch
        {
            get;
            set;
        }

        /// <summary>
        ///     上流設定を解除するかどうか。
        /// </summary>
        public bool Unset
        {
            get => _unset;
            set => SetProperty(ref _unset, value);
        }

        /// <summary>
        ///     コンストラクタ。現在の上流設定またはブランチ名に基づいて初期選択を設定する。
        /// </summary>
        public SetUpstream(Repository repo, Models.Branch local, List<Models.Branch> remoteBranches)
        {
            _repo = repo;
            Local = local;
            RemoteBranches = remoteBranches;
            Unset = false;

            if (!string.IsNullOrEmpty(local.Upstream))
            {
                var upstream = remoteBranches.Find(x => x.FullName == local.Upstream);
                if (upstream != null)
                    SelectedRemoteBranch = upstream;
            }

            if (SelectedRemoteBranch == null)
            {
                var upstream = remoteBranches.Find(x => x.Name == local.Name);
                if (upstream != null)
                    SelectedRemoteBranch = upstream;
            }
        }

        /// <summary>
        ///     上流設定の変更を実行する。変更がない場合はスキップする。
        /// </summary>
        public override async Task<bool> Sure()
        {
            ProgressDescription = "Setting upstream...";
            Models.Branch upstream = _unset ? null : SelectedRemoteBranch;

            if (upstream == null)
            {
                if (string.IsNullOrEmpty(Local.Upstream))
                    return true;
            }
            else if (upstream.FullName.Equals(Local.Upstream, StringComparison.Ordinal))
            {
                return true;
            }

            var log = _repo.CreateLog("Set Upstream");
            Use(log);

            var succ = await new Commands.Branch(_repo.FullPath, Local.Name)
                .Use(log)
                .SetUpstreamAsync(upstream);

            log.Complete();
            if (succ)
                _repo.MarkBranchesDirtyManually();
            return true;
        }

        private readonly Repository _repo;
        private bool _unset = false;
    }
}
