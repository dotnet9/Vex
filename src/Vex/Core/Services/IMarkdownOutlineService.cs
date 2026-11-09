using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace Vex.Core.Services;

public interface IMarkdownOutlineService
{
    IReadOnlyList<MarkdownOutlineItem> BuildOutline(string markdown);
}
