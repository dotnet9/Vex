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
    private readonly IEditorDisplayState _editorDisplayState;
    private readonly IMarkdownEditorController _editorController;
    private double _editorFontSize;
    private string _markdown;
    private bool _showLineNumbers;
    private bool _enableAutoPair = true;

    public MarkdownEditorViewModel(
        IWorkspaceDocumentState documentState,
        IEditorDisplayState editorDisplayState,
        IMarkdownEditorController editorController)
    {
        _editorDisplayState = editorDisplayState;
        _editorController = editorController;
        _editorFontSize = editorDisplayState.EditorFontSize;
        _markdown = documentState.Markdown;
        _showLineNumbers = editorDisplayState.ShowLineNumbers;
        _enableAutoPair = editorDisplayState.EnableAutoPair;
        _editorDisplayState.Changed += OnEditorDisplayChanged;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    /// <summary>自动配对开关；由 Shell 显示偏好下发给编辑器视图。</summary>
    public bool EnableAutoPair
    {
        get => _enableAutoPair;
        private set
        {
            this.RaiseAndSetIfChanged(ref _enableAutoPair, value);
            _editorController.SetAutoPairEnabled(value);
        }
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
        _editorController.SetAutoPairEnabled(EnableAutoPair);
        _editorController.SyncText(Markdown);
    }

    /// <summary>退格命中空配对时按库逻辑删除两侧，返回是否已处理。</summary>
    public bool TryHandleAutoPairBackspace() => _editorController.TryHandleAutoPairBackspace();

    /// <summary>在当前插入点插入文本（图片拖入等场景）。</summary>
    public void InsertText(string text) => _editorController.InsertText(text);

    public void DetachEditor(TextEditor editor)
    {
        _editorController.Detach(editor);
    }

    public bool HandleEditorKeyDown(Key key, KeyModifiers modifiers)
    {
        if (key == Key.Enter && modifiers == KeyModifiers.None)
        {
            PublishEditorAction(EditorActionKind.SmartNewLine);
            return true;
        }

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
        EnableAutoPair = _editorDisplayState.EnableAutoPair;
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
        CodeWF.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
    }
}
