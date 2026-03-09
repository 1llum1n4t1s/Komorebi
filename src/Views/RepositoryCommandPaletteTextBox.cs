using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views
{
    /// <summary>
    ///     リポジトリコマンドパレットの入力テキストボックス。
    /// </summary>
    public class RepositoryCommandPaletteTextBox : TextBox
    {
        protected override Type StyleKeyOverride => typeof(TextBox);

        /// <summary>
        ///     キーが押された際のイベント処理。
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Back && string.IsNullOrEmpty(Text))
            {
                var launcher = App.GetLauncher();
                if (launcher is { ActivePage: { Data: ViewModels.Repository repo } })
                {
                    launcher.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                    e.Handled = true;
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        /// <summary>
        ///     ビジュアルツリーにアタッチされた際の処理。
        /// </summary>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Focus();
        }
    }
}
