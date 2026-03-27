using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters;

/// <summary>
///     リポジトリのダーティ状態を表示用の値に変換するコンバータのコレクション。
///     XAMLバインディングで使用される。
/// </summary>
public static class DirtyStateConverters
{
    /// <summary>
    ///     ダーティ状態をブラシ色に変換するコンバータ。
    ///     ローカル変更あり → グレー、プル/プッシュ待ち → ロイヤルブルー、それ以外 → 透明を返す。
    /// </summary>
    public static readonly FuncValueConverter<Models.DirtyState, IBrush> ToBrush =
        new FuncValueConverter<Models.DirtyState, IBrush>(v =>
        {
            // ローカル変更がある場合はグレーを返す
            if (v.HasFlag(Models.DirtyState.HasLocalChanges))
                return Brushes.Gray;

            // プル/プッシュ待ちの場合はロイヤルブルーを返す
            if (v.HasFlag(Models.DirtyState.HasPendingPullOrPush))
                return Brushes.RoyalBlue;

            // クリーンな状態は透明を返す
            return Brushes.Transparent;
        });

    /// <summary>
    ///     ダーティ状態をローカライズされた説明文字列に変換するコンバータ。
    ///     各状態に応じたテキストを「 ・ 」付きで返す。
    /// </summary>
    public static readonly FuncValueConverter<Models.DirtyState, string> ToDesc =
        new FuncValueConverter<Models.DirtyState, string>(v =>
        {
            // ローカル変更ありの説明テキストを返す
            if (v.HasFlag(Models.DirtyState.HasLocalChanges))
                return " • " + App.Text("DirtyState.HasLocalChanges");

            // プル/プッシュ待ちの説明テキストを返す
            if (v.HasFlag(Models.DirtyState.HasPendingPullOrPush))
                return " • " + App.Text("DirtyState.HasPendingPullOrPush");

            // 最新状態の説明テキストを返す
            return " • " + App.Text("DirtyState.UpToDate");
        });
}
