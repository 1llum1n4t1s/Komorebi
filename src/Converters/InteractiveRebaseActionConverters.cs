using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters;

/// <summary>
/// インタラクティブリベースのアクションを表示用の値に変換するコンバータのコレクション。
/// リベースエディタのXAMLバインディングで使用される。
/// </summary>
public static class InteractiveRebaseActionConverters
{
    /// <summary>
    /// インタラクティブリベースアクションをアイコンブラシ色に変換するコンバータ。
    /// Pick → 緑、Edit/Reword → オレンジ、Squash/Fixup → ライトグレー、Drop → 赤を返す。
    /// </summary>
    public static readonly FuncValueConverter<Models.InteractiveRebaseAction, IBrush> ToIconBrush =
        new(v =>
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
    /// インタラクティブリベースアクションを文字列名に変換するコンバータ。
    /// enum値のToString()結果を返す（例: "Pick", "Edit"）。
    /// </summary>
    public static readonly FuncValueConverter<Models.InteractiveRebaseAction, string> ToName =
        new(v => v.ToString());

    /// <summary>
    /// アクションがDropであるかを判定するコンバータ。
    /// Dropの場合はtrue、それ以外はfalseを返す。取り消し線表示等に使用する。
    /// </summary>
    public static readonly FuncValueConverter<Models.InteractiveRebaseAction, bool> IsDrop =
        new(v => v == Models.InteractiveRebaseAction.Drop);

    /// <summary>
    /// アクションに応じた不透明度を返すコンバータ。
    /// Squash/Fixup/Drop（Rewordより後の値）は0.65（半透明）、それ以外は1.0（不透明）を返す。
    /// </summary>
    public static readonly FuncValueConverter<Models.InteractiveRebaseAction, double> ToOpacity =
        new(v => v > Models.InteractiveRebaseAction.Reword ? 0.65 : 1.0);
}
