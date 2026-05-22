using AvaloniaEdit;
using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorController : IMarkdownEditorController
{
    private readonly IEventBus _eventBus;
    private readonly IMarkdownEditorMutationService _textMutationService;
    private readonly IAppLocalizer _localizer;
    private TextEditor? _editor;
    private bool _suppressTextChanged;

    public MarkdownEditorController(
        IEventBus eventBus,
        IMarkdownEditorMutationService textMutationService,
        IAppLocalizer localizer)
    {
        _eventBus = eventBus;
        _textMutationService = textMutationService;
        _localizer = localizer;
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
        }
        finally
        {
            _suppressTextChanged = false;
        }
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
            caret.Column));
    }

    [EventHandler]
    public void Execute(EditorActionCommand command)
    {
        if (_editor is null)
        {
            return;
        }

        switch (command.Action)
        {
            case EditorActionKind.Undo:
                RunTextMutation(() => _editor.Undo());
                break;
            case EditorActionKind.Redo:
                RunTextMutation(() => _editor.Redo());
                break;
            case EditorActionKind.Cut:
                RunTextMutation(_editor.Cut);
                break;
            case EditorActionKind.Copy:
                _editor.Copy();
                break;
            case EditorActionKind.Paste:
                RunTextMutation(_editor.Paste);
                break;
            case EditorActionKind.SelectAll:
                _editor.SelectAll();
                break;
            case EditorActionKind.Bold:
                WrapSelection("**", "**", "bold text");
                break;
            case EditorActionKind.Italic:
                WrapSelection("*", "*", "italic text");
                break;
            case EditorActionKind.InlineCode:
                WrapSelection("`", "`", "code");
                break;
            case EditorActionKind.Link:
                WrapSelection("[", "](https://example.com)", "link text");
                break;
            case EditorActionKind.Image:
                InsertText("![alt text](image.png)");
                break;
            case EditorActionKind.ClearFormatting:
                ClearFormatting();
                break;
            case EditorActionKind.Paragraph:
                PrefixCurrentLine(string.Empty);
                break;
            case EditorActionKind.Heading1:
                PrefixCurrentLine("# ");
                break;
            case EditorActionKind.Heading2:
                PrefixCurrentLine("## ");
                break;
            case EditorActionKind.Heading3:
                PrefixCurrentLine("### ");
                break;
            case EditorActionKind.Heading4:
                PrefixCurrentLine("#### ");
                break;
            case EditorActionKind.Heading5:
                PrefixCurrentLine("##### ");
                break;
            case EditorActionKind.Heading6:
                PrefixCurrentLine("###### ");
                break;
            case EditorActionKind.Quote:
                PrefixCurrentLine("> ");
                break;
            case EditorActionKind.UnorderedList:
                PrefixCurrentLine("- ");
                break;
            case EditorActionKind.OrderedList:
                PrefixCurrentLine("1. ");
                break;
            case EditorActionKind.TaskList:
                PrefixCurrentLine("- [ ] ");
                break;
            case EditorActionKind.CodeFence:
                WrapSelection("```csharp\n", "\n```", "Console.WriteLine(\"Vex\");");
                break;
            case EditorActionKind.Table:
                InsertText("\n| Column | Value |\n| --- | --- |\n| Item | Description |\n");
                break;
            case EditorActionKind.MathBlock:
                WrapSelection("$$\n", "\n$$", "E = mc^2");
                break;
            case EditorActionKind.HorizontalRule:
                InsertText("\n---\n");
                break;
            case EditorActionKind.Indent:
                IndentSelection();
                break;
            case EditorActionKind.Outdent:
                OutdentSelection();
                break;
            case EditorActionKind.FocusEditor:
                _editor.Focus();
                break;
        }
    }

    [EventHandler]
    public void Search(EditorSearchCommand command)
    {
        if (_editor is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(command.SearchText))
        {
            PublishSearchResult(VexL.StatusEnterSearchTextFirst);
            return;
        }

        switch (command.Action)
        {
            case EditorSearchAction.Count:
                CountMatches(command.SearchText);
                break;
            case EditorSearchAction.FindNext:
                FindNext(command.SearchText, _editor.CaretOffset + Math.Max(0, _editor.SelectionLength));
                break;
            case EditorSearchAction.ReplaceNext:
                ReplaceNext(command.SearchText, command.ReplacementText);
                break;
            case EditorSearchAction.ReplaceAll:
                ReplaceAll(command.SearchText, command.ReplacementText);
                break;
        }
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

    private void WrapSelection(string prefix, string suffix, string placeholder)
    {
        MutateEditor(editor => _textMutationService.WrapSelection(editor, prefix, suffix, placeholder));
    }

    private void InsertText(string insertion)
    {
        MutateEditor(editor => _textMutationService.InsertText(editor, insertion));
    }

    private void IndentSelection()
    {
        MutateEditor(_textMutationService.IndentSelection);
    }

    private void OutdentSelection()
    {
        MutateEditor(_textMutationService.OutdentSelection);
    }

    private void ClearFormatting()
    {
        MutateEditor(_textMutationService.ClearFormatting);
    }

    private void FindNext(string searchText, int startOffset)
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var text = _editor.Text ?? string.Empty;
        var matches = FindMatches(text, searchText);
        var matchIndex = GetNextMatchIndex(matches, startOffset);
        var index = matchIndex >= 0 ? matches[matchIndex] : -1;
        if (index < 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, searchText);
            return;
        }

        _editor.Select(index, searchText.Length);
        _editor.CaretOffset = index + searchText.Length;
        _editor.TextArea.Caret.BringCaretToView();
        _editor.Focus();
        var line = _editor.Document.GetLineByOffset(index).LineNumber;
        _eventBus.Publish(new EditorSearchResultCommand(
            _localizer.Format(VexL.EditorSearchFoundOnLineFormat, searchText, line, matchIndex + 1, matches.Count),
            matchIndex + 1,
            matches.Count));
        PublishTextChanged();
    }

    private void ReplaceNext(string searchText, string replacementText)
    {
        if (_editor is null)
        {
            return;
        }

        var text = _editor.Text ?? string.Empty;
        var start = Math.Clamp(_editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(_editor.SelectionLength, 0, text.Length - start);
        var selected = length > 0 ? text.Substring(start, length) : string.Empty;

        if (!selected.Equals(searchText, StringComparison.CurrentCultureIgnoreCase))
        {
            FindNext(searchText, _editor.CaretOffset);
            return;
        }

        RunTextMutation(() =>
        {
            _editor.Text = text[..start] + replacementText + text[(start + length)..];
            _editor.CaretOffset = start + replacementText.Length;
            _editor.Select(start, replacementText.Length);
        });
        PublishSearchResultFormat(VexL.EditorSearchReplacedNextFormat, searchText);
        FindNext(searchText, start + replacementText.Length);
    }

    private void ReplaceAll(string searchText, string replacementText)
    {
        if (_editor is null)
        {
            return;
        }

        var text = _editor.Text ?? string.Empty;
        var comparison = StringComparison.CurrentCultureIgnoreCase;
        var builder = new System.Text.StringBuilder(text.Length);
        var offset = 0;
        var count = 0;

        while (offset < text.Length)
        {
            var index = text.IndexOf(searchText, offset, comparison);
            if (index < 0)
            {
                builder.Append(text, offset, text.Length - offset);
                break;
            }

            builder.Append(text, offset, index - offset);
            builder.Append(replacementText);
            offset = index + searchText.Length;
            count++;
        }

        if (count == 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, searchText);
            return;
        }

        RunTextMutation(() =>
        {
            _editor.Text = builder.ToString();
            _editor.CaretOffset = 0;
            _editor.SelectionLength = 0;
        });
        PublishSearchResultFormat(VexL.EditorSearchReplacedAllFormat, count);
    }

    private void CountMatches(string searchText)
    {
        if (_editor is null)
        {
            return;
        }

        var matches = FindMatches(_editor.Text ?? string.Empty, searchText);
        if (matches.Count == 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, searchText);
            return;
        }

        var matchIndex = GetNextMatchIndex(matches, _editor.CaretOffset);
        _eventBus.Publish(new EditorSearchResultCommand(
            _localizer.Format(VexL.EditorSearchMatchCountFormat, matches.Count, searchText),
            matchIndex >= 0 ? matchIndex + 1 : 1,
            matches.Count));
    }

    private void PublishSearchResult(string key)
    {
        _eventBus.Publish(new EditorSearchResultCommand(_localizer.Get(key)));
    }

    private void PublishSearchResultFormat(string key, params object?[] args)
    {
        _eventBus.Publish(new EditorSearchResultCommand(_localizer.Format(key, args)));
    }

    private static List<int> FindMatches(string text, string searchText)
    {
        if (text.Length == 0 || searchText.Length == 0)
        {
            return [];
        }

        List<int> matches = [];
        var comparison = StringComparison.CurrentCultureIgnoreCase;
        var offset = 0;
        while (offset <= text.Length - searchText.Length)
        {
            var index = text.IndexOf(searchText, offset, comparison);
            if (index < 0)
            {
                break;
            }

            matches.Add(index);
            offset = index + Math.Max(1, searchText.Length);
        }

        return matches;
    }

    private static int GetNextMatchIndex(IReadOnlyList<int> matches, int startOffset)
    {
        if (matches.Count == 0)
        {
            return -1;
        }

        var start = Math.Max(0, startOffset);
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i] >= start)
            {
                return i;
            }
        }

        return 0;
    }

    private void PrefixCurrentLine(string prefix)
    {
        MutateEditor(editor => _textMutationService.PrefixCurrentLine(editor, prefix));
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

    private void MutateEditor(Action<TextEditor> mutation)
    {
        if (_editor is null)
        {
            return;
        }

        var editor = _editor;
        RunTextMutation(() => mutation(editor));
    }
}
