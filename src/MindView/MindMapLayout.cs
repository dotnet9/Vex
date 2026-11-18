using global::Avalonia;

namespace CodeWF.Avalonia.MindView;

/// <summary>
/// 六种布局的共享入口。几何参考原型 map-layout.js：布局永不改变文档顺序；
/// 节点尺寸来自 MindMapLayoutMetrics 的估算，画布边界由编辑器自行扩展。
/// 深度全程显式传递（节点没有父引用）。
/// </summary>
public static class MindMapLayout
{
    public static void Arrange(IEnumerable<MindMapNode>? roots, MindMapLayoutKind kind = MindMapLayoutKind.Right)
    {
        if (roots is null) return;
        var list = roots as IList<MindMapNode> ?? roots.ToList();
        if (list.Count == 0) return;
        // 多根文档只保证单侧树形的既有行为；其余布局要求单一主题中心。
        if (list.Count > 1 || kind == MindMapLayoutKind.Right)
        {
            MindMapTreeLayout.Arrange(list);
            return;
        }
        var root = list[0];
        switch (kind)
        {
            case MindMapLayoutKind.Balanced: Balanced(root); break;
            case MindMapLayoutKind.Radial: Radial(root); break;
            case MindMapLayoutKind.Down: Down(root); break;
            case MindMapLayoutKind.Outline: Outline(root); break;
            case MindMapLayoutKind.Fishbone: Fishbone(root); break;
            default: MindMapTreeLayout.Arrange(list); break;
        }
    }

    private static double RootX => MindViewThemeResources.GetDouble(null, MindViewStyleKeys.DefaultRootXResource, 420);
    private static double RootY => MindViewThemeResources.GetDouble(null, MindViewStyleKeys.DefaultRootYResource, 220);
    private static double HorizontalSpacing => MindMapLayoutMetrics.DefaultHorizontalSpacing;
    private static double VerticalSpacing => MindMapLayoutMetrics.DefaultVerticalSpacing;

    private static Size SizeOf(MindMapNode node, int depth) => MindMapLayoutMetrics.EstimateNodeSize(node, depth + 1);

    /// <summary>双侧平衡：一级分支贪心分到负载较小的一侧，两侧各自做镜像的横向树。</summary>
    private static void Balanced(MindMapNode root)
    {
        var rootSize = SizeOf(root, 0);
        var sides = new List<MindMapNode>[2] { [], [] };
        var loads = new double[2];
        foreach (var child in root.Children)
        {
            var side = loads[0] <= loads[1] ? 0 : 1;
            sides[side].Add(child);
            loads[side] += SubtreeHeight(child, 1);
        }
        var sideTops = new double[2];
        var sideBottoms = new double[2];
        for (var side = 0; side < 2; side++)
        {
            if (sides[side].Count == 0) continue;
            var sign = side == 0 ? 1 : -1;
            var nextTop = RootY;
            var firstColumnWidth = sides[side].Max(child => SizeOf(child, 1).Width);
            foreach (var child in sides[side])
            {
                LayoutSide(child, 1, sign > 0
                    ? RootX + HorizontalSpacing
                    : RootX - HorizontalSpacing - firstColumnWidth, ref nextTop, sign);
            }
            sideTops[side] = RootY;
            sideBottoms[side] = nextTop - VerticalSpacing;
        }
        var used = sideTops.Zip(sideBottoms).Where(pair => pair.Second >= pair.First).ToList();
        var center = used.Count == 0 ? RootY : used.Average(pair => (pair.First + pair.Second) / 2);
        root.X = RootX - rootSize.Width / 2;
        root.Y = center - rootSize.Height / 2;
    }

    private static void LayoutSide(MindMapNode node, int depth, double x, ref double nextTop, int sign)
    {
        var size = SizeOf(node, depth);
        node.X = x;
        if (node.Children.Count == 0)
        {
            node.Y = nextTop;
            nextTop = node.Y + size.Height + VerticalSpacing;
            return;
        }
        var childTop = nextTop;
        var childWidth = node.Children.Max(child => SizeOf(child, depth + 1).Width);
        foreach (var child in node.Children)
        {
            LayoutSide(child, depth + 1, sign > 0
                ? node.X + size.Width / 2 + HorizontalSpacing
                : node.X - size.Width / 2 - HorizontalSpacing - childWidth, ref childTop, sign);
        }
        var center = CenterBetween(node.Children, depth + 1);
        node.Y = center - size.Height / 2;
        nextTop = Math.Max(childTop, node.Y + size.Height + VerticalSpacing);
    }

