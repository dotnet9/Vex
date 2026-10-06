using Avalonia.Controls;
using Avalonia.Input.Platform;

using AvaloniaEdit;
using CodeWF.AvaloniaControls.Text;
using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorController : IMarkdownEditorController
{
    private readonly IMarkdownEditorActionService _actionService;
    private readonly IMarkdownEditorSearchService _searchService;
    private TextEditor? _editor;
    private bool _suppressTextChanged;
    private bool _autoPairEnabled = true;

    public MarkdownEditorController(
        IMarkdownEditorActionService actionService,
        IMarkdownEditorSearchService searchService)
    {
        _actionService = actionService;
        _searchService = searchService;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
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

    public void SetAutoPairEnabled(bool enabled) => _autoPairEnabled = enabled;

    public void ApplyExternalEdit(string markdown, int start, int length)
    {
        if (_editor?.Document is not { } document)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var canReplaceInPlace = document.TextLength == markdown.Length
                                && start >= 0
                                && length > 0
                                && start + length <= markdown.Length;

        if (!canReplaceInPlace)
        {
            SyncText(markdown);
            return;
        }

        _suppressTextChanged = true;
        try
        {
            document.Replace(start, length, markdown.Substring(start, length));
            _editor.CaretOffset = Math.Min(caret, document.TextLength);
        }
        finally
        {
            _suppressTextChanged = false;
        }

        PublishTextChanged();
    }

    public async Task CopyPlainTextAsync()
    {
        if (_editor is null)
        {
            return;
        }

        var text = _editor.SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            text = _editor.Text ?? string.Empty;
        }

        if (TopLevel.GetTopLevel(_editor)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public void InsertText(string text)
    {
        if (_editor?.Document is not { } document || string.IsNullOrEmpty(text))
        {
            return;
        }

        document.Insert(_editor.CaretOffset, text);
        _editor.CaretOffset += text.Length;
    }

    public bool TryHandleAutoPairBackspace()
    {
        if (_editor?.Document is not { } document || !_autoPairEnabled)
        {
            return false;
        }

        var result = TextAutoPair.HandleBackspace(document.Text, _editor.SelectionStart, _editor.SelectionLength);
        if (result is not { } change)
        {
            return false;
        }

        document.Replace(change.Start, change.Length, change.Text);
        _editor.CaretOffset = change.CaretOffset;
        return true;
    }

    public void PublishTextChanged()
    {
        if (_editor is null)
        {
            return;
        }

        var caret = _editor.TextArea.Caret;
        CodeWF.EventBus.EventBus.Default.Publish(new MarkdownTextChangedCommand(
            _editor.Text ?? string.Empty,
            caret.Line,
            caret.Column,
            _editor.Document?.LineCount ?? 1));
    }

    [EventHandler]
    public void ApplyEditorInsertText(MarkdownEditorInsertTextCommand command) => InsertText(command.Text);

    [EventHandler]
    public async void ApplyCopyPlainText(EditorActionCommand command)
    {
        if (command.Action == EditorActionKind.CopyPlainText)
        {
            await CopyPlainTextAsync();
        }
    }

    [EventHandler]
    public async void Execute(EditorActionCommand command)
    {
        if (_editor is null)
        {
            return;
        }

        await _actionService.ExecuteAsync(_editor, command.Action, RunTextMutation);
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
    public void GetSelection(EditorSelectionQuery query)
    {
        query.Result = GetSelection();
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
        return GetSelection().Text;
    }

    private EditorSelectionInfo GetSelection()
    {
        if (_editor is null || _editor.SelectionLength <= 0)
        {
            return new EditorSelectionInfo(string.Empty, 0, 0);
        }

        var text = _editor.Text ?? string.Empty;
        var start = Math.Clamp(_editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(_editor.SelectionLength, 0, text.Length - start);
        var selected = length > 0 ? text.Substring(start, length) : string.Empty;
        return new EditorSelectionInfo(selected, start, length);
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
