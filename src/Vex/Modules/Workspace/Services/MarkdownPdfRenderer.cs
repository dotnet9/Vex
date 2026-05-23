using Avalonia.Media.Imaging;
using SkiaSharp;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

internal sealed class MarkdownPdfRenderer
{
    private const float PageWidth = 595;
    private const float PageHeight = 842;
    private const float PageMargin = 36;
    private const float HeaderGap = 14;
    private const float HeaderHeight = 26;
    private const float HeaderFontSize = 10;
    private const float FooterGap = 14;
    private const float FooterHeight = 28;
    private const float FooterFontSize = 9;
    private const int MinimumSourceSliceHeight = 1;
    private const int PreferredMinimumSliceHeight = 160;
    private const int BoundarySearchWindow = 120;
    private const int PreferredBlankBandHeight = 10;
    private const byte WhiteThreshold = 245;
    private const double WhiteRowRatio = 0.985;
    private static readonly SKColor MetadataTextColor = new(107, 114, 128);
    private static readonly SKColor MetadataLineColor = new(229, 231, 235);

    private readonly IAppLocalizer _localizer;
    private readonly MarkdownPngRenderer _pngRenderer;

    public MarkdownPdfRenderer(IAppLocalizer localizer)
    {
        _localizer = localizer;
        _pngRenderer = new MarkdownPngRenderer(localizer);
    }

    public void Render(DocumentSnapshot document, string path)
    {
        using var rendered = _pngRenderer.Render(document);
        using var bitmap = DecodeRenderedBitmap(rendered);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        using var stream = File.Create(path);
        using var pdf = SKDocument.CreatePdf(stream);
        if (pdf is null)
        {
            throw new InvalidOperationException(_localizer.Get(VexL.ExportDetailPdfDocumentCreateFailed));
        }

        var contentWidth = PageWidth - (PageMargin * 2);
        var contentTop = PageMargin + HeaderHeight + HeaderGap;
        var contentBottom = PageHeight - PageMargin - FooterGap - FooterHeight;
        var contentHeight = contentBottom - contentTop;
        var scale = contentWidth / bitmap.Width;
        var sourceSliceHeight = Math.Max(MinimumSourceSliceHeight, (int)Math.Floor(contentHeight / scale));
        var slices = CreateSlices(bitmap, sourceSliceHeight).ToList();
        var pageCount = slices.Count;

        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var slice = slices[pageIndex];
            var source = new SKRectI(0, slice.Top, bitmap.Width, slice.Bottom);
            var destinationHeight = source.Height * scale;
            var destination = new SKRect(
                PageMargin,
                contentTop,
                PageMargin + contentWidth,
                contentTop + destinationHeight);

            var canvas = pdf.BeginPage(PageWidth, PageHeight);
            canvas.Clear(SKColors.White);
            DrawHeader(canvas, document);
            canvas.DrawBitmap(bitmap, source, destination);
            DrawFooter(canvas, document, pageIndex + 1, pageCount);
            pdf.EndPage();
        }

