using System;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
/// コマンドパレットのサブコマンド入力用テキストボックス。
/// 空の状態でBackspaceを押すとメインパレットに戻る。
/// </summary>
public class RepositoryCommandPaletteTextBox : TextBox
{
    /// <summary>
    /// スタイルキーをTextBoxとして解決する。
    /// </summary>
    protected override Type StyleKeyOverride => typeof(TextBox);

    /// <summary>
    /// 読み込み時にテキストボックスへフォーカスを設定する。
    /// </summary>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Focus(NavigationMethod.Directional);
    }

    /// <summary>
    /// キー押下時の処理。テキストが空の状態でBackspaceを押すとメインパレットに戻る。
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Back && string.IsNullOrEmpty(Text))
        {
            var launcher = App.GetLauncher();
            if (launcher is { ActivePage: { Data: ViewModels.Repository repo } })
            {
                // メインのコマンドパレットに切り替え
                launcher.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                e.Handled = true;
                return;
            }
        }

        base.OnKeyDown(e);
    }
}
