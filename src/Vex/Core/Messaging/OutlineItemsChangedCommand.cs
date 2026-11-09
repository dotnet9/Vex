using CodeWF.Avalonia.Markdown.Shared.Rendering;
using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class OutlineItemsChangedCommand : Command
{
    public OutlineItemsChangedCommand(IEnumerable<MarkdownOutlineItem> items)
    {
        Items = items.ToArray();
    }

    public IReadOnlyList<MarkdownOutlineItem> Items { get; }
}