        pdf.Close();
    }

    private SKBitmap DecodeRenderedBitmap(RenderTargetBitmap rendered)
    {
        using var stream = new MemoryStream();
        rendered.Save(stream);
        stream.Position = 0;
        return SKBitmap.Decode(stream)
               ?? throw new InvalidOperationException(_localizer.Get(VexL.ExportDetailRenderedBitmapDecodeFailed));
    }

    private static IEnumerable<PdfSlice> CreateSlices(SKBitmap bitmap, int sourceSliceHeight)
    {
        for (var sourceTop = 0; sourceTop < bitmap.Height;)
        {
            var idealBottom = Math.Min(bitmap.Height, sourceTop + sourceSliceHeight);
            var sourceBottom = FindSliceBottom(bitmap, sourceTop, idealBottom);
            yield return new PdfSlice(sourceTop, sourceBottom);
            sourceTop = sourceBottom;
        }
    }

    private static int FindSliceBottom(SKBitmap bitmap, int sourceTop, int idealBottom)
    {
        if (idealBottom >= bitmap.Height)
        {
            return bitmap.Height;
        }

        var minimumBottom = Math.Min(idealBottom, sourceTop + PreferredMinimumSliceHeight);
        var searchTop = Math.Max(minimumBottom, idealBottom - BoundarySearchWindow);
        for (var bandBottom = idealBottom; bandBottom >= searchTop + PreferredBlankBandHeight; bandBottom--)
        {
            if (IsMostlyWhiteBand(bitmap, bandBottom - PreferredBlankBandHeight, bandBottom))
            {
                return bandBottom;
            }
        }

        for (var bottom = idealBottom; bottom >= searchTop; bottom--)
        {
            if (IsMostlyWhiteRow(bitmap, bottom - 1))
            {
                return bottom;
            }
        }

        return idealBottom;
    }

    private static bool IsMostlyWhiteBand(SKBitmap bitmap, int top, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            if (!IsMostlyWhiteRow(bitmap, y))
            {
                return false;
            }
        }

        return true;
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

    private static void DrawHeader(SKCanvas canvas, DocumentSnapshot document)
    {
        var headerBottom = PageMargin + HeaderHeight;
        using var linePaint = CreateLinePaint();
        canvas.DrawLine(PageMargin, headerBottom, PageWidth - PageMargin, headerBottom, linePaint);

        using var textPaint = CreateTextPaint();
        using var font = CreateMetadataFont(HeaderFontSize);

        var titleMaxWidth = PageWidth - (PageMargin * 2);
        var title = TrimToWidth(ResolveHeaderTitle(document), font, textPaint, titleMaxWidth);
        canvas.DrawText(title, PageMargin, PageMargin + 17, font, textPaint);
    }

    private static void DrawFooter(SKCanvas canvas, DocumentSnapshot document, int pageNumber, int pageCount)
    {
        var footerTop = PageHeight - PageMargin - FooterHeight;
        using var linePaint = CreateLinePaint();
        canvas.DrawLine(PageMargin, footerTop, PageWidth - PageMargin, footerTop, linePaint);

        using var textPaint = CreateTextPaint();
        using var font = CreateMetadataFont(FooterFontSize);

        var baseline = footerTop + 18;
        var pageText = $"{pageNumber} / {pageCount}";
        var pageTextWidth = font.MeasureText(pageText, textPaint);
        var pageTextX = PageWidth - PageMargin - pageTextWidth;
        var titleMaxWidth = Math.Max(0, pageTextX - PageMargin - 24);
        var title = TrimToWidth(ResolveFooterTitle(document), font, textPaint, titleMaxWidth);

        canvas.DrawText(title, PageMargin, baseline, font, textPaint);
        canvas.DrawText(pageText, pageTextX, baseline, font, textPaint);
    }

    private static SKPaint CreateLinePaint()
    {
        return new SKPaint
        {
            Color = MetadataLineColor,
            IsAntialias = true,
            StrokeWidth = 1
        };
    }

    private static SKPaint CreateTextPaint()
    {
        return new SKPaint
        {
            Color = MetadataTextColor,
            IsAntialias = true
        };
    }

    private static SKFont CreateMetadataFont(float size)
    {
        return new SKFont(SKTypeface.Default, size);
    }

    private static string ResolveHeaderTitle(DocumentSnapshot document)
    {
        return FindFirstMarkdownHeading(document.Markdown)
               ?? Path.GetFileNameWithoutExtension(ResolveFooterTitle(document));
    }

    private static string? FindFirstMarkdownHeading(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        using var reader = new StringReader(markdown);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.TrimStart();
            var markerCount = CountHeadingMarkers(trimmed);
            if (markerCount == 0 || markerCount >= trimmed.Length || !char.IsWhiteSpace(trimmed[markerCount]))
            {
                continue;
            }

            var title = trimmed[markerCount..].Trim().TrimEnd('#').Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }
        }

        return null;
    }

    private static int CountHeadingMarkers(string line)
    {
        var count = 0;
        while (count < line.Length && count < 6 && line[count] == '#')
        {
            count++;
        }

        return count;
    }

    private static string ResolveFooterTitle(DocumentSnapshot document)
    {
        if (!string.IsNullOrWhiteSpace(document.FilePath))
        {
            return Path.GetFileName(document.FilePath);
        }

        return string.IsNullOrWhiteSpace(document.FileName) ? "Untitled.md" : document.FileName;
    }

    private static string TrimToWidth(string text, SKFont font, SKPaint paint, float maxWidth)
    {
        const string Ellipsis = "...";
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        if (font.MeasureText(text, paint) <= maxWidth)
        {
            return text;
        }

        if (font.MeasureText(Ellipsis, paint) > maxWidth)
        {
            return string.Empty;
        }

        for (var length = text.Length; length > 0; length--)
        {
            var candidate = text[..length] + Ellipsis;
            if (font.MeasureText(candidate, paint) <= maxWidth)
            {
                return candidate;
            }
        }

        return Ellipsis;
    }

    private readonly record struct PdfSlice(int Top, int Bottom);
}
