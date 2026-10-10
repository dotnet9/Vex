using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class EditorActionCommand : Command
{
    public EditorActionCommand(EditorActionKind action)
    {
        Action = action;
    }

    public EditorActionKind Action { get; }
}
