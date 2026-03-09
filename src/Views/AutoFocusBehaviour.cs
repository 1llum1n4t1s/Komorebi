using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Komorebi.Views
{
    /// <summary>
    ///     テキストボックスへの自動フォーカスを実現するビヘイビア。
    /// </summary>
    public class AutoFocusBehaviour : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<AutoFocusBehaviour, TextBox, bool>("IsEnabled");

        /// <summary>
        ///     コンストラクタ。コンポーネントを初期化する。
        /// </summary>
        static AutoFocusBehaviour()
        {
            IsEnabledProperty.Changed.AddClassHandler<TextBox>(OnIsEnabledChanged);
        }

        /// <summary>
        ///     GetIsEnabledの処理を行う。
        /// </summary>
        public static bool GetIsEnabled(AvaloniaObject elem)
        {
            return elem.GetValue(IsEnabledProperty);
        }

        /// <summary>
        ///     SetIsEnabledの処理を行う。
        /// </summary>
        public static void SetIsEnabled(AvaloniaObject elem, bool value)
        {
            elem.SetValue(IsEnabledProperty, value);
        }

        /// <summary>
        ///     IsEnabledChangedイベントのハンドラ。
        /// </summary>
        private static void OnIsEnabledChanged(TextBox elem, AvaloniaPropertyChangedEventArgs e)
        {
            if (GetIsEnabled(elem))
            {
                elem.AttachedToVisualTree += (o, _) =>
                {
                    if (o is TextBox box)
                    {
                        box.Focus(NavigationMethod.Directional);
                        box.CaretIndex = box.Text?.Length ?? 0;
                    }
                };
            }
        }
    }
}
