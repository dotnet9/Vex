using System.Text.RegularExpressions;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed partial class MarkdownStatisticsService : IMarkdownStatisticsService
{
    public MarkdownStatistics Count(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return new MarkdownStatistics(0, 0, 1);
        }

        var textMetrics = CountTextMetrics(markdown);
        var lineMetrics = CountLineMetrics(markdown);
        var readingMinutes = textMetrics.Words == 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(textMetrics.Words / 220d));

        return new MarkdownStatistics(
            textMetrics.Words,
            textMetrics.Characters,
            lineMetrics.Lines,
            lineMetrics.Paragraphs,
            lineMetrics.Headings,
            readingMinutes);
    }

    private static TextMetrics CountTextMetrics(string markdown)
    {
        var words = 0;
        var characters = 0;
        var inLatinWord = false;

        foreach (var character in markdown)
        {
            if (char.IsWhiteSpace(character) || IsMarkdownSyntaxCharacter(character))
            {
                inLatinWord = false;
                continue;
            }

            characters++;

            if (IsCjkCharacter(character))
            {
                words++;
                inLatinWord = false;
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                if (!inLatinWord)
                {
                    words++;
                    inLatinWord = true;
                }

                continue;
            }

            inLatinWord = false;
        }

        return new TextMetrics(words, characters);
    }

    private static LineMetrics CountLineMetrics(string markdown)
    {
        var lines = CountLines(markdown);
        var paragraphs = 0;
        var headings = 0;
        var inParagraph = false;
        using var reader = new StringReader(markdown);

        while (reader.ReadLine() is { } rawLine)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
            {
                if (inParagraph)
                {
                    paragraphs++;
                    inParagraph = false;
                }

                continue;
            }

            if (HeadingRegex().IsMatch(line))
            {
                headings++;
                continue;
            }

            if (!HorizontalRuleRegex().IsMatch(line))
            {
                inParagraph = true;
            }
        }

        if (inParagraph)
        {
            paragraphs++;
        }

        return new LineMetrics(lines, paragraphs, headings);
    }

    private static int CountLines(string markdown)
    {
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

    private static bool IsMarkdownSyntaxCharacter(char character)
    {
        return character is '`' or '*' or '_' or '>' or '#' or '-' or '[' or ']' or '(' or ')' or '!' or '|';
    }

    private static bool IsCjkCharacter(char character)
    {
        return character is >= '\u3400' and <= '\u9FFF';
    }

    private readonly record struct TextMetrics(int Words, int Characters);

    private readonly record struct LineMetrics(int Lines, int Paragraphs, int Headings);

    [GeneratedRegex(@"^#{1,6}\s+\S+", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex HorizontalRuleRegex();
}
