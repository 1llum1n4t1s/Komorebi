// nullable 移行未実施。1 ファイルずつ null 注釈を入れてこの 2 行を削除していく。
#nullable disable warnings
using System;
using System.Collections.Generic;
using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Komorebi.Views;

/// <summary>
/// 全 CommitRefsPresenter インスタンス間で共有するアイコンジオメトリのキャッシュ。
/// </summary>
public class CommitRefsIconCache
{
    /// <summary>シングルトンインスタンス。</summary>
    public static CommitRefsIconCache Instance => s_instance ??= new CommitRefsIconCache();

    /// <summary>
    /// コンストラクタ。各デコレータ種別のアイコンをロードする。
    /// </summary>
    public CommitRefsIconCache()
    {
        _head = LoadIcon("Icons.Head");
        _branch = LoadIcon("Icons.Branch");
        _remote = LoadIcon("Icons.Remote");
        _tag = LoadIcon("Icons.Tag");
    }

    /// <summary>
    /// デコレータ種別に対応するアイコンジオメトリを取得する。
    /// </summary>
    public Geometry GetIcon(Models.DecoratorType type)
    {
        return type switch
        {
            Models.DecoratorType.CurrentBranchHead => _head,
            Models.DecoratorType.CurrentCommitHead => _head,
            Models.DecoratorType.LocalBranchHead => _branch,
            Models.DecoratorType.RemoteBranchHead => _remote,
            Models.DecoratorType.Tag => _tag,
            _ => null,
        };
    }

    /// <summary>
    /// リソースキーからアイコンジオメトリをロードし、10x10 に収まるよう変換を適用する。
    /// </summary>
    private static Geometry LoadIcon(string resourceKey)
    {
        var geo = App.Current.FindResource(resourceKey) as StreamGeometry;
        var drawGeo = geo!.Clone();
        var iconBounds = drawGeo.Bounds;
        var translation = Matrix.CreateTranslation(-(Vector)iconBounds.Position);
        var scale = Math.Min(10.0 / iconBounds.Width, 10.0 / iconBounds.Height);
        var transform = translation * Matrix.CreateScale(scale, scale);
        if (drawGeo.Transform is null || drawGeo.Transform.Value == Matrix.Identity)
            drawGeo.Transform = new MatrixTransform(transform);
        else
            drawGeo.Transform = new MatrixTransform(drawGeo.Transform.Value * transform);

        return drawGeo;
    }

    private static CommitRefsIconCache s_instance;
    private readonly Geometry _head;
    private readonly Geometry _branch;
    private readonly Geometry _remote;
    private readonly Geometry _tag;
}

/// <summary>
/// コミットの参照（ブランチタグ・リモートブランチ等）ラベルを表示するプレゼンタ。
/// </summary>
public class CommitRefsPresenter : Control
{
    /// <summary>
    /// RenderItemクラス。
    /// </summary>
    public class RenderItem
    {
        public Models.Decorator Decorator { get; set; } = null;
        public FormattedText Label { get; set; } = null;
        public IBrush Brush { get; set; } = null;
        public bool IsHead { get; set; } = false;
        public double Width { get; set; } = 0.0;

