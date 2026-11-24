using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Svg.Skia;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using CodeWF.Avalonia.Markdown.Mermaid;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownMermaidRecoveryTests(AvaloniaPlatformFixture platform)
{
    private const string Svg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="120" height="40" viewBox="0 0 120 40">
          <rect width="120" height="40" fill="#3b82f6"/>
        </svg>
        """;

    [Fact]
    public async Task Failure_RetryRestoresPreviewAndClearsFallbackClass()
    {
        await platform.RunAsync(async () =>
        {
            var attempts = 0;
            var source = $"graph TD; transient-{Guid.NewGuid():N}";
            var block = (Border)MermaidBlockRenderer.CreateMermaidBlock(source, renderSvg: (_, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    throw new InvalidOperationException("temporary render failure");
                }

                return Svg;
            });
            var window = new Window { Width = 420, Height = 280, Content = block };
            window.Show();
            try
            {
                await WaitForAsync(() => block.Classes.Contains("MdMermaidFallback"));
                var retry = block.GetVisualDescendants().OfType<Button>()
                    .Single(button => button.Content as string == "Retry");
                retry.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                await WaitForAsync(() =>
                    !block.Classes.Contains("MdMermaidFallback")
                    && block.GetVisualDescendants().OfType<Image>().Any(image => image.Source is SvgImage));
                Assert.Equal(2, attempts);
                Assert.Contains("MdMermaid", block.Classes);
            }
            finally
            {
                window.Close();
                MermaidBlockRenderer.InvalidateDiagramCache(
                    MermaidBlockRenderer.CreateCacheKey(source, MermaidBlockRenderer.GetRenderOptions(source)));
            }
        });
    }

    [Fact]
    public async Task Resolve_DoesNotCacheFailureAndInvalidationForcesRerender()
    {
        await platform.RunAsync(async () =>
        {
            var source = $"graph TD; cache-{Guid.NewGuid():N}";
            var options = MermaidBlockRenderer.GetRenderOptions(source);
            var cacheKey = MermaidBlockRenderer.CreateCacheKey(source, options);
            var attempts = 0;
            try
            {
                var failed = await MermaidBlockRenderer.ResolveSvgSourceAsync(cacheKey, source, options, (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    throw new InvalidOperationException("temporary render failure");
                });
                Assert.Null(failed);

                var first = await MermaidBlockRenderer.ResolveSvgSourceAsync(cacheKey, source, options, (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    return Svg;
                });
                Assert.NotNull(first);

                var cached = await MermaidBlockRenderer.ResolveSvgSourceAsync(cacheKey, source, options, (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    return Svg;
                });
                Assert.Same(first, cached);
                Assert.Equal(2, attempts);

                MermaidBlockRenderer.InvalidateDiagramCache(cacheKey);
                var refreshed = await MermaidBlockRenderer.ResolveSvgSourceAsync(cacheKey, source, options, (_, _) =>
                {
                    Interlocked.Increment(ref attempts);
                    return Svg;
                });
                Assert.NotNull(refreshed);
                Assert.NotSame(first, refreshed);
                Assert.Equal(3, attempts);
            }
            finally
            {
                MermaidBlockRenderer.InvalidateDiagramCache(cacheKey);
            }
        });
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 300; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(10);
        }

        Assert.True(condition(), "等待 Mermaid 状态更新超时");
    }
}
