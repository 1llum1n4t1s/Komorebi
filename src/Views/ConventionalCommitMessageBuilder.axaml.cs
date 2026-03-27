using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
///     Conventional Commitsメッセージ構築ダイアログのコードビハインド。
/// </summary>
public partial class ConventionalCommitMessageBuilder : ChromelessWindow
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public ConventionalCommitMessageBuilder()
    {
        CloseOnESC = true;
        InitializeComponent();
    }

    /// <summary>
    ///     ApplyClickedイベントのハンドラ。
    /// </summary>
    private void OnApplyClicked(object _, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.ConventionalCommitMessageBuilder builder)
        {
            if (builder.Apply())
                Close();
        }

        e.Handled = true;
    }
}
