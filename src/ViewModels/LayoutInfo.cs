using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Komorebi.ViewModels;

/// <summary>
/// UIレイアウト情報を保持するクラス。
/// ウィンドウサイズ、サイドバー幅、各パネルの分割幅などを管理する。
/// </summary>
public class LayoutInfo : ObservableObject
{
    /// <summary>ランチャーウィンドウの幅（ピクセル）。</summary>
    public double LauncherWidth
    {
        get;
        set;
    } = 1280;

    /// <summary>ランチャーウィンドウの高さ（ピクセル）。</summary>
    public double LauncherHeight
    {
        get;
        set;
    } = 720;

    /// <summary>ランチャーウィンドウのX座標。</summary>
    public int LauncherPositionX
    {
        get;
        set;
    } = int.MinValue;

    /// <summary>ランチャーウィンドウのY座標。</summary>
    public int LauncherPositionY
    {
        get;
        set;
    } = int.MinValue;

    /// <summary>ランチャーウィンドウの状態（通常/最大化/最小化）。</summary>
    public WindowState LauncherWindowState
    {
        get;
        set;
    } = WindowState.Normal;

    /// <summary>リポジトリ画面のサイドバー幅。</summary>
    public GridLength RepositorySidebarWidth
    {
        get => _repositorySidebarWidth;
        set => SetProperty(ref _repositorySidebarWidth, value);
    }

    /// <summary>作業コピー画面の左パネル幅。</summary>
    public GridLength WorkingCopyLeftWidth
    {
        get => _workingCopyLeftWidth;
        set => SetProperty(ref _workingCopyLeftWidth, value);
    }

    /// <summary>スタッシュ画面の左パネル幅。</summary>
    public GridLength StashesLeftWidth
    {
        get => _stashesLeftWidth;
        set => SetProperty(ref _stashesLeftWidth, value);
    }

    /// <summary>コミット詳細（変更一覧）の左パネル幅。</summary>
    public GridLength CommitDetailChangesLeftWidth
    {
        get => _commitDetailChangesLeftWidth;
        set => SetProperty(ref _commitDetailChangesLeftWidth, value);
    }

    /// <summary>コミット詳細（ファイル一覧）の左パネル幅。</summary>
    public GridLength CommitDetailFilesLeftWidth
    {
        get => _commitDetailFilesLeftWidth;
        set => SetProperty(ref _commitDetailFilesLeftWidth, value);
    }

    /// <summary>コミット履歴の作者カラム幅。</summary>
    public DataGridLength AuthorColumnWidth
    {
        get => _authorColumnWidth;
        set => SetProperty(ref _authorColumnWidth, new DataGridLength(value.Value, DataGridLengthUnitType.Pixel, 0, value.DisplayValue));
    }

    private GridLength _repositorySidebarWidth = new GridLength(250, GridUnitType.Pixel);
    private GridLength _workingCopyLeftWidth = new GridLength(300, GridUnitType.Pixel);
    private GridLength _stashesLeftWidth = new GridLength(300, GridUnitType.Pixel);
    private GridLength _commitDetailChangesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    private GridLength _commitDetailFilesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    private DataGridLength _authorColumnWidth = new DataGridLength(120, DataGridLengthUnitType.Pixel, 0, 120);
}
