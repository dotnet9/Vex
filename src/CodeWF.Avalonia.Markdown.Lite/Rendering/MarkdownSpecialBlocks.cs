using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 特殊块判定与源文本提取：TOC/数学块识别、按块 Span 回切片段。
/// </summary>
internal static class MarkdownSpecialBlocks
{
    public static bool IsTocBlock(Block block)
    {
        var typeName = block.GetType().Name;
        return typeName.Contains("TableOfContents", StringComparison.OrdinalIgnoreCase)
               || typeName.Equals("TocBlock", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsMathBlock(Block block)
    {
        return block.GetType().Name.Contains("Math", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetSpecialBlockText(Block block, string? sourceMarkdown)
    {
        var sourceText = GetSourceText(block, sourceMarkdown);
        if (!string.IsNullOrWhiteSpace(sourceText))
        {
            return sourceText.Trim();
        }

        return block switch
        {
            ParagraphBlock paragraphBlock => paragraphBlock.Lines.ToString().Trim(),
            HtmlBlock htmlBlock => htmlBlock.Lines.ToString().Trim(),
            LeafBlock leafBlock when IsMathBlock(block) => leafBlock.Lines.ToString().Trim(),
            _ when IsMathBlock(block) => block.ToString()?.Trim(),
            _ => null
        };
    }

    private static string? GetSourceText(Block block, string? sourceMarkdown)
    {
        if (string.IsNullOrEmpty(sourceMarkdown)
            || block.Span.Start < 0
            || block.Span.End < block.Span.Start
            || block.Span.Start >= sourceMarkdown.Length)
        {
            return null;
        }

        var end = Math.Min(block.Span.End, sourceMarkdown.Length - 1);
        return sourceMarkdown[block.Span.Start..(end + 1)];
    }
}
