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

        // 统计前先弱化常见 Markdown 标记，避免 #、*、链接符号等语法字符污染正文指标。
        var text = MarkdownSyntaxRegex().Replace(markdown, " ");
        var latinWords = LatinWordRegex().Matches(text).Count;
        var cjkWords = CjkRegex().Matches(text).Count;
        var words = latinWords + cjkWords;
        var characters = text.Count(c => !char.IsWhiteSpace(c));
        var normalizedMarkdown = markdown.ReplaceLineEndings("\n");
        var lines = normalizedMarkdown.Split('\n').Length;
        var paragraphs = CountParagraphs(normalizedMarkdown);
        var headings = HeadingRegex().Matches(normalizedMarkdown).Count;
        var readingMinutes = words == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(words / 220d));
        return new MarkdownStatistics(words, characters, lines, paragraphs, headings, readingMinutes);
    }

    private static int CountParagraphs(string markdown)
    {
        var paragraphs = 0;
        var inParagraph = false;

        foreach (var rawLine in markdown.Split('\n'))
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

            if (!HeadingRegex().IsMatch(line) && !HorizontalRuleRegex().IsMatch(line))
            {
                inParagraph = true;
            }
        }

        return inParagraph ? paragraphs + 1 : paragraphs;
    }

    [GeneratedRegex(@"[`*_>#\-\[\]\(\)!|]")]
    private static partial Regex MarkdownSyntaxRegex();

    [GeneratedRegex(@"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex LatinWordRegex();

    [GeneratedRegex(@"[\u3400-\u9FFF]")]
    private static partial Regex CjkRegex();

    [GeneratedRegex(@"^#{1,6}\s+\S+", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex HorizontalRuleRegex();
}