        /// <summary>
        /// コンパクト表示時、このアイテムと同名の別リモートに束ねられたラベル群 (upstream 2aaf6978)。
        /// 例えば `main` と `origin/main` は 1 バッジに集約され、ここに `origin` が積まれる。
        /// </summary>
        public List<FormattedText> Remotes { get; set; } = [];
    }

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<CommitRefsPresenter>();

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public static readonly StyledProperty<double> FontSizeProperty =
       TextBlock.FontSizeProperty.AddOwner<CommitRefsPresenter>();

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly StyledProperty<IBrush> BackgroundProperty =
        AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(Background), Brushes.Transparent);

    public IBrush Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<CommitRefsPresenter, IBrush>(nameof(Foreground), Brushes.White);

    public IBrush Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly StyledProperty<bool> UseGraphColorProperty =
        AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(UseGraphColor));

    public bool UseGraphColor
    {
        get => GetValue(UseGraphColorProperty);
        set => SetValue(UseGraphColorProperty, value);
    }

    public static readonly StyledProperty<bool> AllowWrapProperty =
        AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(AllowWrap));

    public bool AllowWrap
    {
        get => GetValue(AllowWrapProperty);
        set => SetValue(AllowWrapProperty, value);
    }

    public static readonly StyledProperty<bool> ShowTagsProperty =
        AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(ShowTags), true);

    public bool ShowTags
    {
        get => GetValue(ShowTagsProperty);
        set => SetValue(ShowTagsProperty, value);
    }

    /// <summary>
    /// 同名のローカル/リモートブランチ（例: `main` と `origin/main`）を 1 バッジに集約して表示するかどうか (upstream ec74c6d4)。
    /// </summary>
    public static readonly StyledProperty<bool> UseCompactBranchNamesProperty =
        AvaloniaProperty.Register<CommitRefsPresenter, bool>(nameof(UseCompactBranchNames), true);

    public bool UseCompactBranchNames
    {
        get => GetValue(UseCompactBranchNamesProperty);
        set => SetValue(UseCompactBranchNamesProperty, value);
    }

    /// <summary>
    /// CommitRefsPresenterの処理を行う。
    /// </summary>
    static CommitRefsPresenter()
    {
        AffectsMeasure<CommitRefsPresenter>(
            FontFamilyProperty,
            FontSizeProperty,
            ForegroundProperty,
            UseGraphColorProperty,
            BackgroundProperty,
            ShowTagsProperty,
            UseCompactBranchNamesProperty);
    }

    /// <summary>
    /// 指定座標にあるデコレータを返す。座標がどのバッジにも該当しない場合は null。
    /// Render と同じレイアウト計算（開始x=1.5・バッジ間4px・AllowWrap時の折返しy）で
    /// 走査しないと、クリック位置とバッジの対応が後方ほど累積してずれる。
    /// </summary>
    public Models.Decorator DecoratorAt(Point point)
    {
        var allowWrap = AllowWrap;
        var x = 1.5;
        var y = 0.5;

        foreach (var item in _items)
        {
            if (allowWrap && x > 1.5 && x + item.Width > Bounds.Width)
            {
                x = 1.5;
                y += 20.0;
            }

            if (new Rect(x, y, item.Width, 16).Contains(point))
                return item.Decorator;

            x += item.Width + 4;
        }

        return null;
    }

    /// <summary>
    /// コントロールの描画処理を行う。
    /// </summary>
    public override void Render(DrawingContext context)
    {
        if (_items.Count == 0)
            return;

        var useGraphColor = UseGraphColor;
        var fg = Foreground;
        var bg = Background;
        var allowWrap = AllowWrap;
        var x = 1.5;
        var y = 0.5;

        // ヒットテスト（右クリックでのコンテキストメニュー表示）を成立させるため全域を透明で塗る。
        // DrawingContext はローカル座標系なので、親相対オフセットを含む Bounds をそのまま渡すと
        // Margin 付き配置でヒット領域がずれる。サイズのみの Rect を使う。
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

        foreach (var item in _items)
        {
            if (allowWrap && x > 1.5 && x + item.Width > Bounds.Width)
            {
                x = 1.5;
                y += 20.0;
            }

            var entireRect = new RoundedRect(new Rect(x, y, item.Width, 16), new CornerRadius(4));

            if (item.IsHead)
            {
                if (useGraphColor)
                {
                    if (bg is not null)
                        context.DrawRectangle(bg, null, entireRect);

                    using (context.PushOpacity(.6))
                        context.DrawRectangle(item.Brush, null, entireRect);
                }
            }
            else
            {
                if (bg is not null)
                    context.DrawRectangle(bg, null, entireRect);

                var labelRect = new RoundedRect(new Rect(x + 16, y, item.Width - 16, 16), new CornerRadius(0, 4, 4, 0));
                using (context.PushOpacity(.2))
                    context.DrawRectangle(item.Brush, null, labelRect);
            }

            context.DrawLine(new Pen(item.Brush), new Point(x + 16, y), new Point(x + 16, y + 16));
            context.DrawText(item.Label, new Point(x + 20, y + 8.0 - item.Label.Height * 0.5));

            // コンパクト表示: 同名リモートを束ねたラベルを区切り線付きで追加描画する (upstream 2aaf6978)
            if (item.Remotes.Count > 0)
            {
                var rx = x + 20 + item.Label.WidthIncludingTrailingWhitespace + 4;
                foreach (var remote in item.Remotes)
                {
                    context.DrawLine(new Pen(item.Brush), new Point(rx, y), new Point(rx, y + 16));
                    context.DrawText(remote, new Point(rx + 4, y + 8.0 - remote.Height * 0.5));
                    rx += remote.WidthIncludingTrailingWhitespace + 9;
                }
            }

            context.DrawRectangle(null, new Pen(item.Brush), entireRect);

            var icon = CommitRefsIconCache.Instance.GetIcon(item.Decorator.Type);
            if (icon != null)
            {
                using (context.PushTransform(Matrix.CreateTranslation(x + 3, y + 3)))
                    context.DrawGeometry(fg, null, icon);
            }

            x += item.Width + 4;
        }
    }

    /// <summary>
    /// データコンテキストが変更された際の処理。
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        InvalidateMeasure();
    }

    /// <summary>
    /// コントロールの測定処理をオーバーライドする。
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        _items.Clear();

        if (DataContext is not Models.Commit commit)
            return new Size(0, 0);

        var refs = commit.Decorators;
        var count = refs?.Count ?? 0;
        if (count == 0)
        {
            InvalidateVisual();
            return new Size(0, 0);
        }

        var typeface = new Typeface(FontFamily);
        var typefaceHead = new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold);
        var typefaceRemote = new Typeface(FontFamily, FontStyle.Italic, FontWeight.Bold);
        var fg = Foreground;
        var normalBG = UseGraphColor ? Models.CommitGraph.Pens[commit.Color].Brush : Brushes.Gray;
        var labelSize = FontSize;
        var requiredHeight = 16.0;
        var x = 0.0;
        var allowWrap = AllowWrap;
        var showTags = ShowTags;
        var useCompact = UseCompactBranchNames;
        var skippedIdx = useCompact ? new HashSet<int>() : null;

        for (var i = 0; i < count; i++)
        {
            if (skippedIdx is not null && skippedIdx.Contains(i))
                continue;

            var decorator = refs[i];
            if (!showTags && decorator.Type == Models.DecoratorType.Tag)
                continue;

            var isHead = decorator.Type is Models.DecoratorType.CurrentBranchHead or Models.DecoratorType.CurrentCommitHead;

            var item = new RenderItem()
            {
                Label = new FormattedText(
                    decorator.Name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    isHead ? typefaceHead : typeface,
                    isHead ? labelSize + 1 : labelSize,
                    fg),
                Brush = decorator.Type == Models.DecoratorType.Tag ? Brushes.Gray : normalBG,
                IsHead = isHead,
                Decorator = decorator,
            };

            item.Width = item.Label.Width + 24;
            _items.Add(item);

            // コンパクト表示: 同名のローカル/HEADブランチにぶら下がるリモートブランチを 1 バッジへ集約する (upstream 2aaf6978)
            if (useCompact && decorator.Type != Models.DecoratorType.RemoteBranchHead && decorator.Type != Models.DecoratorType.Tag)
            {
                for (var j = i + 1; j < count; j++)
                {
                    var test = refs[j];
                    if (test.Type != Models.DecoratorType.RemoteBranchHead)
                        continue;

                    var idxOfSlash = test.Name.IndexOf('/');
                    if (idxOfSlash < 1 || idxOfSlash == test.Name.Length - 1)
                        continue;

                    var name = test.Name.Substring(idxOfSlash + 1);
                    if (!decorator.Name.Equals(name, StringComparison.Ordinal))
                        continue;

                    var remote = new FormattedText(
                        test.Name.Substring(0, idxOfSlash),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typefaceRemote,
                        labelSize,
                        fg);

                    item.Remotes.Add(remote);
                    item.Width += remote.Width + 9;
                    skippedIdx.Add(j);
                }
            }

            x += item.Width + 4;
            if (allowWrap)
            {
                if (x > availableSize.Width)
                {
                    requiredHeight += 20.0;
                    x = item.Width;
                }
            }
        }

        double requiredWidth = 0;
        if (_items.Count > 0)
        {
            requiredWidth = allowWrap && requiredHeight > 16.0
                ? (double.IsInfinity(availableSize.Width) ? x + 2 : availableSize.Width)
                : x + 2;
        }

        InvalidateVisual();
        return new Size(requiredWidth, requiredHeight);
    }

    private List<RenderItem> _items = [];
}
