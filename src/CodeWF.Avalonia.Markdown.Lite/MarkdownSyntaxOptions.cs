using Markdig;
using Markdig.Extensions.AutoLinks;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Mathematics;

namespace CodeWF.Avalonia.Markdown;

/// <summary>每个文档独立的语法偏好，不修改全局默认管道或 Markdown 源文。</summary>
public sealed record MarkdownSyntaxOptions(
    bool Math = true, bool Subscript = true, bool Highlight = true,
    bool AutoLinks = true, bool SoftBreaks = false, bool Diagrams = true, bool Chemistry = true)
{
    public MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter();
        if (!Math) builder.Extensions.RemoveAll(extension => extension is MathExtension);
        if (!AutoLinks) builder.Extensions.RemoveAll(extension => extension is AutoLinkExtension);
        builder.Extensions.RemoveAll(extension => extension is EmphasisExtraExtension);
        var emphasis = EmphasisExtraOptions.Strikethrough;
        if (Subscript) emphasis |= EmphasisExtraOptions.Subscript | EmphasisExtraOptions.Superscript;
        if (Highlight) emphasis |= EmphasisExtraOptions.Marked;
        builder.UseEmphasisExtras(emphasis);
        if (SoftBreaks) builder.UseSoftlineBreakAsHardlineBreak();
        return builder.Build();
    }
}
