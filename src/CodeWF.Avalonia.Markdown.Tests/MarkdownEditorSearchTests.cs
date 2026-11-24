using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class MarkdownSearchIndexTests
{
    [Fact]
    public void GetMatches_ReusesTextAndEvaluationForSameQueryAndVersion()
    {
        var document = new TextDocument("cat scatter cat");
        var index = new MarkdownSearchIndex(document);

        var first = index.GetMatches(Command("cat", MarkdownEditorSearchAction.Count), out _, out _);
        var second = index.GetMatches(Command("cat", MarkdownEditorSearchAction.FindNext), out _, out _);

        Assert.Same(first, second);
        Assert.Equal(3, second.Count);
        Assert.Equal(1, index.TextReadCount);
        Assert.Equal(1, index.EvaluationCount);
    }

    [Fact]
    public void GetMatches_OptionChangeReevaluatesWithoutRereadingText()
    {
        var document = new TextDocument("Cat cat");
        var index = new MarkdownSearchIndex(document);

        index.GetMatches(Command("cat", MarkdownEditorSearchAction.Count), out _, out _);
        var matches = index.GetMatches(
            Command("cat", MarkdownEditorSearchAction.Count, isMatchCase: true),
            out _,
            out _);

        Assert.Single(matches);
        Assert.Equal(1, index.TextReadCount);
        Assert.Equal(2, index.EvaluationCount);
    }

    [Fact]
    public void GetMatches_DocumentEditInvalidatesCachedOffsets()
    {
        var document = new TextDocument("cat cat");
        var index = new MarkdownSearchIndex(document);
        index.GetMatches(Command("cat", MarkdownEditorSearchAction.Count), out _, out _);

        document.Insert(0, "x");
        var matches = index.GetMatches(Command("cat", MarkdownEditorSearchAction.Count), out _, out _);

        Assert.Equal(new[] { 1, 5 }, matches.Select(match => match.Index));
        Assert.Equal(2, index.TextReadCount);
        Assert.Equal(2, index.EvaluationCount);
    }

    [Fact]
    public void GetMatches_WholeWordUsesOneDefinitionForCountAndNavigation()
    {
        var document = new TextDocument("cat scatter cat_cat cat.");
        var index = new MarkdownSearchIndex(document);

        var matches = index.GetMatches(
            Command("cat", MarkdownEditorSearchAction.Count, isWholeWord: true),
            out _,
            out _);

        Assert.Equal(new[] { 0, 20 }, matches.Select(match => match.Index));
        Assert.All(matches, match => Assert.Equal("cat", document.GetText(match.Index, match.Length)));
    }

    [Fact]
    public void GetMatches_RegexRetainsGroupsForReplacement()
    {
        var document = new TextDocument("a1 b22");
        var index = new MarkdownSearchIndex(document);

        var matches = index.GetMatches(
            Command(@"([a-z])(\d+)", MarkdownEditorSearchAction.ReplaceAll, isRegex: true, replacement: "$2$1"),
            out _,
            out _);

        Assert.Equal(2, matches.Count);
        Assert.Equal("1a", matches[0].RegexMatch!.Result("$2$1"));
        Assert.Equal("22b", matches[1].RegexMatch!.Result("$2$1"));
    }

    [Fact]
    public void GetMatches_InvalidRegexFailureIsCachedForDocumentVersion()
    {
        var document = new TextDocument("text");
        var index = new MarkdownSearchIndex(document);
        var command = Command("[", MarkdownEditorSearchAction.Count, isRegex: true);

        index.GetMatches(command, out var firstFailure, out _);
        index.GetMatches(command, out var secondFailure, out _);

        Assert.Equal(MarkdownSearchFailure.InvalidRegex, firstFailure);
        Assert.Equal(firstFailure, secondFailure);
        Assert.Equal(1, index.EvaluationCount);
    }

    [Fact]
    public void GetMatches_RegexTimeoutIsDistinctAndCached()
    {
        var document = new TextDocument(new string('a', 32) + "!");
        var index = new MarkdownSearchIndex(document);
        var command = Command(@"^(a+)+$", MarkdownEditorSearchAction.Count, isRegex: true);

        index.GetMatches(command, out var firstFailure, out var firstDetail);
        index.GetMatches(command, out var secondFailure, out var secondDetail);

        Assert.Equal(MarkdownSearchFailure.TimedOut, firstFailure);
        Assert.Equal(firstFailure, secondFailure);
        Assert.False(string.IsNullOrWhiteSpace(firstDetail));
        Assert.Equal(firstDetail, secondDetail);
        Assert.Equal(1, index.EvaluationCount);
    }

    [Fact]
    public void GetMatches_ZeroLengthRegexMatchesAreIgnored()
    {
        var document = new TextDocument("abc");
        var index = new MarkdownSearchIndex(document);

        var matches = index.GetMatches(
            Command(@"^|$", MarkdownEditorSearchAction.Count, isRegex: true),
            out _,
            out _);

        Assert.Empty(matches);
    }

    private static MarkdownEditorSearchCommand Command(
        string text,
        MarkdownEditorSearchAction action,
        bool isMatchCase = false,
        bool isWholeWord = false,
        bool isRegex = false,
        string replacement = "") =>
        new(action, text, replacement, isMatchCase, isWholeWord, isRegex);
}

