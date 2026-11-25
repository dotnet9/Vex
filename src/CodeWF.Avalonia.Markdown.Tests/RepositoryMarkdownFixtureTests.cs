using CodeWF.Avalonia.Markdown.Controls;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Headless;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class RepositoryMarkdownFixtureTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void ComprehensiveFixtureKeepsEveryElementFamilyAndLongDocumentShape()
    {
        var source = ReadFixture("docs/Markdown功能示例.md");
        var document = Markdig.Markdown.Parse(source, MarkdownViewer.Pipeline);
        var blocks = document.Descendants<Block>().ToList();

        Assert.True(source.Length > 300_000, $"comprehensive fixture became too small: {source.Length} chars");
        Assert.True(blocks.Count >= 1500, $"comprehensive fixture lost long-document coverage: {blocks.Count} blocks");
        Assert.Contains(blocks, block => block is YamlFrontMatterBlock);
        Assert.Equal(Enumerable.Range(1, 6), blocks.OfType<HeadingBlock>().Select(block => block.Level).Distinct().OrderBy(level => level));
        Assert.Contains(blocks, block => block is QuoteBlock);
        Assert.Contains(blocks, block => block is ListBlock);
        Assert.Contains(blocks, block => block is ThematicBreakBlock);
        Assert.Contains(blocks, block => block is CodeBlock and not FencedCodeBlock and not YamlFrontMatterBlock);
        Assert.Contains(blocks, block => block is Footnote);
        Assert.Contains("术语\n: 定义列表", source, StringComparison.Ordinal);
        Assert.Contains(":::note", source, StringComparison.Ordinal);
        Assert.Contains(blocks, block => block is HtmlBlock);
        Assert.True(document.Descendants<HtmlInline>().Any());
        Assert.True(document.Descendants<FootnoteLink>().Any());
        Assert.True(document.Descendants<MathInline>().Any());
        Assert.True(document.Descendants<LinkInline>().Count(inline => inline.IsImage) >= 20);

        var fences = blocks.OfType<FencedCodeBlock>().ToList();
        foreach (var language in new[] { "csharp", "xml", "text", "mermaid" })
        {
            Assert.Contains(fences, fence => string.Equals(fence.Info?.Trim(), language, StringComparison.OrdinalIgnoreCase));
        }

        Assert.Contains(blocks, block => block is Table);
        Assert.Contains("+=========+", source, StringComparison.Ordinal);
        foreach (var alert in new[] { "NOTE", "TIP", "IMPORTANT", "WARNING", "CAUTION" })
        {
            Assert.Contains($"> [!{alert}]", source, StringComparison.Ordinal);
        }

        Assert.Contains("[TOC]", source, StringComparison.Ordinal);
        Assert.Contains("<br>", source, StringComparison.Ordinal);
        Assert.Contains("<span style=\"font-size:48px\">", source, StringComparison.Ordinal);
        Assert.Contains("演示文档/图片/dotnet-avalonia-pipeline.svg", source, StringComparison.Ordinal);
        Assert.Contains("<![字号刻度]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExistingManualFixtureRemainsLargeAndStructurallyComplex()
    {
        var source = ReadFixture("docs/测试文档.md");
        var blocks = Markdig.Markdown.Parse(source, MarkdownViewer.Pipeline).Descendants<Block>().ToList();

        Assert.True(source.Length > 60_000, $"manual fixture became too small: {source.Length} chars");
        Assert.True(blocks.Count >= 1000, $"manual fixture lost long-document coverage: {blocks.Count} blocks");
        Assert.Contains(blocks, block => block is HeadingBlock);
        Assert.Contains(blocks, block => block is ListBlock);
        Assert.Contains(blocks, block => block is Table);
        Assert.Contains(blocks, block => block is FencedCodeBlock);
        Assert.Contains(blocks, block => block is QuoteBlock);
        Assert.Contains(blocks, block => block is ThematicBreakBlock);
    }

    [Fact]
    public void ManualRepositoryDocumentKeepsViewportRealizedAcrossJumpAndResize() => platform.Run(() =>
    {
        var source = ReadFixture("docs/测试文档.md");
        var viewer = new MarkdownViewer { Markdown = source };
        var presenter = new ScrollContentPresenter { Content = viewer };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 760, Height = 420, Content = scroll };
        window.Show();
        try
        {
            for (var pass = 0; pass < 20; pass++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }

            var panel = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().Single();
            Assert.True(panel.IsVirtualizing);
            Assert.True(panel.ItemCount >= 1000);

            foreach (var ratio in new[] { 0d, .17d, .43d, .79d, 1d })
            {
                var maxOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                scroll.Offset = new Vector(0, maxOffset * ratio);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AssertViewportFullyRealized(panel, scroll);
                AssertDeferredContentReady(panel, scroll);
            }

            window.Width = 500;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AssertViewportFullyRealized(panel, scroll);
            AssertDeferredContentReady(panel, scroll);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    private static void AssertViewportFullyRealized(WindowedStackPanel panel, ScrollViewer scroll)
    {
        var viewportTop = scroll.Offset.Y;
        var viewportBottom = viewportTop + scroll.Viewport.Height;
        var missed = new List<int>();
        for (var index = 0; index < panel.ItemCount; index++)
        {
            if (!panel.TryGetItemTop(index, out var top)) continue;
            var height = panel.GetItemHeight(index);
            if (top + height <= viewportTop || top >= viewportBottom) continue;
            if (panel.GetRealizedItem(index) is null) missed.Add(index);
        }

        Assert.True(missed.Count == 0, $"viewport has unrealized bands: {string.Join(",", missed)}");
    }

    private static void AssertDeferredContentReady(WindowedStackPanel panel, ScrollViewer scroll)
    {
        var viewportTop = scroll.Offset.Y;
        var viewportBottom = viewportTop + scroll.Viewport.Height;
        var empty = new List<int>();
        for (var index = 0; index < panel.ItemCount; index++)
        {
            if (!panel.TryGetItemTop(index, out var top)) continue;
            var height = panel.GetItemHeight(index);
            if (top + height <= viewportTop || top >= viewportBottom) continue;
            var realized = panel.GetRealizedItem(index);
            if (realized?.GetType().Name == "DeferredMarkdownBlock" && realized is Decorator { Child: null })
            {
                empty.Add(index);
            }
        }

        Assert.True(empty.Count == 0, $"viewport has deferred blocks without content: {string.Join(",", empty)}");
    }

    private static string ReadFixture(string relativePath, [System.Runtime.CompilerServices.CallerFilePath] string testFile = "")
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFile)!, "..", ".."));
        var path = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"repository fixture is missing: {path}");
        return File.ReadAllText(path);
    }
}
