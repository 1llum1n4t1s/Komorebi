using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace Komorebi.Views;

/// <summary>
/// LauncherTabSizeBoxクラス。
/// </summary>
public class LauncherTabSizeBox : Border
{
    /// <summary>
    /// 固定幅を使用するかどうかのスタイルプロパティ。
    /// </summary>
    public static readonly StyledProperty<bool> UseFixedWidthProperty =
        AvaloniaProperty.Register<LauncherTabSizeBox, bool>(nameof(UseFixedWidth), true);

    /// <summary>
    /// 固定幅を使用するかどうかを取得・設定する。trueの場合は200px固定。
    /// </summary>
    public bool UseFixedWidth
    {
        get => GetValue(UseFixedWidthProperty);
        set => SetValue(UseFixedWidthProperty, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public LauncherTabSizeBox()
    {
        Width = 200;
    }

    /// <summary>
    /// スタイルキーをBorderとしてオーバーライドする。
    /// </summary>
    protected override Type StyleKeyOverride => typeof(Border);

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == UseFixedWidthProperty)
        {
            if (UseFixedWidth)
                Width = 200;
            else
                Width = double.NaN;
        }
    }
}

/// <summary>
/// ランチャーのタブバーコントロールのコードビハインド。
/// </summary>
public partial class LauncherTabBar : UserControl
{
    /// <summary>
    /// タブスクロールボタンの表示状態を保持するスタイルプロパティ。
    /// </summary>
    public static readonly StyledProperty<bool> IsScrollerVisibleProperty =
        AvaloniaProperty.Register<LauncherTabBar, bool>(nameof(IsScrollerVisible));

    /// <summary>
    /// タブスクロールボタンの表示状態を取得・設定する。タブ幅がビューポートを超える場合にtrue。
    /// </summary>
    public bool IsScrollerVisible
    {
        get => GetValue(IsScrollerVisibleProperty);
        set => SetValue(IsScrollerVisibleProperty, value);
    }

    /// <summary>
    /// コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public LauncherTabBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// コントロールの描画処理を行う。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (LauncherTabsList is null || LauncherTabsList.SelectedIndex == -1)
            return;

        var startX = LauncherTabsScroller.Offset.X;
        var endX = startX + LauncherTabsScroller.Viewport.Width;
        var height = LauncherTabsScroller.Viewport.Height;

        var selectedIdx = LauncherTabsList.SelectedIndex;
        var count = LauncherTabsList.ItemCount;
        var separatorPen = new Pen(new SolidColorBrush(ActualThemeVariant == ThemeVariant.Dark ? Colors.White : Colors.Black, 0.2));
        var separatorY = (height - 18) * 0.5 + 1;

        if (!IsScrollerVisible && selectedIdx > 0)
        {
            var container = LauncherTabsList.ContainerFromIndex(0);
            if (container is not null)
            {
                var x = container.Bounds.Left - startX + LauncherTabsScroller.Bounds.X - 0.5;
                context.DrawLine(separatorPen, new Point(x, separatorY), new Point(x, separatorY + 18));
            }
        }

        for (var i = 0; i < count; i++)
        {
            if (i == selectedIdx || i == selectedIdx - 1)
                continue;

            var container = LauncherTabsList.ContainerFromIndex(i);
            if (container is null)
                continue;

            var containerEndX = container.Bounds.Right;
            if (containerEndX < startX || containerEndX > endX)
                continue;

            if (IsScrollerVisible && i == count - 1)
                break;

            var separatorX = containerEndX - startX + LauncherTabsScroller.Bounds.X - 0.5;
            context.DrawLine(separatorPen, new Point(separatorX, separatorY), new Point(separatorX, separatorY + 18));
        }

        var selected = LauncherTabsList.ContainerFromIndex(selectedIdx);
        if (selected is null)
            return;

        var activeStartX = selected.Bounds.X;
        var activeEndX = activeStartX + selected.Bounds.Width;
        if (activeStartX > endX + 5 || activeEndX < startX - 5)
            return;

        var geo = new StreamGeometry();
        const double angle = Math.PI / 2;
        var bottom = height + 0.5;
        var cornerSize = new Size(5, 5);

