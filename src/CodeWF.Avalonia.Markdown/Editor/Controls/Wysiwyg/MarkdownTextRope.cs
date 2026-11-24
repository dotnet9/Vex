namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

internal sealed class MarkdownTextRope
{
    private const int LeafLength = 4096;
    private Node? _root;

    private MarkdownTextRope(Node? root) => _root = root;

    public int Length => _root?.Length ?? 0;
    public int LineCount => _root?.LineCount ?? 1;

    public static MarkdownTextRope FromString(string text)
    {
        if (text.Length == 0) return new MarkdownTextRope(null);
        List<Node> leaves = [];
        for (var start = 0; start < text.Length; start += LeafLength)
            leaves.Add(new Leaf(text.Substring(start, Math.Min(LeafLength, text.Length - start))));
        return new MarkdownTextRope(BuildBalanced(leaves, 0, leaves.Count));
    }

    public MarkdownTextRope Replace(int start, int length, string replacement)
    {
        start = Math.Clamp(start, 0, Length);
        length = Math.Clamp(length, 0, Length - start);
        var inserted = FromString(replacement)._root;
        return new MarkdownTextRope(_root is null ? inserted : _root.Replace(start, length, inserted));
    }

    public override string ToString() => _root?.ToString() ?? string.Empty;

    public string Substring(int start, int length)
    {
        start = Math.Clamp(start, 0, Length);
        length = Math.Clamp(length, 0, Length - start);
        return length == 0 ? string.Empty : _root!.Slice(start, start + length)?.ToString() ?? string.Empty;
    }

    public bool ContentEquals(string value) =>
        Length == value.Length && (_root?.ContentEquals(value, 0) ?? value.Length == 0);

    public (int Line, int Column) GetLineColumn(int offset)
    {
        offset = Math.Clamp(offset, 0, Length);
        var line = 1;
        var lastNewLine = -1;
        CountLinesBefore(_root, offset, 0, ref line, ref lastNewLine);
        return (line, offset - lastNewLine);
    }

    private static Node? BuildBalanced(List<Node> nodes, int start, int count)
    {
        if (count == 0) return null;
        if (count == 1) return nodes[start];
        var leftCount = count / 2;
        return Concat(BuildBalanced(nodes, start, leftCount),
            BuildBalanced(nodes, start + leftCount, count - leftCount));
    }

    private static Node? Concat(Node? left, Node? right)
    {
        if (left is null) return right;
        if (right is null) return left;
        var node = new Branch(left, right);
        var balance = left.Height - right.Height;
        if (balance > 1)
        {
            if (left is Branch branch && branch.Left.Height >= branch.Right.Height)
                return RotateRight(node);
            return RotateLeftRight(node);
        }
        if (balance < -1)
        {
            if (right is Branch branch && branch.Right.Height >= branch.Left.Height)
                return RotateLeft(node);
            return RotateRightLeft(node);
        }
        return node;
    }

    private static Branch RotateRight(Branch node)
    {
        var left = (Branch)node.Left;
        return new Branch(left.Left!, Concat(left.Right, node.Right)!);
    }

    private static Branch RotateLeft(Branch node)
    {
        var right = (Branch)node.Right;
        return new Branch(Concat(node.Left, right.Left)!, right.Right!);
    }

    private static Branch RotateLeftRight(Branch node)
    {
        var left = (Branch)node.Left;
        return RotateRight(new Branch(RotateLeft(left), node.Right));
    }

    private static Branch RotateRightLeft(Branch node)
    {
        var right = (Branch)node.Right;
        return RotateLeft(new Branch(node.Left, RotateRight(right)));
    }

    private static void CountLinesBefore(Node? node, int offset, int position, ref int line, ref int lastNewLine)
    {
        if (node is null || offset <= 0) return;
        if (node is Leaf leaf)
        {
            var count = Math.Min(offset, leaf.Text.Length);
            for (var index = 0; index < count; index++)
            {
                if (leaf.Text[index] != '\n') continue;
                line++;
                lastNewLine = position + index;
            }
            return;
        }

        var branch = (Branch)node;
        if (offset < branch.Left.Length)
        {
            CountLinesBefore(branch.Left, offset, position, ref line, ref lastNewLine);
            return;
        }
        CountLinesBefore(branch.Left, branch.Left.Length, position, ref line, ref lastNewLine);
        CountLinesBefore(branch.Right, offset - branch.Left.Length, position + branch.Left.Length, ref line, ref lastNewLine);
    }

    private abstract class Node
    {
        public abstract int Length { get; }
        public abstract int LineCount { get; }
        public abstract int Height { get; }

        public abstract Node? Replace(int start, int length, Node? inserted);
        public abstract Node? Slice(int start, int end);
        public abstract bool ContentEquals(string value, int offset);
        public abstract void CopyTo(char[] destination, int offset);
        public abstract override string ToString();
    }

    private sealed class Leaf(string text) : Node
    {
        public string Text => text;
        public override int Length => text.Length;
        public override int LineCount => 1 + text.AsSpan().Count('\n');
        public override int Height => 1;

        public override Node? Replace(int start, int length, Node? inserted) =>
            FromString(string.Concat(text.AsSpan(0, start), inserted?.ToString() ?? string.Empty,
                text.AsSpan(start + length)))._root;

        public override Node? Slice(int start, int end) =>
            start == 0 && end == text.Length ? this : new Leaf(text.Substring(start, end - start));

        public override bool ContentEquals(string value, int offset) =>
            text.AsSpan().SequenceEqual(value.AsSpan(offset, text.Length));

        public override void CopyTo(char[] destination, int offset) => text.CopyTo(0, destination, offset, text.Length);

        public override string ToString() => text;
    }

    private sealed class Branch(Node left, Node right) : Node
    {
        public Node Left => left;
        public Node Right => right;
        public override int Length => left.Length + right.Length;
        public override int LineCount => left.LineCount + right.LineCount - 1;
        public override int Height => 1 + Math.Max(left.Height, right.Height);

        public override Node? Replace(int start, int length, Node? inserted)
        {
            var end = start + length;
            if (end <= left.Length) return Concat(left.Replace(start, length, inserted), right);
            if (start >= left.Length)
                return Concat(left, right.Replace(start - left.Length, length, inserted));
            return Concat(Concat(left.Slice(0, start), inserted),
                right.Slice(end - left.Length, right.Length));
        }

        public override Node? Slice(int start, int end)
        {
            if (start == 0 && end == Length) return this;
            Node? leftPart;
            Node? rightPart;
            if (end <= left.Length) return left.Slice(start, end);
            if (start >= left.Length) return right.Slice(start - left.Length, end - left.Length);
            leftPart = left.Slice(start, left.Length);
            rightPart = right.Slice(0, end - left.Length);
            return Concat(leftPart, rightPart);
        }

        public override bool ContentEquals(string value, int offset) =>
            left.ContentEquals(value, offset) && right.ContentEquals(value, offset + left.Length);

        public override void CopyTo(char[] destination, int offset)
        {
            left.CopyTo(destination, offset);
            right.CopyTo(destination, offset + left.Length);
        }

        public override string ToString()
        {
            var buffer = new char[Length];
            CopyTo(buffer, 0);
            return new string(buffer);
        }
    }
}
