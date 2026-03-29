using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// リビジョン比較ビューのViewModel。
/// 2つのリビジョン（ブランチ、タグ、コミット）間の差分を表示し、
/// ファイルのリセット操作やパッチ保存機能を提供する。
/// </summary>
public class Compare : ObservableObject
{
    /// <summary>
    /// 変更一覧を読み込み中かどうかのフラグ。
    /// </summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    /// <summary>
    /// ファイルリセット操作が可能かどうか（ベアリポジトリでは不可）。
    /// </summary>
    public bool CanResetFiles
    {
        get => _canResetFiles;
    }

    /// <summary>
    /// 比較基準リビジョンの表示名。
    /// </summary>
    public string BaseName
    {
        get => _baseName;
        private set => SetProperty(ref _baseName, value);
    }

    /// <summary>
    /// 比較対象リビジョンの表示名。
    /// </summary>
    public string ToName
    {
        get => _toName;
        private set => SetProperty(ref _toName, value);
    }

    /// <summary>
    /// 比較基準リビジョンのコミット情報。
    /// </summary>
    public Models.Commit BaseHead
    {
        get => _baseHead;
        private set => SetProperty(ref _baseHead, value);
    }

    /// <summary>
    /// 比較対象リビジョンのコミット情報。
    /// </summary>
    public Models.Commit ToHead
    {
        get => _toHead;
        private set => SetProperty(ref _toHead, value);
    }

    /// <summary>
    /// 全変更ファイル数。
    /// </summary>
    public int TotalChanges
    {
        get => _totalChanges;
        private set => SetProperty(ref _totalChanges, value);
    }

    /// <summary>
    /// フィルタ適用後の表示中変更ファイルリスト。
    /// </summary>
    public List<Models.Change> VisibleChanges
    {
        get => _visibleChanges;
        private set => SetProperty(ref _visibleChanges, value);
    }

    /// <summary>
    /// 選択中の変更ファイルリスト。変更時にDiffContextを更新する。
    /// </summary>
    public List<Models.Change> SelectedChanges
    {
        get => _selectedChanges;
        set
        {
            if (SetProperty(ref _selectedChanges, value))
            {
                // 選択が1件の場合のみ差分コンテキストを生成する
                if (value is { Count: 1 })
                    DiffContext = new DiffContext(_repo, new Models.DiffOption(_based, _to, value[0]), _diffContext);
                else
                    DiffContext = null;
            }
        }
    }

    /// <summary>
    /// 変更ファイルの検索フィルタ文字列。変更時に表示リストを更新する。
    /// </summary>
    public string SearchFilter
    {
        get => _searchFilter;
        set
        {
            if (SetProperty(ref _searchFilter, value))
                // フィルタ変更時に表示リストを再構築する
                RefreshVisible();
        }
    }

    /// <summary>
    /// 差分表示用のDiffContext。
    /// </summary>
    public DiffContext DiffContext
    {
        get => _diffContext;
        private set => SetProperty(ref _diffContext, value);
    }

    /// <summary>
    /// コンストラクタ。リポジトリと比較対象の2つのオブジェクトを受け取って初期化する。
    /// </summary>
    /// <param name="repo">対象のリポジトリViewModel</param>
    /// <param name="based">比較基準のオブジェクト（ブランチ、タグ、コミット）</param>
    /// <param name="to">比較対象のオブジェクト（ブランチ、タグ、コミット）</param>
    public Compare(Repository repo, object based, object to)
    {
        _repo = repo.FullPath;
        _canResetFiles = !repo.IsBare;
        // オブジェクトからSHA値を抽出する
        _based = GetSHA(based);
        _to = GetSHA(to);
        // オブジェクトから表示名を抽出する
        _baseName = GetName(based);
        _toName = GetName(to);

        // 差分データを非同期で取得する
        Refresh();
    }

    /// <summary>
    /// 指定コミットSHAにナビゲートする。該当リポジトリのページでコミットを選択する。
    /// </summary>
    /// <param name="commitSHA">ナビゲート先のコミットSHA</param>
    public void NavigateTo(string commitSHA)
    {
        var launcher = App.GetLauncher();
        if (launcher is null)
            return;

        // ランチャーのページから該当リポジトリを検索してナビゲートする
        foreach (var page in launcher.Pages)
        {
            if (page.Data is Repository repo && repo.FullPath.Equals(_repo))
            {
                repo.NavigateToCommit(commitSHA);
                break;
            }
        }
    }

