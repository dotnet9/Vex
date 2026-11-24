using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Tests;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownImageCacheTests(AvaloniaPlatformFixture platform)
{
    private const string Svg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="2" height="2">
          <rect width="2" height="2" fill="red"/>
        </svg>
        """;

    [Fact]
    public void CacheKeyIncludesResolvedFileAndFileVersion()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vex-image-key-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = Path.Combine(directory, "first.svg");
            var second = Path.Combine(directory, "second.svg");
            File.WriteAllText(first, Svg);
            File.WriteAllText(second, Svg + " ");
            const string url = "https://example.invalid/image.svg";

            var firstKey = MarkdownImageSourceLoader.CreateCacheKey(url, null, _ => first);
            var secondKey = MarkdownImageSourceLoader.CreateCacheKey(url, null, _ => second);
            Assert.NotEqual(firstKey, secondKey);
            Assert.Equal(firstKey, MarkdownImageSourceLoader.CreateCacheKey(url, null, _ => first));

            File.AppendAllText(first, "  ");
            Assert.NotEqual(firstKey, MarkdownImageSourceLoader.CreateCacheKey(url, null, _ => first));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public Task PrepareAsyncRunsPreviewEncodingAndDecodeOffUiThread() => platform.RunAsync(async () =>
    {
        var source = new MarkdownImageSource(System.Text.Encoding.UTF8.GetBytes(Svg), "image.svg", true, false, null);
        var uiThread = Environment.CurrentManagedThreadId;
        var encoderThread = 0;
        var decoderThread = 0;
        SynchronizationContext? encoderContext = null;
        SynchronizationContext? decoderContext = null;
        MarkdownImage.PreparedMarkdownImage? prepared = null;
        try
        {
            prepared = await MarkdownImage.PrepareAsync(
                source,
                "preview-off-thread",
                MarkdownImage.CacheGeneration,
                CancellationToken.None,
                image =>
                {
                    encoderThread = Environment.CurrentManagedThreadId;
                    encoderContext = SynchronizationContext.Current;
                    return MarkdownImageRasterizer.RenderToPngBytes(image);
                },
                _ =>
                {
                    decoderThread = Environment.CurrentManagedThreadId;
                    decoderContext = SynchronizationContext.Current;
                    return null!;
                });

            Assert.NotEqual(uiThread, encoderThread);
            Assert.NotEqual(uiThread, decoderThread);
            Assert.Null(encoderContext);
            Assert.Null(decoderContext);
            Assert.NotEmpty(prepared.Value.PreviewBytes);
        }
        finally
        {
            prepared?.Bitmap?.Dispose();
        }
    });

    [Fact]
    public Task PrepareAsyncReusesPreviewBytesForEffectiveCacheKey() => platform.RunAsync(async () =>
    {
        MarkdownImage.InvalidateRemoteCaches();
        var source = new MarkdownImageSource(System.Text.Encoding.UTF8.GetBytes(Svg), "image.svg", true, false, null);
        var generation = MarkdownImage.CacheGeneration;
        var first = await MarkdownImage.PrepareAsync(
            source, "same-image", generation, CancellationToken.None, bitmapDecoder: _ => null!);
        var second = await MarkdownImage.PrepareAsync(
            source, "same-image", generation, CancellationToken.None, bitmapDecoder: _ => null!);
        try
        {
            Assert.Same(first.PreviewBytes, second.PreviewBytes);
            Assert.Equal(1, MarkdownImage.ImagePreviewCacheCount);
        }
        finally
        {
            first.Bitmap?.Dispose();
            second.Bitmap?.Dispose();
            MarkdownImage.InvalidateRemoteCaches();
        }
    });

    [Fact]
    public void InvalidateClearsMemoryAndDiskCachesAndRejectsStaleWrites()
    {
        var previousDirectory = MarkdownImageDiskCache.CacheDirectory;
        var directory = Path.Combine(Path.GetTempPath(), "vex-image-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        MarkdownImageDiskCache.CacheDirectory = directory;
        try
        {
            MarkdownImage.InvalidateRemoteCaches();
            var generation = MarkdownImage.CacheGeneration;
            var source = new MarkdownImageSource([1, 2, 3], "image.png", false, false, null);
            MarkdownImage.AddImageBytesToCache("image", source, generation);
            MarkdownImage.AddPreviewBytesToCache("image", [4, 5, 6], generation);
            MarkdownImageDiskCache.Store("remote|https://example.invalid/image.png", [7, 8, 9]);
            Assert.Equal(1, MarkdownImage.ImageByteCacheCount);
            Assert.Equal(1, MarkdownImage.ImagePreviewCacheCount);
            Assert.NotNull(MarkdownImageDiskCache.TryRead("remote|https://example.invalid/image.png"));

            MarkdownImage.InvalidateRemoteCaches();
            Assert.Equal(0, MarkdownImage.ImageByteCacheCount);
            Assert.Equal(0, MarkdownImage.ImagePreviewCacheCount);
            Assert.Null(MarkdownImageDiskCache.TryRead("remote|https://example.invalid/image.png"));

            MarkdownImage.AddImageBytesToCache("image", source, generation);
            MarkdownImage.AddPreviewBytesToCache("image", [4, 5, 6], generation);
            MarkdownImage.TryStoreRemoteDiskCache("https://example.invalid/stale.png", null, [1], generation);
            Assert.Equal(0, MarkdownImage.ImageByteCacheCount);
            Assert.Equal(0, MarkdownImage.ImagePreviewCacheCount);
            Assert.Null(MarkdownImageDiskCache.TryRead("remote|https://example.invalid/stale.png"));

            MarkdownImage.AddImageBytesToCache("image", source, MarkdownImage.CacheGeneration);
            MarkdownImage.TryStoreRemoteDiskCache(
                "https://example.invalid/fresh.png", null, [2], MarkdownImage.CacheGeneration);
            Assert.Equal(1, MarkdownImage.ImageByteCacheCount);
            Assert.NotNull(MarkdownImageDiskCache.TryRead("remote|https://example.invalid/fresh.png"));
        }
        finally
        {
            MarkdownImage.InvalidateRemoteCaches();
            MarkdownImageDiskCache.CacheDirectory = previousDirectory;
            Directory.Delete(directory, recursive: true);
        }
    }
}
