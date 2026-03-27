using System;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Komorebi;

public partial class App
{
    public class Command(Action<object> action) : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter) => action is not null;
        public void Execute(object parameter) => action?.Invoke(parameter);
    }

    public static bool IsCheckForUpdateCommandVisible
    {
        get
        {
#if DISABLE_UPDATE_DETECTION
            return false;
#else
            return true;
#endif
        }
    }

    public static readonly Command OpenPreferencesCommand = new(async _ => await ShowDialog(new Views.Preferences()));
    public static readonly Command OpenHotkeysCommand = new(async _ => await ShowDialog(new Views.Hotkeys()));
    public static readonly Command OpenAppDataDirCommand = new(_ => Native.OS.OpenInFileManager(Native.OS.DataDir));
    public static readonly Command OpenAboutCommand = new(async _ => await ShowDialog(new Views.About()));
    public static readonly Command CheckForUpdateCommand = new(_ => (Current as App)?.Check4Update(true));
    public static readonly Command QuitCommand = new(_ => Quit(0));
    public static readonly Command CopyTextBlockCommand = new(async p =>
    {
        if (p is not TextBlock textBlock)
            return;

        if (textBlock.Inlines is { Count: > 0 } inlines)
            await CopyTextAsync(inlines.Text);
        else if (!string.IsNullOrEmpty(textBlock.Text))
            await CopyTextAsync(textBlock.Text);
    });

    public static readonly Command HideAppCommand = new(_ =>
    {
        if (Current is App app && app.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime lifetime)
            lifetime.TryEnterBackground();
    });

    public static readonly Command ShowAppCommand = new(_ =>
    {
        if (Current is App app && app.TryGetFeature(typeof(IActivatableLifetime)) is IActivatableLifetime lifetime)
            lifetime.TryLeaveBackground();
    });
}
