using AvaloniaEdit;
using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorController : IMarkdownEditorController
{
    private readonly IEventBus _eventBus;
    private readonly IMarkdownEditorActionService _actionService;
    private readonly IMarkdownEditorSearchService _searchService;
    private TextEditor? _editor;
    private bool _suppressTextChanged;

    public MarkdownEditorController(
        IEventBus eventBus,
        IMarkdownEditorActionService actionService,
        IMarkdownEditorSearchService searchService)
    {
        _eventBus = eventBus;
        _actionService = actionService;
        _searchService = searchService;
        eventBus.Subscribe(this);
    }

    public void Attach(TextEditor editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            return;
        }

        DetachCurrentEditor();
        _editor = editor;
        _editor.TextChanged += OnEditorTextChanged;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
    }

    public void Detach(TextEditor editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            DetachCurrentEditor();
        }
    }

    public void SyncText(string? markdown)
    {
        if (_editor is null)
        {
            return;
        }

        var normalized = markdown ?? string.Empty;
        if (_editor.Text == normalized)
        {
            return;
        }

        _suppressTextChanged = true;
        try
        {
            _editor.Text = normalized;
            _editor.CaretOffset = 0;
        }
        finally
        {
            _suppressTextChanged = false;
        }

        PublishTextChanged();
    }

    public void PublishTextChanged()
    {
        if (_editor is null)
        {
            return;
        }

        var caret = _editor.TextArea.Caret;
        _eventBus.Publish(new MarkdownTextChangedCommand(
            _editor.Text ?? string.Empty,
            caret.Line,
            caret.Column,
            _editor.Document?.LineCount ?? 1));
    }

    [EventHandler]
    public void Execute(EditorActionCommand command)
    {
        if (_editor is null)
        {
            return;
        }

        _actionService.Execute(_editor, command.Action, RunTextMutation);
    }

    [EventHandler]
    public void Search(EditorSearchCommand command)
    {
        if (_editor is null)
        {
            return;
        }

        _searchService.Search(_editor, command, RunTextMutation, PublishTextChanged);
    }

    [EventHandler]
    public void GetSelectedText(EditorSelectedTextQuery query)
    {
        query.Result = GetSelectedText();
    }

    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var line = Math.Clamp(command.Line, 1, _editor.Document.LineCount);
        var offset = _editor.Document.GetLineByNumber(line).Offset;
        _editor.CaretOffset = offset;
        _editor.TextArea.Caret.BringCaretToView();
        _editor.Focus();
        PublishTextChanged();
    }

    private string GetSelectedText()
    {
        if (_editor is null || _editor.SelectionLength <= 0)
        {
            return string.Empty;
        }

        var text = _editor.Text ?? string.Empty;
        var start = Math.Clamp(_editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(_editor.SelectionLength, 0, text.Length - start);
        return length > 0
            ? text.Substring(start, length)
            : string.Empty;
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (!_suppressTextChanged)
        {
            PublishTextChanged();
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (!_suppressTextChanged)
        {
            // 光标移动也走同一条消息通道，确保状态栏行列号不依赖文本变化才刷新。
            PublishTextChanged();
        }
    }

    private void RunTextMutation(Action mutation)
    {
        _suppressTextChanged = true;
        try
        {
            mutation();
        }
        finally
        {
            _suppressTextChanged = false;
        }

        PublishTextChanged();
    }

    private void DetachCurrentEditor()
    {
        if (_editor is not null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
            _editor = null;
        }
    }

}
