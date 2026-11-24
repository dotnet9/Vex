namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>智能换行（Enter 续行）：按当前行前缀续写列表 / 任务项 / 引用。</summary>
public static class MarkdownSmartNewLine
{
    public static SmartNewLineChange CreateChange(string text, int selectionStart, int selectionLength)
    {
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);
        var lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineBeforeCaret = text[lineStart..start];

        if (TryParseLinePrefix(lineBeforeCaret, out var prefix)
            && prefix.HasPrefix
            && string.IsNullOrWhiteSpace(prefix.Remaining))
        {
            return new SmartNewLineChange(lineStart, start + length - lineStart, prefix.EmptyReplacement + "\n");
        }

        return new SmartNewLineChange(start, length, "\n" + prefix.Continuation);
    }

    private static bool TryParseLinePrefix(string line, out LinePrefix prefix)
    {
        prefix = default;
        var leading = GetLeadingWhitespace(line);
        var content = line[leading.Length..];
        var quotes = new List<string>();
        while (content.Length > 0 && content[0] == '>')
        {
            var end = 1;
            while (end < content.Length && content[end] is ' ' or '\t') end++;
            quotes.Add(content[..end]);
            content = content[end..];
        }

        var quotePrefix = string.Concat(quotes);
        var listPrefix = string.Empty;
        var hasList = TryParseTaskListPrefix(content, out listPrefix, out var remaining)
            || TryParseUnorderedListPrefix(content, out listPrefix, out remaining)
            || TryParseOrderedListPrefix(content, out listPrefix, out remaining);
        if (!hasList) remaining = content;

        var emptyReplacement = leading;
        if (hasList)
        {
            emptyReplacement += quotePrefix.TrimEnd();
        }
        else if (quotes.Count > 0)
        {
            emptyReplacement += string.Concat(quotes.Take(quotes.Count - 1)).TrimEnd();
        }

        prefix = new LinePrefix(
            leading + quotePrefix + listPrefix,
            remaining,
            emptyReplacement,
            hasList || quotes.Count > 0);
        return true;
    }

    private readonly record struct LinePrefix(
        string Continuation,
        string Remaining,
        string EmptyReplacement,
        bool HasPrefix);

    private static string GetLeadingWhitespace(string line)
    {
        var index = 0;
        while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
        {
            index++;
        }

        return line[..index];
    }

    private static bool TryParseTaskListPrefix(string content, out string prefix, out string remaining)
    {
        prefix = string.Empty;
        remaining = string.Empty;
        if (content.Length < 5
            || !IsUnorderedListMarker(content[0])
            || content[1] is not (' ' or '\t'))
        {
            return false;
        }

        var markerEnd = 1;
        while (markerEnd < content.Length && content[markerEnd] is ' ' or '\t') markerEnd++;
        if (markerEnd + 3 >= content.Length
            || content[markerEnd] != '['
            || content[markerEnd + 2] != ']'
            || content[markerEnd + 3] is not (' ' or '\t'))
        {
            return false;
        }

        var state = content[markerEnd + 1];
        if (state is not (' ' or 'x' or 'X'))
        {
            return false;
        }

        var contentStart = markerEnd + 3;
        while (contentStart < content.Length && content[contentStart] is ' ' or '\t') contentStart++;
        prefix = $"{content[0]} [ ] ";
        remaining = content[contentStart..];
        return true;
    }

    private static bool TryParseUnorderedListPrefix(string content, out string prefix, out string remaining)
    {
        prefix = string.Empty;
        remaining = string.Empty;
        if (content.Length < 2 || !IsUnorderedListMarker(content[0]) || !char.IsWhiteSpace(content[1]))
        {
            return false;
        }

        var index = 1;
        while (index < content.Length && (content[index] == ' ' || content[index] == '\t'))
        {
            index++;
        }

        prefix = content[..index];
        remaining = content[index..];
        return true;
    }

    private static bool TryParseOrderedListPrefix(string content, out string prefix, out string remaining)
    {
        prefix = string.Empty;
        remaining = string.Empty;
        var digitEnd = 0;
        while (digitEnd < content.Length && char.IsDigit(content[digitEnd]))
        {
            digitEnd++;
        }

        if (digitEnd == 0
            || digitEnd >= content.Length
            || content[digitEnd] is not ('.' or ')')
            || digitEnd + 1 >= content.Length
            || !char.IsWhiteSpace(content[digitEnd + 1]))
        {
            return false;
        }

        var spacingEnd = digitEnd + 1;
        while (spacingEnd < content.Length && (content[spacingEnd] == ' ' || content[spacingEnd] == '\t'))
        {
            spacingEnd++;
        }

        if (!int.TryParse(content[..digitEnd], out var number))
        {
            return false;
        }

        prefix = $"{number + 1}{content[digitEnd]}{content[(digitEnd + 1)..spacingEnd]}";
        remaining = content[spacingEnd..];
        return true;
    }

    private static bool IsUnorderedListMarker(char value)
    {
        return value is '-' or '*' or '+';
    }
}

/// <summary>一次智能换行产生的文本替换（<see cref="Start"/> 与 <see cref="Length"/> 为被替换区间）。</summary>
public readonly record struct SmartNewLineChange(int Start, int Length, string Text);
