using System;

using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
/// Clickの実行中だけ入力修飾キーを公開し、キーボード・支援技術の標準操作も受け付けるボタン。
/// </summary>
public class ModifierButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    public KeyModifiers ClickModifiers { get; private set; }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ClickModifiers = e.KeyModifiers;
        try
        {
            base.OnPointerPressed(e);
        }
        finally
        {
            ClickModifiers = KeyModifiers.None;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ClickModifiers = e.KeyModifiers;
        try
        {
            base.OnPointerReleased(e);
        }
        finally
        {
            ClickModifiers = KeyModifiers.None;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ClickModifiers = e.KeyModifiers;
        try
        {
            base.OnKeyDown(e);
        }
        finally
        {
            ClickModifiers = KeyModifiers.None;
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        ClickModifiers = e.KeyModifiers;
        try
        {
            base.OnKeyUp(e);
        }
        finally
        {
            ClickModifiers = KeyModifiers.None;
        }
    }
}
