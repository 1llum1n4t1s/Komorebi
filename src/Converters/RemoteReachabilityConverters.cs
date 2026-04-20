using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Komorebi.Converters;

/// <summary>
/// リモート到達可能性状態を表示用の値に変換するコンバータのコレクション。
/// Welcome 画面のリポジトリリストのバッジ表示に使用される。
/// </summary>
public static class RemoteReachabilityConverters
{
    /// <summary>
    /// 到達可能性状態をバッジの塗りブラシに変換するコンバータ。
    /// 信号機スタイル: AllReachable → 緑 / SomeUnreachable → 黄 / AllUnreachable → 赤 /
    /// NoRemotes → グレー / Unknown → 透明（非表示）。
    /// </summary>
    public static readonly FuncValueConverter<Models.RemoteReachability, IBrush> ToBadgeBrush =
        new FuncValueConverter<Models.RemoteReachability, IBrush>(v =>
        {
            return v switch
            {
                Models.RemoteReachability.AllReachable => Brushes.LimeGreen,
                Models.RemoteReachability.SomeUnreachable => Brushes.Gold,
                Models.RemoteReachability.AllUnreachable => Brushes.Red,
                Models.RemoteReachability.NoRemotes => Brushes.Gray,
                _ => Brushes.Transparent,
            };
        });

    /// <summary>
    /// 到達可能性状態がバッジを表示すべき状態かどうかを判定するコンバータ。
    /// Unknown のみ非表示、それ以外は表示する。
    /// </summary>
    public static readonly FuncValueConverter<Models.RemoteReachability, bool> IsBadgeVisible =
        new FuncValueConverter<Models.RemoteReachability, bool>(v =>
            v != Models.RemoteReachability.Unknown);
}
