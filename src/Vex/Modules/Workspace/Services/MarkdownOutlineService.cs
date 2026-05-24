using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed partial class MarkdownOutlineService : IMarkdownOutlineService
{
    public IReadOnlyList<OutlineItem> BuildOutline(string markdown)
        => MarkdownHeadingScanner.BuildOutline(markdown);
}
