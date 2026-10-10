using CodeWF.Toolkit.EventBus;
using Vex.Core.Models;

namespace Vex.Core.Messaging;

public sealed class OutlineItemsChangedCommand : Command
{
    public OutlineItemsChangedCommand(IEnumerable<OutlineItem> items)
    {
        Items = items.ToArray();
    }

    public IReadOnlyList<OutlineItem> Items { get; }
}