    /// <summary>
    /// 比較基準と比較対象を入れ替える。
    /// </summary>
    public void Swap()
    {
        // SHA値と表示名を入れ替える
        (_based, _to) = (_to, _based);
        (BaseName, ToName) = (_toName, _baseName);

        // コミット情報も入れ替える
        if (_baseHead is not null)
            (BaseHead, ToHead) = (_toHead, _baseHead);

        // 差分データを再取得する
        Refresh();
    }

    /// <summary>
    /// 検索フィルタをクリアする。
    /// </summary>
    public void ClearSearchFilter()
    {
        SearchFilter = string.Empty;
    }

    /// <summary>
    /// 指定パスの絶対パスを取得する。
    /// </summary>
    /// <param name="path">リポジトリ内の相対パス</param>
    /// <returns>絶対パス</returns>
    public string GetAbsPath(string path)
    {
        return Native.OS.GetAbsPath(_repo, path);
    }

    /// <summary>
    /// 外部差分ツールで変更を開く。
    /// </summary>
    /// <param name="change">対象の変更ファイル</param>
    public void OpenInExternalDiffTool(Models.Change change)
    {
        new Commands.DiffTool(_repo, new Models.DiffOption(_based, _to, change)).Open();
    }

    /// <summary>
    /// ファイルを左側（基準リビジョン）の状態にリセットする。
    /// 変更種別に応じてgit rmまたはgit checkoutを実行する。
    /// </summary>
    /// <param name="change">リセット対象の変更ファイル</param>
    public async Task ResetToLeftAsync(Models.Change change)
    {
        if (!_canResetFiles)
            return;

        if (change.Index == Models.ChangeState.Added)
        {
            // 追加されたファイルは削除する
            var fullpath = Native.OS.GetAbsPath(_repo, change.Path);
            if (File.Exists(fullpath))
                await new Commands.Remove(_repo, [change.Path]).ExecAsync();
        }
        else if (change.Index == Models.ChangeState.Renamed)
        {
            // リネームされたファイルは新しいパスを削除し、元のパスを復元する
            var renamed = Native.OS.GetAbsPath(_repo, change.Path);
            if (File.Exists(renamed))
                await new Commands.Remove(_repo, [change.Path]).ExecAsync();

            await new Commands.Checkout(_repo).FileWithRevisionAsync(change.OriginalPath, _baseHead.SHA);
        }
        else
        {
            // 変更・削除されたファイルは基準リビジョンの状態に復元する
            await new Commands.Checkout(_repo).FileWithRevisionAsync(change.Path, _baseHead.SHA);
        }
    }

    /// <summary>
    /// ファイルを右側（対象リビジョン）の状態にリセットする。
    /// 変更種別に応じてgit rmまたはgit checkoutを実行する。
    /// </summary>
    /// <param name="change">リセット対象の変更ファイル</param>
    public async Task ResetToRightAsync(Models.Change change)
    {
        if (change.Index == Models.ChangeState.Deleted)
        {
            // 削除されたファイルは実際に削除する
            var fullpath = Native.OS.GetAbsPath(_repo, change.Path);
            if (File.Exists(fullpath))
                await new Commands.Remove(_repo, [change.Path]).ExecAsync();
        }
        else if (change.Index == Models.ChangeState.Renamed)
        {
            // リネームされたファイルは元のパスを削除し、新しいパスを復元する
            var old = Native.OS.GetAbsPath(_repo, change.OriginalPath);
            if (File.Exists(old))
                await new Commands.Remove(_repo, [change.OriginalPath]).ExecAsync();

            await new Commands.Checkout(_repo).FileWithRevisionAsync(change.Path, ToHead.SHA);
        }
        else
        {
            // 変更・追加されたファイルは対象リビジョンの状態に復元する
            await new Commands.Checkout(_repo).FileWithRevisionAsync(change.Path, ToHead.SHA);
        }
    }

