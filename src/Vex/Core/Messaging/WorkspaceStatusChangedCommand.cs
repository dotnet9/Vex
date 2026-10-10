using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class WorkspaceStatusChangedCommand : Command
{
    public WorkspaceStatusChangedCommand(string message)
    {
        Message = message;
    }

    public string Message { get; }
}
