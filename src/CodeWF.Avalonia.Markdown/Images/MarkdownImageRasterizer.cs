using SkiaSharp;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Converts loaded Markdown image bytes into static PNG preview bytes.
/// </summary>
public static class MarkdownImageRasterizer
{
	public const long DefaultMaxPixelCount = 16_777_216;

	public static byte[] RenderToPngBytes(
		MarkdownImageSource imageSource,
		long maxPixelCount = DefaultMaxPixelCount)
	{
		ArgumentNullException.ThrowIfNull(imageSource);
		if (maxPixelCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxPixelCount));
		}

		return imageSource.IsSvg
			? MarkdownSvgRasterizer.RenderToPngBytes(imageSource.Bytes)
			: RenderBitmapToPngBytes(imageSource.Bytes, maxPixelCount);
	}

	private static byte[] RenderBitmapToPngBytes(byte[] bytes, long maxPixelCount)
	{
		using var sourceData = SKData.CreateCopy(bytes);
		using var codec = SKCodec.Create(sourceData);
		if (codec is null)
		{
			throw new InvalidDataException("Markdown image could not be decoded.");
		}

		var sourceInfo = codec.Info;
		if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0
			|| (long)sourceInfo.Width * sourceInfo.Height > maxPixelCount)
		{
			throw new InvalidDataException("Markdown image dimensions are too large.");
		}

		var imageInfo = new SKImageInfo(
			sourceInfo.Width,
			sourceInfo.Height,
			SKColorType.Rgba8888,
			SKAlphaType.Premul);
		using var bitmap = new SKBitmap(imageInfo);
		var result = codec.GetPixels(imageInfo, bitmap.GetPixels());
		if (result is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
		{
			throw new InvalidDataException("Markdown image could not be decoded.");
		}

		using var image = SKImage.FromBitmap(bitmap);
		using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
		return encoded?.ToArray() ?? throw new InvalidDataException("Markdown image could not be encoded.");
	}
}
