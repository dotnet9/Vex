using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.MindView.I18n;
using CodeWF.Avalonia.MindView.Markdown;

namespace CodeWF.Avalonia.MindView.Controls;

public sealed partial class MarkdownMindMapView
{
    /// <summary>未填充根节点使用的面板底色；默认跟随卡片底色。</summary>
    public static readonly StyledProperty<IBrush?> PanelBrushProperty = AvaloniaProperty.Register<MarkdownMindMapView, IBrush?>(nameof(PanelBrush));
    public static readonly StyledProperty<IBrush?> MutedTextBrushProperty = AvaloniaProperty.Register<MarkdownMindMapView, IBrush?>(nameof(MutedTextBrush));
    public IBrush? PanelBrush { get => GetValue(PanelBrushProperty); set => SetValue(PanelBrushProperty, value); }
    public IBrush? MutedTextBrush { get => GetValue(MutedTextBrushProperty); set => SetValue(MutedTextBrushProperty, value); }

    private Border CreateNodeCard(MarkdownMindMapNode node, Rect rect, bool dark, IBrush line, IBrush background, IBrush foreground)
    {
        var root = ReferenceEquals(node, Root);
        var heading = node.Kind == MarkdownMindMapNodeKind.Heading;
        var ink = root && StyleKind is not (MindMapStyleKind.Outline or MindMapStyleKind.Colorful)
            ? MindViewThemeResources.AccentForeground(AccentColor)
            : !root && !heading && StyleKind != MindMapStyleKind.Colorful ? MutedTextBrush ?? foreground : foreground;
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), VerticalAlignment = VerticalAlignment.Center };
        body.Children.Add(new TextBlock
        {
            Text = node.Title, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxLines = root ? 1 : 4,
            FontSize = root || heading ? 14 : 12, LineHeight = root || heading ? 22.12 : 20,
            FontWeight = root || heading ? FontWeight.SemiBold : FontWeight.Normal,
            Foreground = ink, VerticalAlignment = VerticalAlignment.Center
        });
        var badge = root ? MindViewLocalization.GetResource(MindViewL.MarkdownDocument) : heading ? $"H{node.HeadingLevel}" : null;
        if (badge is not null)
        {
            var text = new TextBlock
            {
                Text = badge, FontSize = 9, LineHeight = 14, Foreground = ink, Opacity = .75,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = root ? VerticalAlignment.Center : VerticalAlignment.Top,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(text, 1); body.Children.Add(text);
        }
        var content = new Grid();
        if (!root && !heading && StyleKind is MindMapStyleKind.Quiet or MindMapStyleKind.Colorful or MindMapStyleKind.Capsule)
            content.Children.Add(new CardDecoration { IsSummary = true, Line = line, Margin = new Thickness(-12, -10), IsHitTestVisible = false });
        content.Children.Add(body);
        var card = new Border
        {
            Width = rect.Width, Height = RenderedHeight(node, rect), Padding = new Thickness(13, 11), Child = content,
            Cursor = new Cursor(StandardCursorType.Hand), Focusable = true
        };
        ApplyCardAppearance(node, card, dark, line, background);
        return card;
    }

    // 布局的最小行盒与卡片的文字/描边盒不同；公开定位和居中必须包含实际绘制高度。
    private static double RenderedHeight(MarkdownMindMapNode node, Rect rect) =>
        Math.Max(rect.Height, (node.Kind is MarkdownMindMapNodeKind.Document or MarkdownMindMapNodeKind.Heading ? 22.12 : 20)
            * Math.Clamp((rect.Height - 22) / 20, 1, 4) + 24);

    private void ApplyCardAppearance(MarkdownMindMapNode node, Border card, bool dark, IBrush line, IBrush background)
    {
        var root = ReferenceEquals(node, Root);
        var heading = node.Kind == MarkdownMindMapNodeKind.Heading;
        var branch = _branches[node];
        var dashedSummary = !root && !heading && StyleKind is MindMapStyleKind.Quiet or MindMapStyleKind.Colorful or MindMapStyleKind.Capsule;
        card.Background = root ? new SolidColorBrush(AccentColor) : background;
        card.BorderBrush = root ? new SolidColorBrush(AccentColor) : dashedSummary ? Brushes.Transparent : line;
        card.BorderThickness = new Thickness(1);
        card.CornerRadius = new CornerRadius(6);
        switch (StyleKind)
        {
            case MindMapStyleKind.Colorful:
                card.Background = Mix(branch, background, heading || root ? .13 : .10, dark);
                if (!dashedSummary) card.BorderBrush = Mix(branch, line, .45, dark);
                break;
            case MindMapStyleKind.Outline:
                card.Background = root ? PanelBrush ?? background : Brushes.Transparent;
                card.BorderBrush = new SolidColorBrush(branch);
                card.BorderThickness = new Thickness(root ? 2 : 1.5);
                break;
            case MindMapStyleKind.Capsule:
                card.CornerRadius = new CornerRadius(root || heading ? card.Height / 2 : 12);
                break;
            case MindMapStyleKind.Underline:
                if (!root) card.Background = Brushes.Transparent;
                card.BorderBrush = new SolidColorBrush(branch);
                card.BorderThickness = new Thickness(0, 0, 0, 2);
                card.CornerRadius = new CornerRadius(root ? 8 : 3);
                break;
            case MindMapStyleKind.Mono:
                if (root) card.BorderBrush = MutedTextBrush ?? line;
                break;
        }
        // 外圈单独表达选中，不能覆盖线框/脉络样式自身的边框。
        var gap = (PanelBrush ?? background) as ISolidColorBrush;
        card.BoxShadow = node == _selected ? BoxShadows.Parse(FormattableString.Invariant($"0 0 0 3 {gap?.Color ?? (dark ? Colors.Black : Colors.White)}, 0 0 0 5 {AccentColor}")) : default;
    }

    private static IBrush Mix(Color color, IBrush basis, double ratio, bool dark)
    {
        var other = (basis as ISolidColorBrush)?.Color ?? (dark ? Color.Parse("#242633") : Colors.White);
        return new SolidColorBrush(Color.FromRgb(
            (byte)Math.Round(color.R * ratio + other.R * (1 - ratio)),
            (byte)Math.Round(color.G * ratio + other.G * (1 - ratio)),
            (byte)Math.Round(color.B * ratio + other.B * (1 - ratio))));
    }
}
