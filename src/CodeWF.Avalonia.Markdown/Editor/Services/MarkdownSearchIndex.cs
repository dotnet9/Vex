using AvaloniaEdit;
using AvaloniaEdit.Document;
using System.Text.RegularExpressions;

namespace CodeWF.Avalonia.Markdown.Editor.Services;

internal enum MarkdownSearchFailure
{
    InvalidRegex,
    TimedOut
}

internal readonly record struct MarkdownSearchMatch(int Index, int Length, Match? RegexMatch = null);

internal sealed class MarkdownSearchIndex(TextDocument document)
{
    private string? _text;
    private ITextSourceVersion? _version;
    private string? _searchText;
    private bool _isMatchCase;
    private bool _isWholeWord;
    private bool _isRegex;
    private List<MarkdownSearchMatch> _matches = [];
    private MarkdownSearchFailure? _failure;
    private string? _failureDetail;

    internal int TextReadCount { get; private set; }
    internal int EvaluationCount { get; private set; }

    internal IReadOnlyList<MarkdownSearchMatch> GetMatches(
        MarkdownEditorSearchCommand command,
        out MarkdownSearchFailure? failure,
        out string? failureDetail)
    {
        var version = document.Version;
        if (!ReferenceEquals(version, _version))
        {
            _version = version;
            _text = document.Text;
            TextReadCount++;
            _searchText = null;
        }

        if (!string.Equals(_searchText, command.SearchText, StringComparison.Ordinal)
            || _isMatchCase != command.IsMatchCase
            || _isWholeWord != command.IsWholeWord
            || _isRegex != command.IsRegex)
        {
            _searchText = command.SearchText;
            _isMatchCase = command.IsMatchCase;
            _isWholeWord = command.IsWholeWord;
            _isRegex = command.IsRegex;
            Evaluate(command);
        }

        failure = _failure;
        failureDetail = _failureDetail;
        return _matches;
    }

    private void Evaluate(MarkdownEditorSearchCommand command)
    {
        EvaluationCount++;
        _matches = [];
        _failure = null;
        _failureDetail = null;
        var text = _text ?? string.Empty;
        var searchText = command.SearchText;
        if (text.Length == 0 || searchText.Length == 0)
        {
            return;
        }

        if (!command.IsRegex)
        {
            FindLiteralMatches(text, command, _matches);
            return;
        }

        var regex = TryCreateRegex(command, out _failure, out _failureDetail);
        if (regex is null)
        {
            return;
        }

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
                    _matches.Add(new MarkdownSearchMatch(match.Index, match.Length, match));
                }
            }
        }
        catch (RegexMatchTimeoutException exception)
        {
            _matches = [];
            _failure = MarkdownSearchFailure.TimedOut;
            _failureDetail = exception.Message;
        }
    }

    private static void FindLiteralMatches(
        string text,
        MarkdownEditorSearchCommand command,
        List<MarkdownSearchMatch> matches)
    {
        var comparison = command.IsMatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;
        var offset = 0;
        while (offset <= text.Length - command.SearchText.Length)
        {
            var index = text.IndexOf(command.SearchText, offset, comparison);
            if (index < 0)
            {
                break;
            }

            if (!command.IsWholeWord || IsWholeWordMatch(text, index, command.SearchText.Length))
            {
                matches.Add(new MarkdownSearchMatch(index, command.SearchText.Length));
            }

            offset = index + command.SearchText.Length;
        }
    }

    private static Regex? TryCreateRegex(
        MarkdownEditorSearchCommand command,
        out MarkdownSearchFailure? failure,
        out string? failureDetail)
    {
        failure = null;
        failureDetail = null;
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
            failure = MarkdownSearchFailure.InvalidRegex;
            failureDetail = exception.Message;
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

    private static bool IsWordCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '_';
}
