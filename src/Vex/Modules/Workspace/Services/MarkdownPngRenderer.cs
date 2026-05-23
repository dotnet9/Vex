using System.Diagnostics.CodeAnalysis;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SkiaSharp;
using Svg.Skia;
using Vex.Core.Models;
using Vex.Core.Services;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace Vex.Modules.Workspace.Services;

internal sealed class MarkdownPngRenderer
{
    private const double PageWidth = 960;
    private const double ContentWidth = 864;
    private const double PagePaddingX = 48;
    private const double PagePaddingTop = 44;
    private const double PagePaddingBottom = 56;
    private const double MinPageHeight = 320;
    private const int MaxSvgRasterDimension = 4096;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly FontFamily BodyFont = new("Inter, Microsoft YaHei UI, Segoe UI");
    private static readonly FontFamily MonoFont = new("Cascadia Mono, Consolas");
    private static readonly IBrush PageBrush = Brush("#ffffff");
    private static readonly IBrush BodyBrush = Brush("#1f2937");
    private static readonly IBrush HeadingBrush = Brush("#111827");
    private static readonly IBrush MutedBrush = Brush("#4b5563");
    private static readonly IBrush BorderBrush = Brush("#e5e7eb");
    private static readonly IBrush CodeBackgroundBrush = Brush("#111827");
    private static readonly IBrush CodeForegroundBrush = Brush("#f9fafb");
    private static readonly IBrush InlineCodeBackgroundBrush = Brush("#f3f4f6");
    private static readonly IBrush LinkBrush = Brush("#2563eb");
    private static readonly IBrush TableHeaderBrush = Brush("#f9fafb");
    private static readonly IBrush QuoteBorderBrush = Brush("#d1d5db");
    private readonly IAppLocalizer _localizer;

    public MarkdownPngRenderer(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public RenderTargetBitmap Render(DocumentSnapshot document)
    {
        var visual = BuildVisual(document, _localizer);
        visual.Measure(new Size(PageWidth, double.PositiveInfinity));

        var width = (int)Math.Ceiling(PageWidth);
        var height = (int)Math.Ceiling(Math.Max(MinPageHeight, visual.DesiredSize.Height));
        visual.Arrange(new Rect(0, 0, width, height));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        bitmap.Render(visual);
        return bitmap;
    }

    private static Border BuildVisual(DocumentSnapshot document, IAppLocalizer localizer)
    {
        var stack = new StackPanel
        {
            Width = ContentWidth,
            Spacing = 0
        };

        var parsed = Markdown.Parse(document.Markdown ?? string.Empty, Pipeline);
        foreach (var block in parsed)
        {
            AddBlock(stack, block, document.FilePath, localizer, 0);
        }

        if (stack.Children.Count == 0)
        {
            stack.Children.Add(CreateParagraphTextBlock(string.Empty));
        }

        return new Border
        {
            Width = PageWidth,
            Background = PageBrush,
            Padding = new Thickness(PagePaddingX, PagePaddingTop, PagePaddingX, PagePaddingBottom),
            Child = stack
        };
    }

    private static void AddBlock(Panel parent, Block block, string? documentPath, IAppLocalizer localizer, int depth)
    {
        switch (block)
        {
            case HeadingBlock heading:
                parent.Children.Add(CreateHeading(heading));
                break;
            case ParagraphBlock paragraph when TryCreateImage(paragraph, documentPath, localizer, out var image):
                parent.Children.Add(image);
                break;
            case ParagraphBlock paragraph:
                parent.Children.Add(CreateParagraph(paragraph, depth));
                break;
            case CodeBlock codeBlock:
                parent.Children.Add(CreateCodeBlock(codeBlock));
                break;
            case QuoteBlock quote:
                parent.Children.Add(CreateQuoteBlock(quote, documentPath, localizer, depth));
                break;
            case ListBlock list:
                parent.Children.Add(CreateListBlock(list, documentPath, localizer, depth));
                break;
            case ThematicBreakBlock:
                parent.Children.Add(CreateThematicBreak());
                break;
            case Table table:
                parent.Children.Add(CreateTable(table));
                break;
            case HtmlBlock html:
                parent.Children.Add(CreateCodeBlock(html));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AddBlock(parent, child, documentPath, localizer, depth);
                }

                break;
        }
    }

