using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class NavigateToLineCommand : Command
{
    public NavigateToLineCommand(int line)
    {
        Line = Math.Max(1, line);
    }

    public int Line { get; }
}
