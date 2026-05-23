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

        for (var sourceTop = 0; sourceTop < bitmap.Height; sourceTop += sourceSliceHeight)
        {
            var sourceBottom = Math.Min(bitmap.Height, sourceTop + sourceSliceHeight);
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
}
