using System.Collections.Concurrent;
using System.Reflection;

using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using CodeWF.Avalonia.Markdown.Streaming;

using Markdig;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public class MarkdownStreamingGenerationTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter()
        .Build();

    [Fact]
    public void Reset_DiscardsInFlightSnapshotAndStartsWithCleanFrozenState()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        try
        {
            var attempts = 0;
            using var document = new MarkdownStreamingDocument(text =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    started.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken))
                    {
                        throw new TimeoutException("释放在途解析超时");
                    }
                }

                return MarkdownParser.Parse(text, Pipeline);
            });
            var snapshots = new ConcurrentQueue<MarkdownStreamingSnapshot>();
            document.SnapshotReady += (_, snapshot) => snapshots.Enqueue(snapshot);

            document.Append("old paragraph\n\n");
            Assert.True(started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            document.Reset("new paragraph\n\n");
            Assert.Same(MarkdownStreamingSnapshot.Empty, document.Latest);
            release.Set();

            Assert.True(SpinWait.SpinUntil(() => document.Latest.Source == "new paragraph\n\n", TimeSpan.FromSeconds(5)));
            Assert.True(SpinWait.SpinUntil(() => snapshots.Any(snapshot => snapshot.Generation == 1), TimeSpan.FromSeconds(5)));
            Assert.DoesNotContain(snapshots, snapshot => snapshot.Generation == 0);
            Assert.All(snapshots, snapshot => Assert.Equal(1, snapshot.Generation));
            Assert.Equal("new paragraph\n\n", document.Latest.Source);
        }
        finally
        {
            release.Set();
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    [Fact]
    public void Complete_InvalidatesEarlierIncrementalGeneration()
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        try
        {
            var attempts = 0;
            using var document = new MarkdownStreamingDocument(text =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    started.Set();
                    if (!release.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken))
                    {
                        throw new TimeoutException("释放在途解析超时");
                    }
                }

                return MarkdownParser.Parse(text, Pipeline);
            });
            var snapshots = new ConcurrentQueue<MarkdownStreamingSnapshot>();
            var completed = 0;
            document.SnapshotReady += (_, snapshot) => snapshots.Enqueue(snapshot);
            document.Completed += (_, _) => Interlocked.Increment(ref completed);

            document.Append("final paragraph\n\n");
            Assert.True(started.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            document.Complete();
            release.Set();

            Assert.True(SpinWait.SpinUntil(() => document.Latest.IsComplete, TimeSpan.FromSeconds(5)));
            Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref completed) == 1, TimeSpan.FromSeconds(5)));
            Assert.DoesNotContain(snapshots, snapshot => snapshot.Generation == 0);
            Assert.All(snapshots, snapshot =>
            {
                Assert.Equal(1, snapshot.Generation);
                Assert.True(snapshot.IsComplete);
            });
        }
        finally
        {
            release.Set();
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }
}

[Collection("AvaloniaPlatform")]
public sealed class MarkdownViewerStreamingGenerationTests(AvaloniaPlatformFixture platform)
{
    private static readonly BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;

    [Fact]
    public async Task Reset_WithSharedPrefix_RebuildsAndRejectsQueuedOldSnapshot()
    {
        await platform.RunAsync(async () =>
        {
            const string firstText = "# Shared\n\nbody";
            const string resetText = "# Shared\n\nbody\n\nmore";
            using var document = new MarkdownStreamingDocument();
            var viewer = new MarkdownViewer();
            var window = new Window { Width = 520, Height = 360, Content = viewer };
            window.Show();
            try
            {
                viewer.StreamingSource = document;
                document.Append(firstText);
                await WaitForAsync(() => viewer.CurrentModel.Source == firstText, window);
                var oldSnapshot = document.Latest;
                var oldControl = FirstRenderedControl(viewer);

                document.Reset(resetText);
                await WaitForAsync(() => viewer.CurrentModel.Source == resetText, window);
                Assert.NotSame(oldControl, FirstRenderedControl(viewer));

                typeof(MarkdownViewer).GetMethod("OnStreamingSnapshotReady", InstancePrivate)!
                    .Invoke(viewer, [document, oldSnapshot]);
                Assert.Equal(resetText, viewer.CurrentModel.Source);
            }
            finally
            {
                viewer.StreamingSource = null;
                window.Close();
            }
        });
    }

    private static Control FirstRenderedControl(MarkdownViewer viewer)
    {
        var blocks = (System.Collections.IList)typeof(MarkdownViewer)
            .GetField("_renderedBlocks", InstancePrivate)!.GetValue(viewer)!;
        var block = blocks[0]!;
        return (Control)block.GetType().GetProperty("Control")!.GetValue(block)!;
    }

    private static async Task WaitForAsync(Func<bool> condition, Window window)
    {
        for (var i = 0; i < 300; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            if (condition()) return;
            await Task.Delay(10);
        }

        Assert.True(condition(), "等待流式 Viewer 更新超时");
    }
}