        using (var ctx = geo.Open())
        {
            var drawLeftX = activeStartX - startX + LauncherTabsScroller.Bounds.X;
            if (drawLeftX < LauncherTabsScroller.Bounds.X)
            {
                ctx.BeginFigure(new Point(LauncherTabsScroller.Bounds.X - 0.5, bottom), true);
                ctx.LineTo(new Point(LauncherTabsScroller.Bounds.X - 0.5, 0.5));
            }
            else
            {
                ctx.BeginFigure(new Point(drawLeftX - 5.5, bottom), true);
                ctx.ArcTo(new Point(drawLeftX - 0.5, bottom - 5), cornerSize, angle, false, SweepDirection.CounterClockwise);
                ctx.LineTo(new Point(drawLeftX - 0.5, 5.5));
                ctx.ArcTo(new Point(drawLeftX + 4.5, 0.5), cornerSize, angle, false, SweepDirection.Clockwise);
            }

            var drawRightX = activeEndX - startX + LauncherTabsScroller.Bounds.X;
            if (drawRightX <= LauncherTabsScroller.Bounds.Right)
            {
                ctx.LineTo(new Point(drawRightX - 5.5, 0.5));
                ctx.ArcTo(new Point(drawRightX - 0.5, 5.5), cornerSize, angle, false, SweepDirection.Clockwise);
                ctx.LineTo(new Point(drawRightX - 0.5, bottom - 5));
                ctx.ArcTo(new Point(drawRightX + 4.5, bottom), cornerSize, angle, false, SweepDirection.CounterClockwise);
            }
            else
            {
                ctx.LineTo(new Point(LauncherTabsScroller.Bounds.Right - 0.5, 0.5));
                ctx.LineTo(new Point(LauncherTabsScroller.Bounds.Right - 0.5, bottom));
            }
        }

