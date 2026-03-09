using System;
using System.Collections.Generic;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// マージ先ブランチを選択するためのコマンドパレットViewModel。
    /// ICommandPaletteを実装し、フィルタリング可能なブランチ一覧を表示する。
    /// </summary>
    public class MergeCommandPalette : ICommandPalette
    {
        /// <summary>
        /// フィルタ適用後のブランチ一覧。
        /// </summary>
        public List<Models.Branch> Branches
        {
            get => _branches;
            private set => SetProperty(ref _branches, value);
        }

        /// <summary>
        /// ブランチ名のフィルタ文字列。変更時にブランチ一覧を更新する。
        /// </summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateBranches();
            }
        }

        /// <summary>
        /// 選択中のブランチ。
        /// </summary>
        public Models.Branch SelectedBranch
        {
            get => _selectedBranch;
            set => SetProperty(ref _selectedBranch, value);
        }

        /// <summary>
        /// リポジトリを指定してコマンドパレットを初期化し、ブランチ一覧を構築する。
        /// </summary>
        public MergeCommandPalette(Repository repo)
        {
            _repo = repo;
            UpdateBranches();
        }

        /// <summary>
        /// フィルタ文字列をクリアする。
        /// </summary>
        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        /// <summary>
        /// 選択されたブランチでマージダイアログを起動する。
        /// ブランチ一覧をクリアしてパレットを閉じた後、Mergeポップアップを表示する。
        /// </summary>
        public void Launch()
        {
            _branches.Clear();
            Close();

            // ポップアップ表示可能かつブランチが選択されている場合にマージダイアログを表示
            if (_repo.CanCreatePopup() && _selectedBranch != null)
                _repo.ShowPopup(new Merge(_repo, _selectedBranch, _repo.CurrentBranch.Name, false));
        }

        /// <summary>
        /// フィルタ条件に基づいてブランチ一覧を更新する。
        /// 現在のブランチを除外し、ローカルブランチを優先してソートする。
        /// </summary>
        private void UpdateBranches()
        {
            var current = _repo.CurrentBranch;
            if (current == null)
                return;

            // 現在のブランチを除外し、フィルタに一致するブランチを収集
            var branches = new List<Models.Branch>();
            foreach (var b in _repo.Branches)
            {
                if (b == current)
                    continue;

                if (string.IsNullOrEmpty(_filter) || b.FriendlyName.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                    branches.Add(b);
            }

            // ローカルブランチを優先し、同種内では数値考慮でソート
            branches.Sort((l, r) =>
            {
                if (l.IsLocal == r.IsLocal)
                    return Models.NumericSort.Compare(l.Name, r.Name);

                return l.IsLocal ? -1 : 1;
            });

            // 選択状態を維持または自動選択
            var autoSelected = _selectedBranch;
            if (branches.Count == 0)
                autoSelected = null;
            else if (_selectedBranch == null || !branches.Contains(_selectedBranch))
                autoSelected = branches[0];

            Branches = branches;
            SelectedBranch = autoSelected;
        }

        /// <summary>対象リポジトリ</summary>
        private Repository _repo = null;
        /// <summary>フィルタ適用後のブランチリスト</summary>
        private List<Models.Branch> _branches = new List<Models.Branch>();
        /// <summary>フィルタ文字列</summary>
        private string _filter = string.Empty;
        /// <summary>選択中のブランチ</summary>
        private Models.Branch _selectedBranch = null;
    }
}
