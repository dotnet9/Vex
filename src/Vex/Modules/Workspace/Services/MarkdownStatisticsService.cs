using CodeWF.Markdown.Shared.Rendering;
using Markdig;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// 字数统计走库的 <see cref="MarkdownTextStatisticsCalculator"/>（与渲染同一套正文口径：
/// 代码块、表格、公式使用同一纯文本提取），本服务只做展示映射。
/// </summary>
public sealed class MarkdownStatisticsService : IMarkdownStatisticsService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public MarkdownStatistics Count(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return new MarkdownStatistics(0, 0, 1);
        }

        var model = MarkdownParser.Parse(markdown, Pipeline);
        var statistics = MarkdownTextStatisticsCalculator.Calculate(model);
        return new MarkdownStatistics(
            statistics.Words,
            statistics.Characters,
            statistics.Lines,
            statistics.Paragraphs,
            statistics.Headings,
            statistics.ReadingMinutes);
    }
}
