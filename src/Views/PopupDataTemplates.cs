using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Komorebi.Views;

/// <summary>
/// ポップアップViewModel→View自動解決用のデータテンプレート。命名規則でビューを生成する。
/// </summary>
public class PopupDataTemplates : IDataTemplate
{
    /// <summary>
    /// 指定されたデータがこのテンプレートに一致するかを判定する。Popup派生クラスに一致。
    /// </summary>
    public bool Match(object data)
    {
        return data is ViewModels.Popup;
    }

    /// <summary>
    /// ViewModelに対応するViewを生成し、最初のフォーカス可能な入力要素にフォーカスする。
    /// </summary>
    public Control Build(object param)
    {
        var control = App.CreateViewForViewModel(param);

        control.Loaded += (o, e) =>
        {
            if (o is not Control ctl)
                return;

            var inputs = ctl.GetVisualDescendants();
            foreach (var input in inputs)
            {
                if (input is SelectableTextBlock)
                    continue;

                if (input is InputElement { Focusable: true, IsEffectivelyEnabled: true } focusable)
                {
                    focusable.Focus(NavigationMethod.Directional);
                    if (input is TextBox box)
                        box.CaretIndex = box.Text?.Length ?? 0;
                    return;
                }
            }
        };

        return control;
    }
}
