using CodeWF.Avalonia.MindView.Markdown;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellWorkbenchViewModel
{
    public WorkspaceEditHistory History { get; } = new();
    private bool _replayingHistory;
    private int? _pendingChapterStart;
    private MarkdownChapterDocument? _chapterDocument;
    private MarkdownChapterDocument ChapterDocument => _chapterDocument?.Text == _document.Markdown
        ? _chapterDocument : _chapterDocument = new MarkdownChapterDocument(_document.Markdown);
    private string _chapterError = "";
    public string ChapterError { get => _chapterError; private set => this.RaiseAndSetIfChanged(ref _chapterError, value); }
    public bool IsChapterSelected => SelectedNode?.Kind == MarkdownMindMapNodeKind.Heading;
    public string ChapterTitle => SelectedChapter()?.Title ?? "";
    public string ChapterBody => SelectedChapter() is { } chapter ? ChapterDocument.Body(chapter) : "";
    private MarkdownChapterDocument.Chapter? SelectedChapter() => SelectedNode is { Kind: MarkdownMindMapNodeKind.Heading } node
        ? ResolveChapter(node.SourceStart) : null;
    // 输入后导图有短暂的防抖延迟；只把明确位于本次编辑之外的源码范围映射到新文档。
    private MarkdownChapterDocument.Chapter? ResolveChapter(int start)
    {
        if (_mapDocumentPath != _document.FilePath) return null;
        var current = _document.Markdown;
        if (_mapMarkdown is not { } previous || current == previous) return ChapterDocument.At(start);
        var prefix = 0;
        while (prefix < Math.Min(previous.Length, current.Length) && previous[prefix] == current[prefix]) prefix++;
        var suffix = 0;
        while (suffix < Math.Min(previous.Length, current.Length) - prefix && previous[^(suffix + 1)] == current[^(suffix + 1)]) suffix++;
        if (start <= prefix) return ChapterDocument.At(start);
        if (start >= previous.Length - suffix) return ChapterDocument.At(start + current.Length - previous.Length);
        return null;
    }
    private void NotifyChapter()
    {
        foreach (var property in new[] { nameof(IsChapterSelected), nameof(ChapterTitle), nameof(ChapterBody) }) this.RaisePropertyChanged(property);
    }
    public void CommitChapterTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        var doc = ChapterDocument;
        if (SelectedChapter() is { } chapter)
            ApplyChapter(doc.Rename(chapter, title), chapter.Start);
    }
    public void CommitChapterBody(string body)
    {
        var doc = ChapterDocument;
        if (SelectedChapter() is { } chapter && body != doc.Body(chapter))
            ApplyChapter(doc.SetBody(chapter, body), chapter.Start);
    }
    public void ExecuteChapter(string command)
    {
        ChapterError = "";
        var doc = ChapterDocument;
        var chapter = SelectedChapter();
        if (IsChapterSelected && chapter is null) return;
        try
        {
            if (command is "child" or "sibling")
            {
                var added = doc.Add(chapter, command == "child", WorkbenchText.Get("WSNewChapter"));
                ApplyChapter(added.Text, added.Start); return;
            }
            if (chapter is null) return;
            var siblings = doc.Siblings(chapter).ToList(); var index = siblings.IndexOf(chapter);
            if (command == "up" && index > 0) ApplyMove(doc, chapter, siblings[index - 1], "before");
            if (command == "down" && index < siblings.Count - 1) ApplyMove(doc, chapter, siblings[index + 1], "after");
            if (command == "demote" && index > 0) ApplyMove(doc, chapter, siblings[index - 1], "child");
            if (command == "promote" && chapter.Parent is { } parent) ApplyMove(doc, chapter, doc.At(parent), "after");
        }
        catch (InvalidOperationException) { ChapterError = WorkbenchText.Get("WSHeadingLimit"); }
    }
    public void MoveChapter(int start, int target, string position)
    {
        var doc = ChapterDocument;
        if (ResolveChapter(start) is not { } chapter || ResolveChapter(target) is not { } destination) return;
        try { ApplyMove(doc, chapter, destination, position); }
        catch (InvalidOperationException) { ChapterError = WorkbenchText.Get("WSHeadingLimit"); }
    }
    private void ApplyMove(MarkdownChapterDocument doc, MarkdownChapterDocument.Chapter chapter, MarkdownChapterDocument.Chapter? target, string position)
    { var moved = doc.Move(chapter, target, position); ApplyChapter(moved.Text, moved.Start); }
    public void DeleteChapter(bool keepContent)
    {
        var doc = ChapterDocument;
        if (SelectedChapter() is { } chapter)
            ApplyChapter(doc.Delete(chapter, keepContent), chapter.Parent);
    }
    private void ApplyChapter(string text, int? start)
    {
        if (text == _document.Markdown) return;
        History.BreakGroup(); _pendingChapterStart = start;
        EventBus.Default.Publish(new MarkdownTextChangedCommand(text, 1, 1, text.Count(c => c == '\n') + 1));
        RefreshChapterMap();
        History.BreakGroup();
    }
    public void ResetHistory() => History.Reset(_document.Markdown);
    public void UndoDocument() => ReplayHistory(false);
    public void RedoDocument() => ReplayHistory(true);
    private void ReplayHistory(bool redo)
    {
        if (_replayingHistory) return;
        var text = redo ? History.Redo() : History.Undo();
        if (text is null) return;
        _replayingHistory = true;
        try
        {
            EventBus.Default.Publish(new MarkdownTextChangedCommand(text, 1, 1, text.Count(c => c == '\n') + 1));
            RefreshChapterMap();
        }
        finally { _replayingHistory = false; }
    }
}