    /// <summary>
    /// 複数ファイルを左側（基準リビジョン）の状態にリセットする。
    /// 削除対象とチェックアウト対象を分類して一括処理する。
    /// </summary>
    /// <param name="changes">リセット対象の変更ファイルリスト</param>
    public async Task ResetMultipleToLeftAsync(List<Models.Change> changes)
    {
        List<string> checkouts = [];
        List<string> removes = [];

        // 変更種別に応じて削除リストとチェックアウトリストに分類する
        foreach (var c in changes)
        {
            if (c.Index == Models.ChangeState.Added)
            {
                // 追加されたファイルは削除リストに追加する
                var fullpath = Native.OS.GetAbsPath(_repo, c.Path);
                if (File.Exists(fullpath))
                    removes.Add(c.Path);
            }
            else if (c.Index == Models.ChangeState.Renamed)
            {
                // リネームされたファイルは新パスを削除し、元パスをチェックアウトする
                var old = Native.OS.GetAbsPath(_repo, c.Path);
                if (File.Exists(old))
                    removes.Add(c.Path);

                checkouts.Add(c.OriginalPath);
            }
            else
            {
                // 変更・削除されたファイルはチェックアウトリストに追加する
                checkouts.Add(c.Path);
            }
        }

        // git rmで不要ファイルを削除する
        if (removes.Count > 0)
            await new Commands.Remove(_repo, removes).ExecAsync();

        // git checkoutで基準リビジョンの状態に復元する
        if (checkouts.Count > 0)
            await new Commands.Checkout(_repo).MultipleFilesWithRevisionAsync(checkouts, _baseHead.SHA);
    }

    /// <summary>
    /// 複数ファイルを右側（対象リビジョン）の状態にリセットする。
    /// 削除対象とチェックアウト対象を分類して一括処理する。
    /// </summary>
    /// <param name="changes">リセット対象の変更ファイルリスト</param>
    public async Task ResetMultipleToRightAsync(List<Models.Change> changes)
    {
        List<string> checkouts = [];
        List<string> removes = [];

        // 変更種別に応じて削除リストとチェックアウトリストに分類する
        foreach (var c in changes)
        {
            if (c.Index == Models.ChangeState.Deleted)
            {
                // 削除されたファイルは削除リストに追加する
                var fullpath = Native.OS.GetAbsPath(_repo, c.Path);
                if (File.Exists(fullpath))
                    removes.Add(c.Path);
            }
            else if (c.Index == Models.ChangeState.Renamed)
            {
                // リネームされたファイルは元パスを削除し、新パスをチェックアウトする
                var renamed = Native.OS.GetAbsPath(_repo, c.OriginalPath);
                if (File.Exists(renamed))
                    removes.Add(c.OriginalPath);

                checkouts.Add(c.Path);
            }
            else
            {
                // 変更・追加されたファイルはチェックアウトリストに追加する
                checkouts.Add(c.Path);
            }
        }

        // git rmで不要ファイルを削除する
        if (removes.Count > 0)
            await new Commands.Remove(_repo, removes).ExecAsync();

        // git checkoutで対象リビジョンの状態に復元する
        if (checkouts.Count > 0)
            await new Commands.Checkout(_repo).MultipleFilesWithRevisionAsync(checkouts, _toHead.SHA);
    }

    /// <summary>
    /// 選択した変更をパッチファイルとして保存する。
    /// </summary>
    /// <param name="changes">パッチに含める変更ファイルリスト</param>
    /// <param name="saveTo">保存先のファイルパス</param>
    public async Task SaveChangesAsPatchAsync(List<Models.Change> changes, string saveTo)
    {
        var succ = await Commands.SaveChangesAsPatch.ProcessRevisionCompareChangesAsync(_repo, changes, _based, _to, saveTo);
        if (succ)
            App.SendNotification(_repo, App.Text("SaveAsPatchSuccess"));
    }

