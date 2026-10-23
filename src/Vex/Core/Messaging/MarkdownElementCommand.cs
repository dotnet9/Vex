using CodeWF.Toolkit.EventBus;
using Vex.Modules.Workspace.Services;

namespace Vex.Core.Messaging;

public enum MarkdownElementOperation
{
    Table,
    FormatTable,
    Convert,
    InsertParagraphAbove,
    InsertParagraphBelow,
    ToggleTask,
    CopyBlock,
    DeleteBlock,
}

/// <summary>
/// 即时渲染块上的元素菜单命令：以块的起始源码行定位，由编辑器 ViewModel 换算为文档偏移执行，
/// 与源码右键菜单共用同一套文本操作。
/// </summary>
public sealed class MarkdownElementCommand : Command
{
    public MarkdownElementOperation Operation { get; }

    public MarkdownTableAction? TableAction { get; }

    public string? ConvertKind { get; }

    /// <summary>目标块的 1 起始源码行号。</summary>
    public int SourceLine { get; }

    public MarkdownElementCommand(MarkdownElementOperation operation, int sourceLine,
        MarkdownTableAction? tableAction = null, string? convertKind = null)
    {
        Operation = operation;
        SourceLine = sourceLine;
        TableAction = tableAction;
        ConvertKind = convertKind;
    }
}
