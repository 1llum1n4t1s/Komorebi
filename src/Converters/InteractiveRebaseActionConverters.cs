using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters
{
    /// <summary>
    ///     インタラクティブリベースのアクションを表示用の値に変換するコンバータのコレクション。
    ///     リベースエディタのXAMLバインディングで使用される。
    /// </summary>
    public static class InteractiveRebaseActionConverters
    {
        /// <summary>
        ///     インタラクティブリベースアクションをアイコンブラシ色に変換するコンバータ。
        ///     Pick → 緑、Edit/Reword → オレンジ、Squash/Fixup → ライトグレー、Drop → 赤を返す。
        /// </summary>
        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, IBrush> ToIconBrush =
            new FuncValueConverter<Models.InteractiveRebaseAction, IBrush>(v =>
            {
                return v switch
                {
                    // Pickアクション（コミットをそのまま適用）は緑
                    Models.InteractiveRebaseAction.Pick => Brushes.Green,
                    // Editアクション（コミットを編集）はオレンジ
                    Models.InteractiveRebaseAction.Edit => Brushes.Orange,
                    // Rewordアクション（コミットメッセージを変更）はオレンジ
                    Models.InteractiveRebaseAction.Reword => Brushes.Orange,
                    // Squashアクション（前のコミットに統合）はライトグレー
                    Models.InteractiveRebaseAction.Squash => Brushes.LightGray,
                    // Fixupアクション（前のコミットに統合、メッセージ破棄）はライトグレー
                    Models.InteractiveRebaseAction.Fixup => Brushes.LightGray,
                    // Dropアクション（コミットを削除）は赤
                    _ => Brushes.Red,
                };
            });

        /// <summary>
        ///     インタラクティブリベースアクションを文字列名に変換するコンバータ。
        ///     enum値のToString()結果を返す（例: "Pick", "Edit"）。
        /// </summary>
        public static readonly FuncValueConverter<Models.InteractiveRebaseAction, string> ToName =
            new FuncValueConverter<Models.InteractiveRebaseAction, string>(v => v.ToString());
    }
}
