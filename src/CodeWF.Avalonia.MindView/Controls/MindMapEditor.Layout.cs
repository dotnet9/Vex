using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace CodeWF.Avalonia.MindView.Controls;

public partial class MindMapEditor
{
    private void UpdateNodePosition(MindMapNode node)
    {
        if (!_nodeFrames.TryGetValue(node, out var control))
        {
            return;
        }

        Canvas.SetLeft(control, node.X);
        Canvas.SetTop(control, node.Y);
        PositionNodeToolbar();
    }

    private void UpdateConnectors()
    {
        var vertical = LayoutKind is MindMapLayoutKind.Down or MindMapLayoutKind.Outline
            or MindMapLayoutKind.Fishbone or MindMapLayoutKind.Radial;
        foreach (var connector in _connectors)
        {
            var parentSize = GetRenderedNodeSize(connector.Parent);
            var childSize = GetRenderedNodeSize(connector.Child);
            var start = vertical
                ? new Point(connector.Parent.X + parentSize.Width / 2,
                    connector.Child.Y >= connector.Parent.Y ? connector.Parent.Y + parentSize.Height : connector.Parent.Y)
                : GetConnectorStart(connector.Parent, parentSize);
            var end = vertical
                ? new Point(connector.Child.X + childSize.Width / 2,
                    connector.Child.Y >= connector.Parent.Y ? connector.Child.Y : connector.Child.Y + childSize.Height)
                : GetConnectorEnd(connector.Child, childSize);

            connector.Path.Data = CreateConnectorGeometry(start, end, vertical);
        }
    }

    private Point GetConnectorStart(MindMapNode node, Size size)
    {
        return new Point(node.X + size.Width, node.Y + size.Height / 2);
    }

    private Point GetConnectorEnd(MindMapNode node, Size size)
    {
        return new Point(node.X, node.Y + size.Height / 2);
    }

    private void EnsureCanvasSize()
    {
        var nodes = _nodeFrames.Keys.ToList();
        var horizontalPadding = GetCanvasHorizontalPadding();
        var verticalPadding = GetCanvasVerticalPadding();
        var width = nodes.Count == 0
            ? MinCanvasWidth + horizontalPadding
            : nodes.Max(node => node.X + GetRenderedNodeSize(node).Width + horizontalPadding);
        var height = nodes.Count == 0
            ? MinCanvasHeight + verticalPadding
            : nodes.Max(node => node.Y + GetRenderedNodeSize(node).Height + verticalPadding);

        _canvas.Width = Math.Max(MinCanvasWidth + horizontalPadding, width);
        _canvas.Height = Math.Max(MinCanvasHeight + verticalPadding, height);
    }

    private double GetCanvasHorizontalPadding()
    {
        var viewportWidth = _scrollViewer.Viewport.Width;
        if (viewportWidth <= 0 || _zoomScale <= 0)
        {
            return MinCanvasHorizontalPadding;
        }

        return Math.Max(MinCanvasHorizontalPadding, viewportWidth / _zoomScale);
    }

    private double GetCanvasVerticalPadding()
    {
        var viewportHeight = _scrollViewer.Viewport.Height;
        if (viewportHeight <= 0 || _zoomScale <= 0)
        {
            return MinCanvasVerticalPadding;
        }

        return Math.Max(MinCanvasVerticalPadding, viewportHeight / _zoomScale);
    }

    private Size GetRenderedNodeSize(MindMapNode node)
    {
        if (_nodeFrames.TryGetValue(node, out var frame)
            && frame.Bounds.Width > 0
            && frame.Bounds.Height > 0)
        {
            return frame.Bounds.Size;
        }

        var metrics = GetNodeMetrics(node);
        return MindMapLayoutMetrics.EstimateNodeSize(node, GetNodeLevel(node), metrics.Placeholder);
    }

