using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down)
        {
            var value = (int)Method + (e.Key == Key.Down ? 1 : -1);
            Method = (Models.DealWithLocalChanges)((value + 3) % 3);
            e.Handled = true;
        }
        else
            base.OnKeyDown(e);
    }

    private void OnRadioButtonClicked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: Models.DealWithLocalChanges way })
        {
            Focus();
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
