using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

using SkiaSharp;
using Svg.Skia;

namespace CodeWF.Avalonia.Markdown;

/// <summary>
/// Rasterizes SVG image bytes for Markdown preview, export, and clipboard scenarios.
/// </summary>
public static class MarkdownSvgRasterizer
{
	public const int DefaultMaxRasterDimension = 4096;

	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026",
		Justification = "Markdown image sources are runtime content, so build-time SVG generation is not applicable here.")]
	public static byte[] RenderToPngBytes(byte[] svgBytes, int maxRasterDimension = DefaultMaxRasterDimension)
	{
		ArgumentNullException.ThrowIfNull(svgBytes);

		using var svg = new SKSvg();
		using var svgStream = new MemoryStream(PrepareForSkia(svgBytes));
		var picture = svg.Load(svgStream) ?? svg.Picture;
		if (picture is null)
		{
			throw new InvalidDataException("SVG picture could not be loaded.");
		}

		var bounds = picture.CullRect;
		var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
		var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
		var scale = Math.Min(1d, Math.Max(1, maxRasterDimension) / (double)Math.Max(width, height));
		var scaledWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
		var scaledHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

		using var surface = SKSurface.Create(new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
		if (surface is null)
		{
			throw new InvalidDataException("SVG rendering surface could not be created.");
		}

		var canvas = surface.Canvas;
		canvas.Clear(SKColors.Transparent);
		canvas.Scale((float)scale);
		canvas.Translate(-bounds.Left, -bounds.Top);
		canvas.DrawPicture(picture);
		canvas.Flush();

		using var image = surface.Snapshot();
		using var data = image.Encode(SKEncodedImageFormat.Png, 100);
		return data?.ToArray() ?? throw new InvalidDataException("SVG picture could not be encoded.");
	}

	private static byte[] PrepareForSkia(byte[] svgBytes)
	{
		try
		{
			var svgText = Encoding.UTF8.GetString(svgBytes);
			var document = XDocument.Parse(svgText, LoadOptions.PreserveWhitespace);
			var changed = false;

			foreach (var attribute in document.Descendants()
						 .SelectMany(element => element.Attributes())
						 .Where(attribute => string.Equals(attribute.Name.LocalName, "filter", StringComparison.OrdinalIgnoreCase))
						 .ToArray())
			{
				attribute.Remove();
				changed = true;
			}

			return changed
				? Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting))
				: svgBytes;
		}
		catch
		{
			return svgBytes;
		}
	}
}
