using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Komorebi.ViewModels;

/// <summary>
/// BlameコマンドパレットのViewModel。
/// リポジトリ内のファイル一覧から選択してblameビューを開くための検索パレット。
/// </summary>
public class BlameCommandPalette : ICommandPalette
{
    /// <summary>
    /// ファイル一覧のロード中かどうかを示すフラグ。
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// フィルタ適用後の表示ファイルリスト。
    /// </summary>
    public List<string> VisibleFiles
    {
        get => _visibleFiles;
        private set => SetProperty(ref _visibleFiles, value);
    }

    /// <summary>
    /// ファイル名フィルタ文字列。変更時に表示リストを更新する。
    /// </summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
                // フィルタ変更時に表示リストを再構築する
                UpdateVisible();
        }
    }

    /// <summary>
    /// 選択されたファイルパス。
    /// </summary>
    public string SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリのHEADからファイル一覧をバックグラウンドで取得する。
    /// </summary>
    /// <param name="repo">リポジトリのフルパス</param>
    public BlameCommandPalette(string repo)
    {
        _repo = repo;
        _isLoading = true;

        Task.Run(async () =>
        {
            // 独立した2つのgitコマンドを並列実行（旧: 逐次実行で応答時間が2倍）
            var filesTask = new Commands.QueryRevisionFileNames(_repo, "HEAD")
                .GetResultAsync();
            var headTask = new Commands.QuerySingleCommit(_repo, "HEAD")
                .GetResultAsync();
            await Task.WhenAll(filesTask, headTask).ConfigureAwait(false);
            var files = filesTask.Result;
            var head = headTask.Result;

            // UIスレッドでロード完了後の処理を実行する
            Dispatcher.UIThread.Post(() =>
            {
                IsLoading = false;
                _repoFiles = files;
                _head = head;
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
    /// 選択したファイルでblameウィンドウを起動する。
    /// </summary>
    public void Launch()
    {
        // リソースをクリアしてパレットを閉じる
        _repoFiles.Clear();
        _visibleFiles.Clear();
        Close();

        // 選択ファイルがあればblameウィンドウを表示する
        if (!string.IsNullOrEmpty(_selectedFile))
            App.ShowWindow(new Blame(_repo, _selectedFile, _head));
    }

    /// <summary>
    /// フィルタに基づいて表示ファイルリストを更新する。
    /// </summary>
    private void UpdateVisible()
    {
        if (_repoFiles is { Count: > 0 })
        {
            if (string.IsNullOrEmpty(_filter))
            {
                // フィルタが空の場合は全ファイルを表示する
                VisibleFiles = _repoFiles;

                if (string.IsNullOrEmpty(_selectedFile))
                    SelectedFile = _repoFiles[0];
            }
            else
            {
                // フィルタに一致するファイルのみ抽出する
                List<string> visible = [];

                foreach (var f in _repoFiles)
                {
                    if (f.Contains(_filter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(f);
                }

                // 自動選択：一致するものがなければnull、現在の選択が含まれなければ先頭を選択する
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
    /// <summary>ロード中フラグ</summary>
    private bool _isLoading = false;
    /// <summary>HEADコミットの情報</summary>
    private Models.Commit _head = null;
    /// <summary>リポジトリ内の全ファイルリスト</summary>
    private List<string> _repoFiles = null;
    /// <summary>フィルタ文字列</summary>
    private string _filter = string.Empty;
    /// <summary>フィルタ適用後の表示ファイルリスト</summary>
    private List<string> _visibleFiles = [];
    /// <summary>選択されたファイル</summary>
    private string _selectedFile = null;
}
