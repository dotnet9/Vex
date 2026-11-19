using global::Avalonia;

namespace CodeWF.Avalonia.MindView.Markdown;

/// <summary>按同层轮廓压紧子树；节点尺寸由视图测量，布局不修改文档模型。</summary>
internal static class MarkdownMindMapTreeLayout
{
    private const double Gap = 24;

    public static void Arrange(MarkdownMindMapNode root, Dictionary<MarkdownMindMapNode, Rect> bounds, bool balanced)
    {
        var step = bounds.Values.Max(rect => rect.Width) + 72;
        var children = VisibleChildren(root);
        if (balanced)
        {
            var sides = new List<MarkdownMindMapNode>[] { [], [] };
            var loads = new double[2];
            foreach (var child in children)
            {
                var side = loads[0] <= loads[1] ? 0 : 1;
                sides[side].Add(child);
                loads[side] += Load(child);
            }
            for (var side = 0; side < 2; side++)
                Place(Pack(root, sides[side]), 0, 0, side == 0 ? 1 : -1);
        }
        else Place(Pack(root, children), 0, 0, 1);

        var dx = 40 - bounds.Values.Min(rect => rect.Left);
        var dy = 48 - bounds.Values.Min(rect => rect.Top);
        foreach (var node in bounds.Keys.ToArray())
        {
            var rect = bounds[node];
            bounds[node] = new Rect(rect.X + dx, rect.Y + dy, rect.Width, rect.Height);
        }

        MarkdownMindMapNode[] VisibleChildren(MarkdownMindMapNode node) =>
            node.Children.Where(bounds.ContainsKey).ToArray();

        double Load(MarkdownMindMapNode node) => bounds[node].Height + Gap + VisibleChildren(node).Sum(Load);

        Branch Pack(MarkdownMindMapNode node, IReadOnlyList<MarkdownMindMapNode> nodes)
        {
            var packed = nodes.Select(child => Pack(child, VisibleChildren(child))).ToArray();
            var top = new List<double>();
            var bottom = new List<double>();
            foreach (var child in packed)
            {
                var offset = 0d;
                for (var depth = 0; depth < Math.Min(bottom.Count, child.Top.Count); depth++)
                    offset = Math.Max(offset, bottom[depth] + Gap - child.Top[depth]);
                child.Offset = offset;
                for (var depth = 0; depth < child.Top.Count; depth++)
                {
                    if (depth == top.Count)
                    {
                        top.Add(child.Top[depth] + offset);
                        bottom.Add(child.Bottom[depth] + offset);
                    }
                    else
                    {
                        top[depth] = Math.Min(top[depth], child.Top[depth] + offset);
                        bottom[depth] = Math.Max(bottom[depth], child.Bottom[depth] + offset);
                    }
                }
            }
            var center = packed.Length == 0 ? 0 : (packed[0].Offset + packed[^1].Offset) / 2;
            foreach (var child in packed) child.Offset -= center;
            var height = bounds[node].Height;
            return new Branch(node, packed,
                new[] { -height / 2 }.Concat(top.Select(value => value - center)).ToArray(),
                new[] { height / 2 }.Concat(bottom.Select(value => value - center)).ToArray());
        }

        void Place(Branch branch, int depth, double center, int side)
        {
            var size = bounds[branch.Node].Size;
            bounds[branch.Node] = new Rect(side * depth * step - size.Width / 2, center - size.Height / 2, size.Width, size.Height);
            foreach (var child in branch.Children) Place(child, depth + 1, center + child.Offset, side);
        }
    }

    private sealed class Branch(MarkdownMindMapNode node, Branch[] children, IReadOnlyList<double> top, IReadOnlyList<double> bottom)
    {
        public MarkdownMindMapNode Node { get; } = node;
        public Branch[] Children { get; } = children;
        public IReadOnlyList<double> Top { get; } = top;
        public IReadOnlyList<double> Bottom { get; } = bottom;
        public double Offset { get; set; }
    }
}
