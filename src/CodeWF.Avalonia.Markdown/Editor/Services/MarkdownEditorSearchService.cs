using AvaloniaEdit;
using AvaloniaEdit.Document;
using System.Text;
using CodeWF.Avalonia.Markdown.Editor.Controls;

namespace CodeWF.Avalonia.Markdown.Editor.Services;

public sealed class MarkdownEditorSearchService : IMarkdownEditorSearchService
{
    private readonly IMarkdownEditorLocalizer _localizer;
    private MarkdownEditorSearchResultHandler _report = static (_, _, _) => { };
    private TextEditor? _observedEditor;
    private TextDocument? _indexedDocument;
    private MarkdownSearchIndex? _index;
    private MarkdownSearchHighlighter? _highlighter;

    internal MarkdownSearchHighlighter? Highlighter => _highlighter;

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
        EnsureHighlighter(editor);

        if (string.IsNullOrEmpty(command.SearchText))
        {
            _highlighter!.SetMatches([], -1);
            PublishSearchResult(MarkdownEditorText.EnterSearchTextFirst);
            return;
        }

        switch (command.Action)
        {
            case MarkdownEditorSearchAction.Count:
                CountMatches(editor, command);
                break;
            case MarkdownEditorSearchAction.FindNext:
                FindNext(
                    editor,
                    command,
                    editor.SelectionLength > 0
                        ? editor.SelectionStart + editor.SelectionLength
                        : editor.CaretOffset);
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
        var matches = GetMatches(editor, command);
        if (matches is null)
        {
            return;
        }

        var matchIndex = GetNextMatchIndex(matches, startOffset, out var wrapped);
        if (matchIndex < 0)
        {
            _highlighter!.SetMatches(matches, -1);
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        SelectMatch(editor, command, matches, matchIndex, wrapped);
    }

    private void FindPrevious(TextEditor editor, MarkdownEditorSearchCommand command, int startOffset)
    {
        var matches = GetMatches(editor, command);
        if (matches is null)
        {
            return;
        }

        var matchIndex = GetPreviousMatchIndex(matches, startOffset, out var wrapped);
        if (matchIndex < 0)
        {
            _highlighter!.SetMatches(matches, -1);
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        SelectMatch(editor, command, matches, matchIndex, wrapped);
    }

    private void SelectMatch(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
        IReadOnlyList<MarkdownSearchMatch> matches,
        int matchIndex,
        bool wrapped)
    {
        var match = matches[matchIndex];
        editor.Select(match.Index, match.Length);
        editor.CaretOffset = match.Index + match.Length;
        editor.TextArea.Caret.BringCaretToView();
        _highlighter!.SetMatches(matches, matchIndex);
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
        var matches = GetMatches(editor, command);
        if (matches is null || editor.Document is null)
        {
            return;
        }

        var start = Math.Clamp(editor.SelectionStart, 0, editor.Document.TextLength);
        var length = Math.Clamp(editor.SelectionLength, 0, editor.Document.TextLength - start);
        var currentIndex = -1;
        for (var index = 0; index < matches.Count; index++)
        {
            if (matches[index].Index == start && matches[index].Length == length)
            {
                currentIndex = index;
                break;
            }
        }

        if (currentIndex < 0)
        {
            FindNext(editor, command, editor.CaretOffset);
            return;
        }

        var currentMatch = matches[currentIndex];
        if (!TryGetReplacementText(currentMatch, command, out var replacementText))
        {
            return;
        }

        runTextMutation(() =>
        {
            editor.Document.Replace(start, currentMatch.Length, replacementText);
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
        var matches = GetMatches(editor, command);
        if (matches is null)
        {
            return;
        }

        if (matches.Count == 0)
        {
            _highlighter!.SetMatches(matches, -1);
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        var text = editor.Document!.Text;
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
            editor.Document.Replace(0, editor.Document.TextLength, builder.ToString());
            editor.CaretOffset = 0;
            editor.SelectionLength = 0;
        });
        PublishSearchResultFormat(MarkdownEditorText.SearchReplacedAllFormat, matches.Count);
        RefreshHighlights(editor, command, -1);
    }

    private void CountMatches(TextEditor editor, MarkdownEditorSearchCommand command)
    {
        var matches = GetMatches(editor, command);
        if (matches is null)
        {
            return;
        }

        if (matches.Count == 0)
        {
            _highlighter!.SetMatches(matches, -1);
            PublishSearchResultFormat(MarkdownEditorText.SearchNoMatchFormat, command.SearchText);
            return;
        }

        var matchIndex = GetNextMatchIndex(matches, editor.CaretOffset, out _);
        if (matchIndex < 0)
        {
            matchIndex = 0;
        }

        _highlighter!.SetMatches(matches, matchIndex);
        _report(
            _localizer.Format(MarkdownEditorText.SearchMatchCountFormat, matches.Count, command.SearchText),
            matchIndex + 1,
            matches.Count);
    }

    private IReadOnlyList<MarkdownSearchMatch>? GetMatches(
        TextEditor editor,
        MarkdownEditorSearchCommand command)
    {
        var document = editor.Document;
        if (document is null)
        {
            _highlighter!.SetMatches([], -1);
            return [];
        }

        if (!ReferenceEquals(document, _indexedDocument))
        {
            _indexedDocument = document;
            _index = new MarkdownSearchIndex(document);
        }

        var matches = _index!.GetMatches(
            command,
            out var failure,
            out var failureDetail);
        if (failure is not null)
        {
            _highlighter!.SetMatches([], -1);
            PublishSearchResultFormat(
                failure == MarkdownSearchFailure.TimedOut
                    ? MarkdownEditorText.SearchTimedOutFormat
                    : MarkdownEditorText.SearchInvalidRegexFormat,
                failureDetail ?? string.Empty);
            return null;
        }

        return matches;
    }

    private void RefreshHighlights(
        TextEditor editor,
        MarkdownEditorSearchCommand command,
        int currentIndex)
    {
        var matches = GetMatches(editor, command);
        if (matches is not null)
        {
            _highlighter!.SetMatches(matches, currentIndex);
        }
    }

    private void EnsureHighlighter(TextEditor editor)
    {
        if (!ReferenceEquals(editor, _observedEditor))
        {
            if (_observedEditor is not null)
            {
                _observedEditor.TextChanged -= OnEditorTextChanged;
                _observedEditor.DocumentChanged -= OnEditorDocumentChanged;
            }

            _observedEditor = editor;
            editor.TextChanged += OnEditorTextChanged;
            editor.DocumentChanged += OnEditorDocumentChanged;
            _highlighter?.Detach();
            _highlighter = new MarkdownSearchHighlighter(editor);
            _index = null;
            _indexedDocument = null;
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        _index = null;
        _indexedDocument = null;
        _highlighter?.SetMatches([], -1);
    }

    private void OnEditorDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        _index = null;
        _indexedDocument = null;
        _highlighter?.SetMatches([], -1);
    }

    private bool TryGetReplacementText(
        MarkdownSearchMatch match,
        MarkdownEditorSearchCommand command,
        out string replacementText)
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

    private void PublishSearchResult(MarkdownEditorText text) =>
        _report(_localizer.Get(text), 0, 0);

    private void PublishSearchResultFormat(MarkdownEditorText text, params object?[] args) =>
        _report(_localizer.Format(text, args), 0, 0);

    private static int GetNextMatchIndex(
        IReadOnlyList<MarkdownSearchMatch> matches,
        int startOffset,
        out bool wrapped)
    {
        wrapped = false;
        if (matches.Count == 0)
        {
            return -1;
        }

        var start = Math.Max(0, startOffset);
        for (var index = 0; index < matches.Count; index++)
        {
            if (matches[index].Index >= start)
            {
                return index;
            }
        }

        wrapped = true;
        return 0;
    }

    private static int GetPreviousMatchIndex(
        IReadOnlyList<MarkdownSearchMatch> matches,
        int startOffset,
        out bool wrapped)
    {
        wrapped = false;
        if (matches.Count == 0)
        {
            return -1;
        }

        var start = Math.Max(0, startOffset);
        for (var index = matches.Count - 1; index >= 0; index--)
        {
            if (matches[index].Index < start)
            {
                return index;
            }
        }

        wrapped = true;
        return matches.Count - 1;
    }
}
