using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

public partial class DealWithLocalChangesMethod : UserControl
{
    public static readonly StyledProperty<Models.DealWithLocalChanges> MethodProperty =
        AvaloniaProperty.Register<DealWithLocalChangesMethod, Models.DealWithLocalChanges>(
            nameof(Method),
            defaultValue: Models.DealWithLocalChanges.DoNothing);

    public Models.DealWithLocalChanges Method
    {
        get => GetValue(MethodProperty);
        set => SetValue(MethodProperty, value);
    }

    static DealWithLocalChangesMethod()
    {
        MethodProperty.Changed.AddClassHandler<DealWithLocalChangesMethod>((x, _) => x.UpdateRadioButtons());
    }

    public DealWithLocalChangesMethod()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Method が既定値 (DoNothing) のままバインドされると MethodProperty.Changed が発火せず、
        // どのラジオボタンも未選択になる。ロード時に一度明示的に同期して初期選択を確定させる。
        UpdateRadioButtons();
    }

    private void OnRadioButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: Models.DealWithLocalChanges way })
        {
            Method = way;
            e.Handled = true;
        }
    }

    private void UpdateRadioButtons()
    {
        RadioDoNothing.IsChecked = Method == Models.DealWithLocalChanges.DoNothing;
        RadioStashAndReapply.IsChecked = Method == Models.DealWithLocalChanges.StashAndReapply;
        RadioDiscard.IsChecked = Method == Models.DealWithLocalChanges.Discard;
    }
}
