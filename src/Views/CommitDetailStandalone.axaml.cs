using System;

namespace Komorebi.Views;

public partial class CommitDetailStandalone : ChromelessWindow
{
    public CommitDetailStandalone()
    {
        InitializeComponent();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        (DataContext as IDisposable)?.Dispose();
        DataContext = null;
    }
}
