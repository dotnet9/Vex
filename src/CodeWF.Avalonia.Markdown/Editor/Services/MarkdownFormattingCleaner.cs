using System.Text;
using System.Text.RegularExpressions;

using Markdig;
using Markdig.Helpers;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CodeWF.Avalonia.Markdown.Editor.Services;

public static class MarkdownFormattingCleaner
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseMathematics()
        .Build();

    private static readonly Regex HtmlTagPattern = new(
        @"<!--[\s\S]*?-->|<[^>]*>",
        RegexOptions.Compiled);

    public static string Clear(string? markdown)
    {
        var source = markdown ?? string.Empty;
        if (source.Length == 0) return string.Empty;
        var document = Markdig.Markdown.Parse(source, Pipeline);
        var builder = new StringBuilder();
        AppendBlocks(builder, document);
        var result = builder.ToString();
        var sourceTrailingNewLines = 0;
        for (var index = source.Length - 1; index >= 0 && source[index] == '\n'; index--) sourceTrailingNewLines++;
        var resultTrailingNewLines = 0;
        for (var index = result.Length - 1; index >= 0 && result[index] == '\n'; index--) resultTrailingNewLines++;
        if (resultTrailingNewLines > sourceTrailingNewLines)
        {
            result = result[..^(resultTrailingNewLines - sourceTrailingNewLines)];
        }
        else if (resultTrailingNewLines < sourceTrailingNewLines)
        {
            result += new string('\n', sourceTrailingNewLines - resultTrailingNewLines);
        }

        return result;
    }

    private static void AppendBlocks(StringBuilder builder, IEnumerable<Block> blocks)
    {
        foreach (var block in blocks)
        {
            AppendBlock(builder, block);
        }
    }

    private static void AppendBlock(StringBuilder builder, Block block)
    {
        switch (block)
        {
            case LinkReferenceDefinitionGroup:
                return;

            case ListBlock list:
                foreach (var item in list)
                {
                    if (item is ContainerBlock container) AppendBlocks(builder, container);
                }
                return;

            case QuoteBlock quote:
                AppendBlocks(builder, quote);
                return;

            case Table table:
                foreach (var row in table)
                {
                    if (row is not TableRow tableRow) continue;
                    var cells = new List<string>();
                    foreach (var cell in tableRow)
                    {
                        if (cell is not TableCell tableCell) continue;
                        var cellBuilder = new StringBuilder();
                        AppendBlocks(cellBuilder, tableCell);
                        cells.Add(cellBuilder.ToString().TrimEnd('\n'));
                    }
                    AppendLine(builder, string.Join(" | ", cells));
                }
                return;

            case FencedCodeBlock or CodeBlock:
                AppendLine(builder, ExtractLines(((LeafBlock)block).Lines));
                return;

            case ThematicBreakBlock:
                return;

            case HtmlBlock html:
                AppendLine(builder, System.Net.WebUtility.HtmlDecode(HtmlTagPattern.Replace(ExtractLines(html.Lines), string.Empty)));
                return;

            case LeafBlock leaf:
                var inlineBuilder = new StringBuilder();
                AppendInlines(inlineBuilder, leaf.Inline);
                AppendLine(builder, inlineBuilder.ToString());
                return;

            default:
                if (block is ContainerBlock containerBlock) AppendBlocks(builder, containerBlock);
                return;
        }
    }

    private static void AppendLine(StringBuilder builder, string text)
    {
        if (builder.Length > 0 && builder[^1] != '\n') builder.Append('\n');
        builder.Append(text);
        if (text.Length == 0 || text[^1] != '\n') builder.Append('\n');
    }

    private static void AppendInlines(StringBuilder builder, ContainerInline? container)
    {
        if (container is null) return;
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    builder.Append(entity.Transcoded.ToString());
                    break;
                case LineBreakInline:
                    builder.Append('\n');
                    break;
                case HtmlInline html:
                    var tag = html.Tag ?? string.Empty;
                    if (tag.Equals("<br>", StringComparison.OrdinalIgnoreCase) || tag.Equals("<br/>", StringComparison.OrdinalIgnoreCase)
                        || tag.Equals("<br />", StringComparison.OrdinalIgnoreCase))
                    {
                        builder.Append('\n');
                    }
                    break;
                case ContainerInline nested:
                    AppendInlines(builder, nested);
                    break;
            }
        }
    }

    private static string ExtractLines(StringLineGroup lines)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            builder.Append(lines.Lines[index].Slice.ToString());
            if (index < lines.Count - 1) builder.Append('\n');
        }
        return builder.ToString();
    }
}
