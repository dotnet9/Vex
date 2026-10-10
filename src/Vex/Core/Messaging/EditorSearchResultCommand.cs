using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

public sealed class EditorSearchResultCommand : Command
{
    public EditorSearchResultCommand(string message, int currentIndex = 0, int totalCount = 0)
    {
        Message = message;
        CurrentIndex = currentIndex;
        TotalCount = totalCount;
    }

    public string Message { get; }

    public int CurrentIndex { get; }

    public int TotalCount { get; }
}