    /// <summary>样式层：在主题度量之上叠加六种风格变体（清雅为默认，直接透传）。</summary>
    private NodeMetrics GetNodeMetrics(MindMapNode node)
    {
        var metrics = BuildNodeMetrics(node);
        if (StyleKind == MindMapStyleKind.Quiet) return metrics;
        var level = GetNodeLevel(node);
        if (level <= 1) return metrics;
        var accent = GetNodeAccentColor(node, "#2563EB");
        switch (StyleKind)
        {
            case MindMapStyleKind.Mono:
            {
                var gray = IsDarkTheme ? Color.Parse("#9CA3AF") : Color.Parse("#6B7280");
                return metrics with
                {
                    Background = level == 2 ? CreateBranchBackgroundBrush(gray) : Brushes.Transparent,
                    BorderBrush = CreateAccentBrush(gray, 0.55),
                    HoverBackground = level == 2 ? CreateBranchBackgroundBrush(gray, isHover: true) : metrics.HoverBackground,
                };
            }
            case MindMapStyleKind.Colorful:
                return metrics with
                {
                    Background = level == 2 ? new SolidColorBrush(accent) : TintBrush(accent, 0.16),
                    Foreground = level == 2 ? ContrastText(accent) : metrics.Foreground,
                };
            case MindMapStyleKind.Outline:
                return metrics with
                {
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(accent),
                    BorderThickness = new Thickness(level == 2 ? 1.4 : 1),
                    CornerRadius = new CornerRadius(level == 2 ? 8 : 5),
                    BoxShadow = default,
                };
            case MindMapStyleKind.Capsule:
                return metrics with
                {
                    CornerRadius = new CornerRadius(metrics.MinHeight / 2),
                    Padding = new Thickness(metrics.Padding.Left + 6, metrics.Padding.Top),
                };
            case MindMapStyleKind.Underline:
                return metrics with
                {
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(accent),
                    BorderThickness = new Thickness(0, 0, 0, level == 2 ? 2.4 : 1.6),
                    CornerRadius = new CornerRadius(0),
                    BoxShadow = default,
                };
            default: return metrics;
        }
    }

    private static IBrush TintBrush(Color accent, double ratio)
    {
        const int target = 255;
        return new SolidColorBrush(Color.FromRgb(
            (byte)(accent.R + (target - accent.R) * ratio),
            (byte)(accent.G + (target - accent.G) * ratio),
            (byte)(accent.B + (target - accent.B) * ratio)));
    }

    private static IBrush ContrastText(Color accent) =>
        accent.R * 0.299 + accent.G * 0.587 + accent.B * 0.114 > 150 ? Brushes.Black : Brushes.White;

    private NodeMetrics BuildNodeMetrics(MindMapNode node)
    {
        var level = GetNodeLevel(node);
        if (level <= 1)
        {
            return new NodeMetrics(
                MindMapLayoutMetrics.RootMinWidth,
                MindMapLayoutMetrics.RootMaxWidth,
                MindMapLayoutMetrics.RootMinHeight,
                GetResourceCornerRadius(MindViewStyleKeys.RootCornerRadiusResource, new CornerRadius(8)),
                GetResourceBrush(MindViewStyleKeys.RootBackgroundBrushResource, "#1264D7", "#1264D7"),
                Brushes.Transparent,
                GetResourceThickness(MindViewStyleKeys.RootBorderThicknessResource, new Thickness(0)),
                GetResourceThickness(MindViewStyleKeys.RootPaddingResource, new Thickness(10, 5)),
                GetResourceBoxShadows(MindViewStyleKeys.RootBoxShadowResource, "0 6 18 0 #16000000"),
                GetResourceBrush(MindViewStyleKeys.RootForegroundBrushResource, "#FFFFFF", "#FFFFFF"),
                GetResourceDouble(MindViewStyleKeys.RootFontSizeResource, 18),
                FontWeight.SemiBold,
                HorizontalAlignment.Stretch,
                CenterTopicPlaceholder,
                IsTextOnly: false);
        }

        if (level == 2)
        {
            var accent = GetNodeAccentColor(node, "#2563EB");
            return new NodeMetrics(
                MindMapLayoutMetrics.BranchMinWidth,
                MindMapLayoutMetrics.BranchMaxWidth,
                MindMapLayoutMetrics.BranchMinHeight,
                GetResourceCornerRadius(MindViewStyleKeys.BranchCornerRadiusResource, new CornerRadius(8)),
                CreateBranchBackgroundBrush(accent),
                CreateAccentBrush(accent, GetResourceDouble(MindViewStyleKeys.BranchBorderOpacityResource, IsDarkTheme ? 0.5 : 0.34)),
                GetResourceThickness(MindViewStyleKeys.BranchBorderThicknessResource, new Thickness(1)),
                GetResourceThickness(MindViewStyleKeys.BranchPaddingResource, new Thickness(12, 5)),
                GetResourceBoxShadows(MindViewStyleKeys.BranchBoxShadowResource, IsDarkTheme ? "0 4 12 0 #30000000" : "0 3 10 0 #10000000"),
                GetPrimaryTextBrush(),
                GetResourceDouble(MindViewStyleKeys.BranchFontSizeResource, 17),
                FontWeight.SemiBold,
                HorizontalAlignment.Stretch,
                TopicPlaceholder,
                IsTextOnly: false,
                HoverBackground: CreateBranchBackgroundBrush(accent, isHover: true),
                HoverBorderBrush: CreateAccentBrush(accent, GetResourceDouble(MindViewStyleKeys.BranchHoverBorderOpacityResource, IsDarkTheme ? 0.72 : 0.48)),
                HoverBoxShadow: GetResourceBoxShadows(MindViewStyleKeys.BranchHoverBoxShadowResource, IsDarkTheme ? "0 7 18 0 #42000000" : "0 7 18 0 #16000000"),
                SelectedBackground: CreateBranchBackgroundBrush(accent, isSelected: true),
                SelectedBorderBrush: CreateAccentBrush(accent, GetResourceDouble(MindViewStyleKeys.BranchSelectedBorderOpacityResource, IsDarkTheme ? 0.95 : 0.72)),
                SelectedBorderThickness: GetResourceThickness(MindViewStyleKeys.BranchSelectedBorderThicknessResource, new Thickness(1)),
                SelectedBoxShadow: GetResourceBoxShadows(MindViewStyleKeys.BranchSelectedBoxShadowResource, IsDarkTheme ? "0 8 22 0 #4A000000" : "0 8 22 0 #1C000000"),
                DragBoxShadow: GetResourceBoxShadows(MindViewStyleKeys.BranchDragBoxShadowResource, IsDarkTheme ? "0 12 28 0 #58000000" : "0 12 28 0 #22000000"));
        }

        return new NodeMetrics(
            MindMapLayoutMetrics.LeafMinWidth,
            MindMapLayoutMetrics.LeafMaxWidth,
            MindMapLayoutMetrics.LeafMinHeight,
            GetResourceCornerRadius(MindViewStyleKeys.LeafCornerRadiusResource, new CornerRadius(0)),
            Brushes.Transparent,
            Brushes.Transparent,
            new Thickness(0),
            GetResourceThickness(MindViewStyleKeys.LeafPaddingResource, new Thickness(0, 2)),
            default,
            GetPrimaryTextBrush(),
            GetResourceDouble(MindViewStyleKeys.LeafFontSizeResource, 16),
            FontWeight.Regular,
            HorizontalAlignment.Stretch,
            TopicPlaceholder,
            IsTextOnly: true,
            HoverBackground: GetResourceBrush(MindViewStyleKeys.LeafHoverBackgroundBrushResource, "#EEF6FF", "#162235"),
            HoverBoxShadow: GetResourceBoxShadows(MindViewStyleKeys.LeafHoverBoxShadowResource, IsDarkTheme ? "0 2 8 0 #24000000" : "0 2 8 0 #0F000000"),
            SelectedBackground: GetResourceBrush(MindViewStyleKeys.LeafSelectedBackgroundBrushResource, "#E6F2FF", "#1E2F46"),
            SelectedBoxShadow: GetResourceBoxShadows(MindViewStyleKeys.LeafSelectedBoxShadowResource, IsDarkTheme ? "0 3 12 0 #30000000" : "0 3 12 0 #14000000"));
    }

