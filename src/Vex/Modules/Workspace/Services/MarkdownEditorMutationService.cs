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
