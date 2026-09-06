using System;

namespace Komorebi.Views;

public partial class RevisionCompareStandalone : ChromelessWindow
{
    public RevisionCompareStandalone()
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
