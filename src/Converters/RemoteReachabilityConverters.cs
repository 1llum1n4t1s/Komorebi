using Avalonia;
using Avalonia.Controls;
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
    /// 色は Themes.axaml の Brush.RemoteReachability.* から引き、テーマ上書き（Color.RemoteReachability.*）に追従させる。
    /// 返すブラシの Color は DynamicResource で Color.* に結び付いているため、上書き JSON の適用後も
    /// 同じブラシ インスタンスのまま色が変わる。リソースが見つからない場合（テスト環境等）は旧来の固定色へ戻す。
    /// </summary>
    public static readonly FuncValueConverter<Models.RemoteReachability, IBrush> ToBadgeBrush =
        new FuncValueConverter<Models.RemoteReachability, IBrush>(v =>
        {
            return v switch
            {
                Models.RemoteReachability.AllReachable => FindBrush("Brush.RemoteReachability.AllReachable", Brushes.LimeGreen),
                Models.RemoteReachability.SomeUnreachable => FindBrush("Brush.RemoteReachability.SomeUnreachable", Brushes.Gold),
                Models.RemoteReachability.AllUnreachable => FindBrush("Brush.RemoteReachability.AllUnreachable", Brushes.Red),
                Models.RemoteReachability.NoRemotes => FindBrush("Brush.RemoteReachability.NoRemotes", Brushes.Gray),
                _ => Brushes.Transparent,
            };
        });

    /// <summary>
    /// アプリケーション リソースからブラシを引く。BoolConverters/IntConverters と同じく
    /// Application.Current.FindResource を使う（コンバーターは要素ツリーを持たないため）。
    /// </summary>
    private static IBrush FindBrush(string key, IBrush fallback)
    {
        return Application.Current?.FindResource(key) as IBrush ?? fallback;
    }

    /// <summary>
    /// 到達可能性状態がバッジを表示すべき状態かどうかを判定するコンバータ。
    /// Unknown のみ非表示、それ以外は表示する。
    /// </summary>
    public static readonly FuncValueConverter<Models.RemoteReachability, bool> IsBadgeVisible =
        new FuncValueConverter<Models.RemoteReachability, bool>(v =>
            v != Models.RemoteReachability.Unknown);
}
