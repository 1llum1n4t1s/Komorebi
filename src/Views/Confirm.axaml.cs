using Avalonia.Interactivity;

namespace Komorebi.Views;

/// <summary>
///     汎用確認ダイアログのコードビハインド。
/// </summary>
public partial class Confirm : ChromelessWindow
{
    /// <summary>
    ///     コンストラクタ。コンポーネントを初期化する。
    /// </summary>
    public Confirm()
    {
        InitializeComponent();
    }

    /// <summary>
    ///     SetDataの処理を行う。
    /// </summary>
    public void SetData(string message, Models.ConfirmButtonType buttonType)
    {
        Message.Text = message;

        switch (buttonType)
        {
            case Models.ConfirmButtonType.OkCancel:
                BtnYes.Content = App.Text("Sure");
                BtnNo.Content = App.Text("Cancel");
                break;
            case Models.ConfirmButtonType.YesNo:
                BtnYes.Content = App.Text("Yes");
                BtnNo.Content = App.Text("No");
                break;
        }
    }

    /// <summary>
    ///     Sureの処理を行う。
    /// </summary>
    private void Sure(object _1, RoutedEventArgs _2)
    {
        Close(true);
    }

    /// <summary>
    ///     CloseWindowの処理を行う。
    /// </summary>
    private void CloseWindow(object _1, RoutedEventArgs _2)
    {
        Close(false);
    }
}