[Collection("AvaloniaPlatform")]
public sealed class MarkdownEditorSearchServiceTests(AvaloniaPlatformFixture platform) : IDisposable
{
    private readonly List<Window> _windows = [];

    public void Dispose() => platform.Run(() =>
    {
        foreach (var window in _windows) window.Close();
    });
    [Fact]
    public void Count_PersistsAllHighlightsAndMarksCurrentMatch()
    {
        platform.Run(() =>
        {
            var (view, service) = CreateEditor("cat scatter cat");
            var (message, current, total) = Search(service, view, Command("cat", MarkdownEditorSearchAction.Count));
            var highlighter = GetHighlighter(view);

            Assert.Equal(3, total);
            Assert.Equal(1, current);
            Assert.Contains("3", message);
            Assert.Equal(3, highlighter.MatchCount);
            Assert.Equal(0, highlighter.CurrentMatchIndex);
            Assert.NotSame(highlighter.BackgroundBrushAt(0), highlighter.BackgroundBrushAt(5));
            Assert.NotNull(highlighter.BackgroundBrushAt(0));
            Assert.NotNull(highlighter.BackgroundBrushAt(5));
            Assert.Null(highlighter.BackgroundBrushAt(3));

            view.Editor.TextArea.TextView.EnsureVisualLines();
            var line = view.Editor.TextArea.TextView.VisualLines[0];
            var highlights = highlighter.GetHighlightRectangles(line);
            Assert.Equal(3, highlights.Count);
            Assert.All(highlights, highlight =>
            {
                Assert.NotNull(highlight.Brush);
                Assert.True(highlight.Bounds.Width > 0);
                Assert.True(highlight.Bounds.Height > 0);
            });
        });
    }

    [Fact]
    public void FindNext_AndPrevious_NavigateAndWrap()
    {
        platform.Run(() =>
        {
            var (view, service) = CreateEditor("cat cat cat");
            Search(service, view, Command("cat", MarkdownEditorSearchAction.FindNext));
            var highlighter = service.Highlighter!;
            Assert.Contains(highlighter, view.Editor.TextArea.TextView.BackgroundRenderers);
            Assert.Equal("cat", view.SelectedText);
            Assert.Equal(0, highlighter.CurrentMatchIndex);

            Search(service, view, Command("cat", MarkdownEditorSearchAction.FindNext));
            Assert.Equal(4, view.Selection.Start);
            Assert.Equal(1, highlighter.CurrentMatchIndex);

            Search(service, view, Command("cat", MarkdownEditorSearchAction.FindPrevious));
            Assert.Equal(0, view.Selection.Start);
            Search(service, view, Command("cat", MarkdownEditorSearchAction.FindPrevious));
            Assert.Equal(8, view.Selection.Start);
            Assert.Equal(2, highlighter.CurrentMatchIndex);
        });
    }

    [Fact]
    public void TextEdit_ClearsHighlightsAndNextSearchUsesNewOffsets()
    {
        platform.Run(() =>
        {
            var (view, service) = CreateEditor("cat cat");
            Search(service, view, Command("cat", MarkdownEditorSearchAction.Count));
            var highlighter = GetHighlighter(view);
            Assert.Equal(2, highlighter.MatchCount);

            view.Editor.Document!.Insert(0, "x");
            Assert.Equal(0, highlighter.MatchCount);

            Search(service, view, Command("cat", MarkdownEditorSearchAction.Count));
            Assert.Equal(2, highlighter.MatchCount);
            Assert.NotNull(highlighter.BackgroundBrushAt(1));
            Assert.Null(highlighter.BackgroundBrushAt(0));
        });
    }

