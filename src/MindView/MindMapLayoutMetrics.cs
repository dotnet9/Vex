using global::Avalonia;

namespace CodeWF.Avalonia.MindView;

public enum MindMapNodeVisualKind
{
    Root,
    Branch,
    Leaf
}

public readonly record struct MindMapNodeSizeMetrics(
    double MinWidth,
    double MaxWidth,
    double MinHeight,
    Thickness Padding,
    double FontSize);

public static class MindMapLayoutMetrics
{
    public static double DefaultHorizontalSpacing => GetDouble(MindViewStyleKeys.DefaultHorizontalSpacingResource, 104);
    public static double DefaultVerticalSpacing => GetDouble(MindViewStyleKeys.DefaultVerticalSpacingResource, 34);
    public static double RootMinWidth => GetDouble(MindViewStyleKeys.RootMinWidthResource, 88);
    public static double RootMaxWidth => GetDouble(MindViewStyleKeys.RootMaxWidthResource, 260);
    public static double RootMinHeight => GetDouble(MindViewStyleKeys.RootMinHeightResource, 46);
    public static double BranchMinWidth => GetDouble(MindViewStyleKeys.BranchMinWidthResource, 112);
    public static double BranchMaxWidth => GetDouble(MindViewStyleKeys.BranchMaxWidthResource, 240);
    public static double BranchMinHeight => GetDouble(MindViewStyleKeys.BranchMinHeightResource, 42);
    public static double LeafMinWidth => GetDouble(MindViewStyleKeys.LeafMinWidthResource, 48);
    public static double LeafMaxWidth => GetDouble(MindViewStyleKeys.LeafMaxWidthResource, 260);
    public static double LeafMinHeight => GetDouble(MindViewStyleKeys.LeafMinHeightResource, 30);
    public static double DragHandleHitWidth => GetDouble(MindViewStyleKeys.DragHandleHitWidthResource, 24);
    public static double NoteFontSize => GetDouble(MindViewStyleKeys.NoteFontSizeResource, 13);
    public static double NoteVerticalSpacing => GetDouble(MindViewStyleKeys.NoteVerticalSpacingResource, 4);
    public static double NoteMinHeight => GetDouble(MindViewStyleKeys.NoteMinHeightResource, 28);
    public static double NoteMaxHeight => GetDouble(MindViewStyleKeys.NoteMaxHeightResource, 96);

    public static MindMapNodeVisualKind GetVisualKind(int level)
    {
        if (level <= 1)
        {
            return MindMapNodeVisualKind.Root;
        }

        return level == 2 ? MindMapNodeVisualKind.Branch : MindMapNodeVisualKind.Leaf;
    }

    public static MindMapNodeSizeMetrics GetSizeMetrics(int level)
    {
        return GetVisualKind(level) switch
        {
            MindMapNodeVisualKind.Root => new MindMapNodeSizeMetrics(
                RootMinWidth,
                RootMaxWidth,
                RootMinHeight,
                GetThickness(MindViewStyleKeys.RootPaddingResource, new Thickness(10, 5)),
                GetDouble(MindViewStyleKeys.RootFontSizeResource, 18)),
            MindMapNodeVisualKind.Branch => new MindMapNodeSizeMetrics(
                BranchMinWidth,
                BranchMaxWidth,
                BranchMinHeight,
                GetThickness(MindViewStyleKeys.BranchPaddingResource, new Thickness(12, 5)),
                GetDouble(MindViewStyleKeys.BranchFontSizeResource, 17)),
            _ => new MindMapNodeSizeMetrics(
                LeafMinWidth,
                LeafMaxWidth,
                LeafMinHeight,
                GetThickness(MindViewStyleKeys.LeafPaddingResource, new Thickness(0, 2)),
                GetDouble(MindViewStyleKeys.LeafFontSizeResource, 16))
        };
    }

    public static Size EstimateNodeSize(MindMapNode node, int level, string placeholder = "Topic")
    {
        var metrics = GetSizeMetrics(level);
        var text = string.IsNullOrWhiteSpace(node.Title) ? placeholder : node.Title.Trim();
        var paddingWidth = metrics.Padding.Left + metrics.Padding.Right;
        var paddingHeight = metrics.Padding.Top + metrics.Padding.Bottom;
        var minContentWidth = Math.Max(12, metrics.MinWidth - paddingWidth);
        var maxContentWidth = Math.Max(minContentWidth, metrics.MaxWidth - paddingWidth);
        var textWidth = Math.Max(minContentWidth, EstimateMaxLineWidth(text, metrics.FontSize));
        var contentWidth = Math.Clamp(textWidth, minContentWidth, maxContentWidth);
        var lineCount = EstimateWrappedLineCount(text, metrics.FontSize, maxContentWidth);
        var lineHeight = Math.Ceiling(metrics.FontSize * 1.35);
        var height = Math.Max(metrics.MinHeight, lineCount * lineHeight + paddingHeight);

        if (!string.IsNullOrWhiteSpace(node.Note))
        {
            var noteText = node.Note.Trim();
            // 备注参与布局估算，按显式换行逐段计算，避免多行备注压到后续兄弟节点。
            var noteWidth = Math.Max(minContentWidth, EstimateMaxLineWidth(noteText, NoteFontSize));
            var noteLines = EstimateWrappedLineCount(noteText, NoteFontSize, maxContentWidth);
            var noteHeight = Math.Clamp(
                noteLines * Math.Ceiling(NoteFontSize * 1.45),
                NoteMinHeight,
                NoteMaxHeight);
            height += NoteVerticalSpacing + noteHeight;
            contentWidth = Math.Max(contentWidth, Math.Clamp(noteWidth, minContentWidth, maxContentWidth));
        }

        return new Size(contentWidth + paddingWidth, height);
    }

    private static int EstimateWrappedLineCount(string text, double fontSize, double maxLineWidth)
    {
        var lines = 0;
        foreach (var paragraph in SplitLines(text))
        {
            var width = EstimateTextWidth(paragraph, fontSize);
            lines += Math.Max(1, (int)Math.Ceiling(width / maxLineWidth));
        }

        return Math.Max(1, lines);
    }

    private static double EstimateMaxLineWidth(string text, double fontSize)
    {
        var width = 0d;
        foreach (var line in SplitLines(text))
        {
            width = Math.Max(width, EstimateTextWidth(line, fontSize));
        }

        return Math.Max(fontSize, width);
    }

    private static IEnumerable<string> SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static double EstimateTextWidth(string text, double fontSize)
    {
        var width = 0d;
        foreach (var character in text)
        {
            width += character <= '\u007f' ? fontSize * 0.56 : fontSize;
        }

        return Math.Max(fontSize, width);
    }

    private static double GetDouble(string key, double fallback)
    {
        return MindViewThemeResources.GetDouble(null, key, fallback);
    }

    private static Thickness GetThickness(string key, Thickness fallback)
    {
        return MindViewThemeResources.GetThickness(null, key, fallback);
    }
}
