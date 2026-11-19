using AvaloniaEdit;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeWF.Avalonia.Markdown.Editor.Services;

public sealed class MarkdownEditorSearchService : IMarkdownEditorSearchService
{
    private readonly IMarkdownEditorLocalizer _localizer;
    private MarkdownEditorSearchResultHandler _report = static (_, _, _) => { };

    public MarkdownEditorSearchService(IMarkdownEditorLocalizer localizer)
    {
        _localizer = localizer;
    }

    public void Search(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
        Action<Action> runTextMutation,
        MarkdownEditorSearchResultHandler report)
    {
        _report = report;

        if (string.IsNullOrEmpty(command.SearchText))
        {
            PublishSearchResult(MarkdownEditorText.EnterSearchTextFirst);
            return;
        }

        switch (command.Action)
        {
            case MarkdownEditorSearchAction.Count:
                CountMatches(editor, command);
                break;
            case MarkdownEditorSearchAction.FindNext:
                FindNext(editor, command, editor.CaretOffset + Math.Max(0, editor.SelectionLength));
                break;
            case MarkdownEditorSearchAction.FindPrevious:
                FindPrevious(editor, command, editor.SelectionStart);
                break;
            case MarkdownEditorSearchAction.ReplaceNext:
                ReplaceNext(editor, command, runTextMutation);
                break;
            case MarkdownEditorSearchAction.ReplaceAll:
                ReplaceAll(editor, command, runTextMutation);
                break;
        }
    }

    private void FindNext(TextEditor editor, MarkdownEditorSearchCommand command, int startOffset)
    {
        if (editor.Document is null)
        {
            return;
        }

        var matches = FindMatches(editor.Text ?? string.Empty, command);
        if (matches is null)
        {
            return;
        }

        var matchIndex = GetNextMatchIndex(matches, startOffset, out var wrapped);
        if (matchIndex < 0)
        {
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        SelectMatch(editor, command, matches, matchIndex, wrapped);
    }

    /// <summary>
    /// 向上查找。起点取选区起点，只接受严格在它之前开始的命中，
    /// 因此停在某个命中上再按「上一个」会跳到它前一个，而不是原地不动；全部落空时回绕到文末命中。
    /// </summary>
    private void FindPrevious(TextEditor editor, MarkdownEditorSearchCommand command, int startOffset)
    {
        if (editor.Document is null)
        {
            return;
        }

        var matches = FindMatches(editor.Text ?? string.Empty, command);
        if (matches is null)
        {
            return;
        }

        var matchIndex = GetPreviousMatchIndex(matches, startOffset, out var wrapped);
        if (matchIndex < 0)
        {
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        SelectMatch(editor, command, matches, matchIndex, wrapped);
    }

    private void SelectMatch(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
        IReadOnlyList<SearchMatch> matches,
        int matchIndex,
        bool wrapped)
    {
        var match = matches[matchIndex];
        editor.Select(match.Index, match.Length);
        editor.CaretOffset = match.Index + match.Length;
        editor.TextArea.Caret.BringCaretToView();
        var line = editor.Document!.GetLineByOffset(match.Index).LineNumber;
        var messageKey = wrapped
            ? MarkdownEditorText.SearchFoundWrappedOnLineFormat
            : MarkdownEditorText.SearchFoundOnLineFormat;
        _report(
            _localizer.Format(messageKey, command.SearchText, line, matchIndex + 1, matches.Count),
            matchIndex + 1,
            matches.Count);
    }

    private void ReplaceNext(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
        Action<Action> runTextMutation)
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
            FindNext(editor, command, editor.CaretOffset);
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
        PublishSearchResultFormat(MarkdownEditorText.SearchReplacedNextFormat, command.SearchText);
        FindNext(editor, command, start + replacementText.Length);
    }

    private void ReplaceAll(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
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
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
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
        PublishSearchResultFormat(MarkdownEditorText.SearchReplacedAllFormat, matches.Count);
    }

    private void CountMatches(TextEditor editor, MarkdownEditorSearchCommand command)
    {
        var matchCount = CountMatches(editor.Text ?? string.Empty, command, editor.CaretOffset, out var matchIndex);
        if (matchCount is null)
        {
            return;
        }

        if (matchCount == 0)
        {
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        _report(
            _localizer.Format(MarkdownEditorText.SearchMatchCountFormat, matchCount, command.SearchText),
            matchIndex + 1,
            matchCount.Value);
    }

    private void PublishSearchResult(MarkdownEditorText text)
    {
        _report(_localizer.Get(text), 0, 0);
    }

    private void PublishSearchResultFormat(MarkdownEditorText text, params object?[] args)
    {
        _report(_localizer.Format(text, args), 0, 0);
    }

    private List<SearchMatch>? FindMatches(string text, MarkdownEditorSearchCommand command)
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

    private List<SearchMatch>? FindRegexMatches(string text, MarkdownEditorSearchCommand command)
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
            PublishSearchResultFormat(MarkdownEditorText.SearchInvalidRegexFormat, exception.Message);
            return null;
        }

        return matches;
    }

    private int? CountMatches(string text, MarkdownEditorSearchCommand command, int startOffset, out int matchIndex)
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

    private int? CountRegexMatches(string text, MarkdownEditorSearchCommand command, int startOffset, out int matchIndex)
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
            PublishSearchResultFormat(MarkdownEditorText.SearchInvalidRegexFormat, exception.Message);
            return null;
        }

        return count;
    }

    private static int CountLiteralMatches(string text, MarkdownEditorSearchCommand command, int startOffset, out int matchIndex)
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

    private static int FindNextIndex(string text, string searchText, int offset, MarkdownEditorSearchCommand command)
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

    private SearchMatch? FindCurrentSelectionMatch(string text, MarkdownEditorSearchCommand command, int start, int length)
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
                PublishSearchResultFormat(MarkdownEditorText.SearchInvalidRegexFormat, exception.Message);
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

    private bool TryGetReplacementText(SearchMatch match, MarkdownEditorSearchCommand command, out string replacementText)
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
            PublishSearchResultFormat(MarkdownEditorText.SearchInvalidRegexFormat, exception.Message);
            return false;
        }
    }

    private Regex? TryCreateRegex(MarkdownEditorSearchCommand command)
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
            PublishSearchResultFormat(MarkdownEditorText.SearchInvalidRegexFormat, exception.Message);
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

    private static int GetPreviousMatchIndex(IReadOnlyList<SearchMatch> matches, int startOffset, out bool wrapped)
    {
        wrapped = false;
        if (matches.Count == 0)
        {
            return -1;
        }

        var start = Math.Max(0, startOffset);
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            if (matches[i].Index < start)
            {
                return i;
            }
        }

        wrapped = true;
        return matches.Count - 1;
    }

    private readonly record struct SearchMatch(int Index, int Length, Match? RegexMatch = null);
}
