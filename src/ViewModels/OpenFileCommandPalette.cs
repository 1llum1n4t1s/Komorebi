using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Komorebi.ViewModels
{
    /// <summary>
    /// リポジトリ内のファイルを検索して開くためのコマンドパレットViewModel。
    /// ICommandPaletteを実装し、HEADリビジョンのファイル一覧からフィルタリングして選択・起動する。
    /// </summary>
    public class OpenFileCommandPalette : ICommandPalette
    {
        /// <summary>
        /// ファイル一覧の読み込み中かどうか。
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// フィルタ適用後の表示ファイル一覧。
        /// </summary>
        public List<string> VisibleFiles
        {
            get => _visibleFiles;
            private set => SetProperty(ref _visibleFiles, value);
        }

        /// <summary>
        /// ファイル名のフィルタ文字列。変更時に表示一覧を更新する。
        /// </summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    UpdateVisible();
            }
        }

        /// <summary>
        /// 選択中のファイルパス。
        /// </summary>
        public string SelectedFile
        {
            get => _selectedFile;
            set => SetProperty(ref _selectedFile, value);
        }

        /// <summary>
        /// リポジトリパスを指定してコマンドパレットを初期化する。
        /// バックグラウンドでHEADリビジョンのファイル一覧を非同期に取得する。
        /// </summary>
        public OpenFileCommandPalette(string repo)
        {
            _repo = repo;
            _isLoading = true;

            // バックグラウンドスレッドでファイル一覧を取得
            Task.Run(async () =>
            {
                var files = await new Commands.QueryRevisionFileNames(_repo, "HEAD")
                    .GetResultAsync()
                    .ConfigureAwait(false);

                // UIスレッドで結果を反映
                Dispatcher.UIThread.Post(() =>
                {
                    IsLoading = false;
                    _repoFiles = files;
                    UpdateVisible();
                });
            });
        }

        /// <summary>
        /// フィルタ文字列をクリアする。
        /// </summary>
        public void ClearFilter()
        {
            Filter = string.Empty;
        }

        /// <summary>
        /// 選択されたファイルをデフォルトエディタで開く。
        /// ファイル一覧をクリアしてパレットを閉じた後、OSのデフォルトエディタで起動する。
        /// </summary>
        public void Launch()
        {
            _repoFiles.Clear();
            _visibleFiles.Clear();
            Close();

            if (!string.IsNullOrEmpty(_selectedFile))
                Native.OS.OpenWithDefaultEditor(Native.OS.GetAbsPath(_repo, _selectedFile));
        }

        /// <summary>
        /// フィルタ条件に基づいて表示ファイル一覧を更新する。
        /// フィルタが空の場合は全ファイルを表示し、そうでなければ部分一致で絞り込む。
        /// </summary>
        private void UpdateVisible()
        {
            if (_repoFiles is { Count: > 0 })
            {
                if (string.IsNullOrEmpty(_filter))
                {
                    VisibleFiles = _repoFiles;

                    if (string.IsNullOrEmpty(_selectedFile))
                        SelectedFile = _repoFiles[0];
                }
                else
                {
                    // フィルタ文字列で大文字小文字を無視して絞り込み
                    var visible = new List<string>();

                    foreach (var f in _repoFiles)
                    {
                        if (f.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                            visible.Add(f);
                    }

                    // 選択状態を維持または自動選択
                    var autoSelected = _selectedFile;
                    if (visible.Count == 0)
                        autoSelected = null;
                    else if (string.IsNullOrEmpty(_selectedFile) || !visible.Contains(_selectedFile))
                        autoSelected = visible[0];

                    VisibleFiles = visible;
                    SelectedFile = autoSelected;
                }
            }
        }

        /// <summary>リポジトリのフルパス</summary>
        private string _repo = null;
        /// <summary>読み込み中フラグ</summary>
        private bool _isLoading = false;
        /// <summary>リポジトリ内の全ファイルリスト</summary>
        private List<string> _repoFiles = null;
        /// <summary>フィルタ文字列</summary>
        private string _filter = string.Empty;
        /// <summary>表示中のファイルリスト</summary>
        private List<string> _visibleFiles = [];
        /// <summary>選択中のファイルパス</summary>
        private string _selectedFile = null;
    }
}
