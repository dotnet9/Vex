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
            _eventBus.Publish(new EditorSearchResultCommand("Enter search text first."));
            return;
        }

        switch (command.Action)
        {
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

    private void ClearFormatting()
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

            if (length == 0)
            {
                var offset = Math.Clamp(_editor.CaretOffset, 0, text.Length);
                start = text.LastIndexOf('\n', Math.Max(0, offset - 1));
                start = start < 0 ? 0 : start + 1;
                var end = text.IndexOf('\n', start);
                if (end < 0)
                {
                    end = text.Length;
                }

                length = end - start;
            }

            var selected = text.Substring(start, length);
            var cleaned = ClearMarkdownFormatting(selected);
            _editor.Text = text[..start] + cleaned + text[(start + length)..];
            _editor.SelectionStart = start;
            _editor.SelectionLength = cleaned.Length;
            _editor.CaretOffset = start + cleaned.Length;
        });
    }

    private void FindNext(string searchText, int startOffset)
    {
        if (_editor?.Document is null)
        {
            return;
        }

        var text = _editor.Text ?? string.Empty;
        var index = IndexOfWrapped(text, searchText, startOffset);
        if (index < 0)
        {
            _eventBus.Publish(new EditorSearchResultCommand($"No match for \"{searchText}\"."));
            return;
        }

        _editor.Select(index, searchText.Length);
        _editor.CaretOffset = index + searchText.Length;
        _editor.TextArea.Caret.BringCaretToView();
        _editor.Focus();
        var line = _editor.Document.GetLineByOffset(index).LineNumber;
        _eventBus.Publish(new EditorSearchResultCommand($"Found \"{searchText}\" on line {line}."));
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
        _eventBus.Publish(new EditorSearchResultCommand($"Replaced next \"{searchText}\"."));
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
            _eventBus.Publish(new EditorSearchResultCommand($"No match for \"{searchText}\"."));
            return;
        }

        RunTextMutation(() =>
        {
            _editor.Text = builder.ToString();
            _editor.CaretOffset = 0;
            _editor.SelectionLength = 0;
        });
        _eventBus.Publish(new EditorSearchResultCommand($"Replaced {count} occurrence(s)."));
    }

    private static int IndexOfWrapped(string text, string searchText, int startOffset)
    {
        if (text.Length == 0)
        {
            return -1;
        }

        var start = Math.Clamp(startOffset, 0, text.Length);
        var comparison = StringComparison.CurrentCultureIgnoreCase;
        var index = text.IndexOf(searchText, start, comparison);
        return index >= 0 ? index : text.IndexOf(searchText, 0, comparison);
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

    private static string ClearMarkdownFormatting(string markdown)
    {
        var lines = markdown
            .Replace("**", string.Empty, StringComparison.Ordinal)
            .Replace("__", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = RemoveMarkdownLinePrefix(lines[i])
                .Replace("![", "[", StringComparison.Ordinal)
                .Replace("](", " (", StringComparison.Ordinal)
                .Replace(")", string.Empty, StringComparison.Ordinal);
        }

        return string.Join('\n', lines);
    }
}
