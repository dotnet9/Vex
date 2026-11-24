namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>临时演练或切换文档时保存正文、活动块、选择及跨块撤销历史。</summary>
public sealed record MarkdownLiveEditorSnapshot
{
    private string? _text;
    private readonly int _textLength;
    private readonly Func<string>? _materializer;

    public string Text => _text ??= _materializer!();
    public int TextLength => _text?.Length ?? _textLength;
    public int ActiveBlock { get; }
    public int SelectionStart { get; }
    public int SelectionEnd { get; }

    public MarkdownLiveEditorSnapshot(string text, int activeBlock, int selectionStart, int selectionEnd)
    {
        _text = text;
        _textLength = text.Length;
        ActiveBlock = activeBlock;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
    }

    internal MarkdownLiveEditorSnapshot(Func<string> materializer, int textLength,
        int activeBlock, int selectionStart, int selectionEnd)
    {
        _materializer = materializer;
        _textLength = textLength;
        ActiveBlock = activeBlock;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
    }
}
public sealed record MarkdownLiveEditorState(MarkdownLiveEditorSnapshot Current,
    IReadOnlyList<MarkdownLiveEditorSnapshot> Undo, IReadOnlyList<MarkdownLiveEditorSnapshot> Redo)
{
    /// <summary>
    /// 操作式撤销条目（文本编辑=块级翻转量，其余=整篇快照）。视图内 CaptureState/RestoreState
    /// 走此通道，撤销粒度与内存特性得以跨视图切换保留；null 表示旧快照契约（整栈降级为快照条目）。
    /// </summary>
    internal IReadOnlyList<object>? InternalUndo { get; init; }

    internal IReadOnlyList<object>? InternalRedo { get; init; }

    internal Func<IReadOnlyList<MarkdownLiveEditorSnapshot>>? UndoMaterializer { get; init; }
    internal Func<IReadOnlyList<MarkdownLiveEditorSnapshot>>? RedoMaterializer { get; init; }
}
