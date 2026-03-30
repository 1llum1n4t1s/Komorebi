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
        switch (Method)
        {
            case Models.DealWithLocalChanges.DoNothing:
                RadioDoNothing.IsChecked = true;
                RadioStashAndReapply.IsChecked = false;
                RadioDiscard.IsChecked = false;
                break;
            case Models.DealWithLocalChanges.StashAndReapply:
                RadioDoNothing.IsChecked = false;
                RadioStashAndReapply.IsChecked = true;
                RadioDiscard.IsChecked = false;
                break;
            default:
                RadioDoNothing.IsChecked = false;
                RadioStashAndReapply.IsChecked = false;
                RadioDiscard.IsChecked = true;
                break;
        }
    }
}
