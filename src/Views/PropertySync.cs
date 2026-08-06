using System;
using System.ComponentModel;

using Avalonia;

namespace Komorebi.Views;

/// <summary>
/// <see cref="INotifyPropertyChanged"/> のプロパティを Avalonia プロパティへ片方向同期する購読ヘルパー。
/// </summary>
/// <remarks>
/// upstream (SourceGit) は code-behind で <c>new Binding { Path = "..." }</c> や
/// <c>new ReflectionBinding("...")</c> を使うが、これらは文字列パスをリフレクションで解決するため
/// <c>RequiresDynamicCode</c> が付いており、Native AOT 発行時に IL3050 警告 (＝実行時に解決できず
/// バインドが無効化されうる) になる。Komorebi は AOT 発行するので、文字列パス解決を経由しない
/// 直接購読へ置き換えている (upstream からの意図的な乖離)。
/// </remarks>
internal static class PropertySync
{
    /// <summary>
    /// <paramref name="source"/> の指定プロパティを <paramref name="target"/> の Avalonia プロパティへ
    /// 片方向同期する。購読直後に現在値を 1 回適用する。
    /// </summary>
    /// <param name="target">値を受け取る Avalonia オブジェクト。</param>
    /// <param name="property">値を書き込む Avalonia プロパティ。</param>
    /// <param name="source">監視元 (INotifyPropertyChanged 実装)。</param>
    /// <param name="sourcePropertyName">監視するプロパティ名。</param>
    /// <param name="getter">現在値を取り出すデリゲート (リフレクションを使わないため呼び出し側が渡す)。</param>
    /// <returns>Dispose で購読解除されるハンドル。</returns>
    public static IDisposable OneWay<TSource>(
        AvaloniaObject target,
        AvaloniaProperty property,
        TSource source,
        string sourcePropertyName,
        Func<TSource, object> getter)
        where TSource : INotifyPropertyChanged
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(getter);

        void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // PropertyName が空の通知は「全プロパティ変更」を意味するので同じく反映する。
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == sourcePropertyName)
                target.SetCurrentValue(property, getter(source));
        }

        source.PropertyChanged += OnSourcePropertyChanged;
        target.SetCurrentValue(property, getter(source));
        return new Subscription(() => source.PropertyChanged -= OnSourcePropertyChanged);
    }

    /// <summary>Dispose 時に指定処理を 1 回だけ実行するハンドル。</summary>
    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            var d = _dispose;
            _dispose = null;
            d?.Invoke();
        }
    }
}
