namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>临时演练或切换文档时保存正文、活动块、选择及跨块撤销历史。</summary>
public sealed record MarkdownLiveEditorSnapshot(string Text, int ActiveBlock, int SelectionStart, int SelectionEnd);
public sealed record MarkdownLiveEditorState(MarkdownLiveEditorSnapshot Current,
    IReadOnlyList<MarkdownLiveEditorSnapshot> Undo, IReadOnlyList<MarkdownLiveEditorSnapshot> Redo);
