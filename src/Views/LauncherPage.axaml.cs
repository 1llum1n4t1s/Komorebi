using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Komorebi.Views
{
    /// <summary>
    ///     ランチャーページ（各タブの内容表示）のコードビハインド。
    /// </summary>
    public partial class LauncherPage : UserControl
    {
        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        public LauncherPage()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     PopupSureByHotKeyイベントのハンドラ。
        /// </summary>
        private async void OnPopupSureByHotKey(object sender, RoutedEventArgs e)
        {
            var children = PopupPanel.GetLogicalDescendants();
            foreach (var child in children)
            {
                if (child is Control { IsKeyboardFocusWithin: true, Tag: StealHotKey steal } control &&
                    steal is { Key: Key.Enter, KeyModifiers: KeyModifiers.None })
                {
                    var fake = new KeyEventArgs()
                    {
                        RoutedEvent = KeyDownEvent,
                        Route = RoutingStrategies.Direct,
                        Source = control,
                        Key = Key.Enter,
                        KeyModifiers = KeyModifiers.None,
                        PhysicalKey = PhysicalKey.Enter,
                    };

                    if (control is AvaloniaEdit.TextEditor editor)
                        editor.TextArea.TextView.RaiseEvent(fake);
                    else
                        control.RaiseEvent(fake);

                    e.Handled = false;
                    return;
                }
            }

            if (DataContext is ViewModels.LauncherPage page)
                await page.ProcessPopupAsync();

            e.Handled = true;
        }

        /// <summary>
        ///     PopupSureイベントのハンドラ。
        /// </summary>
        private async void OnPopupSure(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                await page.ProcessPopupAsync();

            e.Handled = true;
        }

        /// <summary>
        ///     PopupCancelイベントのハンドラ。
        /// </summary>
        private void OnPopupCancel(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.LauncherPage page)
                page.CancelPopup();

            e.Handled = true;
        }

        /// <summary>
        ///     MaskClickedイベントのハンドラ。
        /// </summary>
        private void OnMaskClicked(object sender, PointerPressedEventArgs e)
        {
            OnPopupCancel(sender, e);
        }

        /// <summary>
        ///     CopyNotificationイベントのハンドラ。
        /// </summary>
        private async void OnCopyNotification(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Notification notice })
                await App.CopyTextAsync(notice.Message);

            e.Handled = true;
        }

        /// <summary>
        ///     DismissNotificationイベントのハンドラ。
        /// </summary>
        private void OnDismissNotification(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: Models.Notification notice } &&
                DataContext is ViewModels.LauncherPage page)
                page.Notifications.Remove(notice);

            e.Handled = true;
        }

        /// <summary>
        ///     ToolBarPointerPressedイベントのハンドラ。
        /// </summary>
        private void OnToolBarPointerPressed(object sender, PointerPressedEventArgs e)
        {
            this.FindAncestorOfType<ChromelessWindow>()?.BeginMoveWindow(sender, e);
        }
    }
}
