using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels
{
    /// <summary>
    ///     リポジトリツリー内のノード（リポジトリまたはグループフォルダ）を表すViewModel。
    ///     ウェルカム画面やランチャーのサイドバーに表示されるツリー構造の各項目を管理する。
    /// </summary>
    public class RepositoryNode : ObservableObject
    {
        /// <summary>
        ///     ノードの一意識別子。リポジトリの場合はフルパス、グループの場合はグループ名。
        ///     パス区切り文字は正規化され、末尾のスラッシュは除去される。
        /// </summary>
        public string Id
        {
            get => _id;
            set
            {
                var normalized = value.Replace('\\', '/').TrimEnd('/');
                SetProperty(ref _id, normalized);
            }
        }

        /// <summary>
        ///     ノードの表示名。UIに表示されるリポジトリ名またはグループ名。
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        ///     ブックマークのインデックス。色分け用のブックマーク識別子（0はブックマークなし）。
        /// </summary>
        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        /// <summary>
        ///     このノードがリポジトリかどうか。falseの場合はグループフォルダ。
        /// </summary>
        public bool IsRepository
        {
            get => _isRepository;
            set => SetProperty(ref _isRepository, value);
        }

        /// <summary>
        ///     ツリー上でこのノードが展開されているかどうか。
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        ///     検索フィルタリング時の表示/非表示状態。JSON保存対象外。
        /// </summary>
        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        ///     リポジトリのパスが存在しない場合にtrueを返す。無効なリポジトリの検出に使用。
        /// </summary>
        [JsonIgnore]
        public bool IsInvalid
        {
            get => _isRepository && !Directory.Exists(_id);
        }

        /// <summary>
        ///     ツリー内のネスト深度。UIのインデント表示に使用。JSON保存対象外。
        /// </summary>
        [JsonIgnore]
        public int Depth
        {
            get;
            set;
        } = 0;

        /// <summary>
        ///     リポジトリの現在のステータス（未コミットの変更数など）。
        /// </summary>
        public Models.RepositoryStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        ///     子ノードのリスト。グループフォルダの場合に子リポジトリやサブグループを保持する。
        /// </summary>
        public List<RepositoryNode> SubNodes
        {
            get;
            set;
        } = [];

        /// <summary>
        ///     ノードを開く。リポジトリの場合は新しいタブで開き、グループの場合は全子ノードを再帰的に開く。
        /// </summary>
        public void Open()
        {
            if (IsRepository)
            {
                App.GetLauncher().OpenRepositoryInTab(this, null);
                return;
            }

            foreach (var subNode in SubNodes)
                subNode.Open();
        }

        /// <summary>
        ///     ノードの編集ダイアログを表示する。名前やパスの変更が可能。
        /// </summary>
        public void Edit()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new EditRepositoryNode(this);
        }

        /// <summary>
        ///     このノードの下にサブフォルダ（グループ）を作成するダイアログを表示する。
        /// </summary>
        public void AddSubFolder()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new CreateGroup(this);
        }

        /// <summary>
        ///     ノードを別のグループに移動するダイアログを表示する。
        /// </summary>
        public void Move()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new MoveRepositoryNode(this);
        }

        /// <summary>
        ///     リポジトリをOSのファイルマネージャで開く。
        /// </summary>
        public void OpenInFileManager()
        {
            if (!IsRepository)
                return;
            Native.OS.OpenInFileManager(_id);
        }

        /// <summary>
        ///     リポジトリのディレクトリでターミナルを開く。
        /// </summary>
        public void OpenTerminal()
        {
            if (!IsRepository)
                return;
            Native.OS.OpenTerminal(_id);
        }

        /// <summary>
        ///     ノードの削除確認ダイアログを表示する。
        /// </summary>
        public void Delete()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new DeleteRepositoryNode(this);
        }

        /// <summary>
        ///     リポジトリのステータスを非同期に更新する。
        ///     グループノードの場合は全子ノードを再帰的に更新する。
        ///     強制更新でない場合、前回の更新から10秒以内の再更新はスキップする。
        /// </summary>
        /// <param name="force">trueの場合、クールダウン期間を無視して強制更新する</param>
        /// <param name="token">キャンセルトークン</param>
        public async Task UpdateStatusAsync(bool force, CancellationToken? token)
        {
            if (token is { IsCancellationRequested: true })
                return;

            if (!_isRepository)
            {
                Status = null;

                if (SubNodes.Count > 0)
                {
                    // 列挙中のコレクション変更を回避するためコピーを作成
                    var nodes = new List<RepositoryNode>();
                    nodes.AddRange(SubNodes);

                    foreach (var node in nodes)
                        await node.UpdateStatusAsync(force, token);
                }

                return;
            }

            if (!Directory.Exists(_id))
            {
                _lastUpdateStatus = DateTime.Now;
                Status = null;
                OnPropertyChanged(nameof(IsInvalid));
                return;
            }

            // 強制更新でなければ10秒のクールダウンを適用
            if (!force)
            {
                var passed = DateTime.Now - _lastUpdateStatus;
                if (passed.TotalSeconds < 10.0)
                    return;
            }

            _lastUpdateStatus = DateTime.Now;
            Status = await new Commands.QueryRepositoryStatus(_id).GetResultAsync();
            OnPropertyChanged(nameof(IsInvalid));
        }

        private string _id = string.Empty;
        private string _name = string.Empty;
        private bool _isRepository = false;
        private int _bookmark = 0;
        private bool _isExpanded = false;
        private bool _isVisible = true;
        private Models.RepositoryStatus _status = null;
        private DateTime _lastUpdateStatus = DateTime.UnixEpoch.ToLocalTime();
    }
}
