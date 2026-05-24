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

    public void FocusEditor()
    {
        Publish(EditorActionKind.FocusEditor);
    }

    public void InsertAction(EditorActionKind action)
    {
        Publish(action);
    }

    private void Publish(EditorActionKind action)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
    }
}
