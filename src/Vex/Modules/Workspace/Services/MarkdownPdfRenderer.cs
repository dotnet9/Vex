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
    private const byte BackgroundTolerance = 8;
    private const double BackgroundRowRatio = 0.985;
    private static readonly SKColor MetadataTextColor = new(107, 114, 128);
    private static readonly SKColor MetadataLineColor = new(229, 231, 235);

    private readonly IAppLocalizer _localizer;
    private readonly MarkdownPngRenderer _pngRenderer;

    public MarkdownPdfRenderer(IAppLocalizer localizer)
    {
        _localizer = localizer;
        _pngRenderer = new MarkdownPngRenderer(localizer);
    }

    public void Render(DocumentSnapshot document, string path, MarkdownExportStyle? exportStyle = null)
    {
        var style = exportStyle ?? MarkdownExportStyle.Resolve(null, null);
        using var rendered = _pngRenderer.Render(document, style);
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
        var pageBackgroundColor = ParseColor(style.PageBackgroundColor, SKColors.White);
        var metadataTextColor = ParseColor(style.MutedColor, MetadataTextColor);
        var metadataLineColor = ParseColor(style.BorderColor, MetadataLineColor);
        var slices = CreateSlices(bitmap, sourceSliceHeight, pageBackgroundColor).ToList();
        var pageCount = slices.Count;
        var headerTitle = ResolveHeaderTitle(document);
        var footerTitle = ResolveFooterTitle(document);

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
            canvas.Clear(pageBackgroundColor);
            DrawHeader(canvas, headerTitle, metadataTextColor, metadataLineColor);
            canvas.DrawBitmap(bitmap, source, destination);
            DrawFooter(canvas, footerTitle, pageIndex + 1, pageCount, metadataTextColor, metadataLineColor);
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

    private static IEnumerable<PdfSlice> CreateSlices(SKBitmap bitmap, int sourceSliceHeight, SKColor pageBackgroundColor)
    {
        for (var sourceTop = 0; sourceTop < bitmap.Height;)
        {
            var idealBottom = Math.Min(bitmap.Height, sourceTop + sourceSliceHeight);
            var sourceBottom = FindSliceBottom(bitmap, sourceTop, idealBottom, pageBackgroundColor);
            yield return new PdfSlice(sourceTop, sourceBottom);
            sourceTop = sourceBottom;
        }
    }

    private static int FindSliceBottom(SKBitmap bitmap, int sourceTop, int idealBottom, SKColor pageBackgroundColor)
    {
        if (idealBottom >= bitmap.Height)
        {
            return bitmap.Height;
        }

        var minimumBottom = Math.Min(idealBottom, sourceTop + PreferredMinimumSliceHeight);
        var searchTop = Math.Max(minimumBottom, idealBottom - BoundarySearchWindow);
        for (var bandBottom = idealBottom; bandBottom >= searchTop + PreferredBlankBandHeight; bandBottom--)
        {
            if (IsMostlyBackgroundBand(bitmap, bandBottom - PreferredBlankBandHeight, bandBottom, pageBackgroundColor))
            {
                return bandBottom;
            }
        }

        for (var bottom = idealBottom; bottom >= searchTop; bottom--)
        {
            if (IsMostlyBackgroundRow(bitmap, bottom - 1, pageBackgroundColor))
            {
                return bottom;
            }
        }

        return idealBottom;
    }

    private static bool IsMostlyBackgroundBand(SKBitmap bitmap, int top, int bottom, SKColor pageBackgroundColor)
    {
        for (var y = top; y < bottom; y++)
        {
            if (!IsMostlyBackgroundRow(bitmap, y, pageBackgroundColor))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMostlyBackgroundRow(SKBitmap bitmap, int y, SKColor pageBackgroundColor)
    {
        const int SampleStep = 8;
        var samples = 0;
        var backgroundSamples = 0;
        for (var x = 0; x < bitmap.Width; x += SampleStep)
        {
            var color = bitmap.GetPixel(x, y);
            samples++;
            if (IsNearColor(color, pageBackgroundColor))
            {
                backgroundSamples++;
            }
        }

        return samples > 0 && (double)backgroundSamples / samples >= BackgroundRowRatio;
    }

    private static bool IsNearColor(SKColor color, SKColor expected)
    {
        return Math.Abs(color.Red - expected.Red) <= BackgroundTolerance
               && Math.Abs(color.Green - expected.Green) <= BackgroundTolerance
               && Math.Abs(color.Blue - expected.Blue) <= BackgroundTolerance;
    }

    private static void DrawHeader(SKCanvas canvas, string title, SKColor textColor, SKColor lineColor)
    {
        var headerBottom = PageMargin + HeaderHeight;
        using var linePaint = CreateLinePaint(lineColor);
        canvas.DrawLine(PageMargin, headerBottom, PageWidth - PageMargin, headerBottom, linePaint);

        using var textPaint = CreateTextPaint(textColor);
        using var font = CreateMetadataFont(HeaderFontSize);

        var titleMaxWidth = PageWidth - (PageMargin * 2);
        var visibleTitle = TrimToWidth(title, font, textPaint, titleMaxWidth);
        canvas.DrawText(visibleTitle, PageMargin, PageMargin + 17, font, textPaint);
    }

    private static void DrawFooter(SKCanvas canvas, string title, int pageNumber, int pageCount, SKColor textColor, SKColor lineColor)
    {
        var footerTop = PageHeight - PageMargin - FooterHeight;
        using var linePaint = CreateLinePaint(lineColor);
        canvas.DrawLine(PageMargin, footerTop, PageWidth - PageMargin, footerTop, linePaint);

        using var textPaint = CreateTextPaint(textColor);
        using var font = CreateMetadataFont(FooterFontSize);

        var baseline = footerTop + 18;
        var pageText = $"{pageNumber} / {pageCount}";
        var pageTextWidth = font.MeasureText(pageText, textPaint);
        var pageTextX = PageWidth - PageMargin - pageTextWidth;
        var titleMaxWidth = Math.Max(0, pageTextX - PageMargin - 24);
        var visibleTitle = TrimToWidth(title, font, textPaint, titleMaxWidth);

        canvas.DrawText(visibleTitle, PageMargin, baseline, font, textPaint);
        canvas.DrawText(pageText, pageTextX, baseline, font, textPaint);
    }

    private static SKPaint CreateLinePaint(SKColor color)
    {
        return new SKPaint
        {
            Color = color,
            IsAntialias = true,
            StrokeWidth = 1
        };
    }

    private static SKPaint CreateTextPaint(SKColor color)
    {
        return new SKPaint
        {
            Color = color,
            IsAntialias = true
        };
    }

    private static SKFont CreateMetadataFont(float size)
    {
        return new SKFont(SKTypeface.Default, size);
    }

    private string ResolveHeaderTitle(DocumentSnapshot document)
    {
        return MarkdownHeadingScanner.FindFirstHeading(document.Markdown)
               ?? Path.GetFileNameWithoutExtension(ResolveFooterTitle(document))
               ?? _localizer.Get(VexL.DocumentDefaultHeading);
    }

    private string ResolveFooterTitle(DocumentSnapshot document)
    {
        if (!string.IsNullOrWhiteSpace(document.FilePath))
        {
            return Path.GetFileName(document.FilePath);
        }

        return string.IsNullOrWhiteSpace(document.FileName)
            ? _localizer.Get(VexL.DocumentDefaultFileName)
            : document.FileName;
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

    private static SKColor ParseColor(string color, SKColor fallback)
    {
        try
        {
            return SKColor.Parse(color);
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private readonly record struct PdfSlice(int Top, int Bottom);
}
