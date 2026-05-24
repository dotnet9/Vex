using AvaloniaEdit;
using System.Text;
using System.Text.RegularExpressions;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownEditorSearchService : IMarkdownEditorSearchService
{
    private readonly IAppLocalizer _localizer;

    public MarkdownEditorSearchService(IAppLocalizer localizer)
    {
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
        var matches = FindMatches(text, command);
        if (matches is null)
        {
            return;
        }

        var matchIndex = GetNextMatchIndex(matches, startOffset, out var wrapped);
        if (matchIndex < 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, command.SearchText);
            return;
        }

        var match = matches[matchIndex];
        editor.Select(match.Index, match.Length);
        editor.CaretOffset = match.Index + match.Length;
        editor.TextArea.Caret.BringCaretToView();
        editor.Focus();
        var line = editor.Document.GetLineByOffset(match.Index).LineNumber;
        var messageKey = wrapped
            ? VexL.EditorSearchFoundWrappedOnLineFormat
            : VexL.EditorSearchFoundOnLineFormat;
        CodeWF.EventBus.EventBus.Default.Publish(new EditorSearchResultCommand(
            _localizer.Format(messageKey, command.SearchText, line, matchIndex + 1, matches.Count),
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
        if (editor.Document is null)
        {
            return;
        }

        var text = editor.Text ?? string.Empty;
        var start = Math.Clamp(editor.SelectionStart, 0, text.Length);
        var length = Math.Clamp(editor.SelectionLength, 0, text.Length - start);
        var currentMatch = FindCurrentSelectionMatch(text, command, start, length);
        if (currentMatch is null)
        {
            FindNext(editor, command, editor.CaretOffset, publishTextChanged);
            return;
        }

        if (!TryGetReplacementText(currentMatch.Value, command, out var replacementText))
        {
            return;
        }

        runTextMutation(() =>
        {
            editor.Document.Replace(start, currentMatch.Value.Length, replacementText);
            editor.CaretOffset = start + replacementText.Length;
            editor.Select(start, replacementText.Length);
        });
        PublishSearchResultFormat(VexL.EditorSearchReplacedNextFormat, command.SearchText);
        FindNext(editor, command, start + replacementText.Length, publishTextChanged);
    }

    private void ReplaceAll(
        TextEditor editor,
        EditorSearchCommand command,
        Action<Action> runTextMutation)
    {
        var text = editor.Text ?? string.Empty;
        var matches = FindMatches(text, command);
        if (matches is null)
        {
            return;
        }

        if (matches.Count == 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, command.SearchText);
            return;
        }

        var builder = new StringBuilder(text.Length);
        var offset = 0;
        foreach (var match in matches)
        {
            if (!TryGetReplacementText(match, command, out var replacementText))
            {
                return;
            }

            builder.Append(text, offset, match.Index - offset);
            builder.Append(replacementText);
            offset = match.Index + match.Length;
        }
        builder.Append(text, offset, text.Length - offset);

        runTextMutation(() =>
        {
            if (editor.Document is null)
            {
                return;
            }

            editor.Document.Replace(0, editor.Document.TextLength, builder.ToString());
            editor.CaretOffset = 0;
            editor.SelectionLength = 0;
        });
        PublishSearchResultFormat(VexL.EditorSearchReplacedAllFormat, matches.Count);
    }

    private void CountMatches(TextEditor editor, EditorSearchCommand command)
    {
        var matchCount = CountMatches(editor.Text ?? string.Empty, command, editor.CaretOffset, out var matchIndex);
        if (matchCount is null)
        {
            return;
        }

        if (matchCount == 0)
        {
            PublishSearchResultFormat(VexL.EditorSearchNoMatchFormat, command.SearchText);
            return;
        }

        CodeWF.EventBus.EventBus.Default.Publish(new EditorSearchResultCommand(
            _localizer.Format(VexL.EditorSearchMatchCountFormat, matchCount, command.SearchText),
            matchIndex + 1,
            matchCount.Value));
    }

    private void PublishSearchResult(string key)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new EditorSearchResultCommand(_localizer.Get(key)));
    }

    private void PublishSearchResultFormat(string key, params object?[] args)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new EditorSearchResultCommand(_localizer.Format(key, args)));
    }

    private List<SearchMatch>? FindMatches(string text, EditorSearchCommand command)
    {
        var searchText = command.SearchText;
        if (text.Length == 0 || searchText.Length == 0)
        {
            return [];
        }

        if (command.IsRegex)
        {
            return FindRegexMatches(text, command);
        }

        List<SearchMatch> matches = [];
        var offset = 0;
        while (offset <= text.Length - searchText.Length)
        {
            var index = FindNextIndex(text, searchText, offset, command);
            if (index < 0)
            {
                break;
            }

            matches.Add(new SearchMatch(index, searchText.Length));
            offset = index + searchText.Length;
        }

        return matches;
    }

    private List<SearchMatch>? FindRegexMatches(string text, EditorSearchCommand command)
    {
        var regex = TryCreateRegex(command);
        if (regex is null)
        {
            return null;
        }

        List<SearchMatch> matches = [];
        try
        {
            foreach (Match match in regex.Matches(text))
            {
                if (!match.Success || match.Length == 0)
                {
                    continue;
                }

                if (!command.IsWholeWord || IsWholeWordMatch(text, match.Index, match.Length))
                {
                    matches.Add(new SearchMatch(match.Index, match.Length, match));
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            PublishSearchResultFormat(VexL.EditorSearchInvalidRegexFormat, exception.Message);
            return null;
        }

        return matches;
    }

    private int? CountMatches(string text, EditorSearchCommand command, int startOffset, out int matchIndex)
    {
        matchIndex = -1;
        var searchText = command.SearchText;
        if (text.Length == 0 || searchText.Length == 0)
        {
            return 0;
        }

        var count = command.IsRegex
            ? CountRegexMatches(text, command, startOffset, out matchIndex)
            : CountLiteralMatches(text, command, startOffset, out matchIndex);
        if (count is > 0 && matchIndex < 0)
        {
            matchIndex = 0;
        }

        return count;
    }

    private int? CountRegexMatches(string text, EditorSearchCommand command, int startOffset, out int matchIndex)
    {
        matchIndex = -1;
        var regex = TryCreateRegex(command);
        if (regex is null)
        {
            return null;
        }

        var count = 0;
        try
        {
            foreach (Match match in regex.Matches(text))
            {
                if (!match.Success || match.Length == 0)
                {
                    continue;
                }

                if (command.IsWholeWord && !IsWholeWordMatch(text, match.Index, match.Length))
                {
                    continue;
                }

                if (matchIndex < 0 && match.Index >= startOffset)
                {
                    matchIndex = count;
                }

                count++;
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            PublishSearchResultFormat(VexL.EditorSearchInvalidRegexFormat, exception.Message);
            return null;
        }

        return count;
    }

    private static int CountLiteralMatches(string text, EditorSearchCommand command, int startOffset, out int matchIndex)
    {
        matchIndex = -1;
        var count = 0;
        var offset = 0;
        while (offset <= text.Length - command.SearchText.Length)
        {
            var index = FindNextIndex(text, command.SearchText, offset, command);
            if (index < 0)
            {
                break;
            }

            if (matchIndex < 0 && index >= startOffset)
            {
                matchIndex = count;
            }

            count++;
            offset = index + command.SearchText.Length;
        }

        return count;
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

    private SearchMatch? FindCurrentSelectionMatch(string text, EditorSearchCommand command, int start, int length)
    {
        if (length <= 0)
        {
            return null;
        }

        if (command.IsRegex)
        {
            var regex = TryCreateRegex(command);
            if (regex is null)
            {
                return null;
            }

            try
            {
                var match = regex.Match(text, start);
                if (!match.Success || match.Index != start || match.Length != length || match.Length == 0)
                {
                    return null;
                }

                return command.IsWholeWord && !IsWholeWordMatch(text, start, length)
                    ? null
                    : new SearchMatch(start, length, match);
            }
            catch (RegexMatchTimeoutException exception)
            {
                PublishSearchResultFormat(VexL.EditorSearchInvalidRegexFormat, exception.Message);
                return null;
            }
        }

        var selected = text.Substring(start, length);
        var comparison = command.IsMatchCase
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;
        if (!selected.Equals(command.SearchText, comparison))
        {
            return null;
        }

        return command.IsWholeWord && !IsWholeWordMatch(text, start, length)
            ? null
            : new SearchMatch(start, length);
    }

    private bool TryGetReplacementText(SearchMatch match, EditorSearchCommand command, out string replacementText)
    {
        replacementText = command.ReplacementText;
        if (!command.IsRegex || match.RegexMatch is null)
        {
            return true;
        }

        try
        {
            replacementText = match.RegexMatch.Result(command.ReplacementText);
            return true;
        }
        catch (ArgumentException exception)
        {
            PublishSearchResultFormat(VexL.EditorSearchInvalidRegexFormat, exception.Message);
            return false;
        }
    }

    private Regex? TryCreateRegex(EditorSearchCommand command)
    {
        var options = RegexOptions.Multiline;
        if (!command.IsMatchCase)
        {
            options |= RegexOptions.IgnoreCase;
        }

        try
        {
            return new Regex(command.SearchText, options, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException exception)
        {
            PublishSearchResultFormat(VexL.EditorSearchInvalidRegexFormat, exception.Message);
            return null;
        }
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

    private static int GetNextMatchIndex(IReadOnlyList<SearchMatch> matches, int startOffset)
    {
        return GetNextMatchIndex(matches, startOffset, out _);
    }

    private static int GetNextMatchIndex(IReadOnlyList<SearchMatch> matches, int startOffset, out bool wrapped)
    {
        wrapped = false;
        if (matches.Count == 0)
        {
            return -1;
        }

        var start = Math.Max(0, startOffset);
        for (var i = 0; i < matches.Count; i++)
        {
            if (matches[i].Index >= start)
            {
                return i;
            }
        }

        wrapped = true;
        return 0;
    }

    private readonly record struct SearchMatch(int Index, int Length, Match? RegexMatch = null);
}
