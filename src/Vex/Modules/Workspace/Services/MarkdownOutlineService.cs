using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// 大纲提取走库的 AST 实现（<see cref="MarkdownOutlineExtractor"/>），
/// 天然规避代码块里的 "#" 误判，不再自行扫描行文本。
/// </summary>
public sealed partial class MarkdownOutlineService : IMarkdownOutlineService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public IReadOnlyList<OutlineItem> BuildOutline(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return [];
        }

        var model = MarkdownParser.Parse(markdown, Pipeline);
        var items = MarkdownOutlineExtractor.Extract(model);
        return items.Select(item => new OutlineItem(item.Level, item.Title, item.Line)).ToArray();
    }
}
