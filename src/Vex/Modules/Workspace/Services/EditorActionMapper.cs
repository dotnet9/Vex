using CodeWF.Avalonia.Markdown.Editor.Services;

using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.Services;

/// <summary>
/// Shell 的消息枚举 ↔ 库编辑器动作的映射（下沉后 Vex 只保留这一层适配）。
/// </summary>
public static class EditorActionMapper
{
    public static MarkdownEditorAction ToEditorAction(this EditorActionKind action) => action switch
    {
        EditorActionKind.Undo => MarkdownEditorAction.Undo,
        EditorActionKind.Redo => MarkdownEditorAction.Redo,
        EditorActionKind.Cut => MarkdownEditorAction.Cut,
        EditorActionKind.Copy => MarkdownEditorAction.Copy,
        EditorActionKind.Paste => MarkdownEditorAction.Paste,
        EditorActionKind.SelectAll => MarkdownEditorAction.SelectAll,
        EditorActionKind.CopyPlainText => MarkdownEditorAction.CopyPlainText,
        EditorActionKind.Bold => MarkdownEditorAction.Bold,
        EditorActionKind.Italic => MarkdownEditorAction.Italic,
        EditorActionKind.InlineCode => MarkdownEditorAction.InlineCode,
        EditorActionKind.Link => MarkdownEditorAction.Link,
        EditorActionKind.Image => MarkdownEditorAction.Image,
        EditorActionKind.ClearFormatting => MarkdownEditorAction.ClearFormatting,
        EditorActionKind.Paragraph => MarkdownEditorAction.Paragraph,
        EditorActionKind.Heading1 => MarkdownEditorAction.Heading1,
        EditorActionKind.Heading2 => MarkdownEditorAction.Heading2,
        EditorActionKind.Heading3 => MarkdownEditorAction.Heading3,
        EditorActionKind.Heading4 => MarkdownEditorAction.Heading4,
        EditorActionKind.Heading5 => MarkdownEditorAction.Heading5,
        EditorActionKind.Heading6 => MarkdownEditorAction.Heading6,
        EditorActionKind.Quote => MarkdownEditorAction.Quote,
        EditorActionKind.UnorderedList => MarkdownEditorAction.UnorderedList,
        EditorActionKind.OrderedList => MarkdownEditorAction.OrderedList,
        EditorActionKind.TaskList => MarkdownEditorAction.TaskList,
        EditorActionKind.CodeFence => MarkdownEditorAction.CodeFence,
        EditorActionKind.Table => MarkdownEditorAction.Table,
        EditorActionKind.MathBlock => MarkdownEditorAction.MathBlock,
        EditorActionKind.HorizontalRule => MarkdownEditorAction.HorizontalRule,
        EditorActionKind.SmartNewLine => MarkdownEditorAction.SmartNewLine,
        EditorActionKind.Indent => MarkdownEditorAction.Indent,
        EditorActionKind.Outdent => MarkdownEditorAction.Outdent,
        EditorActionKind.FocusEditor => MarkdownEditorAction.FocusEditor,
        _ => MarkdownEditorAction.FocusEditor
    };

    public static MarkdownEditorSearchCommand ToEditorSearchCommand(this EditorSearchCommand command) =>
        new(
            command.Action switch
            {
                EditorSearchAction.Count => MarkdownEditorSearchAction.Count,
                EditorSearchAction.ReplaceNext => MarkdownEditorSearchAction.ReplaceNext,
                EditorSearchAction.ReplaceAll => MarkdownEditorSearchAction.ReplaceAll,
                _ => MarkdownEditorSearchAction.FindNext
            },
            command.SearchText,
            command.ReplacementText,
            command.IsMatchCase,
            command.IsWholeWord,
            command.IsRegex);
}
