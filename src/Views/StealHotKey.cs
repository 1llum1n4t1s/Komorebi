using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
///     ホットキーのキー入力をキャプチャするためのカスタムコントロール。
/// </summary>
public class StealHotKey(Key key, KeyModifiers keyModifiers = KeyModifiers.None)
{
    public Key Key { get; } = key;
    public KeyModifiers KeyModifiers { get; } = keyModifiers;

    public static StealHotKey Enter { get; } = new StealHotKey(Key.Enter);
}
