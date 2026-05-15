using System;

using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views;

/// <summary>
/// <see cref="ListBox"/> をそのまま継承し、Enter/Space キーの自動消化だけ抑止する派生クラス。
/// </summary>
/// <remarks>
/// AvaloniaUI 12 の <see cref="ListBox"/> は <c>OnKeyDown</c> で Enter/Space を hard-coded に
/// 処理してしまうため、上位 (Command Palette のキー確定、ドロップダウンの閉じる等) で
/// それらキーをハンドルしたい場面と干渉する。<see cref="StyleKeyOverride"/> を維持することで
/// 既存スタイルは全て継承されるので、置き換えはほぼ no-op で済む。
///
/// upstream <c>fbe82dbf</c> 由来。
/// </remarks>
public class ListBoxEx : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter || e.Key == Key.Space)
            return;

        base.OnKeyDown(e);
    }
}
