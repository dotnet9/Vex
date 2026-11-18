using System.Globalization;
using System.Net;
using System.Xml.Linq;
using CodeWF.Avalonia.MindView.I18n;

namespace CodeWF.Avalonia.MindView;

public static partial class MindMapDocumentCodec
{
    private const double DrawIoPageMinWidth = 850;
    private const double DrawIoPageMinHeight = 1100;
    private const double DrawIoPagePadding = 120;

    public static string ToDrawIo(MindMapNode root, MindMapLayoutKind layoutKind = MindMapLayoutKind.Right)
    {
        var exportRoot = CloneDrawIoNode(root);
        MindMapLayout.Arrange([exportRoot], layoutKind);

        var cells = new List<XElement>
        {
            new("mxCell", new XAttribute("id", "0")),
            new("mxCell", new XAttribute("id", "1"), new XAttribute("parent", "0"))
        };
        var maxRight = 0d;
        var maxBottom = 0d;
        WriteDrawIoNode(cells, exportRoot, 1, null, ref maxRight, ref maxBottom);

        var graphModel = new XElement(
            "mxGraphModel",
            new XAttribute("dx", "1200"),
            new XAttribute("dy", "800"),
            new XAttribute("grid", "1"),
            new XAttribute("gridSize", "10"),
            new XAttribute("guides", "1"),
            new XAttribute("tooltips", "1"),
            new XAttribute("connect", "1"),
            new XAttribute("arrows", "1"),
            new XAttribute("fold", "1"),
            new XAttribute("page", "1"),
            new XAttribute("pageScale", "1"),
            new XAttribute("pageWidth", FormatDrawIoNumber(Math.Max(DrawIoPageMinWidth, maxRight + DrawIoPagePadding))),
            new XAttribute("pageHeight", FormatDrawIoNumber(Math.Max(DrawIoPageMinHeight, maxBottom + DrawIoPagePadding))),
            new XAttribute("math", "0"),
            new XAttribute("shadow", "0"),
            new XElement("root", cells));

        var document = new XDocument(
            new XElement(
                "mxfile",
                new XAttribute("host", "app.diagrams.net"),
                new XAttribute("agent", "Zhijian"),
                new XAttribute("version", "1.0"),
                new XElement(
                    "diagram",
                    new XAttribute("id", CreateId()),
                    new XAttribute("name", GetDrawIoTitle(exportRoot, 1)),
                    graphModel)));

        return document.ToString();
    }

    private static MindMapNode CloneDrawIoNode(MindMapNode node)
    {
        var clone = new MindMapNode(node.Title)
        {
            Note = node.Note,
            AccentColor = node.AccentColor
        };

        foreach (var child in node.Children)
        {
            clone.Children.Add(CloneDrawIoNode(child));
        }

        return clone;
    }

    private static string WriteDrawIoNode(
        List<XElement> cells,
        MindMapNode node,
        int level,
        string? parentNodeId,
        ref double maxRight,
        ref double maxBottom)
    {
        var nodeId = $"node-{CreateId()}";
        var size = MindMapLayoutMetrics.EstimateNodeSize(node, level, GetDrawIoFallbackTitle(level));
        maxRight = Math.Max(maxRight, node.X + size.Width);
        maxBottom = Math.Max(maxBottom, node.Y + size.Height);

        var cell = new XElement(
            "mxCell",
            new XAttribute("id", nodeId),
            new XAttribute("value", EncodeDrawIoHtml(GetDrawIoTitle(node, level))),
            new XAttribute("style", GetDrawIoVertexStyle(node, level)),
            new XAttribute("vertex", "1"),
            new XAttribute("parent", "1"),
            new XElement(
                "mxGeometry",
                new XAttribute("x", FormatDrawIoNumber(node.X)),
                new XAttribute("y", FormatDrawIoNumber(node.Y)),
                new XAttribute("width", FormatDrawIoNumber(size.Width)),
                new XAttribute("height", FormatDrawIoNumber(size.Height)),
                new XAttribute("as", "geometry")));

        if (!string.IsNullOrWhiteSpace(node.Note))
        {
            cell.SetAttributeValue("tooltip", node.Note.Trim());
        }

        cells.Add(cell);

        if (parentNodeId is not null)
        {
            cells.Add(new XElement(
                "mxCell",
                new XAttribute("id", $"edge-{CreateId()}"),
                new XAttribute("style", "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;jettySize=auto;html=1;endArrow=block;endFill=1;strokeColor=#6C8EBF;"),
                new XAttribute("edge", "1"),
                new XAttribute("parent", "1"),
                new XAttribute("source", parentNodeId),
                new XAttribute("target", nodeId),
                new XElement(
                    "mxGeometry",
                    new XAttribute("relative", "1"),
                    new XAttribute("as", "geometry"))));
        }

        foreach (var child in node.Children)
        {
            WriteDrawIoNode(cells, child, level + 1, nodeId, ref maxRight, ref maxBottom);
        }

        return nodeId;
    }

    private static string GetDrawIoVertexStyle(MindMapNode node, int level)
    {
        if (level <= 1)
        {
            return "rounded=1;whiteSpace=wrap;html=1;fontStyle=1;fillColor=#148BFF;strokeColor=#0F6FCB;fontColor=#FFFFFF;";
        }

        var fillColor = level == 2 ? GetDrawIoAccentColor(node, "#EAF3FF") : "#FFFFFF";
        var strokeColor = level == 2 ? GetDrawIoAccentColor(node, "#6C8EBF") : "#CBD5E1";
        return $"rounded=1;whiteSpace=wrap;html=1;fillColor={fillColor};strokeColor={strokeColor};fontColor=#111827;";
    }

    private static string GetDrawIoAccentColor(MindMapNode node, string fallback)
    {
        var color = node.AccentColor.Trim();
        return color.Length is 7 or 9 && color[0] == '#'
            ? color
            : fallback;
    }

    private static string GetDrawIoTitle(MindMapNode node, int level)
    {
        var title = GetTitle(node);
        return string.IsNullOrWhiteSpace(title) ? GetDrawIoFallbackTitle(level) : title;
    }

    private static string GetDrawIoFallbackTitle(int level)
    {
        return level <= 1
            ? GetResource(MindViewL.DefaultRootTitle, "Center topic")
            : GetResource(MindViewL.UntitledTopic, "Untitled topic");
    }

    private static string EncodeDrawIoHtml(string value)
    {
        return WebUtility.HtmlEncode(value.Trim())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string FormatDrawIoNumber(double value)
    {
        return Math.Ceiling(value).ToString("0", CultureInfo.InvariantCulture);
    }
}
