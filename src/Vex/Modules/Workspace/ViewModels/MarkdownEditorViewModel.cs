using Avalonia.Input;
using AvaloniaEdit;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Workspace.ViewModels;

public sealed class MarkdownEditorViewModel : ReactiveObject
{
    private readonly IEventBus _eventBus;
    private readonly IEditorDisplayState _editorDisplayState;
    private readonly IMarkdownEditorController _editorController;
    private double _editorFontSize;
    private string _markdown;
    private bool _showLineNumbers;

    public MarkdownEditorViewModel(
        IEventBus eventBus,
        IWorkspaceDocumentState documentState,
        IEditorDisplayState editorDisplayState,
        IMarkdownEditorController editorController)
    {
        _eventBus = eventBus;
        _editorDisplayState = editorDisplayState;
        _editorController = editorController;
        _editorFontSize = editorDisplayState.EditorFontSize;
        _markdown = documentState.Markdown;
        _showLineNumbers = editorDisplayState.ShowLineNumbers;
        _editorDisplayState.Changed += OnEditorDisplayChanged;
        eventBus.Subscribe(this);
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        private set => this.RaiseAndSetIfChanged(ref _editorFontSize, value);
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        private set => this.RaiseAndSetIfChanged(ref _showLineNumbers, value);
    }

    public string Markdown
    {
        get => _markdown;
        private set
        {
            if (_markdown == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _markdown, value);
            _editorController.SyncText(_markdown);
        }
    }

    public void AttachEditor(TextEditor editor)
    {
        _editorController.Attach(editor);
        _editorController.SyncText(Markdown);
    }

    public void DetachEditor(TextEditor editor)
    {
        _editorController.Detach(editor);
    }

    public bool HandleEditorKeyDown(Key key, KeyModifiers modifiers)
    {
        if (key != Key.Tab)
        {
            return false;
        }

        PublishEditorAction(modifiers.HasFlag(KeyModifiers.Shift)
            ? EditorActionKind.Outdent
            : EditorActionKind.Indent);
        return true;
    }

    [EventHandler]
    public void ApplyMarkdownDocumentChanged(MarkdownDocumentChangedCommand command)
    {
        Markdown = command.Markdown;
    }

    private void OnEditorDisplayChanged(object? sender, EventArgs e)
    {
        EditorFontSize = _editorDisplayState.EditorFontSize;
        ShowLineNumbers = _editorDisplayState.ShowLineNumbers;
    }

    public void Undo() => PublishEditorAction(EditorActionKind.Undo);

    public void Redo() => PublishEditorAction(EditorActionKind.Redo);

    public void Cut() => PublishEditorAction(EditorActionKind.Cut);

    public void Copy() => PublishEditorAction(EditorActionKind.Copy);

    public void Paste() => PublishEditorAction(EditorActionKind.Paste);

    public void SelectAll() => PublishEditorAction(EditorActionKind.SelectAll);

    public void InsertAction(EditorActionKind action) => PublishEditorAction(action);

    private void PublishEditorAction(EditorActionKind action)
    {
        _eventBus.Publish(new EditorActionCommand(action));
    }
}
