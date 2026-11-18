using System.Text;
using System.Text.RegularExpressions;

using CodeWF.Avalonia.Markdown.Shared.Rendering;

using Markdig;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>一段带样式的文本（富文本渲染的最小单元）。</summary>
public readonly record struct MarkdownTextRun(
    string Text,
    bool Bold = false,
    bool Italic = false,
    bool Strike = false,
    bool Code = false,
    string? LinkUrl = null,
    int SourceStart = 0,
    string? Foreground = null,
    string? Background = null,
    double? FontSize = null);

/// <summary>把行内 Markdown 文本切成带样式的 <see cref="MarkdownTextRun"/> 列表。</summary>
public static class MarkdownTextRunParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>解析行内 Markdown（支持 **粗**、*斜*、`码`、~~删~~、[链接](url)、&lt;span style&gt; 行内样式）。</summary>
    public static List<MarkdownTextRun> Parse(string? inlineMarkdown, MarkdownSyntaxOptions? syntaxOptions = null)
    {
        var runs = new List<MarkdownTextRun>();
        var text = inlineMarkdown ?? string.Empty;
        if (text.Length == 0)
        {
            return runs;
        }

        var container = Markdig.Markdown.Parse(text, syntaxOptions?.CreatePipeline() ?? Pipeline);
        var added = false;
        var spanStack = new Stack<MarkdownSpanStyle>();
        foreach (var block in container)
        {
            if (block is Markdig.Syntax.LeafBlock leaf)
            {
                added |= AppendInlines(runs, leaf.Inline, bold: false, italic: false, strike: false, linkUrl: null, style: null, spanStack);
            }
        }

        if (!added)
        {
            runs.Add(new MarkdownTextRun(text));
        }

        return runs;
    }

    /// <summary>纯文本（不做 Markdown 解析），用于代码块/原始块。</summary>
    public static List<MarkdownTextRun> Plain(string? text, bool code = false)
    {
        var value = text ?? string.Empty;
        return value.Length == 0 ? [] : [new MarkdownTextRun(value, Code: code)];
    }

    /// <summary>产出用于拼接回 Markdown 的纯文本（去掉样式标记）。</summary>
    public static string ToPlainText(IEnumerable<MarkdownTextRun> runs)
    {
        var builder = new StringBuilder();
        foreach (var run in runs)
        {
            builder.Append(run.Text);
        }

        return builder.ToString();
    }

    /// <summary>把可见文字偏移映射回行内 Markdown，跳过隐藏的格式标记。</summary>
    public static int GetSourceOffset(string markdown, int textOffset)
    {
        var remaining = Math.Max(0, textOffset);
        foreach (var run in Parse(markdown))
        {
            if (remaining <= run.Text.Length) return Math.Clamp(run.SourceStart + remaining, 0, markdown.Length);
            remaining -= run.Text.Length;
        }
        return markdown.Length;
    }

    /// <summary>源码选区映射到预览文字，格式标记本身不占可见宽度。</summary>
    public static int GetTextOffset(string markdown, int sourceOffset)
    {
        var visible = 0;
        foreach (var run in Parse(markdown))
        {
            if (sourceOffset <= run.SourceStart + run.Text.Length)
                return visible + Math.Clamp(sourceOffset - run.SourceStart, 0, run.Text.Length);
            visible += run.Text.Length;
        }
        return visible;
    }

    private static bool AppendInlines(
        List<MarkdownTextRun> runs,
        Markdig.Syntax.Inlines.ContainerInline? container,
        bool bold,
        bool italic,
        bool strike,
        string? linkUrl = null,
        MarkdownSpanStyle? style = null,
        Stack<MarkdownSpanStyle>? spanStack = null)
    {
        if (container is null)
        {
            return false;
        }

        var added = false;
        foreach (var inline in container)
        {
            added |= Append(runs, inline, bold, italic, strike, linkUrl, style, spanStack);
        }

        return added;
    }

    private static readonly Regex SpanOpenPattern = new(
        @"^<span\b[^>]*\bstyle\s*=\s*""([^""]*)""[^>]*>$|^<span\b[^>]*\bstyle\s*=\s*'([^']*)'[^>]*>$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool Append(
        List<MarkdownTextRun> runs,
        Markdig.Syntax.Inlines.Inline inline,
        bool bold,
        bool italic,
        bool strike,
        string? linkUrl,
        MarkdownSpanStyle? style,
        Stack<MarkdownSpanStyle>? spanStack)
    {
        // <span style> 是兄弟序列：文字节点的有效样式 = 参数样式叠加当前栈顶（开标签压栈后覆盖后续兄弟）。
        var effective = spanStack is { Count: > 0 }
            ? (style ?? new MarkdownSpanStyle()).Over(spanStack.Peek())
            : style;
        switch (inline)
        {
            case Markdig.Syntax.Inlines.LiteralInline literal:
                runs.Add(new MarkdownTextRun(literal.Content.ToString(), bold, italic, strike, LinkUrl: linkUrl, SourceStart: literal.Span.Start + (literal.IsFirstCharacterEscaped ? 1 : 0),
                    Foreground: effective?.Foreground, Background: effective?.Background, FontSize: effective?.FontSize));
                return true;

            case Markdig.Syntax.Inlines.CodeInline code:
                var padding = code.Span.Length - code.DelimiterCount * 2 == code.Content.Length + 2 ? 1 : 0;
                runs.Add(new MarkdownTextRun(code.Content, bold, italic, strike, Code: true, LinkUrl: linkUrl, SourceStart: code.Span.Start + code.DelimiterCount + padding,
                    Foreground: effective?.Foreground, Background: effective?.Background, FontSize: effective?.FontSize));
                return true;

            case Markdig.Syntax.Inlines.HtmlEntityInline entity:
                runs.Add(new MarkdownTextRun(entity.Transcoded.ToString(), bold, italic, strike, LinkUrl: linkUrl, SourceStart: entity.Span.Start,
                    Foreground: effective?.Foreground, Background: effective?.Background, FontSize: effective?.FontSize));
                return true;

            case Markdig.Syntax.Inlines.HtmlInline html:
                // <span style> 开/闭标签以兄弟节点形式出现：开标签压栈，后续兄弟取栈顶样式，闭标签弹栈。
                if (spanStack is null)
                {
                    return false;
                }
                var tag = html.Tag ?? string.Empty;
                if (tag.Equals("</span>", StringComparison.OrdinalIgnoreCase) || tag.Equals("</span >", StringComparison.OrdinalIgnoreCase))
                {
                    if (spanStack.Count > 0) spanStack.Pop();
                    return true;
                }
                var open = SpanOpenPattern.Match(tag);
                if (open.Success)
                {
                    // 叠加基是栈顶（嵌套时继承外层 span），而不是进入时的参数样式。
                    var outer = spanStack.Count > 0 ? spanStack.Peek() : style ?? new MarkdownSpanStyle();
                    var parsed = MarkdownSpanStyle.FromDeclarations(open.Groups[1].Success ? open.Groups[1].Value : open.Groups[2].Value);
                    spanStack.Push(parsed is null ? outer : outer.Over(parsed));
                    return true;
                }
                if (tag.Equals("<br>", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(tag, @"^<br\s*/?>$", RegexOptions.IgnoreCase))
                {
                    runs.Add(new MarkdownTextRun("\n", SourceStart: html.Span.Start, Foreground: style?.Foreground, Background: style?.Background, FontSize: style?.FontSize));
                    return true;
                }
                return false;

            case Markdig.Syntax.Inlines.EmphasisInline emphasis:
                var isStrike = emphasis.DelimiterChar == '~';
                var isBold = !isStrike && emphasis.DelimiterCount >= 2;
                var isItalic = !isStrike && !isBold;
                return AppendInlines(
                    runs,
                    emphasis,
                    bold || isBold,
                    italic || isItalic,
                    strike || isStrike,
                    linkUrl,
                    style,
                    spanStack);

            case Markdig.Syntax.Inlines.LinkInline link:
                return AppendInlines(runs, link, bold, italic, strike, link.IsImage ? null : link.Url, style, spanStack);

            case Markdig.Syntax.Inlines.LineBreakInline lineBreak:
                // 预览保留软换行，输入层必须使用相同换行规则，避免点入时整段重排。
                runs.Add(new MarkdownTextRun("\n", SourceStart: lineBreak.Span.Start));
                return true;

            case Markdig.Syntax.Inlines.ContainerInline nested:
                return AppendInlines(runs, nested, bold, italic, strike, linkUrl, style, spanStack);

            default:
                return false;
        }
    }

    private static string CollectText(Markdig.Syntax.Inlines.ContainerInline container)
    {
        var builder = new StringBuilder();
        foreach (var child in container)
        {
            switch (child)
            {
                case Markdig.Syntax.Inlines.LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case Markdig.Syntax.Inlines.CodeInline code:
                    builder.Append(code.Content);
                    break;
                case Markdig.Syntax.Inlines.ContainerInline nested:
                    builder.Append(CollectText(nested));
                    break;
            }
        }

        return builder.ToString();
    }
}
