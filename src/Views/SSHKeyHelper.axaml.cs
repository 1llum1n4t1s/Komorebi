#nullable disable warnings
using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Komorebi.Views;

public partial class SSHKeyHelper : ChromelessWindow
{
    public SSHKeyHelper()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is ViewModels.SSHKeyHelper { IsGenerating: true })
            e.Cancel = true;
        base.OnClosing(e);
    }

    private void OnAddNewKey(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.SSHKeyHelper vm)
            return;

        vm.OpenGenerator();
        e.Handled = true;
    }

    private async void OnGenerateKey(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.SSHKeyHelper vm)
            return;

        await vm.GenerateAsync();
        e.Handled = true;
    }

    private void OnCancelGenerateKey(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.SSHKeyHelper vm)
            return;

        vm.CloseGenerator();
        e.Handled = true;
    }

    private async void OnDeleteSelectedKey(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.SSHKeyHelper { SelectedKey: { } key } vm)
            return;

        var message = new StringBuilder();
        message
            .AppendLine(App.Text("SSHKeyHelper.ConfirmDeletion"))
            .AppendLine()
            .Append("- ").Append(key.PrivateKeyPath).AppendLine()
            .Append("- ").Append(key.PublicKeyPath);

        var confirm = new Confirm();
        confirm.SetData(message.ToString(), Models.ConfirmButtonType.YesNo);

        var yes = await confirm.ShowDialog<bool>(this);
        if (yes)
        {
            try
            {
                vm.DeleteSelected();
            }
            catch (Exception ex)
            {
                await new Alert().ShowAsync(this, ex.Message, isError: true);
            }
        }

        e.Handled = true;
    }
}