    private static TextBlock CreateHeading(HeadingBlock heading)
    {
        var fontSize = heading.Level switch
        {
            1 => 32,
            2 => 26,
            3 => 22,
            4 => 19,
            5 => 17,
            _ => 16
        };

        var textBlock = CreateTextBlock(fontSize, HeadingBrush, FontWeight.SemiBold, new Thickness(0, heading.Level == 1 ? 0 : 18, 0, 10));
        AppendInlines(textBlock.Inlines!, heading.Inline);
        return textBlock;
    }

    private static TextBlock CreateParagraph(ParagraphBlock paragraph, int depth)
    {
        var margin = new Thickness(0, 0, 0, depth == 0 ? 14 : 8);
        var textBlock = CreateTextBlock(15, depth == 0 ? BodyBrush : MutedBrush, FontWeight.Normal, margin);
        AppendInlines(textBlock.Inlines!, paragraph.Inline);
        return textBlock;
    }

    private static TextBlock CreateParagraphTextBlock(string text)
    {
        var textBlock = CreateTextBlock(15, BodyBrush, FontWeight.Normal, new Thickness(0, 0, 0, 14));
        textBlock.Text = text;
        return textBlock;
    }

    private static Border CreateCodeBlock(LeafBlock codeBlock)
    {
        return new Border
        {
            Background = CodeBackgroundBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4, 0, 18),
            Child = new TextBlock
            {
                Text = codeBlock.Lines.ToString(),
                FontFamily = MonoFont,
                FontSize = 13,
                Foreground = CodeForegroundBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21
            }
        };
    }

    private static Border CreateQuoteBlock(QuoteBlock quote, string? documentPath, IAppLocalizer localizer, int depth)
    {
        var stack = new StackPanel
        {
            Spacing = 0
        };

        foreach (var child in quote)
        {
            AddBlock(stack, child, documentPath, localizer, depth + 1);
        }

        return new Border
        {
            BorderBrush = QuoteBorderBrush,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(14, 0, 0, 0),
            Margin = new Thickness(0, 4, 0, 18),
            Child = stack
        };
    }

    private static StackPanel CreateListBlock(ListBlock list, string? documentPath, IAppLocalizer localizer, int depth)
    {
        var stack = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, depth == 0 ? 14 : 8)
        };

        var index = string.IsNullOrWhiteSpace(list.OrderedStart) ? 1 : int.TryParse(list.OrderedStart, out var start) ? start : 1;
        foreach (var child in list.OfType<ListItemBlock>())
        {
            var marker = list.IsOrdered ? $"{index++}." : "-";
            stack.Children.Add(CreateListItem(marker, child, documentPath, localizer, depth));
        }

        return stack;
    }

    private static Grid CreateListItem(string marker, ListItemBlock item, string? documentPath, IAppLocalizer localizer, int depth)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            }
        };

        var markerBlock = CreateTextBlock(15, MutedBrush, FontWeight.Normal, new Thickness(0, 0, 10, 0));
        markerBlock.Text = marker;
        Grid.SetColumn(markerBlock, 0);
        grid.Children.Add(markerBlock);

        var content = new StackPanel { Spacing = 0 };
        foreach (var child in item)
        {
            AddBlock(content, child, documentPath, localizer, depth + 1);
        }

        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private static Border CreateThematicBreak()
    {
        return new Border
        {
            Height = 1,
            Background = BorderBrush,
            Margin = new Thickness(0, 10, 0, 24)
        };
    }

    private static Grid CreateTable(Table table)
    {
        var rows = table.OfType<TableRow>().ToList();
        var columnCount = Math.Max(1, rows.Select(row => row.OfType<TableCell>().Count()).DefaultIfEmpty(1).Max());
        var grid = new Grid
        {
            Margin = new Thickness(0, 4, 0, 18)
        };

        for (var column = 0; column < columnCount; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1, GridUnitType.Star)));
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var cells = rows[rowIndex].OfType<TableCell>().ToList();

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = columnIndex < cells.Count ? cells[columnIndex] : null;
                var border = CreateTableCell(cell, rows[rowIndex].IsHeader);
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, columnIndex);
                grid.Children.Add(border);
            }
        }

        return grid;
    }

    private static Border CreateTableCell(TableCell? cell, bool isHeader)
    {
        var textBlock = CreateTextBlock(14, BodyBrush, isHeader ? FontWeight.SemiBold : FontWeight.Normal, new Thickness());
        textBlock.Text = cell is null ? string.Empty : GetTableCellText(cell);

        return new Border
        {
            Background = isHeader ? TableHeaderBrush : PageBrush,
            BorderBrush = BorderBrush,
            BorderThickness = new Thickness(1, 1, 0, 0),
            Padding = new Thickness(8, 7),
            Child = textBlock
        };
    }

    private static TextBlock CreateTextBlock(double fontSize, IBrush foreground, FontWeight fontWeight, Thickness margin)
    {
        return new TextBlock
        {
            FontFamily = BodyFont,
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = fontSize * 1.55,
            Margin = margin,
            Inlines = new InlineCollection()
        };
    }

    private static void AppendInlines(InlineCollection collection, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(collection, inline);
        }
    }

    private static void AppendInline(InlineCollection collection, MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                collection.Add(new Run(literal.Content.ToString()));
                break;
            case CodeInline code:
                collection.Add(new Run(code.Content)
                {
                    FontFamily = MonoFont,
                    Background = InlineCodeBackgroundBrush
                });
                break;
            case LineBreakInline:
                collection.Add(new LineBreak());
                break;
            case EmphasisInline emphasis:
                collection.Add(CreateEmphasisSpan(emphasis));
                break;
            case LinkInline link:
                collection.Add(CreateLinkSpan(link));
                break;
            case ContainerInline container:
                AppendInlines(collection, container);
                break;
        }
    }

    private static Span CreateEmphasisSpan(EmphasisInline emphasis)
    {
        var span = new Span();
        if (emphasis.DelimiterChar == '~')
        {
            span.TextDecorations = TextDecorations.Strikethrough;
        }
        else if (emphasis.DelimiterCount >= 2)
        {
            span.FontWeight = FontWeight.Bold;
        }
        else
        {
            span.FontStyle = FontStyle.Italic;
        }

        foreach (var child in emphasis)
        {
            AppendInline(span.Inlines, child);
        }

        return span;
    }

    private static Span CreateLinkSpan(LinkInline link)
    {
        var span = new Span
        {
            Foreground = LinkBrush,
            TextDecorations = TextDecorations.Underline
        };

        if (link.IsImage)
        {
            span.Inlines.Add(new Run(GetInlineText(link)));
            return span;
        }

        foreach (var child in link)
        {
            AppendInline(span.Inlines, child);
        }

        if (span.Inlines.Count == 0 && !string.IsNullOrWhiteSpace(link.Url))
        {
            span.Inlines.Add(new Run(link.Url));
        }

        return span;
    }

    private static bool TryCreateImage(
        ParagraphBlock paragraph,
        string? documentPath,
        IAppLocalizer localizer,
        out Control imageControl)
    {
        imageControl = null!;

        if (paragraph.Inline?.FirstChild is not LinkInline { IsImage: true } imageInline)
        {
            return false;
        }

        var hasTrailingContent = imageInline.NextSibling is not null;
        var path = ResolveLocalImagePath(imageInline.Url, documentPath);
        if (hasTrailingContent || path is null)
        {
            return false;
        }

        try
        {
            var bitmap = LoadLocalBitmap(path, localizer);
            imageControl = new Image
            {
                Source = bitmap,
                MaxWidth = ContentWidth,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 18)
            };

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveLocalImagePath(string? url, string? documentPath)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.IsFile && File.Exists(uri.LocalPath) ? uri.LocalPath : null;
        }

        if (Path.IsPathRooted(url))
        {
            return File.Exists(url) ? url : null;
        }

        var baseDirectory = string.IsNullOrWhiteSpace(documentPath) ? null : Path.GetDirectoryName(documentPath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        var relativePath = Path.GetFullPath(Path.Combine(baseDirectory, url));
        return File.Exists(relativePath) ? relativePath : null;
    }

    private static Bitmap LoadLocalBitmap(string path, IAppLocalizer localizer)
    {
        var bytes = File.ReadAllBytes(path);
        if (IsSvgPath(path) || IsSvgBytes(bytes))
        {
            using var svgPng = new MemoryStream(RenderSvgToPngBytes(bytes, localizer));
            return new Bitmap(svgPng);
        }

        using var stream = new MemoryStream(bytes);
        return new Bitmap(stream);
    }

    private static bool IsSvgPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSvgBytes(byte[] bytes)
    {
        var length = Math.Min(bytes.Length, 512);
        if (length == 0)
        {
            return false;
        }

        var prefix = Encoding.UTF8.GetString(bytes, 0, length).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
               || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               && prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026",
        Justification = "Markdown export renders user-provided SVG files at runtime, so build-time SVG generation is not applicable.")]
    private static byte[] RenderSvgToPngBytes(byte[] svgBytes, IAppLocalizer localizer)
    {
        using var svg = new SKSvg();
        using var svgStream = new MemoryStream(svgBytes);
        var picture = svg.Load(svgStream) ?? svg.Picture;
        if (picture is null)
        {
            throw new InvalidDataException(localizer.Get(VexL.ExportDetailSvgPictureLoadFailed));
        }

        var bounds = picture.CullRect;
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        var scale = Math.Min(1d, MaxSvgRasterDimension / (double)Math.Max(width, height));
        var scaledWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

        using var surface = SKSurface.Create(new SKImageInfo(scaledWidth, scaledHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
        {
            throw new InvalidDataException(localizer.Get(VexL.ExportDetailSvgSurfaceCreateFailed));
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? throw new InvalidDataException(localizer.Get(VexL.ExportDetailSvgEncodeFailed));
    }

    private static string GetTableCellText(TableCell cell)
    {
        if (cell is ContainerBlock container)
        {
            var parts = new List<string>();
            foreach (var child in container)
            {
                parts.Add(child is LeafBlock leaf ? GetBlockText(leaf) : child.ToString() ?? string.Empty);
            }

            return string.Join(Environment.NewLine, parts);
        }

        return cell.ToString() ?? string.Empty;
    }

    private static string GetBlockText(LeafBlock block)
    {
        return block.Inline is null
            ? block.Lines.ToString()
            : GetInlineText(block.Inline);
    }

    private static string GetInlineText(ContainerInline container)
    {
        var parts = new List<string>();
        foreach (var inline in container)
        {
            CollectInlineText(parts, inline);
        }

        return string.Concat(parts);
    }

    private static void CollectInlineText(List<string> parts, MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                parts.Add(literal.Content.ToString());
                break;
            case CodeInline code:
                parts.Add(code.Content);
                break;
            case LineBreakInline:
                parts.Add(Environment.NewLine);
                break;
            case LinkInline { IsImage: true } image:
                var altText = GetInlineText(image);
                parts.Add(string.IsNullOrWhiteSpace(altText) ? image.Url ?? string.Empty : altText);
                break;
            case ContainerInline container:
                parts.Add(GetInlineText(container));
                break;
        }
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
}
