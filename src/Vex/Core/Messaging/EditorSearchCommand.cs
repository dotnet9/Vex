using CodeWF.EventBus;

namespace Vex.Core.Messaging;

public sealed class EditorSearchCommand : Command
{
    public EditorSearchCommand(
        EditorSearchAction action,
        string searchText,
        string? replacementText = null,
        bool isMatchCase = false,
        bool isWholeWord = false)
    {
        Action = action;
        SearchText = searchText;
        ReplacementText = replacementText ?? string.Empty;
        IsMatchCase = isMatchCase;
        IsWholeWord = isWholeWord;
    }

    public EditorSearchAction Action { get; }

    public string SearchText { get; }

    public string ReplacementText { get; }

    public bool IsMatchCase { get; }

    public bool IsWholeWord { get; }
}
