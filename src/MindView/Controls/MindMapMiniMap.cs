using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CodeWF.Avalonia.MindView.Controls;

public class MindMapMiniMap : Control
{
    public static readonly StyledProperty<Color?> AccentColorProperty =
        AvaloniaProperty.Register<MindMapMiniMap, Color?>(nameof(AccentColor));
    public Color? AccentColor { get => GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }
    public static readonly StyledProperty<ObservableCollection<MindMapNode>?> RootsProperty =
        AvaloniaProperty.Register<MindMapMiniMap, ObservableCollection<MindMapNode>?>(nameof(Roots));

    public static readonly StyledProperty<bool> IsDarkThemeProperty =
        AvaloniaProperty.Register<MindMapMiniMap, bool>(nameof(IsDarkTheme));

    public static readonly StyledProperty<Rect> ViewportBoundsProperty =
        AvaloniaProperty.Register<MindMapMiniMap, Rect>(nameof(ViewportBounds));

    private double PreviewPadding => GetResourceDouble(MindViewStyleKeys.MiniMapPreviewPaddingResource, 14);

    private readonly List<MindMapNode> _observedNodes = [];
    private readonly List<INotifyCollectionChanged> _observedCollections = [];
    private Func<Point, Point>? _toCanvasPoint;

    public event EventHandler<Point>? MapPointRequested;

    public MindMapMiniMap()
    {
        ClipToBounds = true;
        ActualThemeVariantChanged += (_, _) =>
        {
            SetCurrentValue(IsDarkThemeProperty, MindViewThemeResources.IsDarkVariant(ActualThemeVariant));
            InvalidateVisual();
        };
    }

    public ObservableCollection<MindMapNode>? Roots
    {
        get => GetValue(RootsProperty);
        set => SetValue(RootsProperty, value);
    }

    public bool IsDarkTheme
    {
        get => GetValue(IsDarkThemeProperty);
        set => SetValue(IsDarkThemeProperty, value);
    }

    public Rect ViewportBounds
    {
        get => GetValue(ViewportBoundsProperty);
        set => SetValue(ViewportBoundsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RootsProperty)
        {
            RewireTree();
        }

        if (change.Property == RootsProperty
            || change.Property == IsDarkThemeProperty
            || change.Property == AccentColorProperty
            || change.Property == ViewportBoundsProperty)
        {
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _toCanvasPoint is null)
        {
            return;
        }

        MapPointRequested?.Invoke(this, _toCanvasPoint(e.GetPosition(this)));
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var background = GetResourceBrush(MindViewStyleKeys.MiniMapBackgroundBrushResource, "#F8FAFC", "#0F172A");
        var border = new Pen(GetResourceBrush(MindViewStyleKeys.MiniMapBorderBrushResource, "#D8E0EA", "#334155"), 1);
        context.DrawRectangle(background, border, new RoundedRect(Bounds, 6));

        var nodes = FlattenNodes();
        if (nodes.Count == 0)
        {
            _toCanvasPoint = null;
            return;
        }

        var nodeLookup = nodes.ToDictionary(node => node.Node);
        var mapBounds = GetMapBounds(nodes);
        // 小图使用真实节点坐标缩放绘制，始终反映当前脑图全局结构和视口位置。
        var scale = Math.Min(
            Math.Max(0.01, (Bounds.Width - PreviewPadding * 2) / mapBounds.Width),
            Math.Max(0.01, (Bounds.Height - PreviewPadding * 2) / mapBounds.Height));

        var contentWidth = mapBounds.Width * scale;
        var contentHeight = mapBounds.Height * scale;
        var offset = new Vector(
            (Bounds.Width - contentWidth) / 2 - mapBounds.X * scale,
            (Bounds.Height - contentHeight) / 2 - mapBounds.Y * scale);

        Point ToPreview(Point point) => new(point.X * scale + offset.X, point.Y * scale + offset.Y);
        _toCanvasPoint = point => new Point((point.X - offset.X) / scale, (point.Y - offset.Y) / scale);

        if (Roots is not null)
        {
            foreach (var root in Roots)
            {
                DrawConnectors(context, root, nodeLookup, ToPreview);
            }
        }

        foreach (var node in nodes)
        {
            DrawNode(context, node, ToPreview);
        }

        if (ViewportBounds.Width > 0 && ViewportBounds.Height > 0)
        {
            var topLeft = ToPreview(ViewportBounds.Position);
            var bottomRight = ToPreview(ViewportBounds.BottomRight);
            var rect = new Rect(topLeft, bottomRight).Intersect(Bounds.Deflate(4));
            if (rect.Width > 2 && rect.Height > 2)
            {
                context.DrawRectangle(
                    GetResourceBrush(MindViewStyleKeys.MiniMapViewportBrushResource, "#148BFF22", "#1D4ED826"),
                    new Pen(GetResourceBrush(MindViewStyleKeys.SelectionBrushResource, "#148BFF", "#60A5FA"), 1.2),
                    rect);
            }
        }
    }