    [Fact]
    public void ReplaceNext_WithoutSelectionFindsBeforeReplacingAndAdvances()
    {
        platform.Run(() =>
        {
            var (view, service) = CreateEditor("cat cat");
            var command = Command("cat", MarkdownEditorSearchAction.ReplaceNext, replacement: "dog");

            Search(service, view, command);
            Assert.Equal("cat cat", view.Text);
            Assert.Equal(0, view.Selection.Start);

            Search(service, view, command);
            Assert.Equal("dog cat", view.Text);
            Assert.Equal(4, view.Selection.Start);
            Assert.Equal("cat", view.SelectedText);
            Assert.Equal(1, GetHighlighter(view).MatchCount);
        });
    }

    [Fact]
    public void ReplaceAll_UsesRegexGroupsAndRefreshesRemainingMatches()
    {
        platform.Run(() =>
        {
            var (view, service) = CreateEditor("a1 b22");
            var (message, _, _) = Search(
                service,
                view,
                Command("([a-z])(\\d+)", MarkdownEditorSearchAction.ReplaceAll, isRegex: true, replacement: "$2$1"));

            Assert.Equal("1a 22b", view.Text);
            Assert.Contains("2", message);
            Assert.Equal(0, GetHighlighter(view).MatchCount);
        });
    }

    [Fact]
    public void InvalidRegex_ReportsFailureAndClearsHighlights()
    {
        platform.Run(() =>
        {
            var (view, service) = CreateEditor("cat");
            Search(service, view, Command("cat", MarkdownEditorSearchAction.Count));
            var (message, current, total) = Search(
                service,
                view,
                Command("[", MarkdownEditorSearchAction.Count, isRegex: true));

            Assert.StartsWith(MarkdownEditorText.SearchInvalidRegexFormat.ToString(), message);
            Assert.Equal(0, current);
            Assert.Equal(0, total);
            Assert.Equal(0, GetHighlighter(view).MatchCount);
        });
    }

    private (MarkdownEditorView View, MarkdownEditorSearchService Service) CreateEditor(string text)
    {
        Application.Current!.Resources["FontFamilyNormal"] = FontFamily.Default;
        Application.Current.Resources["ContentControlThemeFontFamily"] = FontFamily.Default;
        Application.Current.Resources["ControlContentThemeFontFamily"] = FontFamily.Default;
        Application.Current.Resources["FontSizeNormal"] = 14d;
        Application.Current.Resources["ControlContentThemeFontSize"] = 14d;
        Application.Current.Resources["FontWeightNormal"] = FontWeight.Normal;
        Application.Current.Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(
            new Uri("avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml"))
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml")
        });
        var localizer = new TestSearchLocalizer();
        var service = new MarkdownEditorSearchService(localizer);
        var view = new MarkdownEditorView(localizer: localizer, searchService: service) { Text = text };
        var window = new Window { Width = 600, Height = 400, Content = view };
        _windows.Add(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (view, service);
    }

    private static MarkdownSearchHighlighter GetHighlighter(MarkdownEditorView view) =>
        view.Editor.TextArea.TextView.BackgroundRenderers.OfType<MarkdownSearchHighlighter>().Single();

    private static (string Message, int Current, int Total) Search(
        MarkdownEditorSearchService service,
        MarkdownEditorView view,
        MarkdownEditorSearchCommand command)
    {
        var message = string.Empty;
        var current = -1;
        var total = -1;
        service.Search(view.Editor, command, static action => action(), (text, index, count) =>
        {
            message = text;
            current = index;
            total = count;
        });
        return (message, current, total);
    }

    private static MarkdownEditorSearchCommand Command(
        string text,
        MarkdownEditorSearchAction action,
        bool isRegex = false,
        string replacement = "") =>
        new(action, text, replacement, isRegex: isRegex);

    private sealed class TestSearchLocalizer : IMarkdownEditorLocalizer
    {
        public string Get(MarkdownEditorText text) => text.ToString();

        public string Format(MarkdownEditorText text, params object?[] args) =>
            $"{text}:{string.Join('|', args)}";
    }
}
