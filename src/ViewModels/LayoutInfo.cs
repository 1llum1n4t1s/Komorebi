using System;

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

    /// <summary>File History ウィンドウの幅（upstream issue #2100 対応）。</summary>
    public double FileHistoriesWidth
    {
        get;
        set;
    } = 1280;

    /// <summary>File History ウィンドウの高さ（upstream issue #2100 対応）。</summary>
    public double FileHistoriesHeight
    {
        get;
        set;
    } = 720;

    /// <summary>File History ウィンドウの X 座標（複数モニタ対応）。</summary>
    public int FileHistoriesPositionX
    {
        get;
        set;
    } = int.MinValue;

    /// <summary>File History ウィンドウの Y 座標（複数モニタ対応）。</summary>
    public int FileHistoriesPositionY
    {
        get;
        set;
    } = int.MinValue;

    /// <summary>File History ウィンドウの状態（通常/最大化）。</summary>
    public WindowState FileHistoriesWindowState
    {
        get;
        set;
    } = WindowState.Normal;

    /// <summary>Blame ウィンドウの幅（upstream issue #2100 対応）。</summary>
    public double BlameWidth
    {
        get;
        set;
    } = 1280;

    /// <summary>Blame ウィンドウの高さ。</summary>
    public double BlameHeight
    {
        get;
        set;
    } = 720;

    /// <summary>Blame ウィンドウの X 座標。</summary>
    public int BlamePositionX
    {
        get;
        set;
    } = int.MinValue;

    /// <summary>Blame ウィンドウの Y 座標。</summary>
    public int BlamePositionY
    {
        get;
        set;
    } = int.MinValue;

    /// <summary>Blame ウィンドウの状態（通常/最大化）。</summary>
    public WindowState BlameWindowState
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
        set
        {
            // 永続化された巨大値や 0 / 負値を防ぐため両端をクランプ。
            // XAML 側の MinWidth=80 / MaxWidth=400 と一致させる。
            var clamped = Math.Clamp(value.Value, AuthorColumnMinWidth, AuthorColumnMaxWidth);
            // 2 引数コンストラクタで Value/DesiredValue/DisplayValue を統一する。
            // Histories.axaml.cs の WidthProperty 観測経由で DataGrid から書き戻される
            // DataGridLength（DesiredValue=DisplayValue）と struct 等価判定が一致し、
            // バインディング往復による ping-pong を避けられる。
            SetProperty(ref _authorColumnWidth, new DataGridLength(clamped, DataGridLengthUnitType.Pixel));
        }
    }

    private const double AuthorColumnMinWidth = 80;
    private const double AuthorColumnMaxWidth = 400;
    private const double AuthorColumnDefaultWidth = 120;

    private GridLength _repositorySidebarWidth = new GridLength(250, GridUnitType.Pixel);
    private GridLength _workingCopyLeftWidth = new GridLength(300, GridUnitType.Pixel);
    private GridLength _stashesLeftWidth = new GridLength(300, GridUnitType.Pixel);
    private GridLength _commitDetailChangesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    private GridLength _commitDetailFilesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    private DataGridLength _authorColumnWidth = new DataGridLength(AuthorColumnDefaultWidth, DataGridLengthUnitType.Pixel);
}