    private void RewireTree()
    {
        foreach (var node in _observedNodes)
        {
            node.PropertyChanged -= HandleNodeChanged;
        }

        foreach (var collection in _observedCollections)
        {
            collection.CollectionChanged -= HandleTreeChanged;
        }

        _observedNodes.Clear();
        _observedCollections.Clear();

        if (Roots is null)
        {
            return;
        }

        Roots.CollectionChanged += HandleTreeChanged;
        _observedCollections.Add(Roots);

        foreach (var root in Roots)
        {
            WatchNode(root);
        }
    }

    private void WatchNode(MindMapNode node)
    {
        _observedNodes.Add(node);
        node.PropertyChanged += HandleNodeChanged;
        node.Children.CollectionChanged += HandleTreeChanged;
        _observedCollections.Add(node.Children);

        foreach (var child in node.Children)
        {
            WatchNode(child);
        }
    }

    private void HandleNodeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MindMapNode.Title)
            or nameof(MindMapNode.Note)
            or nameof(MindMapNode.X)
            or nameof(MindMapNode.Y)
            or nameof(MindMapNode.AccentColor))
        {
            InvalidateVisual();
        }
    }

    private void HandleTreeChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RewireTree();
        InvalidateVisual();
    }

    private List<PreviewNode> FlattenNodes()
    {
        var nodes = new List<PreviewNode>();
        if (Roots is null)
        {
            return nodes;
        }

        foreach (var root in Roots)
        {
            AppendNode(root, 1, nodes);
        }

        return nodes;
    }

    private static void AppendNode(MindMapNode node, int level, List<PreviewNode> nodes)
    {
        nodes.Add(new PreviewNode(node, level, MindMapLayoutMetrics.EstimateNodeSize(node, level)));
        foreach (var child in node.Children)
        {
            AppendNode(child, level + 1, nodes);
        }
    }

    private static Rect GetMapBounds(IReadOnlyCollection<PreviewNode> nodes)
    {
        var minX = nodes.Min(node => node.Node.X);
        var minY = nodes.Min(node => node.Node.Y);
        var maxX = nodes.Max(node => node.Node.X + node.Size.Width);
        var maxY = nodes.Max(node => node.Node.Y + node.Size.Height);
        return new Rect(minX - 40, minY - 32, maxX - minX + 80, maxY - minY + 64);
    }

    private void DrawConnectors(
        DrawingContext context,
        MindMapNode parent,
        IReadOnlyDictionary<MindMapNode, PreviewNode> nodes,
        Func<Point, Point> toPreview)
    {
        var pen = new Pen(GetResourceBrush(MindViewStyleKeys.ConnectorBrushResource, "#148BFF", "#60A5FA"), 1);
        if (!nodes.TryGetValue(parent, out var parentPreview))
        {
            return;
        }

        foreach (var child in parent.Children)
        {
            if (!nodes.TryGetValue(child, out var childPreview))
            {
                continue;
            }

            var start = toPreview(GetConnectorStart(parentPreview));
            var end = toPreview(GetConnectorEnd(childPreview));
            var geometry = new StreamGeometry();
            using (var stream = geometry.Open())
            {
                var distance = Math.Max(14, end.X - start.X);
                var controlOffset = distance * 0.55;
                stream.BeginFigure(start, isFilled: false);
                stream.CubicBezierTo(
                    new Point(start.X + controlOffset, start.Y),
                    new Point(end.X - controlOffset, end.Y),
                    end);
            }

            context.DrawGeometry(null, pen, geometry);
            DrawConnectors(context, child, nodes, toPreview);
        }
    }

    private void DrawNode(DrawingContext context, PreviewNode node, Func<Point, Point> toPreview)
    {
        var topLeft = toPreview(new Point(node.Node.X, node.Node.Y));
        var bottomRight = toPreview(new Point(node.Node.X + node.Size.Width, node.Node.Y + node.Size.Height));
        var rect = new Rect(topLeft, bottomRight);
        var kind = MindMapLayoutMetrics.GetVisualKind(node.Level);

        if (kind == MindMapNodeVisualKind.Leaf)
        {
            var lineHeight = Math.Clamp(rect.Height * 0.18, 1.2, 3.2);
            var lineRect = new Rect(rect.X, rect.Center.Y - lineHeight / 2, Math.Max(6, rect.Width), lineHeight);
            context.DrawRectangle(WithOpacity(GetResourceBrush(MindViewStyleKeys.PrimaryTextBrushResource, "#111827", "#F9FAFB"), 0.72), null, lineRect);
            return;
        }

        var fill = kind == MindMapNodeVisualKind.Root
            ? GetResourceBrush(MindViewStyleKeys.RootBackgroundBrushResource, "#148BFF", "#148BFF")
            : CreateBranchPreviewBrush(GetNodeAccentColor(node.Node, "#2563EB"));
        context.DrawRectangle(WithOpacity(fill, IsDarkTheme ? 0.78 : 0.92), null, new RoundedRect(rect, 4));
    }

    private sealed record PreviewNode(MindMapNode Node, int Level, Size Size);

    private static Point GetConnectorStart(PreviewNode node)
    {
        return new Point(node.Node.X + node.Size.Width, node.Node.Y + node.Size.Height / 2);
    }

    private static Point GetConnectorEnd(PreviewNode node)
    {
        return new Point(node.Node.X, node.Node.Y + node.Size.Height / 2);
    }

    private IBrush GetResourceBrush(string key, string lightFallback, string darkFallback)
    {
        if (AccentColor is { } accent && key is MindViewStyleKeys.RootBackgroundBrushResource
            or MindViewStyleKeys.ConnectorBrushResource or MindViewStyleKeys.SelectionBrushResource)
            return new SolidColorBrush(accent);
        return MindViewThemeResources.GetBrush(this, key, lightFallback, darkFallback);
    }

    private static IBrush WithOpacity(IBrush brush, double opacity)
    {
        return brush is ISolidColorBrush solidColorBrush
            ? new SolidColorBrush(solidColorBrush.Color, opacity)
            : brush;
    }

    private IBrush CreateBranchPreviewBrush(Color accent)
    {
        var target = GetResourceColor(MindViewStyleKeys.MiniMapBranchBlendTargetBrushResource, "#FFFFFF", "#111827");
        var targetWeight = GetResourceDouble(MindViewStyleKeys.MiniMapBranchBackgroundTargetWeightResource, IsDarkTheme ? 0.7 : 0.88);
        return new SolidColorBrush(MixColor(accent, target, targetWeight));
    }

    private static Color GetNodeAccentColor(MindMapNode node, string fallback)
    {
        try
        {
            return Color.Parse(string.IsNullOrWhiteSpace(node.AccentColor) ? fallback : node.AccentColor);
        }
        catch (FormatException)
        {
            return Color.Parse(fallback);
        }
    }

    private static Color MixColor(Color source, Color target, double targetWeight)
    {
        targetWeight = Math.Clamp(targetWeight, 0, 1);
        var sourceWeight = 1 - targetWeight;
        return Color.FromArgb(
            MixByte(source.A, target.A, sourceWeight, targetWeight),
            MixByte(source.R, target.R, sourceWeight, targetWeight),
            MixByte(source.G, target.G, sourceWeight, targetWeight),
            MixByte(source.B, target.B, sourceWeight, targetWeight));
    }

    private static byte MixByte(byte source, byte target, double sourceWeight, double targetWeight)
    {
        return (byte)Math.Round(source * sourceWeight + target * targetWeight);
    }

    private double GetResourceDouble(string key, double fallback)
    {
        return MindViewThemeResources.GetDouble(this, key, fallback);
    }

    private Color GetResourceColor(string key, string lightFallback, string darkFallback)
    {
        return MindViewThemeResources.GetColor(this, key, lightFallback, darkFallback);
    }
}
