namespace CodeWF.Avalonia.MindView;

/// <summary>
/// Shared tree layout algorithm for mind-map roots.
/// </summary>
public static class MindMapTreeLayout
{
    private static double DefaultRootX => GetDouble(MindViewStyleKeys.DefaultRootXResource, 420);
    private static double DefaultRootY => GetDouble(MindViewStyleKeys.DefaultRootYResource, 220);
    private static double DefaultMinNodeY => GetDouble(MindViewStyleKeys.DefaultMinNodeYResource, 24);

    public static void Arrange(IEnumerable<MindMapNode> roots)
    {
        var nextTop = DefaultRootY;
        foreach (var root in roots)
        {
            var columnPositions = CalculateColumnPositions(root);
            LayoutNode(root, 0, columnPositions, ref nextTop);
            nextTop += MindMapLayoutMetrics.DefaultVerticalSpacing;
        }
    }

    private static double[] CalculateColumnPositions(MindMapNode root)
    {
        var columnWidths = new List<double>();
        CollectColumnWidths(root, 0, columnWidths);

        var columnPositions = new double[columnWidths.Count];
        var x = DefaultRootX;
        for (var i = 0; i < columnPositions.Length; i++)
        {
            columnPositions[i] = x;
            x += columnWidths[i] + MindMapLayoutMetrics.DefaultHorizontalSpacing;
        }

        return columnPositions;
    }

    private static void CollectColumnWidths(MindMapNode node, int depth, List<double> columnWidths)
    {
        var level = depth + 1;
        var size = MindMapLayoutMetrics.EstimateNodeSize(node, level);
        while (columnWidths.Count <= depth)
        {
            columnWidths.Add(0);
        }

        columnWidths[depth] = Math.Max(columnWidths[depth], size.Width);
        foreach (var child in node.Children)
        {
            CollectColumnWidths(child, depth + 1, columnWidths);
        }
    }

    private static LayoutResult LayoutNode(
        MindMapNode node,
        int depth,
        IReadOnlyList<double> columnPositions,
        ref double nextTop)
    {
        var level = depth + 1;
        var size = MindMapLayoutMetrics.EstimateNodeSize(node, level);
        node.X = depth < columnPositions.Count
            ? columnPositions[depth]
            : columnPositions[^1] + (depth - columnPositions.Count + 1)
            * (MindMapLayoutMetrics.LeafMaxWidth + MindMapLayoutMetrics.DefaultHorizontalSpacing);

        if (node.Children.Count == 0)
        {
            node.Y = nextTop;
            nextTop = node.Y + size.Height + MindMapLayoutMetrics.DefaultVerticalSpacing;
            return new LayoutResult(node.Y + size.Height / 2, node.Y, node.Y + size.Height);
        }

        var firstCenter = 0d;
        var lastCenter = 0d;
        var subtreeTop = double.MaxValue;
        var subtreeBottom = double.MinValue;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var childLayout = LayoutNode(node.Children[i], depth + 1, columnPositions, ref nextTop);
            if (i == 0)
            {
                firstCenter = childLayout.CenterY;
            }

            lastCenter = childLayout.CenterY;
            subtreeTop = Math.Min(subtreeTop, childLayout.Top);
            subtreeBottom = Math.Max(subtreeBottom, childLayout.Bottom);
        }

        var nodeCenter = (firstCenter + lastCenter) / 2;
        node.Y = Math.Max(DefaultMinNodeY, nodeCenter - size.Height / 2);
        subtreeTop = Math.Min(subtreeTop, node.Y);
        subtreeBottom = Math.Max(subtreeBottom, node.Y + size.Height);
        nextTop = Math.Max(nextTop, subtreeBottom + MindMapLayoutMetrics.DefaultVerticalSpacing);
        return new LayoutResult(node.Y + size.Height / 2, subtreeTop, subtreeBottom);
    }

    private readonly record struct LayoutResult(double CenterY, double Top, double Bottom);

    private static double GetDouble(string key, double fallback)
    {
        return MindViewThemeResources.GetDouble(null, key, fallback);
    }
}
