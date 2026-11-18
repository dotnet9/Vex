using System.Text;
using CodeWF.Avalonia.Markdown.Core.Blocks;

using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Footnotes;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>
/// Markdown ↔ <see cref="MarkdownBlockModel"/> 双向转换：解析用一个 Markdig 管道，
/// 回写用 <see cref="MarkdownSmartNewLine"/> 同样的行前缀规则，保证「渲染 → 编辑 → 回写」幂等。
/// </summary>
public static class MarkdownBlockParser
{
    private const string TaskUncheckedPrefix = "- [ ] ";
    private const string TaskCheckedPrefix = "- [x] ";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>解析 Markdown 文本为块列表。</summary>
    public static List<MarkdownBlockModel> Parse(string? markdown)
    {
        var blocks = new List<MarkdownBlockModel>();
        var text = markdown ?? string.Empty;
        if (text.Length == 0)
        {
            blocks.Add(new MarkdownBlockModel
            {
                Kind = MarkdownBlockKind.Paragraph,
                Source = new MarkdownBlockSource(string.Empty, string.Empty, string.Empty, "\n", string.Empty)
            });
            return blocks;
        }

        var document = Markdig.Markdown.Parse(text, Pipeline);
        var sourceCursor = 0;
        // Markdig 把链接定义和脚注整理为合成分组；其 Span 可能从 0 开始，
        // 且脚注组被移动到文档末尾。定义保留在源文 trivia 中，不能当作顺序正文切片。
        var sourceBlocks = document.Where(block => block is not LinkReferenceDefinitionGroup and not FootnoteGroup).ToList();
        foreach (var block in sourceBlocks)
        {
            var firstModel = blocks.Count;
            switch (block)
            {
                case HeadingBlock heading:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Heading,
                        HeadingLevel = Math.Clamp(heading.Level, 1, 6),
                        Text = ExtractInlineText(heading.Inline, text)
                    });
                    break;

                case ParagraphBlock paragraph:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Paragraph,
                        Text = ExtractInlineText(paragraph.Inline, text)
                    });
                    break;

                case ListBlock list:
                    AppendList(blocks, list, text, indentLevel: 0, quoteDepth: 0);
                    break;

                case QuoteBlock quote:
                    AppendQuote(blocks, quote, text);
                    break;

                case MathBlock:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Raw,
                        Raw = text.Substring(block.Span.Start, block.Span.Length)
                    });
                    break;

                case FencedCodeBlock fenced:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Code,
                        IsFenced = true,
                        Language = fenced.Info ?? string.Empty,
                        Text = ExtractCode(fenced)
                    });
                    break;

                case Markdig.Extensions.Yaml.YamlFrontMatterBlock:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Raw,
                        Raw = text.Substring(block.Span.Start, block.Span.Length)
                    });
                    break;

                case CodeBlock code:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Code,
                        IsFenced = false,
                        Text = ExtractCode(code)
                    });
                    break;

                case Table table:
                    blocks.Add(ParseTable(table));
                    break;

                case ThematicBreakBlock:
                    blocks.Add(new MarkdownBlockModel { Kind = MarkdownBlockKind.ThematicBreak });
                    break;

                case HtmlBlock html:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Raw,
                        Raw = ExtractLines(html.Lines)
                    });
                    break;

                default:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Raw,
                        Raw = text.Substring(block.Span.Start, block.Span.Length)
                    });
                    break;
            }

            var end = Math.Min(text.Length, block.Span.End + 1);
            var group = blocks.GetRange(firstModel, blocks.Count - firstModel);
            var source = new MarkdownBlockSource(text[sourceCursor..block.Span.Start], text[block.Span.Start..end],
                WriteCanonical(group).TrimEnd('\n'), text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n",
                block == sourceBlocks.LastOrDefault() ? text[end..] : string.Empty);
            foreach (var model in group)
            {
                model.Source = source;
                model.SourceLine = block.Line + 1;
            }
            sourceCursor = end;
        }

        if (blocks.Count == 0)
        {
            blocks.Add(new MarkdownBlockModel
            {
                Kind = MarkdownBlockKind.Paragraph,
                Source = new MarkdownBlockSource(string.Empty, text, string.Empty,
                    text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n", string.Empty)
            });
        }

        return blocks;
    }

    /// <summary>把块列表回写为 Markdown 文本。</summary>
    public static string Write(IReadOnlyList<MarkdownBlockModel> blocks)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < blocks.Count;)
        {
            var source = blocks[index].Source;
            var first = index++;
            while (source is not null && index < blocks.Count && ReferenceEquals(blocks[index].Source, source))
            {
                index++;
            }
            var group = blocks.Skip(first).Take(index - first).ToList();
            var canonical = WriteCanonical(group).TrimEnd('\n');
            if (source is not null)
            {
                builder.Append(source.LeadingTrivia);
                builder.Append(canonical == source.CanonicalMarkdown ? source.Markdown
                    : canonical.Replace("\n", source.LineEnding, StringComparison.Ordinal));
                if (index == blocks.Count)
                {
                    builder.Append(source.TrailingTrivia);
                }
            }
            else
            {
                if (builder.Length > 0 && builder[^1] != '\n') builder.Append('\n');
                if (builder.Length > 0) builder.Append('\n');
                builder.Append(WriteCanonical(group));
            }
        }
        return builder.ToString();
    }

    internal static string WriteCanonical(IReadOnlyList<MarkdownBlockModel> blocks)
    {
        var builder = new StringBuilder();
        foreach (var block in blocks)
        {
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            AppendBlock(builder, block);
        }

        var text = builder.ToString();
        return text.Length == 0 ? text : text.TrimEnd('\n') + "\n";
    }

    private static void AppendBlock(StringBuilder builder, MarkdownBlockModel block)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
                builder.Append('#', Math.Clamp(block.HeadingLevel, 1, 6)).Append(' ').Append(block.Text).Append('\n');
                break;

            case MarkdownBlockKind.Paragraph:
                builder.Append(block.Text).Append('\n');
                break;

            case MarkdownBlockKind.ListItem:
                builder.Append(' ', block.IndentLevel * 2);
                for (var depth = 0; depth < block.QuoteDepth; depth++)
                {
                    builder.Append("> ");
                }

                if (block.IsTask)
                {
                    if (block.IsOrdered)
                        builder.Append(block.OrderedNumber).Append(block.IsChecked ? ". [x] " : ". [ ] ");
                    else builder.Append(block.IsChecked ? TaskCheckedPrefix : TaskUncheckedPrefix);
                }
                else if (block.IsOrdered || block.OrderedNumber > 0)
                {
                    builder.Append(block.OrderedNumber).Append(". ");
                }
                else
                {
                    builder.Append("- ");
                }

                builder.Append(block.Text).Append('\n');
                break;

            case MarkdownBlockKind.Quote:
                foreach (var line in block.Text.Split('\n'))
                {
                    for (var depth = 0; depth < Math.Max(1, block.QuoteDepth); depth++) builder.Append("> ");
                    builder.Append(line).Append('\n');
                }
                break;

            case MarkdownBlockKind.Code:
                var language = block.IsFenced ? block.Language : string.Empty;
                builder.Append("```").Append(language).Append('\n');
                builder.Append(block.Text);
                builder.Append('\n').Append("```").Append('\n');
                break;

            case MarkdownBlockKind.Table:
                AppendTable(builder, block);
                break;

            case MarkdownBlockKind.ThematicBreak:
                builder.Append("---").Append('\n');
                break;

            default:
                builder.Append(block.Raw).Append('\n');
                break;
        }
    }

    private static void AppendTable(StringBuilder builder, MarkdownBlockModel block)
    {
        var columnCount = block.Cells.Count == 0 ? 0 : block.Cells.Max(row => row.Count);
        if (columnCount == 0)
        {
            return;
        }

        for (var row = 0; row < block.Cells.Count; row++)
        {
            builder.Append('|');
            for (var column = 0; column < columnCount; column++)
            {
                var value = column < block.Cells[row].Count ? block.Cells[row][column] : string.Empty;
                builder.Append(' ').Append(EscapeCell(value)).Append(" |");
            }

            builder.Append('\n');

            if (row == 0 && block.HasHeader)
            {
                builder.Append('|');
                for (var column = 0; column < columnCount; column++)
                {
                    var alignment = column < block.Alignments.Count ? block.Alignments[column] : TableCellAlignment.None;
                    builder.Append(alignment switch
                    {
                        TableCellAlignment.Left => " :--- |",
                        TableCellAlignment.Center => " :---: |",
                        TableCellAlignment.Right => " ---: |",
                        _ => " --- |"
                    });
                }

                builder.Append('\n');
            }
        }
    }

    private static void AppendList(List<MarkdownBlockModel> blocks, ListBlock list, string sourceText, int indentLevel, int quoteDepth)
    {
        var number = list.IsOrdered ? (list.OrderedStart is { } start && int.TryParse(start, out var parsed) ? parsed : 1) : 0;
        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem)
            {
                continue;
            }

            var isTask = false;
            var isChecked = false;
            var first = true;

            foreach (var child in listItem)
            {
                switch (child)
                {
                    case ParagraphBlock paragraph:
                        var inline = paragraph.Inline;
                        var text = ExtractInlineText(inline, sourceText, quoteDepth);
                        if (first && TryGetTaskState(inline, out var taskChecked))
                        {
                            isTask = true;
                            isChecked = taskChecked;
                            // TaskLists extension keeps the space after the marker; trim it for editing.
                            text = text.TrimStart();
                            if (text.Length >= 3 && text[0] == '[' && text[2] == ']') text = text[3..].TrimStart();
                        }

                        blocks.Add(new MarkdownBlockModel
                        {
                            Kind = MarkdownBlockKind.ListItem,
                            Text = text,
                            IndentLevel = indentLevel,
                            QuoteDepth = quoteDepth,
                            IsTask = isTask,
                            IsChecked = isChecked,
                            OrderedNumber = number,
                            IsOrdered = list.IsOrdered
                        });
                        first = false;
                        break;

                    case ListBlock nested:
                        AppendList(blocks, nested, sourceText, indentLevel + 1, quoteDepth);
                        break;

                    case FencedCodeBlock nestedFenced:
                        blocks.Add(new MarkdownBlockModel
                        {
                            Kind = MarkdownBlockKind.Code,
                            IsFenced = true,
                            Language = nestedFenced.Info ?? string.Empty,
                            Text = ExtractCode(nestedFenced),
                            IndentLevel = indentLevel + 1,
                            QuoteDepth = quoteDepth
                        });
                        break;

                    case ContainerBlock container:
                        foreach (var nestedChild in container)
                        {
                            if (nestedChild is LeafBlock leaf)
                            {
                                blocks.Add(new MarkdownBlockModel
                                {
                                    Kind = MarkdownBlockKind.Raw,
                                    Raw = ExtractLines(leaf.Lines),
                                    IndentLevel = indentLevel + 1,
                                    QuoteDepth = quoteDepth
                                });
                            }
                        }

                        break;
                }
            }

            if (first)
            {
                blocks.Add(new MarkdownBlockModel
                {
                    Kind = MarkdownBlockKind.ListItem, IndentLevel = indentLevel, QuoteDepth = quoteDepth,
                    IsOrdered = list.IsOrdered, OrderedNumber = number
                });
            }

            if (list.IsOrdered)
            {
                number++;
            }
        }
    }

    private static void AppendQuote(List<MarkdownBlockModel> blocks, QuoteBlock quote, string sourceText, int depth = 1)
    {
        foreach (var child in quote)
        {
            switch (child)
            {
                case ParagraphBlock paragraph:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Quote,
                        Text = ExtractInlineText(paragraph.Inline, sourceText, depth),
                        QuoteDepth = depth
                    });
                    break;

                case HeadingBlock heading:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Quote,
                        Text = ExtractInlineText(heading.Inline, sourceText, depth),
                        HeadingLevel = heading.Level,
                        QuoteDepth = depth
                    });
                    break;

                case ListBlock list:
                    AppendList(blocks, list, sourceText, indentLevel: 0, quoteDepth: depth);
                    break;

                case QuoteBlock nested:
                    AppendQuote(blocks, nested, sourceText, depth + 1);
                    break;

                case LeafBlock leaf:
                    blocks.Add(new MarkdownBlockModel
                    {
                        Kind = MarkdownBlockKind.Quote,
                        Text = ExtractLines(leaf.Lines)
                    });
                    break;
            }
        }
    }

    private static MarkdownBlockModel ParseTable(Table table)
    {
        var rows = new List<List<string>>();
        var alignments = new List<TableCellAlignment>();
        var hasHeader = false;

        // 该版本 Markdig 不填充 TableCell.ColumnIndex（恒为 -1），因此按行内顺序对照 ColumnDefinitions。
        var columnCount = table.ColumnDefinitions.Count;
        foreach (var row in table)
        {
            if (row is not TableRow tableRow)
            {
                continue;
            }

            if (tableRow.IsHeader)
            {
                hasHeader = true;
            }

            var cells = new List<string>();
            var cellIndex = 0;
            foreach (var cell in tableRow)
            {
                if (cell is not TableCell tableCell)
                {
                    continue;
                }

                var builder = new StringBuilder();
                foreach (var block in tableCell)
                {
                    if (block is ParagraphBlock paragraph)
                    {
                        builder.Append(ExtractInlineText(paragraph.Inline));
                    }
                }

                if (tableRow.IsHeader)
                {
                    alignments.Add(cellIndex < columnCount
                        ? table.ColumnDefinitions[cellIndex].Alignment switch
                        {
                            TableColumnAlign.Left => TableCellAlignment.Left,
                            TableColumnAlign.Center => TableCellAlignment.Center,
                            TableColumnAlign.Right => TableCellAlignment.Right,
                            _ => TableCellAlignment.None
                        }
                        : TableCellAlignment.None);
                }

                cells.Add(builder.ToString());
                cellIndex++;
            }

            rows.Add(cells);
        }

        // Fallback single empty row when the table has no parsed rows.
        if (rows.Count == 0)
        {
            rows.Add([string.Empty]);
        }

        return new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.Table,
            HasHeader = hasHeader,
            Alignments = alignments,
            Cells = rows
        };
    }

    /// <summary>
    /// 识别 Markdig 的任务列表标记（<c>- [x]</c> / <c>- [ ]</c>）：开启 TaskLists 扩展后它是
    /// <see cref="TaskListInline"/>，正文里已经不含 <c>[x]</c>，因此不能再按前缀剥离。
    /// </summary>
    private static bool TryGetTaskState(ContainerInline? inline, out bool isChecked)
    {
        isChecked = false;
        if (inline is null)
        {
            return false;
        }

        foreach (var child in inline)
        {
            if (child is TaskList task)
            {
                isChecked = task.Checked;
                return true;
            }
        }

        return false;
    }

    private static bool TryStripTaskMarker(ContainerInline? inline, out bool isChecked, out string text)
    {
        isChecked = false;
        text = string.Empty;
        if (inline is null)
        {
            return false;
        }

        var full = ExtractInlineText(inline);
        if (full.StartsWith(TaskUncheckedPrefix, StringComparison.Ordinal))
        {
            text = full[TaskUncheckedPrefix.Length..];
            return true;
        }

        if (full.StartsWith(TaskCheckedPrefix, StringComparison.Ordinal))
        {
            isChecked = true;
            text = full[TaskCheckedPrefix.Length..];
            return true;
        }

        return false;
    }

    private static string ExtractInlineText(ContainerInline? inline, string? sourceText = null, int quoteDepth = 0)
    {
        if (inline is null)
        {
            return string.Empty;
        }

        var children = inline.Where(child => child.Span.Length > 0).ToList();
        if (sourceText is not null && children.Count > 0)
        {
            var start = children.Min(child => child.Span.Start);
            var end = children.Max(child => child.Span.End) + 1;
            if (start >= 0 && end <= sourceText.Length)
            {
                var text = sourceText[start..end];
                if (quoteDepth > 0)
                    text = System.Text.RegularExpressions.Regex.Replace(text,
                        $@"(?m)^[ ]{{0,3}}(?:>[ \t]?){{1,{quoteDepth}}}", string.Empty);
                return text;
            }
        }

        var builder = new StringBuilder();
        foreach (var child in inline)
        {
            AppendInline(builder, child);
        }

        return builder.ToString();
    }

    private static void AppendInline(StringBuilder builder, Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;

            case CodeInline code:
                builder.Append('`').Append(code.Content).Append('`');
                break;

            case EmphasisInline emphasis:
                var marker = emphasis.DelimiterCount >= 2 ? "**" : "*";
                builder.Append(marker);
                foreach (var child in emphasis)
                {
                    AppendInline(builder, child);
                }

                builder.Append(marker);
                break;

            case LinkInline link when link.IsImage:
                builder.Append("![")
                    .Append(ExtractInlineText(link))
                    .Append("](")
                    .Append(link.Url ?? string.Empty)
                    .Append(')');
                break;

            case LinkInline link:
                builder.Append('[')
                    .Append(ExtractInlineText(link))
                    .Append("](")
                    .Append(link.Url ?? string.Empty)
                    .Append(')');
                break;

            case LineBreakInline:
                builder.Append('\n');
                break;

            case Markdig.Syntax.Inlines.HtmlInline html:
                // 行内 HTML（<span style> 等）原样回写：编辑块重序列化时标签不丢。
                builder.Append(html.Tag);
                break;

            case ContainerInline container:
                foreach (var child in container)
                {
                    AppendInline(builder, child);
                }

                break;
        }
    }

    private static string ExtractCode(LeafBlock block)
    {
        var lines = block.Lines;
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines.Lines[index];
            builder.Append(lines.Lines[index].Slice.ToString());
            if (index < lines.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string ExtractLines(StringLineGroup lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            builder.Append(lines.Lines[index].Slice.ToString());
            if (index < lines.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static string EscapeCell(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
