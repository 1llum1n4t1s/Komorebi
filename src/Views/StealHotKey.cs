using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
/// ホットキーのキー入力をキャプチャするためのカスタムコントロール。
/// </summary>
public class StealHotKey(Key key, KeyModifiers keyModifiers = KeyModifiers.None)
{
    /// <summary>
    /// キャプチャ対象のキー。
    /// </summary>
    public Key Key { get; } = key;

    /// <summary>
    /// キャプチャ対象の修飾キー（Ctrl, Shift等）。
    /// </summary>
    public KeyModifiers KeyModifiers { get; } = keyModifiers;

    /// <summary>
    /// Enterキーのホットキー定義（プリセット）。
    /// </summary>
    public static StealHotKey Enter { get; } = new StealHotKey(Key.Enter);
}
