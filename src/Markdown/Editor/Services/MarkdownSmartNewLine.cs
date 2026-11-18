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

        if (TryCreateEmptyContinuationReplacement(lineBeforeCaret, out var lineReplacement))
        {
            return new SmartNewLineChange(lineStart, start + length - lineStart, lineReplacement + "\n");
        }

        return new SmartNewLineChange(start, length, "\n" + CreateContinuationPrefix(lineBeforeCaret));
    }

    private static bool TryCreateEmptyContinuationReplacement(string lineBeforeCaret, out string replacement)
    {
        replacement = string.Empty;
        var leading = GetLeadingWhitespace(lineBeforeCaret);
        var content = lineBeforeCaret[leading.Length..];

        if ((TryParseTaskListPrefix(content, out _, out var remaining)
             || TryParseUnorderedListPrefix(content, out _, out remaining)
             || TryParseOrderedListPrefix(content, out _, out remaining)
             || TryParseQuotePrefix(content, out _, out remaining))
            && string.IsNullOrWhiteSpace(remaining))
        {
            replacement = leading;
            return true;
        }

        return false;
    }

    private static string CreateContinuationPrefix(string lineBeforeCaret)
    {
        var leading = GetLeadingWhitespace(lineBeforeCaret);
        var content = lineBeforeCaret[leading.Length..];

        if (TryParseTaskListPrefix(content, out var taskPrefix, out _))
        {
            return leading + taskPrefix;
        }

        if (TryParseUnorderedListPrefix(content, out var listPrefix, out _))
        {
            return leading + listPrefix;
        }

        if (TryParseOrderedListPrefix(content, out var orderedPrefix, out _))
        {
            return leading + orderedPrefix;
        }

        if (TryParseQuotePrefix(content, out var quotePrefix, out _))
        {
            return leading + quotePrefix;
        }

        return leading;
    }

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
        if (content.Length < 6
            || !IsUnorderedListMarker(content[0])
            || content[1] != ' '
            || content[2] != '['
            || content[4] != ']'
            || content[5] != ' ')
        {
            return false;
        }

        var state = content[3];
        if (state is not (' ' or 'x' or 'X'))
        {
            return false;
        }

        prefix = $"{content[0]} [ ] ";
        remaining = content[6..];
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

    private static bool TryParseQuotePrefix(string content, out string prefix, out string remaining)
    {
        prefix = string.Empty;
        remaining = string.Empty;
        if (!content.StartsWith('>'))
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

    private static bool IsUnorderedListMarker(char value)
    {
        return value is '-' or '*' or '+';
    }
}

/// <summary>一次智能换行产生的文本替换（<see cref="Start"/> 与 <see cref="Length"/> 为被替换区间）。</summary>
public readonly record struct SmartNewLineChange(int Start, int Length, string Text);
