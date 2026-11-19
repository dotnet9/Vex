using System.Text.RegularExpressions;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;
using global::Avalonia.Controls;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown;

/// <summary>将 Markdown 中常见的 HTML 表格接入原生表格排版，源文仍保持 HTML。</summary>
public static class CodeWFMarkdownHtmlExtensions
{
    private static readonly object RegistrationLock = new();
    private static bool _registered;

    /// <summary>在创建 Viewer 前启用；不执行 HTML 脚本、样式或浏览器内容。</summary>
    public static void UseHtmlTables()
    {
        lock (RegistrationLock)
        {
            if (_registered) return;
            MarkdownViewer.RegisterBlockRenderer(new HtmlTableBlockRenderer());
            _registered = true;
        }
    }
}

internal sealed class HtmlTableBlockRenderer : IMarkdownBlockRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not HtmlBlock htmlBlock) return null;
        var html = htmlBlock.Lines.ToString().Trim();
        if (!Regex.IsMatch(html, @"^<table(?:\s[^>]*)?>", RegexOptions.IgnoreCase)
            || !html.EndsWith("</table>", StringComparison.OrdinalIgnoreCase)) return null;

        // 当前原生表格没有合并单元格语义；保留原文回落，避免悄悄改变数据结构。
        if (Regex.IsMatch(html, @"\b(?:rowspan|colspan)\s*=", RegexOptions.IgnoreCase)
            || Regex.Matches(html, @"<table(?:\s|>)", RegexOptions.IgnoreCase).Count != 1) return null;

        var markdown = MarkdownHtmlConverter.Html2Markdown(html);
        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        if (document.Count != 1 || document[0] is not Table table) return null;

        // 管道表格必须转义单元格内的 |，而行内代码解析会保留这层转义。
        foreach (var code in table.Descendants<CodeInline>())
            code.Content = code.Content.Replace("\\|", "|", StringComparison.Ordinal);

        // HTML 不要求第一行一定是表头；转换器生成的管道分隔行只用于解析。
        var firstRow = Regex.Match(html, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (table.FirstOrDefault() is TableRow header)
            header.IsHeader = Regex.IsMatch(firstRow.Value, @"<th(?:\s|>)", RegexOptions.IgnoreCase);
        return new TableRenderer().TryRender(table, markdown, context);
    }
}