        var fill = this.FindResource("Brush.ToolBar") as IBrush;
        var stroke = new Pen(this.FindResource("Brush.Border0") as IBrush);
        context.DrawGeometry(fill, stroke, geo);
    }

    /// <summary>
    /// プロパティが変更された際の処理。
    /// </summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue is not null)
            InvalidateVisual();
    }

    /// <summary>
    /// ScrollTabsの処理を行う。
    /// </summary>
    private void ScrollTabs(object _, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (e.Delta.Y < 0)
                LauncherTabsScroller.LineRight();
            else if (e.Delta.Y > 0)
                LauncherTabsScroller.LineLeft();
            e.Handled = true;
        }
    }

    /// <summary>
    /// ScrollTabsLeftの処理を行う。
    /// </summary>
    private void ScrollTabsLeft(object _, RoutedEventArgs e)
    {
        LauncherTabsScroller.LineLeft();
        e.Handled = true;
    }

    /// <summary>
    /// ScrollTabsRightの処理を行う。
    /// </summary>
    private void ScrollTabsRight(object _, RoutedEventArgs e)
    {
        LauncherTabsScroller.LineRight();
        e.Handled = true;
    }

    /// <summary>
    /// TabsLayoutUpdatedイベントのハンドラ。
    /// </summary>
    private void OnTabsLayoutUpdated(object _1, EventArgs _2)
    {
        SetCurrentValue(IsScrollerVisibleProperty, LauncherTabsScroller.Extent.Width > LauncherTabsScroller.Viewport.Width);
        InvalidateVisual();
    }

    /// <summary>
    /// TabsSelectionChangedイベントのハンドラ。
    /// </summary>
    private void OnTabsSelectionChanged(object _1, SelectionChangedEventArgs _2)
    {
        InvalidateVisual();
    }

    /// <summary>
    /// PointerPressedTabイベントのハンドラ。
    /// </summary>
    private void OnPointerPressedTab(object sender, PointerPressedEventArgs e)
    {
        if (sender is Border border)
        {
            var point = e.GetCurrentPoint(border);
            if (point.Properties.IsMiddleButtonPressed && border.DataContext is ViewModels.LauncherPage page)
            {
                if (DataContext is ViewModels.Launcher vm)
                    vm.CloseTab(page);
                e.Handled = true;
            }
            else
            {
                _pressedTab = true;
                _startDragTab = false;
                _pressedTabPosition = e.GetPosition(border);
            }
        }
    }

    /// <summary>
    /// PointerReleasedTabイベントのハンドラ。
    /// </summary>
    private void OnPointerReleasedTab(object _1, PointerReleasedEventArgs _2)
    {
        _pressedTab = false;
        _startDragTab = false;
    }

    /// <summary>
    /// PointerMovedOverTabイベントのハンドラ。
    /// </summary>
    private async void OnPointerMovedOverTab(object sender, PointerEventArgs e)
    {
        if (_pressedTab && !_startDragTab && sender is Border { DataContext: ViewModels.LauncherPage page } border)
        {
            var delta = e.GetPosition(border) - _pressedTabPosition;
            var sizeSquired = delta.X * delta.X + delta.Y * delta.Y;
            if (sizeSquired < 64)
                return;

            _startDragTab = true;

            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(_dndMainTabFormat, page.Node.Id));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        e.Handled = true;
    }

    /// <summary>
    /// DropTabの処理を行う。
    /// </summary>
    private void DropTab(object sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(_dndMainTabFormat) is not { Length: > 0 } id)
            return;

        if (DataContext is not ViewModels.Launcher launcher)
            return;

        ViewModels.LauncherPage target = null;
        foreach (var page in launcher.Pages)
        {
            if (page.Node.Id.Equals(id, StringComparison.Ordinal))
            {
                target = page;
                break;
            }
        }

        if (target is null)
            return;

        if (sender is not Border { DataContext: ViewModels.LauncherPage to })
            return;

        if (target == to)
            return;

        launcher.MoveTab(target, to);

        _pressedTab = false;
        _startDragTab = false;
        e.Handled = true;
    }

    /// <summary>
    /// TabContextRequestedイベントのハンドラ。
    /// </summary>
    private void OnTabContextRequested(object sender, ContextRequestedEventArgs e)
    {
        if (sender is Border { DataContext: ViewModels.LauncherPage page } border &&
            DataContext is ViewModels.Launcher vm)
        {
            var menu = new ContextMenu();

            if (vm.ActivePage.Data is ViewModels.Repository repo)
            {
                var refresh = new MenuItem();
                refresh.Header = App.Text("PageTabBar.Tab.Refresh");
                refresh.Icon = App.CreateMenuIcon("Icons.Loading");
                refresh.Tag = "F5";
                refresh.Click += (_, ev) =>
                {
                    repo.RefreshAll();
                    ev.Handled = true;
                };
                menu.Items.Add(refresh);

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("PageTabBar.Tab.CopyPath");
                copyPath.Icon = App.CreateMenuIcon("Icons.Copy");
                copyPath.Click += async (_, ev) =>
                {
                    await page.CopyPathAsync();
                    ev.Handled = true;
                };
                menu.Items.Add(copyPath);
                menu.Items.Add(new MenuItem() { Header = "-" });

                var bookmark = new MenuItem();
                bookmark.Header = App.Text("PageTabBar.Tab.Bookmark");
                bookmark.Icon = App.CreateMenuIcon("Icons.Bookmark");

                for (int i = 0; i < Models.Bookmarks.Brushes.Length; i++)
                {
                    var brush = Models.Bookmarks.Brushes[i];
                    var icon = App.CreateMenuIcon("Icons.Bookmark");
                    if (brush is not null)
                        icon.Fill = brush;

                    var dupIdx = i;
                    var setter = new MenuItem() { Header = icon };
                    if (i == page.Node.Bookmark)
                        setter.Icon = App.CreateMenuIcon("Icons.Check");
                    else
                        setter.Click += (_, ev) =>
                        {
                            page.Node.Bookmark = dupIdx;
                            ev.Handled = true;
                        };

                    bookmark.Items.Add(setter);
                }
                menu.Items.Add(bookmark);

                var workspaces = ViewModels.Preferences.Instance.Workspaces;
                if (workspaces.Count > 1)
                {
                    var moveTo = new MenuItem();
                    moveTo.Header = App.Text("PageTabBar.Tab.MoveToWorkspace");
                    moveTo.Icon = App.CreateMenuIcon("Icons.MoveTo");

                    foreach (var ws in workspaces)
                    {
                        var dupWs = ws;
                        var isCurrent = dupWs == vm.ActiveWorkspace;
                        var icon = App.CreateMenuIcon(isCurrent ? "Icons.Check" : "Icons.Workspace");
                        icon.Fill = dupWs.Brush;

                        var target = new MenuItem();
                        target.Header = ws.Name;
                        target.Icon = icon;
                        target.Click += (_, ev) =>
                        {
                            if (!isCurrent)
                            {
                                vm.CloseTab(page);
                                dupWs.Repositories.Add(repo.FullPath);
                            }

                            ev.Handled = true;
                        };
                        moveTo.Items.Add(target);
                    }

                    menu.Items.Add(moveTo);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var close = new MenuItem();
            close.Header = App.Text("PageTabBar.Tab.Close");
            close.Tag = OperatingSystem.IsMacOS() ? "?+W" : "Ctrl+W";
            close.Click += (_, ev) =>
            {
                vm.CloseTab(page);
                ev.Handled = true;
            };
            menu.Items.Add(close);

            var closeOthers = new MenuItem();
            closeOthers.Header = App.Text("PageTabBar.Tab.CloseOther");
            closeOthers.Click += (_, ev) =>
            {
                vm.CloseOtherTabs();
                ev.Handled = true;
            };
            menu.Items.Add(closeOthers);

            var closeRight = new MenuItem();
            closeRight.Header = App.Text("PageTabBar.Tab.CloseRight");
            closeRight.Click += (_, ev) =>
            {
                vm.CloseRightTabs();
                ev.Handled = true;
            };
            menu.Items.Add(closeRight);
            menu.Open(border);
        }

        e.Handled = true;
    }

    /// <summary>
    /// CloseTabイベントのハンドラ。
    /// </summary>
    private void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && DataContext is ViewModels.Launcher vm)
        {
            if (btn.DataContext is ViewModels.LauncherPage page)
                vm.CloseTab(page);
        }

        e.Handled = true;
    }

    /// <summary>
    /// タブがポインター押下中かどうか。
    /// </summary>
    private bool _pressedTab = false;

    /// <summary>
    /// タブが押下された位置。ドラッグ判定に使用する。
    /// </summary>
    private Point _pressedTabPosition = new();

    /// <summary>
    /// タブのドラッグ操作が開始されたかどうか。
    /// </summary>
    private bool _startDragTab = false;

    /// <summary>
    /// タブのドラッグ&amp;ドロップ時の識別用データフォーマット。
    /// </summary>
    private readonly DataFormat<string> _dndMainTabFormat = DataFormat.CreateStringApplicationFormat("komorebi-dnd-main-tab");
}
