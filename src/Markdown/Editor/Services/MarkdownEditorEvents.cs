namespace CodeWF.Avalonia.Markdown.Editor.Services;

/// <summary>编辑器变化事件的专用参数（值类型事件参数，避免每次分配）。</summary>
public sealed class MarkdownCaretEventArgs : EventArgs
{
    public MarkdownCaretEventArgs(int line, int column, int lineCount)
    {
        Line = line;
        Column = column;
        LineCount = Math.Max(1, lineCount);
    }

    public int Line { get; }

    public int Column { get; }

    public int LineCount { get; }
}

/// <summary>查找/替换结果事件参数。</summary>
public sealed class MarkdownEditorSearchResultEventArgs : EventArgs
{
    public MarkdownEditorSearchResultEventArgs(string message, int currentIndex, int totalCount)
    {
        Message = message;
        CurrentIndex = currentIndex;
        TotalCount = totalCount;
    }

    public string Message { get; }

    public int CurrentIndex { get; }

    public int TotalCount { get; }
}

/// <summary>编辑器动作请求事件参数。</summary>
public sealed class MarkdownEditorActionEventArgs : EventArgs
{
    public MarkdownEditorActionEventArgs(MarkdownEditorAction action)
    {
        Action = action;
    }

    public MarkdownEditorAction Action { get; }
}
