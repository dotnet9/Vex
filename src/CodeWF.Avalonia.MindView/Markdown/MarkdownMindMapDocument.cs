using System.Globalization;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Syntax;
using Markdig.Extensions.Tables;

namespace CodeWF.Avalonia.MindView.Markdown;

public enum MarkdownMindMapNodeKind { Document, Heading, Paragraph, Quote, Alert, ListItem, Code, Table }

/// <summary>摘要和完整正文分开保存；源码范围含起点、不含终点。</summary>
public sealed class MarkdownMindMapNode
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string FullText { get; init; }
    public MarkdownMindMapNodeKind Kind { get; init; }
    public int SourceStart { get; init; }
    public int SourceEnd { get; init; }
    public int HeadingLevel { get; init; }
    public string? AlertKind { get; init; }
    public int SourceLine { get; init; }
    public List<MarkdownMindMapNode> Children { get; } = [];
    public bool IsCollapsed { get; set; }
}

public sealed record MarkdownMindMapOptions
{
    public int SummaryLength { get; init; } = 30;
    public bool ShowParagraphs { get; init; } = true;
    public bool ShowLists { get; init; } = true;
    public bool ShowCode { get; init; }
    public bool ShowTables { get; init; }
}

/// <summary>从 Markdown AST 构造文档树，不会将代码围栏内的 # 当作标题。</summary>
public static class MarkdownMindMapDocument
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter().Build();

    public static MarkdownMindMapNode Parse(string? markdown, string title = "Markdown", MarkdownMindMapOptions? options = null)
    {
        markdown ??= string.Empty;
        options ??= new();
        var limit = Math.Clamp(options.SummaryLength, 1, 120);
        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        var root = new MarkdownMindMapNode
        {
            Id = "document", Title = title, FullText = title, Kind = MarkdownMindMapNodeKind.Document,
            SourceEnd = markdown.Length
        };
        var headings = new Stack<(int Level, MarkdownMindMapNode Node)>();
        headings.Push((0, root));
        foreach (var block in document)
        {
            if (block is HeadingBlock heading)
            {
                var node = Create(block, MarkdownMindMapNodeKind.Heading, truncate: false);
                while (headings.Count > 1 && headings.Peek().Level >= heading.Level) headings.Pop();
                headings.Peek().Node.Children.Add(node);
                headings.Push((heading.Level, node));
            }
            else AddBlock(block, headings.Peek().Node);
        }
        // 单个开头 H1 直接作为中心主题；多 H1 或开头正文保留文件名根节点。
        var firstContent = document.FirstOrDefault(b => b is not Markdig.Extensions.Yaml.YamlFrontMatterBlock);
        return firstContent is HeadingBlock { Level: 1 }
            && document.OfType<HeadingBlock>().Count(h => h.Level == 1) == 1 && root.Children.Count == 1
            ? root.Children[0] : root;

        string Source(Block block) => markdown.Substring(Math.Clamp(block.Span.Start, 0, markdown.Length),
            Math.Max(0, Math.Min(block.Span.End + 1, markdown.Length) - Math.Clamp(block.Span.Start, 0, markdown.Length)));

        MarkdownMindMapNode Create(Block block, MarkdownMindMapNodeKind kind, bool truncate = true, string? text = null, string? alertKind = null)
        {
            var full = text ?? Markdig.Markdown.ToPlainText(Source(block), Pipeline).Trim();
            full = string.Join(" ", full.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return new()
            {
                Id = $"{kind.ToString().ToLowerInvariant()}:{block.Span.Start}", Kind = kind, FullText = full,
                Title = truncate ? Summarize(full, limit) : full,
                SourceStart = block.Span.Start, SourceEnd = Math.Min(markdown.Length, block.Span.End + 1),
                HeadingLevel = block is HeadingBlock heading ? heading.Level : 0,
                AlertKind = alertKind,
                SourceLine = block.Line + 1
            };
        }

        void AddBlock(Block block, MarkdownMindMapNode parent)
        {
            switch (block)
            {
                case ListBlock list when options.ShowLists:
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        var content = item.FirstOrDefault(b => b is ParagraphBlock);
                        var raw = content is null ? string.Empty : Source(content);
                        // 单独重解析段落会丢失任务列表上下文，把 [x] 当成普通文字。
                        if (content is ParagraphBlock { Inline.FirstChild: Markdig.Extensions.TaskLists.TaskList }) raw = raw[3..].TrimStart();
                        var full = Markdig.Markdown.ToPlainText(raw, Pipeline).Trim();
                        var node = Create(item, MarkdownMindMapNodeKind.ListItem, text: full);
                        parent.Children.Add(node);
                        foreach (var child in item.Where(b => !ReferenceEquals(b, content))) AddBlock(child, node);
                    }
                    break;
                case AlertBlock alert when options.ShowParagraphs:
                    var alertKind = alert.Kind.ToString().ToLowerInvariant();
                    var alertText = string.Join(" ", ((QuoteBlock)alert).Select(child => Markdig.Markdown.ToPlainText(Source(child), Pipeline)));
                    alertText = Regex.Replace(alertText, @"^\s*\[![A-Za-z]+\]\s*", string.Empty).Trim();
                    parent.Children.Add(Create(alert, MarkdownMindMapNodeKind.Alert, text: alertText, alertKind: alertKind));
                    break;
                case QuoteBlock quote when options.ShowParagraphs:
                    parent.Children.Add(Create(quote, MarkdownMindMapNodeKind.Quote));
                    break;
                case CodeBlock code when options.ShowCode:
                    parent.Children.Add(Create(code, MarkdownMindMapNodeKind.Code, text: code.Lines.ToString()));
                    break;
                case Table table when options.ShowTables:
                    parent.Children.Add(Create(table, MarkdownMindMapNodeKind.Table));
                    break;
                case ParagraphBlock paragraph when options.ShowParagraphs:
                    var paragraphNode = Create(paragraph, MarkdownMindMapNodeKind.Paragraph);
                    if (paragraphNode.FullText.Length > 0) parent.Children.Add(paragraphNode);
                    break;
            }
        }
    }

    public static MarkdownMindMapNode? FindCorrespondingNode(
        MarkdownMindMapNode? oldRoot,
        MarkdownMindMapNode? newRoot,
        MarkdownMindMapNode? oldNode)
    {
        if (oldNode is null || oldRoot is null || newRoot is null) return null;
        return BuildCorrespondence(oldRoot, newRoot).GetValueOrDefault(oldNode);
    }

    public static void CopyCollapsedState(MarkdownMindMapNode? oldRoot, MarkdownMindMapNode? newRoot)
    {
        if (oldRoot is null || newRoot is null) return;
        foreach (var (oldNode, newNode) in BuildCorrespondence(oldRoot, newRoot))
            newNode.IsCollapsed = oldNode.IsCollapsed;
    }

    private static Dictionary<MarkdownMindMapNode, MarkdownMindMapNode> BuildCorrespondence(
        MarkdownMindMapNode oldRoot,
        MarkdownMindMapNode newRoot)
    {
        var oldNodes = Enumerate(oldRoot).ToArray();
        var newNodes = Enumerate(newRoot).ToArray();
        var matches = new Dictionary<MarkdownMindMapNode, MarkdownMindMapNode>();
        var used = new HashSet<MarkdownMindMapNode>();

        foreach (var oldNode in oldNodes)
        {
            var match = newNodes.FirstOrDefault(node => node.Id == oldNode.Id && used.Add(node));
            if (match is not null) matches.Add(oldNode, match);
        }

        foreach (var oldNode in oldNodes.Where(node => !matches.ContainsKey(node)))
        {
            var match = newNodes.Where(node => !used.Contains(node)
                    && node.Kind == oldNode.Kind
                    && node.HeadingLevel == oldNode.HeadingLevel
                    && node.FullText == oldNode.FullText)
                .OrderBy(node => Math.Abs(node.SourceStart - oldNode.SourceStart))
                .FirstOrDefault();
            if (match is not null)
            {
                used.Add(match);
                matches.Add(oldNode, match);
            }
        }

        foreach (var oldNode in oldNodes.Where(node => !matches.ContainsKey(node)))
        {
            var oldPath = PathOf(oldRoot, oldNode);
            var match = newNodes.Where(node => !used.Contains(node)
                    && node.Kind == oldNode.Kind
                    && node.HeadingLevel == oldNode.HeadingLevel
                    && PathOf(newRoot, node).SequenceEqual(oldPath))
                .OrderBy(node => Math.Abs(node.SourceStart - oldNode.SourceStart))
                .FirstOrDefault();
            if (match is not null)
            {
                used.Add(match);
                matches.Add(oldNode, match);
            }
        }

        return matches;

        static IEnumerable<MarkdownMindMapNode> Enumerate(MarkdownMindMapNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            foreach (var descendant in Enumerate(child))
                yield return descendant;
        }

        static int[] PathOf(MarkdownMindMapNode root, MarkdownMindMapNode target)
        {
            var path = new List<int>();
            return Find(root, path) ? path.ToArray() : [];

            bool Find(MarkdownMindMapNode current, List<int> currentPath)
            {
                if (ReferenceEquals(current, target)) return true;
                for (var index = 0; index < current.Children.Count; index++)
                {
                    currentPath.Add(index);
                    if (Find(current.Children[index], currentPath)) return true;
                    currentPath.RemoveAt(currentPath.Count - 1);
                }
                return false;
            }
        }
    }

    /// <summary>按用户可见字素计数，emoji、组合附加符不截断；省略号不占额度。</summary>
    public static string Summarize(string text, int length)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        length = Math.Clamp(length, 1, 120);
        return starts.Length <= length ? text : text[..starts[length]] + "…";
    }
}
