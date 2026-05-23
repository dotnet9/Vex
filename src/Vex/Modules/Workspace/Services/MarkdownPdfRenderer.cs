using Avalonia.Media.Imaging;
using SkiaSharp;
using Vex.Core.Models;

namespace Vex.Modules.Workspace.Services;

internal sealed class MarkdownPdfRenderer
{
    private const float PageWidth = 595;
    private const float PageHeight = 842;
    private const float PageMargin = 36;
    private const int MinimumSourceSliceHeight = 1;
    private const int PreferredMinimumSliceHeight = 160;
    private const int BoundarySearchWindow = 120;
    private const byte WhiteThreshold = 245;
    private const double WhiteRowRatio = 0.985;

    private readonly MarkdownPngRenderer _pngRenderer = new();

    public void Render(DocumentSnapshot document, string path)
    {
        using var rendered = _pngRenderer.Render(document);
        using var bitmap = DecodeRenderedBitmap(rendered);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        using var stream = File.Create(path);
        using var pdf = SKDocument.CreatePdf(stream);
        if (pdf is null)
        {
            throw new InvalidOperationException("Could not create PDF document.");
        }

        var contentWidth = PageWidth - (PageMargin * 2);
        var contentHeight = PageHeight - (PageMargin * 2);
        var scale = contentWidth / bitmap.Width;
        var sourceSliceHeight = Math.Max(MinimumSourceSliceHeight, (int)Math.Floor(contentHeight / scale));

        for (var sourceTop = 0; sourceTop < bitmap.Height;)
        {
            var idealBottom = Math.Min(bitmap.Height, sourceTop + sourceSliceHeight);
            var sourceBottom = FindSliceBottom(bitmap, sourceTop, idealBottom);
            var source = new SKRectI(0, sourceTop, bitmap.Width, sourceBottom);
            var destinationHeight = source.Height * scale;
            var destination = new SKRect(
                PageMargin,
                PageMargin,
                PageMargin + contentWidth,
                PageMargin + destinationHeight);

            var canvas = pdf.BeginPage(PageWidth, PageHeight);
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(bitmap, source, destination);
            pdf.EndPage();
            sourceTop = sourceBottom;
        }

        pdf.Close();
    }

    private static SKBitmap DecodeRenderedBitmap(RenderTargetBitmap rendered)
    {
        using var stream = new MemoryStream();
        rendered.Save(stream);
        stream.Position = 0;
        return SKBitmap.Decode(stream)
               ?? throw new InvalidOperationException("Could not decode rendered Markdown bitmap.");
    }

    private static int FindSliceBottom(SKBitmap bitmap, int sourceTop, int idealBottom)
    {
        if (idealBottom >= bitmap.Height)
        {
            return bitmap.Height;
        }

        var minimumBottom = Math.Min(idealBottom, sourceTop + PreferredMinimumSliceHeight);
        var searchTop = Math.Max(minimumBottom, idealBottom - BoundarySearchWindow);
        for (var bottom = idealBottom; bottom >= searchTop; bottom--)
        {
            if (IsMostlyWhiteRow(bitmap, bottom - 1))
            {
                return bottom;
            }
        }

        return idealBottom;
    }

    private static bool IsMostlyWhiteRow(SKBitmap bitmap, int y)
    {
        const int SampleStep = 8;
        var samples = 0;
        var whiteSamples = 0;
        for (var x = 0; x < bitmap.Width; x += SampleStep)
        {
            var color = bitmap.GetPixel(x, y);
            samples++;
            if (color.Red >= WhiteThreshold
                && color.Green >= WhiteThreshold
                && color.Blue >= WhiteThreshold)
            {
                whiteSamples++;
            }
        }

        return samples > 0 && (double)whiteSamples / samples >= WhiteRowRatio;
    }
}