    private static double CenterBetween(IReadOnlyList<MindMapNode> nodes, int depth)
    {
        var first = nodes[0].Y + SizeOf(nodes[0], depth).Height / 2;
        var last = nodes[^1].Y + SizeOf(nodes[^1], depth).Height / 2;
        return (first + last) / 2;
    }

    private static double SubtreeHeight(MindMapNode node, int depth)
    {
        var height = SizeOf(node, depth).Height;
        if (node.Children.Count == 0) return height;
        return Math.Max(height, node.Children.Sum(child => SubtreeHeight(child, depth + 1)) + VerticalSpacing * (node.Children.Count - 1));
    }

    /// <summary>中心放射：一级分支按叶子数分配角度，半径随深度增加，碰撞时绕中心推开。</summary>
    private static void Radial(MindMapNode root)
    {
        var rootSize = SizeOf(root, 0);
        var centerX = RootX;
        var centerY = RootY;
        root.X = centerX - rootSize.Width / 2;
        root.Y = centerY - rootSize.Height / 2;
        if (root.Children.Count == 0) return;
        var leaves = root.Children.Select(SubtreeLeafCount).ToList();
        var total = Math.Max(1, leaves.Sum());
        var fullCircle = Math.PI * 2;
        var angle = -Math.PI / 2;
        var ringStep = MindMapLayoutMetrics.LeafMaxWidth * 0.9 + HorizontalSpacing;
        for (var i = 0; i < root.Children.Count; i++)
        {
            var span = fullCircle * leaves[i] / total;
            var childAngle = angle + span / 2;
            PlaceRadial(root.Children[i], 1, centerX, centerY, childAngle, span, ringStep, leaves[i]);
            angle += span;
        }
        // 两轮碰撞推开：子树包围盒重叠时绕中心小幅旋转。
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < root.Children.Count; i++)
            {
                for (var j = i + 1; j < root.Children.Count; j++)
                {
                    if (Intersects(SubtreeBounds(root.Children[i]), SubtreeBounds(root.Children[j]), 16))
                    {
                        RotateSubtree(root.Children[j], centerX, centerY, 0.07);
                    }
                }
            }
        }
    }

    private static void PlaceRadial(MindMapNode node, int depth, double parentCenterX, double parentCenterY,
        double angle, double span, double ringStep, int leaves)
    {
        var size = SizeOf(node, depth);
        var radius = depth * ringStep;
        var x = parentCenterX + Math.Cos(angle) * radius;
        var y = parentCenterY + Math.Sin(angle) * radius * 0.74;
        node.X = x - size.Width / 2;
        node.Y = y - size.Height / 2;
        if (node.Children.Count == 0) return;
        var childLeaves = node.Children.Select(SubtreeLeafCount).ToList();
        var childTotal = Math.Max(1, childLeaves.Sum());
        var localSpan = Math.Max(span, 0.24);
        var start = angle - localSpan / 2;
        var accumulated = 0;
        for (var i = 0; i < node.Children.Count; i++)
        {
            var childAngle = start + localSpan * (accumulated + childLeaves[i] / 2d) / childTotal;
            PlaceRadial(node.Children[i], depth + 1, node.X + size.Width / 2, node.Y + size.Height / 2,
                childAngle, localSpan * childLeaves[i] / childTotal, ringStep, childLeaves[i]);
            accumulated += childLeaves[i];
        }
    }

    private static int SubtreeLeafCount(MindMapNode node) =>
        node.Children.Count == 0 ? 1 : node.Children.Sum(SubtreeLeafCount);

    private static (double L, double T, double R, double B) SubtreeBounds(MindMapNode node)
    {
        var size = SizeOf(node, 1);
        var left = node.X; var top = node.Y; var right = node.X + size.Width; var bottom = node.Y + size.Height;
        foreach (var child in node.Children)
        {
            var (l, t, r, b) = SubtreeBounds(child);
            left = Math.Min(left, l); top = Math.Min(top, t);
            right = Math.Max(right, r); bottom = Math.Max(bottom, b);
        }
        return (left, top, right, bottom);
    }

    private static bool Intersects((double L, double T, double R, double B) a, (double L, double T, double R, double B) b, double gap) =>
        a.L < b.R + gap && b.L < a.R + gap && a.T < b.B + gap && b.T < a.B + gap;

    private static void RotateSubtree(MindMapNode node, double centerX, double centerY, double delta)
    {
        var size = SizeOf(node, 1);
        Rotate(node, centerX, centerY, delta, size);
        foreach (var child in node.Children) RotateSubtree(child, centerX, centerY, delta);
    }

    private static void Rotate(MindMapNode node, double centerX, double centerY, double delta, Size size)
    {
        var cx = node.X + size.Width / 2 - centerX;
        var cy = node.Y + size.Height / 2 - centerY;
        var rotated = Math.Atan2(cy, cx) + delta;
        var radius = Math.Sqrt(cx * cx + cy * cy);
        node.X = centerX + Math.Cos(rotated) * radius - size.Width / 2;
        node.Y = centerY + Math.Sin(rotated) * radius - size.Height / 2;
    }

    /// <summary>向下树形：层自上而下，叶子从左到右顺序铺开，父节点居中于子节点之上。</summary>
    private static void Down(MindMapNode root)
    {
        var cursor = RootX;
        MeasureDown(root, 0, ref cursor);
        PlaceDown(root, 0, RootY);
    }

    private static double MeasureDown(MindMapNode node, int depth, ref double cursor)
    {
        var size = SizeOf(node, depth);
        if (node.Children.Count == 0)
        {
            node.X = cursor;
            cursor += size.Width + HorizontalSpacing * 0.6;
            return node.X + size.Width / 2;
        }
        var centers = new List<double>();
        foreach (var child in node.Children)
        {
            centers.Add(MeasureDown(child, depth + 1, ref cursor));
        }
        var center = (centers[0] + centers[^1]) / 2;
        node.X = center - size.Width / 2;
        return center;
    }

    private static void PlaceDown(MindMapNode node, int depth, double top)
    {
        var size = SizeOf(node, depth);
        node.Y = top;
        var childTop = top + size.Height + VerticalSpacing + 26;
        foreach (var child in node.Children) PlaceDown(child, depth + 1, childTop);
    }

    /// <summary>目录树：深度缩进的纵向列表（对应原型目录树）。</summary>
    private static void Outline(MindMapNode root)
    {
        var cursor = RootY;
        WalkOutline(root, 0, RootX, ref cursor);
    }

    private static void WalkOutline(MindMapNode node, int depth, double x, ref double cursor)
    {
        var size = SizeOf(node, depth);
        node.X = x;
        node.Y = cursor;
        cursor += size.Height + VerticalSpacing * 0.66;
        foreach (var child in node.Children) WalkOutline(child, depth + 1, x + 64, ref cursor);
    }

    /// <summary>鱼骨图：一级分支沿主脊上下交替，子项沿分支向外排布（对应原型鱼骨图）。</summary>
    private static void Fishbone(MindMapNode root)
    {
        var rootSize = SizeOf(root, 0);
        var spineY = RootY;
        root.X = RootX - rootSize.Width - 56;
        root.Y = spineY - rootSize.Height / 2;
        var branchX = RootX + 64;
        var index = 0;
        foreach (var branch in root.Children)
        {
            var size = SizeOf(branch, 1);
            var direction = index % 2 == 0 ? -1 : 1;
            branch.X = branchX;
            branch.Y = spineY + direction * 72 - size.Height / 2;
            var childX = branchX;
            var childCursor = spineY + direction * (72 + size.Height / 2 + VerticalSpacing);
            foreach (var child in branch.Children)
            {
                var childSize = SizeOf(child, 2);
                child.X = childX;
                child.Y = direction > 0 ? childCursor : childCursor - childSize.Height;
                StackChildren(child, 2, childX, ref childCursor, direction);
                childX += childSize.Width + 36;
            }
            branchX += 224;
            index++;
        }
    }

    private static void StackChildren(MindMapNode node, int depth, double x, ref double cursor, int direction)
    {
        var local = cursor;
        foreach (var child in node.Children)
        {
            var size = SizeOf(child, depth + 1);
            child.X = x + 32;
            child.Y = direction > 0 ? local : local - size.Height;
            local += direction > 0 ? size.Height + 14 : -(size.Height + 14);
            StackChildren(child, depth + 1, x + 32, ref local, direction);
        }
        cursor = local;
    }
}
