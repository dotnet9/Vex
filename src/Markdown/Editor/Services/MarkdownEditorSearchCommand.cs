namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>查找/替换动作。</summary>
public enum MarkdownEditorSearchAction
{
    Count,
    FindNext,
    FindPrevious,
    ReplaceNext,
    ReplaceAll
}

/// <summary>查找/替换请求。</summary>
public sealed class MarkdownEditorSearchCommand
{
    public MarkdownEditorSearchCommand(
        MarkdownEditorSearchAction action,
        string searchText,
        string? replacementText = null,
        bool isMatchCase = false,
        bool isWholeWord = false,
        bool isRegex = false)
    {
        Action = action;
        SearchText = searchText;
        ReplacementText = replacementText ?? string.Empty;
        IsMatchCase = isMatchCase;
        IsWholeWord = isWholeWord;
        IsRegex = isRegex;
    }

    public MarkdownEditorSearchAction Action { get; }

    public string SearchText { get; }

    public string ReplacementText { get; }

    public bool IsMatchCase { get; }

    public bool IsWholeWord { get; }

    public bool IsRegex { get; }
}
