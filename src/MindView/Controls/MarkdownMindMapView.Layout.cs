using global::Avalonia;
using CodeWF.Avalonia.MindView.Markdown;

namespace CodeWF.Avalonia.MindView.Controls;

public sealed partial class MarkdownMindMapView
{
    /// <summary>Place 先按单侧树铺出尺寸，再按所选布局重排坐标（尺寸沿用测量结果）。</summary>
    private void ApplyLayoutKind()
    {
        if (Root is null) return;
        if (LayoutKind is MindMapLayoutKind.Right or MindMapLayoutKind.Balanced)
        { MarkdownMindMapTreeLayout.Arrange(Root, _bounds, LayoutKind == MindMapLayoutKind.Balanced); return; }
        if (LayoutKind == MindMapLayoutKind.Down) { var top = _bounds[Root].Top; var cursor = _bounds[Root].X; MeasureDownBounds(Root, ref cursor); PlaceDownBounds(Root, top); return; }
        if (LayoutKind == MindMapLayoutKind.Outline) { var cursor = _bounds.Values.Min(rect => rect.Top); OutlineBounds(Root, 0, ref cursor); return; }
        if (LayoutKind == MindMapLayoutKind.Radial) { RadialBounds(Root); return; }
        if (LayoutKind == MindMapLayoutKind.Fishbone) { FishboneBounds(Root); return; }
    }

    private double NodeHeight(MarkdownMindMapNode node) => _bounds.TryGetValue(node, out var rect) ? rect.Height : 40;
    private double NodeWidth(MarkdownMindMapNode node) => _bounds.TryGetValue(node, out var rect) ? rect.Width : 224;

    private void ShiftTree(MarkdownMindMapNode node, double dy, double dx)
    {
        if (_bounds.TryGetValue(node, out var rect)) _bounds[node] = new Rect(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);
        foreach (var child in node.Children) ShiftTree(child, dy, dx);
    }

    private double MeasureDownBounds(MarkdownMindMapNode node, ref double cursor)
    {
        var width = NodeWidth(node);
        if (node.IsCollapsed || node.Children.Count == 0)
        {
            var rect = _bounds[node];
            _bounds[node] = new Rect(cursor, rect.Y, width, rect.Height);
            cursor += width + 22;
            return _bounds[node].Center.X;
        }
        var centers = new List<double>();
        foreach (var child in node.Children) centers.Add(MeasureDownBounds(child, ref cursor));
        var center = (centers[0] + centers[^1]) / 2;
        var existing = _bounds[node];
        _bounds[node] = new Rect(center - width / 2, existing.Y, width, existing.Height);
        return center;
    }

    private void PlaceDownBounds(MarkdownMindMapNode node, double top)
    {
        var existing = _bounds[node];
        _bounds[node] = new Rect(existing.X, top, existing.Width, existing.Height);
        foreach (var child in node.Children) PlaceDownBounds(child, top + existing.Height + 40);
    }

    private void OutlineBounds(MarkdownMindMapNode node, int depth, ref double cursor)
    {
        var existing = _bounds[node];
        var indent = 32 + depth * 64;
        _bounds[node] = new Rect(indent, cursor, Math.Min(existing.Width, 340 - indent), existing.Height);
        cursor += existing.Height + 12;
        foreach (var child in node.Children) OutlineBounds(child, depth + 1, ref cursor);
    }

    private int SubtreeLeafCountOf(MarkdownMindMapNode node) =>
        node.IsCollapsed || node.Children.Count == 0 ? 1 : node.Children.Sum(SubtreeLeafCountOf);

