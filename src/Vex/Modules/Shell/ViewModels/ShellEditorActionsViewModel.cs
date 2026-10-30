using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellEditorActionsViewModel
{
    public void Undo()
    {
        Publish(EditorActionKind.Undo);
    }

    public void Redo()
    {
        Publish(EditorActionKind.Redo);
    }

    public void Cut()
    {
        Publish(EditorActionKind.Cut);
    }

    public void Copy()
    {
        Publish(EditorActionKind.Copy);
    }

    public void Paste()
    {
        Publish(EditorActionKind.Paste);
    }

    public void SelectAll()
    {
        Publish(EditorActionKind.SelectAll);
    }

    public void CopyPlainText()
    {
        Publish(EditorActionKind.CopyPlainText);
    }

    public void FocusEditor()
    {
        Publish(EditorActionKind.FocusEditor);
    }

    public void InsertAction(EditorActionKind action)
    {
        Publish(action);
    }

    public void InsertSnippet(string key) =>
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorSnippetCommand(key));

    /// <summary>给当前选区套行内样式（字号 / 文字颜色 / 背景颜色），由编辑器 VM 接收执行。</summary>
    public void ApplySpanStyle(string property, string value)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorSpanStyleCommand(property, value));
    }

    private void Publish(EditorActionKind action)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
    }
}
