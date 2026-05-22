using AvaloniaEdit;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorMutationService : IMarkdownEditorMutationService
{
    private const string IndentText = "    ";

    public void WrapSelection(TextEditor editor, string prefix, string suffix, string placeholder)
    {
        var text = editor.Text ?? string.Empty;
        var start = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(editor.SelectionLength, 0, text.Length - start);
        var selected = length > 0 ? text.Substring(start, length) : placeholder;
        var replacement = $"{prefix}{selected}{suffix}";
        editor.Text = text[..start] + replacement + text[(start + length)..];
        editor.SelectionStart = start + prefix.Length;
        editor.SelectionLength = selected.Length;
        editor.CaretOffset = start + replacement.Length;
    }

    public void InsertText(TextEditor editor, string insertion)
    {
        var text = editor.Text ?? string.Empty;
        var start = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(editor.SelectionLength, 0, text.Length - start);
        editor.Text = text[..start] + insertion + text[(start + length)..];
        editor.CaretOffset = start + insertion.Length;
    }

    public void InsertLink(TextEditor editor, string textPlaceholder, string urlPlaceholder)
    {
        var selection = GetSelection(editor);
        var selected = selection.Selected.Trim();
        var hasSingleLineSelection = selected.Length > 0 && !selected.Contains('\n', StringComparison.Ordinal);

        var label = textPlaceholder;
        var target = urlPlaceholder;
        var selectLabel = true;

        if (hasSingleLineSelection && IsLikelyUrl(selected))
        {
            target = selected;
        }
        else if (hasSingleLineSelection)
        {
            label = selection.Selected;
            selectLabel = false;
        }

        var replacement = $"[{label}]({target})";
        var selectionStart = selectLabel
            ? selection.Start + 1
            : selection.Start + label.Length + 3;
        var selectionLength = selectLabel ? label.Length : target.Length;
        ReplaceSelection(editor, selection.Start, selection.Length, replacement, selectionStart, selectionLength);
    }

    public void InsertImage(TextEditor editor, string altPlaceholder, string targetPlaceholder)
    {
        var selection = GetSelection(editor);
        var selected = selection.Selected.Trim();
        var hasSingleLineSelection = selected.Length > 0 && !selected.Contains('\n', StringComparison.Ordinal);

        var alt = altPlaceholder;
        var target = targetPlaceholder;
        var selectAlt = true;

        if (hasSingleLineSelection && IsLikelyImageTarget(selected))
        {
            target = NormalizeMarkdownTarget(selected);
        }
        else if (hasSingleLineSelection)
        {
            alt = selection.Selected;
            selectAlt = false;
        }

        var replacement = $"![{alt}]({target})";
        var selectionStart = selectAlt
            ? selection.Start + 2
            : selection.Start + alt.Length + 4;
        var selectionLength = selectAlt ? alt.Length : target.Length;
        ReplaceSelection(editor, selection.Start, selection.Length, replacement, selectionStart, selectionLength);
    }

    public void InsertTable(TextEditor editor, string fallbackInsertion)
    {
        var selection = GetSelection(editor);
        if (selection.Length > 0 && TryCreateMarkdownTable(selection.Selected, out var table))
        {
            ReplaceSelection(editor, selection.Start, selection.Length, table, selection.Start + table.Length, 0);
            return;
        }

        InsertText(editor, fallbackInsertion);
    }

    public void IndentSelection(TextEditor editor)
    {
        if (editor.SelectionLength == 0)
        {
            InsertText(editor, IndentText);
            return;
        }

        var text = editor.Text ?? string.Empty;
        var range = GetSelectedLineRange(text, editor.SelectionStart, editor.SelectionLength);
        var selectedLines = text[range.Start..range.End];
        var replacement = IndentText + selectedLines.Replace("\n", "\n" + IndentText, StringComparison.Ordinal);
        editor.Text = text[..range.Start] + replacement + text[range.End..];
        editor.SelectionStart = range.Start;
        editor.SelectionLength = replacement.Length;
        editor.CaretOffset = range.Start + replacement.Length;
    }

    public void OutdentSelection(TextEditor editor)
    {
        var text = editor.Text ?? string.Empty;
        var range = GetSelectedLineRange(text, editor.SelectionStart, editor.SelectionLength);
        var selectedLines = text[range.Start..range.End];
        var replacement = OutdentLines(selectedLines);
        editor.Text = text[..range.Start] + replacement + text[range.End..];
        editor.SelectionStart = range.Start;
        editor.SelectionLength = replacement.Length;
        editor.CaretOffset = range.Start + replacement.Length;
    }

    public void ClearFormatting(TextEditor editor)
    {
        var text = editor.Text ?? string.Empty;
        var start = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(editor.SelectionLength, 0, text.Length - start);

        if (length == 0)
        {
            var offset = Math.Clamp(editor.CaretOffset, 0, text.Length);
            start = text.LastIndexOf('\n', Math.Max(0, offset - 1));
            start = start < 0 ? 0 : start + 1;
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                end = text.Length;
            }

            length = end - start;
        }

        var selected = text.Substring(start, length);
        var cleaned = ClearMarkdownFormatting(selected);
        editor.Text = text[..start] + cleaned + text[(start + length)..];
        editor.SelectionStart = start;
        editor.SelectionLength = cleaned.Length;
        editor.CaretOffset = start + cleaned.Length;
    }

    public void PrefixCurrentLine(TextEditor editor, string prefix)
    {
        var text = editor.Text ?? string.Empty;
        var offset = Math.Clamp(editor.CaretOffset, 0, text.Length);
        var lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', lineStart);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var line = text[lineStart..lineEnd];
        var normalized = RemoveMarkdownLinePrefix(line);
        var replacement = string.IsNullOrEmpty(prefix) ? normalized : prefix + normalized;
        editor.Text = text[..lineStart] + replacement + text[lineEnd..];
        editor.CaretOffset = lineStart + replacement.Length;
    }

    private static (string Text, int Start, int Length, string Selected) GetSelection(TextEditor editor)
    {
        var text = editor.Text ?? string.Empty;
        var start = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(editor.SelectionLength, 0, text.Length - start);
        return (text, start, length, length > 0 ? text.Substring(start, length) : string.Empty);
    }

    private static void ReplaceSelection(
        TextEditor editor,
        int start,
        int length,
        string replacement,
        int selectionStart,
        int selectionLength)
    {
        var text = editor.Text ?? string.Empty;
        editor.Text = text[..start] + replacement + text[(start + length)..];
        editor.CaretOffset = start + replacement.Length;
        editor.SelectionStart = Math.Clamp(selectionStart, 0, editor.Text.Length);
        editor.SelectionLength = Math.Clamp(selectionLength, 0, editor.Text.Length - editor.SelectionStart);
    }

    private static bool TryCreateMarkdownTable(string selectedText, out string table)
    {
        var normalized = selectedText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length < 2)
        {
            table = string.Empty;
            return false;
        }

        var delimiter = SelectTableDelimiter(normalized);
        if (delimiter == '\0')
        {
            table = string.Empty;
            return false;
        }

        var rows = lines
            .Select(line => SplitTableCells(line, delimiter))
            .Where(row => row.Length > 0)
            .ToArray();
        var columnCount = rows.Length == 0 ? 0 : rows.Max(row => row.Length);
        if (columnCount < 2)
        {
            table = string.Empty;
            return false;
        }

        var paddedRows = rows.Select(row => PadCells(row, columnCount)).ToArray();
        var output = new List<string>
        {
            FormatTableRow(paddedRows[0]),
            FormatTableRow(Enumerable.Repeat("---", columnCount).ToArray())
        };
        output.AddRange(paddedRows.Skip(1).Select(FormatTableRow));
        table = string.Join('\n', output);
        return true;
    }

    private static char SelectTableDelimiter(string text)
    {
        if (text.Contains('\t', StringComparison.Ordinal))
        {
            return '\t';
        }

        if (text.Contains('|', StringComparison.Ordinal))
        {
            return '|';
        }

        return text.Contains(',', StringComparison.Ordinal) ? ',' : '\0';
    }

    private static string[] SplitTableCells(string line, char delimiter)
    {
        var cells = line.Split(delimiter, StringSplitOptions.TrimEntries);
        if (delimiter == '|')
        {
            cells = cells
                .Skip(cells.Length > 0 && cells[0].Length == 0 ? 1 : 0)
                .ToArray();

            if (cells.Length > 0 && cells[^1].Length == 0)
            {
                cells = cells[..^1];
            }
        }

        return cells;
    }

    private static string[] PadCells(string[] cells, int columnCount)
    {
        if (cells.Length == columnCount)
        {
            return cells;
        }

        var padded = new string[columnCount];
        Array.Copy(cells, padded, cells.Length);
        for (var i = cells.Length; i < columnCount; i++)
        {
            padded[i] = string.Empty;
        }

        return padded;
    }

    private static string FormatTableRow(string[] cells)
    {
        return "| " + string.Join(" | ", cells.Select(EscapeTableCell)) + " |";
    }

    private static string EscapeTableCell(string cell)
    {
        return cell.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static bool IsLikelyUrl(string value)
    {
        return value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyImageTarget(string value)
    {
        if (IsLikelyUrl(value))
        {
            return true;
        }

        var extension = Path.GetExtension(value);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMarkdownTarget(string target)
    {
        return target.Replace('\\', '/');
    }

    private static (int Start, int End) GetSelectedLineRange(string text, int selectionStart, int selectionLength)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(selectionStart + selectionLength, start, text.Length);

        var lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;

        if (selectionLength == 0)
        {
            var lineEnd = text.IndexOf('\n', start);
            return (lineStart, lineEnd < 0 ? text.Length : lineEnd);
        }

        if (end > start && end <= text.Length && text[end - 1] == '\n')
        {
            end--;
        }

        var nextLine = text.IndexOf('\n', end);
        return (lineStart, nextLine < 0 ? text.Length : nextLine);
    }

    private static string OutdentLines(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = OutdentLine(lines[i]);
        }

        return string.Join('\n', lines);
    }

    private static string OutdentLine(string line)
    {
        if (line.StartsWith('\t'))
        {
            return line[1..];
        }

        var spaces = 0;
        while (spaces < Math.Min(IndentText.Length, line.Length) && line[spaces] == ' ')
        {
            spaces++;
        }

        return line[spaces..];
    }

    private static string RemoveMarkdownLinePrefix(string line)
    {
        var trimmed = line.TrimStart();
        var leading = line.Length - trimmed.Length;

        string[] prefixes =
        [
            "###### ",
            "##### ",
            "#### ",
            "### ",
            "## ",
            "# ",
            "> ",
            "- [ ] ",
            "- ",
            "1. "
        ];

        foreach (var prefix in prefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[..leading] + trimmed[prefix.Length..];
            }
        }

        return line;
    }

    private static string ClearMarkdownFormatting(string markdown)
    {
        // 这里只处理常见 Markdown 标记，复杂语法后续应放到可测试的 Markdown AST 服务里扩展。
        var lines = markdown
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = RemoveMarkdownLinePrefix(lines[i])
                .Replace("![", "[", StringComparison.Ordinal)
                .Replace("](", " (", StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal);
        }

        return string.Join('\n', lines);
    }
}
