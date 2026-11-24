using System.Reflection;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerFormulaSelectionTests(AvaloniaPlatformFixture platform)
{
    private const string Document = "前缀 $x^2+alpha$ 后缀\n\n$$\nx+y\n$$";
    private const string InlineLatex = "x^2+alpha";
    private const string BlockLatex = "x+y";

    private static readonly BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public void Find_HighlightsInlineAndBlockFormulaControls()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window);
            try
            {
                Render(viewer, window);
                var formulas = FormulaControls(viewer);
                var inline = formulas.Single(control => MarkdownFormulaState.GetText(control) == InlineLatex);
                var block = formulas.Single(control => MarkdownFormulaState.GetText(control) == BlockLatex);

                Assert.Equal(1, viewer.Find("alpha"));
                Assert.Same(viewer.SearchHighlightBrush, MarkdownFormulaState.GetSearchBrush(inline));
                Assert.Null(MarkdownFormulaState.GetSearchBrush(block));

                viewer.FindNext();
                Assert.Same(viewer.SearchCurrentBrush, MarkdownFormulaState.GetSearchBrush(inline));

                viewer.ClearSearch();
                Assert.Null(MarkdownFormulaState.GetSearchBrush(inline));
                Assert.Equal(0, viewer.MatchCount);

                Assert.Equal(1, viewer.Find(BlockLatex));
                Assert.Same(viewer.SearchHighlightBrush, MarkdownFormulaState.GetSearchBrush(block));

                Assert.Equal(1, viewer.Find("后缀"));
                Assert.Contains(viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
                    .SelectMany(block => block.Inlines?.OfType<global::Avalonia.Controls.Documents.Run>() ?? []),
                    run => run.Text?.Contains("后缀") == true && ReferenceEquals(run.Background, viewer.SearchHighlightBrush));
            }
            finally
            {
                viewer.ClearSearch();
                window.Close();
                MarkdownViewer.RegisterMathViewFactory(null);
            }
        });
    }

    [Fact]
    public void FindNext_CurrentBrushWinsWhenFormulaContainsMultipleMatches()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window, "$alpha+alpha$");
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var formula = FormulaControls(viewer).Single(control => MarkdownFormulaState.GetText(control) == "alpha+alpha");

                Assert.Equal(2, viewer.Find("alpha"));
                viewer.FindNext();
                Assert.Same(viewer.SearchCurrentBrush, MarkdownFormulaState.GetSearchBrush(formula));
                viewer.FindPrevious();
                Assert.Same(viewer.SearchCurrentBrush, MarkdownFormulaState.GetSearchBrush(formula));
            }
            finally
            {
                viewer.ClearSearch();
                window.Close();
                MarkdownViewer.RegisterMathViewFactory(null);
            }
        });
    }

    [Fact]
    public void DragSelection_ProjectsFormulaGeometryAndCopiesLatex()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window);
            try
            {
                Render(viewer, window);
                var inline = FormulaControls(viewer).Single(control => MarkdownFormulaState.GetText(control) == InlineLatex);
                var controller = SelectionController(viewer);
                var host = DocumentHost(viewer);

                SelectControl(controller, host, inline, 0.25, 0.75);
                UpdateSelectionState(viewer);
                var start = (int)Math.Round(InlineLatex.Length * 0.25);
                var end = (int)Math.Round(InlineLatex.Length * 0.75);
                Assert.Equal(start, MarkdownFormulaState.GetSelectionStart(inline));
                Assert.Equal(end, MarkdownFormulaState.GetSelectionEnd(inline));
                Assert.Equal(InlineLatex[start..end], viewer.SelectedText);

                controller.Clear();
                UpdateSelectionState(viewer);
                Assert.Equal(-1, MarkdownFormulaState.GetSelectionStart(inline));
                Assert.Equal(-1, MarkdownFormulaState.GetSelectionEnd(inline));
                Assert.False(viewer.HasSelection);

                var block = FormulaControls(viewer).Single(control => MarkdownFormulaState.GetText(control) == BlockLatex);
                SelectControl(controller, host, block, 0, 1);
                UpdateSelectionState(viewer);
                Assert.Equal(BlockLatex, viewer.SelectedText);
            }
            finally
            {
                window.Close();
                MarkdownViewer.RegisterMathViewFactory(null);
            }
        });
    }

    [Fact]
    public void DragSelectionAcrossFormula_KeepsSurroundingRunsSelectable()
    {
        platform.Run(() =>
        {
            var viewer = CreateViewer(out var window);
            try
            {
                Render(viewer, window);
                var text = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
                    .Single(block => (block.Inlines?.Text ?? block.Text ?? string.Empty).Contains("前缀"));
                var controller = SelectionController(viewer);
                var host = DocumentHost(viewer);
                var origin = text.TranslatePoint(default, host)!.Value;
                var y = origin.Y + text.Bounds.Height / 2;
                Assert.True(controller.Begin(new Point(origin.X + 2, y)));
                Assert.True(controller.Extend(new Point(origin.X + text.Bounds.Width - 2, y)));
                UpdateSelectionState(viewer);

                Assert.Equal("前缀 x^2+alpha 后缀", viewer.SelectedText);
                Assert.Equal(0, text.SelectionStart);
                Assert.Equal(text.Inlines!.Text!.Length, text.SelectionEnd);
                var inline = FormulaControls(viewer).Single(control => MarkdownFormulaState.GetText(control) == InlineLatex);
                Assert.Equal(0, MarkdownFormulaState.GetSelectionStart(inline));
                Assert.Equal(InlineLatex.Length, MarkdownFormulaState.GetSelectionEnd(inline));
            }
            finally
            {
                window.Close();
                MarkdownViewer.RegisterMathViewFactory(null);
            }
        });
    }

    private static MarkdownViewer CreateViewer(out Window window, string? markdown = null)
    {
        MarkdownViewer.RegisterMathViewFactory(static (_, latex, _, _) => new Border
        {
            Width = 120,
            Height = 24,
            Background = Brushes.Transparent,
            Child = new TextBlock { Text = latex, VerticalAlignment = VerticalAlignment.Center }
        });
        var viewer = new MarkdownViewer { Markdown = markdown };
        window = new Window { Width = 520, Height = 360, Content = viewer };
        window.Show();
        return viewer;
    }

    private static void Render(MarkdownViewer viewer, Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        viewer.Markdown = Document;
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static List<Control> FormulaControls(MarkdownViewer viewer) => viewer.GetVisualDescendants().OfType<Control>()
        .Where(control => MarkdownFormulaState.GetText(control) is not null).ToList();

    private static MarkdownSelectionController SelectionController(MarkdownViewer viewer) =>
        (MarkdownSelectionController)typeof(MarkdownViewer).GetField("_selectionController", InstancePrivate)!.GetValue(viewer)!;

    private static Visual DocumentHost(MarkdownViewer viewer) =>
        (Visual)typeof(MarkdownViewer).GetField("_documentHost", InstancePrivate)!.GetValue(viewer)!;

    private static void SelectControl(
        MarkdownSelectionController controller,
        Visual host,
        Control control,
        double fromRatio,
        double toRatio)
    {
        var origin = control.TranslatePoint(default, host)!.Value;
        var y = origin.Y + control.Bounds.Height / 2;
        Assert.True(controller.Begin(new Point(origin.X + control.Bounds.Width * fromRatio, y)));
        Assert.True(controller.Extend(new Point(origin.X + control.Bounds.Width * toRatio, y)));
    }

    private static void UpdateSelectionState(MarkdownViewer viewer) =>
        typeof(MarkdownViewer).GetMethod("UpdateSelectionState", InstancePrivate)!.Invoke(viewer, []);
}