    /// <summary>
    /// 差分データを非同期で再取得する。
    /// 初回はコミット情報も取得し、変更ファイル一覧を読み込む。
    /// </summary>
    private void Refresh()
    {
        IsLoading = true;
        VisibleChanges = [];
        SelectedChanges = [];

        Task.Run(async () =>
        {
            // 初回のみコミット情報を取得する（独立した2コマンドを並列実行）
            if (_baseHead is null)
            {
                var baseHeadTask = new Commands.QuerySingleCommit(_repo, _based)
                    .GetResultAsync();
                var toHeadTask = new Commands.QuerySingleCommit(_repo, _to)
                    .GetResultAsync();
                await Task.WhenAll(baseHeadTask, toHeadTask).ConfigureAwait(false);

                Dispatcher.UIThread.Post(() =>
                {
                    BaseHead = baseHeadTask.Result;
                    ToHead = toHeadTask.Result;
                });
            }

            // 2つのリビジョン間の変更ファイル一覧を取得する
            _changes = await new Commands.CompareRevisions(_repo, _based, _to)
                .ReadAsync()
                .ConfigureAwait(false);

            // フィルタが設定されている場合はフィルタを適用する
            var visible = _changes;
            if (!string.IsNullOrWhiteSpace(_searchFilter))
            {
                visible = [];
                foreach (var c in _changes)
                {
                    if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                        visible.Add(c);
                }
            }

            // UIスレッドで結果を反映する
            Dispatcher.UIThread.Post(() =>
            {
                TotalChanges = _changes.Count;
                VisibleChanges = visible;
                IsLoading = false;

                // 先頭の変更ファイルを自動選択する
                if (VisibleChanges.Count > 0)
                    SelectedChanges = [VisibleChanges[0]];
                else
                    SelectedChanges = [];
            });
        });
    }

    /// <summary>
    /// フィルタに基づいて表示用変更ファイルリストを更新する。
    /// </summary>
    private void RefreshVisible()
    {
        if (_changes is null)
            return;

        if (string.IsNullOrEmpty(_searchFilter))
        {
            // フィルタなしの場合は全変更を表示する
            VisibleChanges = _changes;
        }
        else
        {
            // パスにフィルタ文字列を含む変更のみ表示する
            List<Models.Change> visible = [];
            foreach (var c in _changes)
            {
                if (c.Path.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase))
                    visible.Add(c);
            }

            VisibleChanges = visible;
        }
    }

    /// <summary>
    /// オブジェクトから表示名を取得する。
    /// ブランチならFriendlyName、タグならName、コミットなら短縮SHAを返す。
    /// </summary>
    /// <param name="obj">ブランチ、タグ、またはコミットオブジェクト</param>
    /// <returns>表示名</returns>
    private static string GetName(object obj)
    {
        return obj switch
        {
            Models.Branch b => b.FriendlyName,
            Models.Tag t => t.Name,
            Models.Commit c => c.SHA[..10],
            _ => "HEAD",
        };
    }

    /// <summary>
    /// オブジェクトからSHA値を取得する。
    /// ブランチならHead、タグならSHA、コミットならSHAを返す。
    /// </summary>
    /// <param name="obj">ブランチ、タグ、またはコミットオブジェクト</param>
    /// <returns>SHA値</returns>
    private static string GetSHA(object obj)
    {
        return obj switch
        {
            Models.Branch b => b.Head,
            Models.Tag t => t.SHA,
            Models.Commit c => c.SHA,
            _ => "HEAD",
        };
    }

    /// <summary>リポジトリのフルパス</summary>
    private string _repo;
    /// <summary>読み込み中フラグ</summary>
    private bool _isLoading = true;
    /// <summary>ファイルリセット可能フラグ</summary>
    private bool _canResetFiles = false;
    /// <summary>比較基準リビジョンのSHA</summary>
    private string _based = string.Empty;
    /// <summary>比較対象リビジョンのSHA</summary>
    private string _to = string.Empty;
    /// <summary>基準リビジョンの表示名</summary>
    private string _baseName = string.Empty;
    /// <summary>対象リビジョンの表示名</summary>
    private string _toName = string.Empty;
    /// <summary>基準リビジョンのコミット情報</summary>
    private Models.Commit _baseHead = null;
    /// <summary>対象リビジョンのコミット情報</summary>
    private Models.Commit _toHead = null;
    /// <summary>全変更ファイル数</summary>
    private int _totalChanges = 0;
    /// <summary>全変更ファイルリスト</summary>
    private List<Models.Change> _changes = null;
    /// <summary>表示中の変更ファイルリスト</summary>
    private List<Models.Change> _visibleChanges = null;
    /// <summary>選択中の変更ファイルリスト</summary>
    private List<Models.Change> _selectedChanges = null;
    /// <summary>検索フィルタ文字列</summary>
    private string _searchFilter = string.Empty;
    /// <summary>差分表示用コンテキスト</summary>
    private DiffContext _diffContext = null;
}
