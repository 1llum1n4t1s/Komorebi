using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
/// 差分表示ビュー（テキスト/画像の差分切替）のコードビハインド。
/// </summary>
public partial class DiffView : UserControl
{
    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public DiffView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// ナビゲーション用ホットキーの有効/無効を切り替える。
    /// Avalonia の HotKey は静的 XAML 宣言だと最初にロードされたインスタンスにしか
    /// ルーティングされないため、ロード/アンロードのタイミングで動的に付け外しする。
    /// </summary>
    public void ToggleHotkeyBindings(bool enabled)
    {
        var isMacOS = OperatingSystem.IsMacOS();
        if (enabled)
        {
            BtnGotoFirstChange.HotKey = KeyGesture.Parse(isMacOS ? "Cmd+Alt+Home" : "Ctrl+Alt+Home");
            BtnGotoPrevChange.HotKey = KeyGesture.Parse(isMacOS ? "Cmd+Alt+Up" : "Ctrl+Alt+Up");
            BtnGotoNextChange.HotKey = KeyGesture.Parse(isMacOS ? "Cmd+Alt+Down" : "Ctrl+Alt+Down");
            BtnGotoLastChange.HotKey = KeyGesture.Parse(isMacOS ? "Cmd+Alt+End" : "Ctrl+Alt+End");
            BtnOpenExternalMergeTool.HotKey = KeyGesture.Parse(isMacOS ? "Cmd+Shift+D" : "Ctrl+Shift+D");
        }
        else
        {
            BtnGotoFirstChange.HotKey = null;
            BtnGotoPrevChange.HotKey = null;
            BtnGotoNextChange.HotKey = null;
            BtnGotoLastChange.HotKey = null;
            BtnOpenExternalMergeTool.HotKey = null;
        }
    }

    /// <summary>
    /// コントロールが読み込まれた際の処理。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (DataContext is ViewModels.DiffContext vm)
            vm.CheckSettings();

        ToggleHotkeyBindings(true);
    }

    /// <summary>
    /// コントロールがアンロードされた際の処理。
    /// Komorebi はタブ切替のたびに View インスタンスを作り直すため、
    /// ここでホットキーを解除しないと切替後の新インスタンスに正しくルーティングされない。
    /// </summary>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        ToggleHotkeyBindings(false);
    }

    /// <summary>
    /// GotoFirstChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoFirstChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.First);
        e.Handled = true;
    }

    /// <summary>
    /// GotoPrevChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoPrevChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.Prev);
        e.Handled = true;
    }

    /// <summary>
    /// GotoNextChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoNextChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.Next);
        e.Handled = true;
    }

    /// <summary>
    /// GotoLastChangeイベントのハンドラ。
    /// </summary>
    private void OnGotoLastChange(object _, RoutedEventArgs e)
    {
        this.FindDescendantOfType<ThemedTextDiffPresenter>()?.GotoChange(ViewModels.BlockNavigationDirection.Last);
        e.Handled = true;
    }

    /// <summary>
    /// サブモジュールのリビジョン比較ダイアログを親ウィンドウと同じスクリーンに表示する。
    /// App.ShowWindow はオーナーの位置を継承してウィンドウを配置するため、明示的な owner 指定は不要。
    /// </summary>
    private void OnOpenSubmoduleRevisionCompare(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: Models.SubmoduleDiff diff } && diff.CanOpenDetails)
        {
            var vm = new ViewModels.SubmoduleRevisionCompare(diff);
            App.ShowWindow(vm);
        }
    }
}
