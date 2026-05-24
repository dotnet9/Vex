using Vex.Core.Models;

namespace Vex.Modules.Workspace.Services;

internal static class MarkdownHeadingScanner
{
    private delegate bool HeadingVisitor(int level, string title, int lineNumber);

    public static IReadOnlyList<OutlineItem> BuildOutline(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var result = new List<OutlineItem>();
        ScanHeadings(markdown, (level, title, lineNumber) =>
        {
            result.Add(new OutlineItem(level, title, lineNumber));
            return true;
        });

        return result;
    }

    public static string? FindFirstHeading(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        string? heading = null;
        ScanHeadings(markdown, (_, title, _) =>
        {
            heading = title;
            return false;
        });

        return heading;
    }

    private static void ScanHeadings(string markdown, HeadingVisitor visitor)
    {
        var inFence = false;
        var fenceMarker = '\0';
        var fenceLength = 0;
        var text = markdown.AsSpan();
        var lineNumber = 0;
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var lineEnd = FindLineEnd(text, lineStart);
            var line = text[lineStart..lineEnd];
            lineNumber++;

            if (TryParseFence(line, out var marker, out var markerLength, out var canCloseFence))
            {
                if (!inFence)
                {
                    inFence = true;
                    fenceMarker = marker;
                    fenceLength = markerLength;
                }
                else if (marker == fenceMarker && markerLength >= fenceLength && canCloseFence)
                {
                    inFence = false;
                }

                lineStart = MoveToNextLine(text, lineEnd);
                continue;
            }

            if (!inFence
                && TryParseHeading(line, out var level, out var title)
                && !visitor(level, title, lineNumber))
            {
                return;
            }

            lineStart = MoveToNextLine(text, lineEnd);
        }
    }

    private static int FindLineEnd(ReadOnlySpan<char> text, int start)
    {
        var index = start;
        while (index < text.Length && text[index] is not '\r' and not '\n')
        {
            index++;
        }

        return index;
    }

    private static int MoveToNextLine(ReadOnlySpan<char> text, int lineEnd)
    {
        if (lineEnd >= text.Length)
        {
            return text.Length;
        }

        return text[lineEnd] == '\r' && lineEnd + 1 < text.Length && text[lineEnd + 1] == '\n'
            ? lineEnd + 2
            : lineEnd + 1;
    }

    private static bool TryParseFence(ReadOnlySpan<char> line, out char marker, out int markerLength, out bool canCloseFence)
    {
        marker = '\0';
        markerLength = 0;
        canCloseFence = false;

        var start = CountMarkdownIndent(line);
        if (start < 0 || start >= line.Length)
        {
            return false;
        }

        marker = line[start];
        if (marker is not ('`' or '~'))
        {
            return false;
        }

        var index = start;
        while (index < line.Length && line[index] == marker)
        {
            index++;
        }

        markerLength = index - start;
        canCloseFence = line[index..].Trim().Length == 0;
        return markerLength >= 3;
    }

    private static bool TryParseHeading(ReadOnlySpan<char> line, out int level, out string title)
    {
        level = 0;
        title = string.Empty;

        var markerStart = CountMarkdownIndent(line);
        if (markerStart < 0 || markerStart >= line.Length || line[markerStart] != '#')
        {
            return false;
        }

        var index = markerStart;
        while (index < line.Length && level < 6 && line[index] == '#')
        {
            level++;
            index++;
        }

        if (level == 0 || index < line.Length && !char.IsWhiteSpace(line[index]))
        {
            return false;
        }

        var titleSpan = index >= line.Length
            ? ReadOnlySpan<char>.Empty
            : TrimClosingHeadingMarkers(line[index..].Trim());
        if (titleSpan.Length == 0)
        {
            return false;
        }

        title = titleSpan.ToString();
        return true;
    }

    private static int CountMarkdownIndent(ReadOnlySpan<char> line)
    {
        var index = 0;
        while (index < line.Length && line[index] == ' ')
        {
            index++;
            if (index > 3)
            {
                return -1;
            }
        }

        return index;
    }

    private static ReadOnlySpan<char> TrimClosingHeadingMarkers(ReadOnlySpan<char> title)
    {
        var trimmed = title.TrimEnd();
        var markerStart = trimmed.Length;
        while (markerStart > 0 && trimmed[markerStart - 1] == '#')
        {
            markerStart--;
        }

        if (markerStart == trimmed.Length
            || markerStart <= 0
            || !char.IsWhiteSpace(trimmed[markerStart - 1]))
        {
            return trimmed;
        }

        return trimmed[..markerStart].TrimEnd();
    }
}
