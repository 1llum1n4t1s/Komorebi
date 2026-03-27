using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
///     ファイル履歴表示用のコマンドパレットViewModel。
///     リポジトリ内のファイルをフィルタ検索し、選択ファイルの履歴を表示する。
/// </summary>
public class FileHistoryCommandPalette : ICommandPalette
{
    /// <summary>
    ///     ファイル一覧の読み込み中かどうか。
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    ///     フィルタ適用後の表示ファイルリスト。
    /// </summary>
    public List<string> VisibleFiles
    {
        get => _visibleFiles;
        private set => SetProperty(ref _visibleFiles, value);
    }

    /// <summary>
    ///     フィルタ文字列。変更時に表示ファイルリストを更新する。
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
    ///     現在選択されているファイルパス。
    /// </summary>
    public string SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    /// <summary>
    ///     コンストラクタ。リポジトリパスを指定し、非同期でHEADリビジョンのファイル一覧を取得する。
    /// </summary>
    public FileHistoryCommandPalette(string repo)
    {
        _repo = repo;
        _isLoading = true;

        Task.Run(async () =>
        {
            var files = await new Commands.QueryRevisionFileNames(_repo, "HEAD")
                .GetResultAsync()
                .ConfigureAwait(false);

            Dispatcher.UIThread.Post(() =>
            {
                IsLoading = false;
                _repoFiles = files;
                UpdateVisible();
            });
        });
    }

    /// <summary>
    ///     フィルタをクリアする。
    /// </summary>
    public void ClearFilter()
    {
        Filter = string.Empty;
    }

    /// <summary>
    ///     選択ファイルの履歴ウィンドウを開く。リソースをクリアしてパレットを閉じる。
    /// </summary>
    public void Launch()
    {
        _repoFiles.Clear();
        _visibleFiles.Clear();
        Close();

        if (!string.IsNullOrEmpty(_selectedFile))
            App.ShowWindow(new FileHistories(_repo, _selectedFile));
    }

    /// <summary>
    ///     フィルタ文字列に基づいて表示ファイルリストと選択状態を更新する。
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
                var visible = new List<string>();

                foreach (var f in _repoFiles)
                {
                    if (f.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(f);
                }

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

    private string _repo = null; // リポジトリパス
    private bool _isLoading = false; // 読み込み中フラグ
    private List<string> _repoFiles = null; // リポジトリ内の全ファイル一覧
    private string _filter = string.Empty; // フィルタ文字列
    private List<string> _visibleFiles = []; // フィルタ適用後のファイル一覧
    private string _selectedFile = null; // 現在選択中のファイル
}
