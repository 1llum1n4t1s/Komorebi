using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     LFSファイルロックの管理画面ViewModel。
    ///     ロック一覧の取得、個別・一括ロック解除を行う。
    /// </summary>
    public class LFSLocks : ObservableObject
    {
        /// <summary>有効なユーザー名が設定されているかどうか。</summary>
        public bool HasValidUserName
        {
            get => _hasValidUsername;
            private set => SetProperty(ref _hasValidUsername, value);
        }

        /// <summary>ロック情報を読み込み中かどうか。</summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>自分のロックのみ表示するフィルタ。変更時に表示リストを更新する。</summary>
        public bool ShowOnlyMyLocks
        {
            get => _showOnlyMyLocks;
            set
            {
                if (SetProperty(ref _showOnlyMyLocks, value))
                    UpdateVisibleLocks();
            }
        }

        /// <summary>表示対象のロック一覧（フィルタ適用後）。</summary>
        public List<Models.LFSLock> VisibleLocks
        {
            get => _visibleLocks;
            private set => SetProperty(ref _visibleLocks, value);
        }

        /// <summary>
        ///     コンストラクタ。バックグラウンドでユーザー名とロック一覧を取得する。
        /// </summary>
        public LFSLocks(Repository repo, string remote)
        {
            _repo = repo;
            _remote = remote;

            Task.Run(async () =>
            {
                _userName = await new Commands.Config(repo.FullPath).GetAsync("user.name").ConfigureAwait(false);
                _cachedLocks = await new Commands.LFS(_repo.FullPath).GetLocksAsync(_remote).ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    UpdateVisibleLocks();
                    IsLoading = false;
                    HasValidUserName = !string.IsNullOrEmpty(_userName);
                });
            });
        }

        /// <summary>指定されたロックを解除する。</summary>
        public async Task UnlockAsync(Models.LFSLock lfsLock, bool force)
        {
            if (_isLoading)
                return;

            IsLoading = true;

            var succ = await _repo.UnlockLFSFileAsync(_remote, lfsLock.Path, force, false);
            if (succ)
            {
                _cachedLocks.Remove(lfsLock);
                UpdateVisibleLocks();
            }

            IsLoading = false;
        }

        /// <summary>自分が所有する全てのロックを一括解除する。</summary>
        public async Task UnlockAllMyLocksAsync()
        {
            if (_isLoading || string.IsNullOrEmpty(_userName))
                return;

            var locks = new List<string>();
            foreach (var lfsLock in _cachedLocks)
            {
                if (lfsLock.Owner.Name.Equals(_userName, StringComparison.Ordinal))
                    locks.Add(lfsLock.Path);
            }

            if (locks.Count == 0)
                return;

            IsLoading = true;

            var log = _repo.CreateLog("Unlock LFS Locks");
            var succ = await new Commands.LFS(_repo.FullPath).Use(log).UnlockMultipleAsync(_remote, locks, true);
            if (succ)
            {
                _cachedLocks.RemoveAll(lfsLock => lfsLock.Owner.Name.Equals(_userName, StringComparison.Ordinal));
                UpdateVisibleLocks();
            }

            log.Complete();
            IsLoading = false;
        }

        /// <summary>フィルタ設定に基づいて表示対象のロック一覧を更新する。</summary>
        private void UpdateVisibleLocks()
        {
            var visible = new List<Models.LFSLock>();

            if (!_showOnlyMyLocks)
            {
                visible.AddRange(_cachedLocks);
            }
            else
            {
                foreach (var lfsLock in _cachedLocks)
                {
                    if (lfsLock.Owner.Name.Equals(_userName, StringComparison.Ordinal))
                        visible.Add(lfsLock);
                }
            }

            VisibleLocks = visible;
        }

        private Repository _repo;
        private string _remote;
        private bool _isLoading = true;
        private List<Models.LFSLock> _cachedLocks = [];
        private List<Models.LFSLock> _visibleLocks = [];
        private bool _showOnlyMyLocks = false;
        private string _userName;
        private bool _hasValidUsername;
    }
}
