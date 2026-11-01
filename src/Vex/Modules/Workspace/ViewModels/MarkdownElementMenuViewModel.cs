using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.ViewModels;

/// <summary>把当前上下文块的应用菜单动作交给现有文档命令通道。</summary>
public sealed class MarkdownElementMenuViewModel : ReactiveObject
{
    private int _sourceLine = 1;
    public int SourceLine
    {
        get => _sourceLine;
        set => this.RaiseAndSetIfChanged(ref _sourceLine, Math.Max(1, value));
    }

    public void EditorAction(EditorActionKind action) => EventBus.Default.Publish(new EditorActionCommand(action));
    public void ConvertBlock(string kind) => EventBus.Default.Publish(new MarkdownElementCommand(
        MarkdownElementOperation.Convert, SourceLine, convertKind: kind));
    public void TableAction(MarkdownTableAction action) => EventBus.Default.Publish(new MarkdownElementCommand(
        MarkdownElementOperation.Table, SourceLine, tableAction: action));
    public void FormatTable() => Publish(MarkdownElementOperation.FormatTable);
    public void ToggleTask() => Publish(MarkdownElementOperation.ToggleTask);
    public void CopyBlock() => Publish(MarkdownElementOperation.CopyBlock);
    public void DeleteBlock() => Publish(MarkdownElementOperation.DeleteBlock);
    public void InsertParagraphAbove() => Publish(MarkdownElementOperation.InsertParagraphAbove);
    public void InsertParagraphBelow() => Publish(MarkdownElementOperation.InsertParagraphBelow);
    private void Publish(MarkdownElementOperation operation) => EventBus.Default.Publish(new MarkdownElementCommand(operation, SourceLine));
}
