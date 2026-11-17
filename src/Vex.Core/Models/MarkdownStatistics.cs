namespace Vex.Core.Models;

public sealed record MarkdownStatistics(
    int Words,
    int Characters,
    int Lines,
    int Paragraphs = 0,
    int Headings = 0,
    int ReadingMinutes = 0);
