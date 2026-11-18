namespace CodeWF.Avalonia.Markdown.Core.Blocks;

/// <summary>所见即所得视图中的一个块（对应 Markdown 里的一个块级元素）。</summary>
public sealed class MarkdownBlockModel
{
    /// <summary>初始源码行号；定位时以当前文本重新解析。</summary>
    public int SourceLine { get; set; } = 1;

    public MarkdownBlockKind Kind { get; set; }

    /// <summary>块正文（不含块前缀）。表格块为空，内容在 <see cref="Cells"/>。</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>标题级别（1..6）；非标题为 0。</summary>
    public int HeadingLevel { get; set; }

    /// <summary>列表项序号（有序列表）；无序列表为 0。</summary>
    public int OrderedNumber { get; set; }

    /// <summary>有序列表标识，支持合法的 0 起始编号。</summary>
    public bool IsOrdered { get; set; }

    /// <summary>列表缩进（每级 2 空格）。</summary>
    public int IndentLevel { get; set; }

    /// <summary>是否为任务列表项。</summary>
    public bool IsTask { get; set; }

    /// <summary>任务是否勾选。</summary>
    public bool IsChecked { get; set; }

    /// <summary>引用块深度。</summary>
    public int QuoteDepth { get; set; }

    /// <summary>代码块语言标记。</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>代码块是否使用围栏（```）。</summary>
    public bool IsFenced { get; set; } = true;

    /// <summary>表格首行为表头。</summary>
    public bool HasHeader { get; set; }

    /// <summary>表格对齐（每个列一个值）。</summary>
    public IReadOnlyList<TableCellAlignment> Alignments { get; set; } = [];

    /// <summary>表格单元格文本（行优先）。</summary>
    public List<List<string>> Cells { get; set; } = [];

    /// <summary>无法结构化解析的原始 Markdown（HTML 块、数学块等）。</summary>
    public string Raw { get; set; } = string.Empty;

    /// <summary>同一顶层 AST 块共用源码快照；只有被编辑的组才需要序列化。</summary>
    public MarkdownBlockSource? Source { get; set; }
}

/// <summary>块前导 trivia 与规范源码快照（组序列化用）。</summary>
public sealed record MarkdownBlockSource(string LeadingTrivia, string Markdown, string CanonicalMarkdown,
    string LineEnding, string TrailingTrivia);

/// <summary>块类型。</summary>
public enum MarkdownBlockKind
{
    Paragraph,
    Heading,
    ListItem,
    Quote,
    Code,
    Table,
    ThematicBreak,
    Raw
}

/// <summary>表格列对齐。</summary>
public enum TableCellAlignment
{
    None,
    Left,
    Center,
    Right
}
