using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Komorebi.Views;

/// <summary>
/// ウィンドウ内容を等倍率で拡大／縮小してホストするデコレーター。
/// <para>
/// upstream (および Avalonia) の <see cref="LayoutTransformControl"/> を置き換えるための
/// Komorebi 独自コントロール。LayoutTransformControl の <c>ArrangeOverride</c> には
/// 「子の DesiredSize が与えられた finalSize より大きい場合、finalSize を DesiredSize へ
/// 差し替えて中央寄せする」分岐があり、そこで <b>幅と高さがまとめて</b> DesiredSize に
/// 置き換わる。DPI 125% のようなフラクショナルスケールでは <c>UseLayoutRounding</c> の
/// 切り上げにより子の DesiredSize.Width が利用可能幅を 1 ラウンド単位だけ超えることがあり、
/// 幅のわずかな超過をきっかけに<b>高さまで DesiredSize（＝コンテンツの自然な高さ）に潰され、
/// ウィンドウ内で垂直中央寄せされる</b>という致命的なレイアウト崩れが発生する。
/// </para>
/// <para>
/// 本コントロールは等倍率スケール専用と割り切り、Measure/Arrange で単純に倍率の逆数を
/// 掛けるだけにすることで、その分岐自体を持たない。回転・せん断は扱わない。
/// </para>
/// </summary>
public class ZoomHost : Decorator
{
    /// <summary>
    /// 拡大倍率を保持するスタイルプロパティ。
    /// </summary>
    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ZoomHost, double>(nameof(Zoom), 1.0);

    static ZoomHost()
    {
        AffectsMeasure<ZoomHost>(ZoomProperty);
    }

    /// <summary>
    /// 拡大倍率を取得・設定する。1.0 で等倍。
    /// </summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <summary>
    /// 実際に適用する倍率。不正値（0 以下・NaN・無限大）は 1.0 に丸める。
    /// </summary>
    private double EffectiveZoom
    {
        get
        {
            var zoom = Zoom;
            return double.IsFinite(zoom) && zoom > 0.01 ? zoom : 1.0;
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
            return default;

        var zoom = EffectiveZoom;
        child.Measure(new Size(availableSize.Width / zoom, availableSize.Height / zoom));

        var desired = child.DesiredSize;
        return new Size(desired.Width * zoom, desired.Height * zoom);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is not { } child)
            return finalSize;

        var zoom = EffectiveZoom;
        child.Arrange(new Rect(0, 0, finalSize.Width / zoom, finalSize.Height / zoom));
        return finalSize;
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ZoomProperty || change.Property == ChildProperty)
            UpdateChildRenderTransform();
    }

    /// <summary>
    /// 子要素へ描画用のスケール変換を適用する。等倍のときは変換自体を外す。
    /// </summary>
    private void UpdateChildRenderTransform()
    {
        if (Child is not { } child)
            return;

        var zoom = EffectiveZoom;
        child.RenderTransformOrigin = RelativePoint.TopLeft;
        child.RenderTransform = System.Math.Abs(zoom - 1.0) < 1e-6 ? null : new ScaleTransform(zoom, zoom);
    }
}