    private void RadialBounds(MarkdownMindMapNode root)
    {
        var rootRect = _bounds[root];
        var centerX = rootRect.Center.X;
        var centerY = rootRect.Center.Y;
        var leaves = root.Children.Select(SubtreeLeafCountOf).ToList();
        var total = Math.Max(1, leaves.Sum());
        var angle = -Math.PI / 2;
        const double ring = 236;
        for (var i = 0; i < root.Children.Count; i++)
        {
            var span = Math.PI * 2 * leaves[i] / total;
            PlaceRadial(root.Children[i], 1, centerX, centerY, angle + span / 2, ring, leaves[i]);
            angle += span;
        }
        var maxRight = _bounds.Values.Max(rect => rect.Right);
        var minLeft = _bounds.Values.Min(rect => rect.Left);
        _width = Math.Max(_width, maxRight + 32);
        if (minLeft < 24) ShiftTree(root, 0, 24 - minLeft);
    }

    private void PlaceRadial(MarkdownMindMapNode node, int depth, double parentCenterX, double parentCenterY, double angle, double ring, int leaves)
    {
        var radius = depth * ring;
        var size = new Size(NodeWidth(node), NodeHeight(node));
        node = Bind(node, parentCenterX + Math.Cos(angle) * radius - size.Width / 2,
            parentCenterY + Math.Sin(angle) * radius * 0.74 - size.Height / 2, size);
        if (node.IsCollapsed || node.Children.Count == 0) return;
        var childLeaves = node.Children.Select(SubtreeLeafCountOf).ToList();
        var childTotal = Math.Max(1, childLeaves.Sum());
        var total = Math.Max(1, SubtreeLeafCountOf(Root!));
        var span = Math.Max(Math.PI * 2 * leaves / total, 0.24);
        var start = angle - span / 2;
        var accumulated = 0;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var childAngle = start + span * (accumulated + childLeaves[i] / 2d) / childTotal;
            var nodeRect = _bounds[node];
            PlaceRadial(node.Children[i], depth + 1, nodeRect.Center.X, nodeRect.Center.Y, childAngle, ring, childLeaves[i]);
            accumulated += childLeaves[i];
        }
    }

    private MarkdownMindMapNode Bind(MarkdownMindMapNode node, double x, double y, Size size)
    {
        _bounds[node] = new Rect(x, y, size.Width, size.Height);
        return node;
    }

    private void FishboneBounds(MarkdownMindMapNode root)
    {
        var rootRect = _bounds[root];
        var spineY = rootRect.Center.Y;
        var branchX = rootRect.Right + 56;
        var index = 0;
        foreach (var branch in root.Children)
        {
            var direction = index % 2 == 0 ? -1 : 1;
            var branchRect = _bounds[branch];
            _bounds[branch] = new Rect(branchX, spineY + direction * 64 - branchRect.Height / 2, branchRect.Width, branchRect.Height);
            var childX = branchX;
            var cursor = spineY + direction * (64 + branchRect.Height / 2 + 18);
            foreach (var child in branch.Children)
            {
                var childRect = _bounds[child];
                Bind(child, childX, direction > 0 ? cursor : cursor - childRect.Height, childRect.Size);
                StackFishChildren(child, childX, ref cursor, direction);
                childX += childRect.Width + 30;
            }
            branchX += branchRect.Width + 30;
            index++;
        }
        var maxRight = _bounds.Values.Max(rect => rect.Right);
        var minTop = _bounds.Values.Min(rect => rect.Top);
        if (minTop < 24) ShiftTree(root, 24 - minTop, 0);
        _width = Math.Max(_width, maxRight + 32);
        _height = Math.Max(_height, _bounds.Values.Max(rect => rect.Bottom) + 20);
    }

    private void StackFishChildren(MarkdownMindMapNode node, double x, ref double cursor, int direction)
    {
        var local = cursor;
        foreach (var child in node.Children)
        {
            var rect = _bounds[child];
            Bind(child, x + 30, direction > 0 ? local : local - rect.Height, rect.Size);
            local += direction > 0 ? rect.Height + 12 : -(rect.Height + 12);
            StackFishChildren(child, x + 30, ref local, direction);
        }
        cursor = local;
    }

}
