using AvaloniaEdit;
using CodeWF.EventBus;
using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorController : IMarkdownEditorController
{
    private readonly IEventBus _eventBus;
    private TextEditor? _editor;
    private bool _suppressTextChanged;

    public MarkdownEditorController(IEventBus eventBus)
    {
        _eventBus = eventBus;
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
            case EditorActionKind.FocusEditor:
                _editor.Focus();
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
        if (_editor is null)
        {
            return;
        }

        RunTextMutation(() =>
        {
            var text = _editor.Text ?? string.Empty;
            var start = Math.Clamp(_editor.SelectionStart, 0, text.Length);
            var length = Math.Clamp(_editor.SelectionLength, 0, text.Length - start);
            var selected = length > 0 ? text.Substring(start, length) : placeholder;
            var replacement = $"{prefix}{selected}{suffix}";
            _editor.Text = text[..start] + replacement + text[(start + length)..];
            _editor.SelectionStart = start + prefix.Length;
            _editor.SelectionLength = selected.Length;
            _editor.CaretOffset = start + replacement.Length;
        });
    }

    private void InsertText(string insertion)
    {
        if (_editor is null)
        {
            return;
        }

        RunTextMutation(() =>
        {
            var text = _editor.Text ?? string.Empty;
            var start = Math.Clamp(_editor.SelectionStart, 0, text.Length);
            var length = Math.Clamp(_editor.SelectionLength, 0, text.Length - start);
            _editor.Text = text[..start] + insertion + text[(start + length)..];
            _editor.CaretOffset = start + insertion.Length;
        });
    }

    private void PrefixCurrentLine(string prefix)
    {
        if (_editor is null)
        {
            return;
        }

        RunTextMutation(() =>
        {
            var text = _editor.Text ?? string.Empty;
            var offset = Math.Clamp(_editor.CaretOffset, 0, text.Length);
            var lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            var line = text[lineStart..lineEnd];
            var normalized = RemoveMarkdownLinePrefix(line);
            var replacement = string.IsNullOrEmpty(prefix) ? normalized : prefix + normalized;
            _editor.Text = text[..lineStart] + replacement + text[lineEnd..];
            _editor.CaretOffset = lineStart + replacement.Length;
        });
    }

    private void DetachCurrentEditor()
    {
        if (_editor is not null)
        {
            _editor.TextChanged -= OnEditorTextChanged;
            _editor = null;
        }
    }

    private static string RemoveMarkdownLinePrefix(string line)
    {
        var trimmed = line.TrimStart();
        var leading = line.Length - trimmed.Length;

        string[] prefixes =
        [
            "###### ",
            "##### ",
            "#### ",
            "### ",
            "## ",
            "# ",
            "> ",
            "- [ ] ",
            "- ",
            "1. "
        ];

        foreach (var prefix in prefixes)
        {
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[..leading] + trimmed[prefix.Length..];
            }
        }

        return line;
    }
}
