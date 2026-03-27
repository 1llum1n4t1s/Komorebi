using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
///     Gitコマンドログ表示ビューのコードビハインド。
/// </summary>
public partial class ViewLogs : ChromelessWindow
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ViewLogs()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    ///     LogContextRequestedイベントのハンドラ。
    /// </summary>
    private void OnLogContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is not Grid { DataContext: ViewModels.CommandLog log } grid || DataContext is not ViewModels.ViewLogs vm)
            return;

        var copy = new MenuItem();
        copy.Header = App.Text("ViewLogs.CopyLog");
        copy.Icon = App.CreateMenuIcon("Icons.Copy");
        copy.Click += async (_, ev) =>
        {
            await App.CopyTextAsync(log.Content);
            ev.Handled = true;
        };

        var rm = new MenuItem();
        rm.Header = App.Text("ViewLogs.Delete");
        rm.Icon = App.CreateMenuIcon("Icons.Clear");
        rm.Click += (_, ev) =>
        {
            vm.Logs.Remove(log);
            ev.Handled = true;
        };

        var menu = new ContextMenu();
        menu.Items.Add(copy);
        menu.Items.Add(rm);
        menu.Open(grid);

        e.Handled = true;
    }

    /// <summary>
    ///     LogKeyDownイベントのハンドラ。
    /// </summary>
    private void OnLogKeyDown(object _, KeyEventArgs e)
    {
        if (e.Key is not (Key.Delete or Key.Back))
            return;

        if (DataContext is ViewModels.ViewLogs { SelectedLog: { } log } vm)
            vm.Logs.Remove(log);

        e.Handled = true;
    }
}
