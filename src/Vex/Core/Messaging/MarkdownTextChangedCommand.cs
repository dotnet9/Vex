using CodeWF.EventBus;

namespace Vex.Core.Messaging;

public sealed class MarkdownTextChangedCommand : Command
{
    public MarkdownTextChangedCommand(string markdown, int caretLine, int caretColumn)
        : this(markdown, caretLine, caretColumn, CountLines(markdown))
    {
    }

    public MarkdownTextChangedCommand(string markdown, int caretLine, int caretColumn, int lineCount)
    {
        Markdown = markdown;
        CaretLine = caretLine;
        CaretColumn = caretColumn;
        LineCount = Math.Max(1, lineCount);
    }

    public string Markdown { get; }

    public int CaretLine { get; }

    public int CaretColumn { get; }

    public int LineCount { get; }

    private static int CountLines(string? markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return 1;
        }

        var lines = 1;
        for (var i = 0; i < markdown.Length; i++)
        {
            if (markdown[i] == '\n')
            {
                lines++;
            }
            else if (markdown[i] == '\r' && (i + 1 >= markdown.Length || markdown[i + 1] != '\n'))
            {
                lines++;
            }
        }

        return lines;
    }
}
