using CodeWF.Avalonia.Markdown.Tests;
using CodeWF.Avalonia.Markdown.Controls;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Threading;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class WindowedStackPanelIndexTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void LayoutIndexIsReusedUntilGeometryChanges() => platform.Run(() =>
    {
        var panel = new WindowedStackPanel { EstimatedItemHeight = 10, Spacing = 2 };
        for (var i = 0; i < 1_000; i++)
        {
            panel.AddItem(new Border());
        }

        Assert.True(panel.TryGetItemTop(999, out var top));
        Assert.Equal(11_988, top, 6);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(i, panel.FindItemAtOffset(i * 12 + 5));
        }
        Assert.Equal(1, panel.LayoutIndexBuildCount);

        panel.InsertItem(500, new Border());
        Assert.True(panel.TryGetItemTop(500, out top));
        Assert.Equal(6_000, top, 6);
        Assert.Equal(2, panel.LayoutIndexBuildCount);

        panel.Spacing = 4;
        Assert.True(panel.TryGetItemTop(500, out top));
        Assert.Equal(7_000, top, 6);
        Assert.Equal(3, panel.LayoutIndexBuildCount);
    });

    [Fact]
    public void OffsetLookupSkipsUnknownBandsAndUsesMeasuredHeights() => platform.Run(() =>
    {
        var panel = new WindowedStackPanel { EstimatedItemHeight = 10, Spacing = 0 };
        panel.AddItem(new Border { Height = 20 });
        panel.AddItem(new Border { Height = 20 });
        panel.AddItem(new Border { Height = 20 });
        panel.Measure(new Size(100, double.PositiveInfinity));

        panel.InsertItem(1, new Border());
        Assert.True(panel.TryGetItemTop(1, out var unknownTop));
        Assert.True(panel.TryGetItemTop(2, out var nextMeasuredTop));
        Assert.Equal(20, unknownTop, 6);
        Assert.Equal(30, nextMeasuredTop, 6);
        Assert.Equal(1, panel.FindItemAtOffset(25));
        Assert.Equal(2, panel.FindMeasuredItemAtOffset(25));
        Assert.Equal(2, panel.FindItemStartIndexAtOffset(35));
        Assert.Equal(1, panel.FindItemStartIndexAtOffset(29));
    });

    [Fact]
    public void OffsetLookupTracksRemovalAndClampsDocumentEdges() => platform.Run(() =>
    {
        var panel = new WindowedStackPanel { EstimatedItemHeight = 10, Spacing = 2 };
        for (var i = 0; i < 5; i++)
        {
            panel.AddItem(new Border { Height = 20 });
        }
        panel.Measure(new Size(100, double.PositiveInfinity));
        panel.RemoveAt(1);

        Assert.Equal(0, panel.FindItemAtOffset(-10));
        Assert.Equal(0, panel.FindItemAtOffset(0));
        Assert.Equal(1, panel.FindItemAtOffset(25));
        Assert.Equal(3, panel.FindItemAtOffset(10_000));
        Assert.Equal(0, panel.FindItemStartIndexAtOffset(-10));
        Assert.Equal(1, panel.FindItemStartIndexAtOffset(22));
        Assert.Equal(3, panel.FindItemStartIndexAtOffset(10_000));
        Assert.False(panel.TryGetItemTop(4, out _));
        panel.ClearItems();
        Assert.Equal(-1, panel.FindItemAtOffset(0));
        Assert.Equal(-1, panel.FindMeasuredItemAtOffset(0));
        Assert.Equal(0, panel.PinnedItemCount);
    });

    [Fact]
    public void ReadingPositionRoundTripsInLongVirtualizedDocument() => platform.Run(() =>
    {
        var viewer = new MarkdownViewer
        {
            Markdown = string.Join("\n\n", Enumerable.Range(0, 200)
                .Select(i => $"Paragraph {i} with enough text to occupy a stable reading band."))
        };
        var presenter = new ScrollContentPresenter { Content = viewer };
        var scroll = new ScrollViewer
        {
            Template = new FuncControlTemplate<ScrollViewer>((_, scope) =>
            {
                scope.Register("PART_ScrollContentPresenter", presenter);
                return presenter;
            })
        };
        var window = new Window { Width = 600, Height = 400, Content = scroll };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height * .45 - scroll.Viewport.Height / 2));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var saved = viewer.SaveReadingPosition();
            Assert.NotNull(saved);
            Assert.InRange(saved.Value.BlockIndex, 60, 140);

            scroll.Offset = default;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.True(viewer.RestoreReadingPosition(saved));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var restored = viewer.SaveReadingPosition();
            Assert.NotNull(restored);
            Assert.Equal(saved.Value.BlockIndex, restored.Value.BlockIndex);
            Assert.InRange(Math.Abs(saved.Value.BlockRatio - restored.Value.BlockRatio), 0, .02);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    });

    [Fact]
    public void PinnedIndexTracksStructuralEditsAndSelectiveRelease() => platform.Run(() =>
    {
        var panel = new WindowedStackPanel();
        for (var i = 0; i < 5; i++)
        {
            panel.AddItem(new Border());
        }
        panel.PinItem(1);
        panel.PinItem(3);
        panel.InsertItem(0, new Border(), pinned: true);

        Assert.Equal(3, panel.PinnedItemCount);
        Assert.True(panel.IsItemPinned(0));
        Assert.True(panel.IsItemPinned(2));
        Assert.True(panel.IsItemPinned(4));

        panel.ReleasePinnedItemsExcept(2, 4);
        Assert.Equal(2, panel.PinnedItemCount);
        Assert.False(panel.IsItemPinned(0));
        Assert.True(panel.IsItemPinned(2));
        Assert.True(panel.IsItemPinned(4));

        panel.RemoveAt(2);
        Assert.Equal(1, panel.PinnedItemCount);
        Assert.True(panel.IsItemPinned(3));
        panel.DetachAllPinned();
        Assert.Equal(0, panel.PinnedItemCount);
        Assert.False(panel.IsItemPinned(3));
    });
}