    private Geometry CreateConnectorGeometry(Point start, Point end, bool straight = false)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        if (straight)
        {
            context.BeginFigure(start, isFilled: false);
            context.LineTo(end);
            context.EndFigure(false);
            return geometry;
        }
        var distance = Math.Max(32, end.X - start.X);
        var controlOffset = distance * 0.55;
        context.BeginFigure(start, isFilled: false);
        context.CubicBezierTo(
            new Point(start.X + controlOffset, start.Y),
            new Point(end.X - controlOffset, end.Y),
            end);
        return geometry;
    }

    private sealed record Connector(MindMapNode Parent, MindMapNode Child, global::Avalonia.Controls.Shapes.Path Path);

    private sealed record ConnectorWorkItem(MindMapNode Parent, MindMapNode Child);

    private sealed record NodeMetrics(
        double MinWidth,
        double MaxWidth,
        double MinHeight,
        CornerRadius CornerRadius,
        IBrush Background,
        IBrush BorderBrush,
        Thickness BorderThickness,
        Thickness Padding,
        BoxShadows BoxShadow,
        IBrush Foreground,
        double FontSize,
        FontWeight FontWeight,
        HorizontalAlignment ContentAlignment,
        string Placeholder,
        bool IsTextOnly,
        IBrush? HoverBackground = null,
        IBrush? HoverBorderBrush = null,
        BoxShadows? HoverBoxShadow = null,
        IBrush? SelectedBackground = null,
        IBrush? SelectedBorderBrush = null,
        Thickness? SelectedBorderThickness = null,
        BoxShadows? SelectedBoxShadow = null,
        BoxShadows? DragBoxShadow = null);

    private void ApplyTheme()
    {
        var canvasBrush = GetCanvasBackgroundBrush();
        _canvas.MinWidth = MinCanvasWidth;
        _canvas.MinHeight = MinCanvasHeight;
        _canvas.Background = canvasBrush;
        _scrollViewer.Background = canvasBrush;
    }

    private IBrush GetCanvasBackgroundBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.CanvasBackgroundBrushResource, "#F8FAFC", "#111827");
    }

    private IBrush GetConnectorBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.ConnectorBrushResource, "#148BFF", "#60A5FA");
    }

    private IBrush GetSelectionBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.SelectionBrushResource, "#148BFF", "#60A5FA");
    }

    private IBrush GetPanelBackgroundBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.PanelBackgroundBrushResource, "#F8FAFC", "#1F2937");
    }

    private IBrush GetPanelBorderBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.PanelBorderBrushResource, "#CBD5E1", "#374151");
    }

    private IBrush GetPrimaryTextBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.PrimaryTextBrushResource, "#111827", "#F9FAFB");
    }

    private IBrush GetSecondaryTextBrush()
    {
        return GetResourceBrush(MindViewStyleKeys.SecondaryTextBrushResource, "#6B7280", "#9CA3AF");
    }

    private IBrush GetTitlePlaceholderBrush(bool isRoot)
    {
        return isRoot
            ? GetResourceBrush(MindViewStyleKeys.RootPlaceholderForegroundBrushResource, "#B3FFFFFF", "#B3FFFFFF")
            : GetResourceBrush(MindViewStyleKeys.PlaceholderForegroundBrushResource, "#94A3B8", "#64748B");
    }

    private IBrush GetNotePlaceholderBrush(bool isRoot)
    {
        return isRoot
            ? GetResourceBrush(MindViewStyleKeys.RootNotePlaceholderForegroundBrushResource, "#99FFFFFF", "#99FFFFFF")
            : GetResourceBrush(MindViewStyleKeys.NotePlaceholderForegroundBrushResource, "#A0AEC0", "#64748B");
    }

    private IBrush CreateBranchBackgroundBrush(Color accent, bool isHover = false, bool isSelected = false)
    {
        var target = GetResourceColor(MindViewStyleKeys.BranchBlendTargetBrushResource, "#FFFFFF", "#111827");
        var targetWeight = isSelected
            ? GetResourceDouble(MindViewStyleKeys.BranchSelectedBackgroundTargetWeightResource, IsDarkTheme ? 0.66 : 0.82)
            : isHover
                ? GetResourceDouble(MindViewStyleKeys.BranchHoverBackgroundTargetWeightResource, IsDarkTheme ? 0.7 : 0.86)
                : GetResourceDouble(MindViewStyleKeys.BranchBackgroundTargetWeightResource, IsDarkTheme ? 0.76 : 0.91);
        return new SolidColorBrush(MixColor(accent, target, targetWeight));
    }

    private static IBrush CreateAccentBrush(Color accent, double opacity)
    {
        return new SolidColorBrush(accent, opacity);
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

    private static TextAlignment ToTextAlignment(HorizontalAlignment alignment)
    {
        return alignment switch
        {
            HorizontalAlignment.Center => TextAlignment.Center,
            HorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left
        };
    }

    private IBrush GetResourceBrush(string key, string lightFallback, string darkFallback)
    {
        if (AccentColor is { } foregroundAccent && key is MindViewStyleKeys.RootForegroundBrushResource
            or MindViewStyleKeys.RootPlaceholderForegroundBrushResource or MindViewStyleKeys.RootNoteForegroundBrushResource
            or MindViewStyleKeys.RootNotePlaceholderForegroundBrushResource)
            return MindViewThemeResources.AccentForeground(foregroundAccent);
        if (AccentColor is { } accent && key is MindViewStyleKeys.RootBackgroundBrushResource
            or MindViewStyleKeys.SelectionBrushResource or MindViewStyleKeys.ConnectorBrushResource)
            return new SolidColorBrush(accent);
        return MindViewThemeResources.GetBrush(this, key, lightFallback, darkFallback);
    }

    private Color GetResourceColor(string key, string lightFallback, string darkFallback)
    {
        return MindViewThemeResources.GetColor(this, key, lightFallback, darkFallback);
    }

    private double GetResourceDouble(string key, double fallback)
    {
        return MindViewThemeResources.GetDouble(this, key, fallback);
    }

    private int GetResourceInt32(string key, int fallback)
    {
        return MindViewThemeResources.GetInt32(this, key, fallback);
    }

    private string GetResourceString(string key, string fallback)
    {
        return MindViewThemeResources.GetString(this, key, fallback);
    }

    private Thickness GetResourceThickness(string key, Thickness fallback)
    {
        return MindViewThemeResources.GetThickness(this, key, fallback);
    }

    private CornerRadius GetResourceCornerRadius(string key, CornerRadius fallback)
    {
        return MindViewThemeResources.GetCornerRadius(this, key, fallback);
    }

    private BoxShadows GetResourceBoxShadows(string key, string fallback)
    {
        return MindViewThemeResources.GetBoxShadows(this, key, fallback);
    }
}
