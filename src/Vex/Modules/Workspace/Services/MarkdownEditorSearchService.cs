using AvaloniaEdit;
using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorSearchService : IMarkdownEditorSearchService
{
    private readonly IEventBus _eventBus;
    private readonly IAppLocalizer _localizer;

    public MarkdownEditorSearchService(IEventBus eventBus, IAppLocalizer localizer)
    {
        _eventBus = eventBus;
        _localizer = localizer;
    }

    public void Search(
        TextEditor editor,
        EditorSearchCommand command,
        Action<Action> runTextMutation,
        Action publishTextChanged)
    {
        if (string.IsNullOrEmpty(command.SearchText))
        {
            PublishSearchResult(VexL.StatusEnterSearchTextFirst);
            return;
        }

        switch (command.Action)
        {
            case EditorSearchAction.Count:
                CountMatches(editor, command);
                break;
            case EditorSearchAction.FindNext:
                FindNext(editor, command, editor.CaretOffset + Math.Max(0, editor.SelectionLength), publishTextChanged);
                break;
            case EditorSearchAction.ReplaceNext:
                ReplaceNext(editor, command, runTextMutation, publishTextChanged);
                break;
            case EditorSearchAction.ReplaceAll:
                ReplaceAll(editor, command, runTextMutation);
                break;
        }
    }

    private void FindNext(TextEditor editor, EditorSearchCommand command, int startOffset, Action publishTextChanged)
    {
        if (editor.Document is null)
        {
            return;
        }

        var text = editor.Text ?? string.Empty;
        var searchText = command.SearchText;
        var matches = FindMatches(text, command);
        var matchIndex = GetNextMatchIndex(matches, startOffset);
        var index = matchIndex >= 0 ? matches[matchIndex] : -1;
        if (index < 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, searchText);
            return;
        }

        editor.Select(index, searchText.Length);
        editor.CaretOffset = index + searchText.Length;
        editor.TextArea.Caret.BringCaretToView();
        editor.Focus();
        var line = editor.Document.GetLineByOffset(index).LineNumber;
        _eventBus.Publish(new EditorSearchResultCommand(
            _localizer.Format(VexL.EditorSearchFoundOnLineFormat, searchText, line, matchIndex + 1, matches.Count),
            matchIndex + 1,
            matches.Count));
        publishTextChanged();
    }

    private void ReplaceNext(
        TextEditor editor,
        EditorSearchCommand command,
        Action<Action> runTextMutation,
        Action publishTextChanged)
    {
        var text = editor.Text ?? string.Empty;
        var start = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(editor.SelectionLength, 0, text.Length - start);
        var selected = length > 0 ? text.Substring(start, length) : string.Empty;
        var searchText = command.SearchText;

        if (!IsMatch(selected, searchText, command)
            || (command.IsWholeWord && !IsWholeWordMatch(text, start, length)))
        {
            FindNext(editor, command, editor.CaretOffset, publishTextChanged);
            return;
        }

        runTextMutation(() =>
        {
            editor.Text = text[..start] + command.ReplacementText + text[(start + length)..];
            editor.CaretOffset = start + command.ReplacementText.Length;
            editor.Select(start, command.ReplacementText.Length);
        });
        PublishSearchResultFormat(VexL.EditorSearchReplacedNextFormat, searchText);
        FindNext(editor, command, start + command.ReplacementText.Length, publishTextChanged);
    }

    private void ReplaceAll(
        TextEditor editor,
        EditorSearchCommand command,
        Action<Action> runTextMutation)
    {
        var text = editor.Text ?? string.Empty;
        var builder = new System.Text.StringBuilder(text.Length);
        var offset = 0;
        var count = 0;
        var searchText = command.SearchText;

        while (offset < text.Length)
        {
            var index = FindNextIndex(text, searchText, offset, command);
            if (index < 0)
            {
                builder.Append(text, offset, text.Length - offset);
                break;
            }

            builder.Append(text, offset, index - offset);
            builder.Append(command.ReplacementText);
            offset = index + searchText.Length;
            count++;
        }

        if (count == 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, searchText);
            return;
        }

        runTextMutation(() =>
        {
            editor.Text = builder.ToString();
            editor.CaretOffset = 0;
            editor.SelectionLength = 0;
        });
        PublishSearchResultFormat(VexL.EditorSearchReplacedAllFormat, count);
    }

    private void CountMatches(TextEditor editor, EditorSearchCommand command)
    {
        var matches = FindMatches(editor.Text ?? string.Empty, command);
        if (matches.Count == 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, command.SearchText);
            return;
        }

        var matchIndex = GetNextMatchIndex(matches, editor.CaretOffset);
        _eventBus.Publish(new EditorSearchResultCommand(
            _localizer.Format(VexL.EditorSearchMatchCountFormat, matches.Count, command.SearchText),
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

    private static List<int> FindMatches(string text, EditorSearchCommand command)
    {
        var searchText = command.SearchText;
        if (text.Length == 0 || searchText.Length == 0)
        {
            return [];
        }

        List<int> matches = [];
        var offset = 0;
        while (offset <= text.Length - searchText.Length)
        {
            var index = FindNextIndex(text, searchText, offset, command);
            if (index < 0)
            {
                break;
            }

            matches.Add(index);
            offset = index + Math.Max(1, searchText.Length);
        }

        return matches;
    }

    private static int FindNextIndex(string text, string searchText, int offset, EditorSearchCommand command)
    {
        var comparison = command.IsMatchCase
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;

        while (offset <= text.Length - searchText.Length)
        {
            var index = text.IndexOf(searchText, offset, comparison);
            if (index < 0)
            {
                return -1;
            }

            if (!command.IsWholeWord || IsWholeWordMatch(text, index, searchText.Length))
            {
                return index;
            }

            offset = index + Math.Max(1, searchText.Length);
        }

        return -1;
    }

    private static bool IsMatch(string selected, string searchText, EditorSearchCommand command)
    {
        var comparison = command.IsMatchCase
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;
        return selected.Equals(searchText, comparison);
    }

    private static bool IsWholeWordMatch(string text, int index, int length)
    {
        var before = index <= 0 ? '\0' : text[index - 1];
        var afterIndex = index + length;
        var after = afterIndex >= text.Length ? '\0' : text[afterIndex];
        return !IsWordCharacter(before) && !IsWordCharacter(after);
    }

    private static bool IsWordCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
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
}
