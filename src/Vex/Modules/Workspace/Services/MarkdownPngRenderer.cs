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
using Markdig.Extensions.TaskLists;
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

    private readonly IAppLocalizer _localizer;

    public MarkdownPngRenderer(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public RenderTargetBitmap Render(DocumentSnapshot document, MarkdownExportStyle? exportStyle = null)
    {
        var style = exportStyle ?? MarkdownExportStyle.Resolve(null, null);
        var visual = BuildVisual(document, _localizer, style);
        visual.Measure(new Size(PageWidth, double.PositiveInfinity));

        var width = (int)Math.Ceiling(PageWidth);
        var height = (int)Math.Ceiling(Math.Max(MinPageHeight, visual.DesiredSize.Height));
        visual.Arrange(new Rect(0, 0, width, height));

        var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        bitmap.Render(visual);
        return bitmap;
    }

    private static Border BuildVisual(DocumentSnapshot document, IAppLocalizer localizer, MarkdownExportStyle style)
    {
        var stack = new StackPanel
        {
            Width = ContentWidth,
            Spacing = 0
        };

        var parsed = Markdown.Parse(document.Markdown ?? string.Empty, Pipeline);
        foreach (var block in parsed)
        {
            AddBlock(stack, block, document.FilePath, localizer, 0, style);
        }

        if (stack.Children.Count == 0)
        {
            stack.Children.Add(CreateParagraphTextBlock(string.Empty, style));
        }

        return new Border
        {
            Width = PageWidth,
            Background = Brush(style.PageBackgroundColor),
            Padding = new Thickness(PagePaddingX, PagePaddingTop, PagePaddingX, PagePaddingBottom),
            Child = stack
        };
    }

    private static void AddBlock(Panel parent, Block block, string? documentPath, IAppLocalizer localizer, int depth, MarkdownExportStyle style)
    {
        switch (block)
        {
            case HeadingBlock heading:
                parent.Children.Add(CreateHeading(heading, style));
                break;
            case ParagraphBlock paragraph when TryCreateImage(paragraph, documentPath, localizer, out var image):
                parent.Children.Add(image);
                break;
            case ParagraphBlock paragraph:
                parent.Children.Add(CreateParagraph(paragraph, depth, style));
                break;
            case CodeBlock codeBlock:
                parent.Children.Add(CreateCodeBlock(codeBlock, style));
                break;
            case QuoteBlock quote:
                parent.Children.Add(CreateQuoteBlock(quote, documentPath, localizer, depth, style));
                break;
            case ListBlock list:
                parent.Children.Add(CreateListBlock(list, documentPath, localizer, depth, style));
                break;
            case ThematicBreakBlock:
                parent.Children.Add(CreateThematicBreak(style));
                break;
            case Table table:
                parent.Children.Add(CreateTable(table, style));
                break;
            case HtmlBlock html:
                parent.Children.Add(CreateCodeBlock(html, style));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AddBlock(parent, child, documentPath, localizer, depth, style);
                }

                break;
        }
    }

    private static TextBlock CreateHeading(HeadingBlock heading, MarkdownExportStyle style)
    {
        var fontSize = heading.Level switch
        {
            1 => style.Heading1FontSize,
            2 => style.Heading2FontSize,
            3 => style.Heading3FontSize,
            4 => style.Heading4FontSize,
            5 => style.Heading5FontSize,
            _ => style.Heading6FontSize
        };

        var textBlock = CreateTextBlock(fontSize, Brush(style.HeadingColor), FontWeight.SemiBold, new Thickness(0, heading.Level == 1 ? 0 : 18, 0, 10), style);
        AppendInlines(textBlock.Inlines!, heading.Inline, style);
        return textBlock;
    }

    private static TextBlock CreateParagraph(ParagraphBlock paragraph, int depth, MarkdownExportStyle style)
    {
        var margin = new Thickness(0, 0, 0, depth == 0 ? 14 : 8);
        var textBlock = CreateTextBlock(style.BodyFontSize, depth == 0 ? Brush(style.BodyColor) : Brush(style.MutedColor), FontWeight.Normal, margin, style);
        AppendInlines(textBlock.Inlines!, paragraph.Inline, style);
        return textBlock;
    }

    private static TextBlock CreateParagraphTextBlock(string text, MarkdownExportStyle style)
    {
        var textBlock = CreateTextBlock(style.BodyFontSize, Brush(style.BodyColor), FontWeight.Normal, new Thickness(0, 0, 0, 14), style);
        textBlock.Text = text;
        return textBlock;
    }

    private static Border CreateCodeBlock(LeafBlock codeBlock, MarkdownExportStyle style)
    {
        return new Border
        {
            Background = Brush(style.CodeBackgroundColor),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 4, 0, 18),
            Child = new TextBlock
            {
                Text = codeBlock.Lines.ToString(),
                FontFamily = new FontFamily(style.MonoFontFamily),
                FontSize = style.CodeFontSize,
                Foreground = Brush(style.CodeForegroundColor),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = style.CodeFontSize * 1.62
            }
        };
    }

    private static Border CreateQuoteBlock(QuoteBlock quote, string? documentPath, IAppLocalizer localizer, int depth, MarkdownExportStyle style)
    {
        var stack = new StackPanel
        {
            Spacing = 0
        };

        foreach (var child in quote)
        {
            AddBlock(stack, child, documentPath, localizer, depth + 1, style);
        }

        return new Border
        {
            BorderBrush = Brush(style.QuoteBorderColor),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(14, 0, 0, 0),
            Margin = new Thickness(0, 4, 0, 18),
            Child = stack
        };
    }

    private static StackPanel CreateListBlock(ListBlock list, string? documentPath, IAppLocalizer localizer, int depth, MarkdownExportStyle style)
    {
        var stack = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(0, 0, 0, depth == 0 ? 14 : 8)
        };

        var index = string.IsNullOrWhiteSpace(list.OrderedStart) ? 1 : int.TryParse(list.OrderedStart, out var start) ? start : 1;
        foreach (var child in list.OfType<ListItemBlock>())
        {
            var marker = ResolveListMarker(list, child, index);
            if (list.IsOrdered)
            {
                index++;
            }

            stack.Children.Add(CreateListItem(marker, child, documentPath, localizer, depth, style));
        }

        return stack;
    }

    private static string ResolveListMarker(ListBlock list, ListItemBlock item, int index)
    {
        if (TryGetTaskListState(item, out var isChecked))
        {
            return isChecked ? "[x]" : "[ ]";
        }

        return list.IsOrdered ? $"{index}." : "-";
    }

    private static bool TryGetTaskListState(ListItemBlock item, out bool isChecked)
    {
        isChecked = false;
        var paragraph = item.OfType<ParagraphBlock>().FirstOrDefault();
        if (paragraph?.Inline?.FirstChild is not TaskList taskList)
        {
            return false;
        }

        isChecked = taskList.Checked;
        return true;
    }

    private static Grid CreateListItem(string marker, ListItemBlock item, string? documentPath, IAppLocalizer localizer, int depth, MarkdownExportStyle style)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(new GridLength(1, GridUnitType.Star))
            }
        };

        var markerBlock = CreateTextBlock(style.BodyFontSize, Brush(style.MutedColor), FontWeight.Normal, new Thickness(0, 0, 10, 0), style);
        markerBlock.Text = marker;
        Grid.SetColumn(markerBlock, 0);
        grid.Children.Add(markerBlock);

        var content = new StackPanel { Spacing = 0 };
        foreach (var child in item)
        {
            AddBlock(content, child, documentPath, localizer, depth + 1, style);
        }

        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private static Border CreateThematicBreak(MarkdownExportStyle style)
    {
        return new Border
        {
            Height = 1,
            Background = Brush(style.BorderColor),
            Margin = new Thickness(0, 10, 0, 24)
        };
    }

    private static Grid CreateTable(Table table, MarkdownExportStyle style)
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
                var border = CreateTableCell(cell, rows[rowIndex].IsHeader, style);
                Grid.SetRow(border, rowIndex);
                Grid.SetColumn(border, columnIndex);
                grid.Children.Add(border);
            }
        }

        return grid;
    }

    private static Border CreateTableCell(TableCell? cell, bool isHeader, MarkdownExportStyle style)
    {
        return new Border
        {
            Background = isHeader ? Brush(style.TableHeaderBackgroundColor) : Brush(style.PageBackgroundColor),
            BorderBrush = Brush(style.BorderColor),
            BorderThickness = new Thickness(1, 1, 0, 0),
            Padding = new Thickness(8, 7),
            Child = CreateTableCellContent(cell, isHeader, style)
        };
    }

    private static Control CreateTableCellContent(TableCell? cell, bool isHeader, MarkdownExportStyle style)
    {
        var stack = new StackPanel
        {
            Spacing = 4
        };

        if (cell is not null)
        {
            foreach (var child in cell)
            {
                AddTableCellBlock(stack, child, isHeader, style);
            }
        }

        if (stack.Children.Count == 0)
        {
            stack.Children.Add(CreateTableTextBlock(string.Empty, isHeader, style));
        }

        return stack;
    }

    private static void AddTableCellBlock(StackPanel stack, Block block, bool isHeader, MarkdownExportStyle style)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                stack.Children.Add(CreateTableParagraph(paragraph, isHeader, style));
                break;
            case LeafBlock leaf:
                stack.Children.Add(CreateTableTextBlock(leaf.Lines.ToString(), isHeader, style));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AddTableCellBlock(stack, child, isHeader, style);
                }

                break;
        }
    }

    private static TextBlock CreateTableParagraph(ParagraphBlock paragraph, bool isHeader, MarkdownExportStyle style)
    {
        var textBlock = CreateTextBlock(style.TableFontSize, Brush(style.BodyColor), isHeader ? FontWeight.SemiBold : FontWeight.Normal, new Thickness(), style);
        AppendInlines(textBlock.Inlines!, paragraph.Inline, style);
        return textBlock;
    }

    private static TextBlock CreateTableTextBlock(string text, bool isHeader, MarkdownExportStyle style)
    {
        var textBlock = CreateTextBlock(style.TableFontSize, Brush(style.BodyColor), isHeader ? FontWeight.SemiBold : FontWeight.Normal, new Thickness(), style);
        textBlock.Text = text;
        return textBlock;
    }

    private static TextBlock CreateTextBlock(double fontSize, IBrush foreground, FontWeight fontWeight, Thickness margin, MarkdownExportStyle style)
    {
        return new TextBlock
        {
            FontFamily = new FontFamily(style.BodyFontFamily),
            FontSize = fontSize,
            FontWeight = fontWeight,
            Foreground = foreground,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = fontSize * style.LineHeightRatio,
            Margin = margin,
            Inlines = new InlineCollection()
        };
    }

    private static void AppendInlines(InlineCollection collection, ContainerInline? container, MarkdownExportStyle style)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            AppendInline(collection, inline, style);
        }
    }

    private static void AppendInline(InlineCollection collection, MarkdigInline inline, MarkdownExportStyle style)
    {
        switch (inline)
        {
            case LiteralInline literal:
                collection.Add(new Run(literal.Content.ToString()));
                break;
            case CodeInline code:
                collection.Add(new Run(code.Content)
                {
                    FontFamily = new FontFamily(style.MonoFontFamily),
                    Background = Brush(style.InlineCodeBackgroundColor),
                    Foreground = Brush(style.InlineCodeForegroundColor)
                });
                break;
            case LineBreakInline:
                collection.Add(new LineBreak());
                break;
            case EmphasisInline emphasis:
                collection.Add(CreateEmphasisSpan(emphasis, style));
                break;
            case LinkInline link:
                collection.Add(CreateLinkSpan(link, style));
                break;
            case ContainerInline container:
                AppendInlines(collection, container, style);
                break;
        }
    }

    private static Span CreateEmphasisSpan(EmphasisInline emphasis, MarkdownExportStyle style)
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
            AppendInline(span.Inlines, child, style);
        }

        return span;
    }

    private static Span CreateLinkSpan(LinkInline link, MarkdownExportStyle style)
    {
        var span = new Span
        {
            Foreground = Brush(style.LinkColor),
            TextDecorations = TextDecorations.Underline
        };

        if (link.IsImage)
        {
            span.Inlines.Add(new Run(GetInlineText(link)));
            return span;
        }

        foreach (var child in link)
        {
            AppendInline(span.Inlines, child, style);
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

        if (!TryGetOnlyImageInline(paragraph.Inline, out var imageInline))
        {
            return false;
        }

        if (!TryLoadImage(imageInline.Url, documentPath, localizer, out var bitmap))
        {
            return false;
        }

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

    private static bool TryGetOnlyImageInline(ContainerInline? inline, [NotNullWhen(true)] out LinkInline? imageInline)
    {
        imageInline = null;
        var child = inline?.FirstChild;
        while (child is not null)
        {
            if (child is LinkInline { IsImage: true } image)
            {
                if (imageInline is not null)
                {
                    return false;
                }

                imageInline = image;
            }
            else if (!IsWhitespaceInline(child))
            {
                return false;
            }

            child = child.NextSibling;
        }

        return imageInline is not null;
    }

    private static bool IsWhitespaceInline(MarkdigInline inline)
    {
        return inline switch
        {
            LiteralInline literal => string.IsNullOrWhiteSpace(literal.Content.ToString()),
            LineBreakInline => true,
            HtmlInline html => string.IsNullOrWhiteSpace(html.Tag),
            _ => false
        };
    }

    private static bool TryLoadImage(string? url, string? documentPath, IAppLocalizer localizer, [NotNullWhen(true)] out Bitmap? bitmap)
    {
        bitmap = null;
        try
        {
            if (TryGetDataImageBytes(url, out var dataBytes))
            {
                bitmap = LoadBitmap(dataBytes, null, localizer);
                return true;
            }

            var path = ResolveLocalImagePath(url, documentPath);
            if (path is null)
            {
                return false;
            }

            bitmap = LoadBitmap(File.ReadAllBytes(path), path, localizer);
            return true;
        }
        catch
        {
            bitmap?.Dispose();
            bitmap = null;
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

        foreach (var candidate in EnumerateLocalImagePathCandidates(url))
        {
            if (Path.IsPathRooted(candidate) && File.Exists(candidate))
            {
                return candidate;
            }
        }

        var baseDirectory = string.IsNullOrWhiteSpace(documentPath) ? null : Path.GetDirectoryName(documentPath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return null;
        }

        foreach (var candidate in EnumerateLocalImagePathCandidates(url))
        {
            var relativePath = Path.GetFullPath(Path.Combine(baseDirectory, candidate));
            if (File.Exists(relativePath))
            {
                return relativePath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateLocalImagePathCandidates(string url)
    {
        var normalized = url.Replace('/', Path.DirectorySeparatorChar);
        yield return normalized;

        var decoded = DecodeLocalImageUrl(normalized);
        if (!string.Equals(decoded, normalized, StringComparison.Ordinal))
        {
            yield return decoded;
        }
    }

    private static string DecodeLocalImageUrl(string url)
    {
        try
        {
            return Uri.UnescapeDataString(url);
        }
        catch (UriFormatException)
        {
            return url;
        }
    }

    private static bool TryGetDataImageBytes(string? url, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(url)
            || !url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commaIndex = url.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0 || !url[..commaIndex].Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(url[(commaIndex + 1)..]);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }

    private static Bitmap LoadBitmap(byte[] bytes, string? path, IAppLocalizer localizer)
    {
        if ((path is not null && IsSvgPath(path)) || IsSvgBytes(bytes))
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
