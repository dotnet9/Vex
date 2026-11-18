using System.Diagnostics.CodeAnalysis;
using System.Text;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using SkiaSharp;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace CodeWF.Avalonia.Markdown;

public sealed class MarkdownPdfRenderer
{
    private const float HeaderGap = 14;
    private const float HeaderHeight = 26;
    private const float HeaderFontSize = 10;
    private const float FooterGap = 14;
    private const float FooterHeight = 28;
    private const float FooterFontSize = 9;
    private const float ParagraphSpacing = 14;
    private const float NestedParagraphSpacing = 8;
    private const float ListMarkerWidth = 28;
    private const float ListItemSpacing = 4;
    private const float TableCellPaddingX = 7;
    private const float TableCellPaddingY = 6;
    private static readonly SKColor MetadataTextColor = new(107, 114, 128);
    private static readonly SKColor MetadataLineColor = new(229, 231, 235);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public void Render(
        MarkdownExportDocument document,
        string path,
        MarkdownExportStyle? exportStyle = null,
        MarkdownPdfExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        options ??= new MarkdownPdfExportOptions();
        var style = exportStyle ?? MarkdownExportStyle.Resolve(null, null);
        var pageBackgroundColor = ParseColor(style.PageBackgroundColor, SKColors.White);
        var metadataTextColor = ParseColor(style.MutedColor, MetadataTextColor);
        var metadataLineColor = ParseColor(style.BorderColor, MetadataLineColor);
        var headerTitle = ResolveHeaderTitle(document, options);
        var footerTitle = ResolveFooterTitle(document, options);

        using var resources = new PdfRenderResources(style);
        var geometry = PdfPageGeometry.From(options);
        var layout = new PdfLayout(resources, geometry);
        RenderDocument(document, style, layout, options.SyntaxOptions);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");

        var fullPath = Path.GetFullPath(path);
        var temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(temporaryPath))
            using (var pdf = SKDocument.CreatePdf(stream))
            {
                if (pdf is null)
                {
                    throw new InvalidOperationException("Could not create the PDF document.");
                }

                var pageCount = layout.Pages.Count;
                for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var canvas = pdf.BeginPage(geometry.PageWidth, geometry.PageHeight);
                    canvas.Clear(pageBackgroundColor);
                    DrawHeader(canvas, headerTitle, metadataTextColor, metadataLineColor, geometry);
                    layout.Pages[pageIndex].Draw(canvas, resources);
                    DrawWatermark(canvas, options, geometry, resources);
                    DrawFooter(canvas, footerTitle, pageIndex + 1, pageCount, metadataTextColor, metadataLineColor, geometry);
                    pdf.EndPage();
                }

                pdf.Close();
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void RenderDocument(MarkdownExportDocument document, MarkdownExportStyle style, PdfLayout layout, MarkdownSyntaxOptions? syntax)
    {
        var parsed = global::Markdig.Markdown.Parse(document.Markdown, syntax?.CreatePipeline() ?? Pipeline);
        if (!parsed.Any())
        {
            RenderParagraphText(layout, [new PdfInline(string.Empty, CreateBodyStyle(style, 0), null)], layout.Geometry.PageMargin, layout.Geometry.ContentWidth, ParagraphSpacing);
            return;
        }

        foreach (var block in parsed)
        {
            RenderBlock(layout, block, document.FilePath, 0, layout.Geometry.PageMargin, layout.Geometry.ContentWidth, style);
        }
    }

    private static void RenderBlock(
        PdfLayout layout,
        Block block,
        string? documentPath,
        int depth,
        float x,
        float width,
        MarkdownExportStyle style)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(layout, heading, x, width, style);
                break;
            case ParagraphBlock paragraph when TryGetOnlyImageInline(paragraph.Inline, out var imageInline):
                RenderImageParagraph(layout, imageInline, documentPath, x, width, style);
                break;
            case ParagraphBlock paragraph:
                RenderParagraph(layout, paragraph, depth, x, width, style);
                break;
            case CodeBlock codeBlock:
                RenderCodeBlock(layout, codeBlock.Lines.ToString(), x, width, style);
                break;
            case QuoteBlock quote:
                RenderQuoteBlock(layout, quote, documentPath, depth, x, width, style);
                break;
            case ListBlock list:
                RenderListBlock(layout, list, documentPath, depth, x, width, style);
                break;
            case ThematicBreakBlock:
                RenderThematicBreak(layout, x, width, style);
                break;
            case Table table:
                RenderTable(layout, table, x, width, style);
                break;
            case HtmlBlock html:
                RenderCodeBlock(layout, html.Lines.ToString(), x, width, style);
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    RenderBlock(layout, child, documentPath, depth, x, width, style);
                }

                break;
        }
    }

    private static void RenderHeading(PdfLayout layout, HeadingBlock heading, float x, float width, MarkdownExportStyle style)
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
        var textColor = heading.Level <= 2 ? style.HeadingColor : style.BodyColor;
        var textStyle = CreateTextStyle(
            style.BodyFontFamily,
            (float)fontSize,
            700,
            false,
            ParseColor(textColor, SKColors.Black),
            false,
            false,
            (float)style.LineHeightRatio);

        layout.AddGap(heading.Level == 1 ? 0 : 18);
        var lines = WrapInlines(CollectInlines(heading.Inline, textStyle, style), width, layout.Resources);
        RenderLines(layout, lines, x, width);

        if (heading.Level == 2)
        {
            layout.AddGap(6);
            layout.EnsureSpace(2);
            layout.AddOperation(new PdfLineOperation(x, layout.Y, x + width, layout.Y, ParseColor(style.HeadingColor, SKColors.Black), 1.4f));
            layout.AddGap(10);
            return;
        }

        layout.AddGap(10);
    }

    private static void RenderParagraph(
        PdfLayout layout,
        ParagraphBlock paragraph,
        int depth,
        float x,
        float width,
        MarkdownExportStyle style)
    {
        var bodyStyle = CreateBodyStyle(style, depth);
        RenderParagraphText(
            layout,
            CollectInlines(paragraph.Inline, bodyStyle, style),
            x,
            width,
            depth == 0 ? ParagraphSpacing : NestedParagraphSpacing);
    }

    private static void RenderParagraphText(
        PdfLayout layout,
        IReadOnlyList<PdfInline> inlines,
        float x,
        float width,
        float bottomMargin)
    {
        var lines = WrapInlines(inlines, width, layout.Resources);
        RenderLines(layout, lines, x, width);
        layout.AddGap(bottomMargin);
    }

    private static void RenderLines(PdfLayout layout, IReadOnlyList<PdfLine> lines, float x, float width)
    {
        foreach (var line in lines)
        {
            layout.EnsureSpace(line.Height);
            AddLineOperations(layout, line, x, layout.Y, width);
            layout.Y += line.Height;
        }
    }

    private static void AddLineOperations(PdfLayout layout, PdfLine line, float x, float y, float width)
    {
        var cursorX = x;
        foreach (var run in line.Runs)
        {
            var runWidth = layout.Resources.MeasureText(run.Text, run.Style);
            if (run.BackgroundColor.HasValue)
            {
                var backgroundHeight = run.Style.FontSize + 4;
                var backgroundY = y + ((line.Height - backgroundHeight) / 2);
                layout.AddOperation(new PdfRoundRectOperation(
                    cursorX - 2,
                    backgroundY,
                    Math.Min(runWidth + 4, Math.Max(0, x + width - cursorX + 2)),
                    backgroundHeight,
                    3,
                    run.BackgroundColor.Value,
                    null,
                    0));
            }

            layout.AddOperation(new PdfTextOperation(
                run.Text,
                cursorX,
                y + line.BaselineOffset,
                runWidth,
                run.Style));
            cursorX += runWidth;
        }
    }

    private static void RenderCodeBlock(PdfLayout layout, string text, float x, float width, MarkdownExportStyle style)
    {
        var codeStyle = CreateTextStyle(
            style.MonoFontFamily,
            (float)style.CodeFontSize,
            400,
            false,
            ParseColor(style.CodeForegroundColor, SKColors.Black),
            false,
            false,
            1.62f);
        var lines = new List<PdfLine>();
        var normalized = NormalizeLineEndings(text);
        var rawLines = normalized.Length == 0 ? [string.Empty] : normalized.Split('\n');
        foreach (var rawLine in rawLines)
        {
            var sourceLine = rawLine.Length == 0 ? " " : rawLine;
            lines.AddRange(WrapInlines(
                [new PdfInline(sourceLine, codeStyle, null)],
                Math.Max(1, width - 24),
                layout.Resources));
        }

        layout.AddGap(8);
        RenderBoxedLines(
            layout,
            lines,
            x,
            width,
            12,
            10,
            ParseColor(style.CodeBackgroundColor, SKColors.White),
            ParseColor(style.BorderColor, SKColors.LightGray),
            1);
        layout.AddGap(12);
    }

    private static void RenderBoxedLines(
        PdfLayout layout,
        IReadOnlyList<PdfLine> lines,
        float x,
        float width,
        float paddingX,
        float paddingY,
        SKColor backgroundColor,
        SKColor borderColor,
        float borderWidth)
    {
        var index = 0;
        while (index < lines.Count)
        {
            var minimumHeight = paddingY * 2 + lines[index].Height;
            layout.EnsureSpace(minimumHeight);

            var chunkStart = index;
            var chunkHeight = paddingY * 2;
            while (index < lines.Count && layout.Y + chunkHeight + lines[index].Height <= layout.Geometry.ContentBottom)
            {
                chunkHeight += lines[index].Height;
                index++;
            }

            if (index == chunkStart)
            {
                chunkHeight += lines[index].Height;
                index++;
            }

            var boxY = layout.Y;
            layout.AddOperation(new PdfRoundRectOperation(x, boxY, width, chunkHeight, 6, backgroundColor, borderColor, borderWidth));

            var textY = boxY + paddingY;
            for (var lineIndex = chunkStart; lineIndex < index; lineIndex++)
            {
                AddLineOperations(layout, lines[lineIndex], x + paddingX, textY, width - (paddingX * 2));
                textY += lines[lineIndex].Height;
            }

            layout.Y = boxY + chunkHeight;
            if (index < lines.Count)
            {
                layout.NewPage();
            }
        }
    }

    private static void RenderQuoteBlock(
        PdfLayout layout,
        QuoteBlock quote,
        string? documentPath,
        int depth,
        float x,
        float width,
        MarkdownExportStyle style)
    {
        layout.AddGap(6);
        layout.EnsureSpace(18);
        var rangeStart = layout.Mark();
        var contentX = x + 16;
        var contentWidth = Math.Max(1, width - 16);
        foreach (var child in quote)
        {
            RenderBlock(layout, child, documentPath, depth + 1, contentX, contentWidth, style);
        }

        var rangeEnd = layout.Mark();
        layout.InsertDecorationRange(
            rangeStart,
            rangeEnd,
            new PdfQuoteDecoration(
                x,
                width,
                ParseColor(style.QuoteBackgroundColor, SKColors.White),
                ParseColor(style.QuoteBorderColor, SKColors.Gray)));
        layout.AddGap(12);
    }

    private static void RenderListBlock(
        PdfLayout layout,
        ListBlock list,
        string? documentPath,
        int depth,
        float x,
        float width,
        MarkdownExportStyle style)
    {
        var markerStyle = CreateTextStyle(
            style.BodyFontFamily,
            (float)style.BodyFontSize,
            700,
            false,
            ParseColor(style.HeadingColor, SKColors.Black),
            false,
            false,
            (float)style.LineHeightRatio);
        var index = string.IsNullOrWhiteSpace(list.OrderedStart) || !int.TryParse(list.OrderedStart, out var start)
            ? 1
            : start;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = ResolveListMarker(list, item, index);
            if (list.IsOrdered)
            {
                index++;
            }

            var markerLine = WrapInlines([new PdfInline(marker, markerStyle, null)], ListMarkerWidth, layout.Resources)[0];
            layout.EnsureSpace(markerLine.Height);
            AddLineOperations(layout, markerLine, x, layout.Y, ListMarkerWidth);

            var beforeItemY = layout.Y;
            var childX = x + ListMarkerWidth;
            var childWidth = Math.Max(1, width - ListMarkerWidth);
            foreach (var child in item)
            {
                RenderBlock(layout, child, documentPath, depth + 1, childX, childWidth, style);
            }

            if (Math.Abs(layout.Y - beforeItemY) < 0.1f)
            {
                layout.Y += markerLine.Height;
            }

            layout.AddGap(ListItemSpacing);
        }

        layout.AddGap(depth == 0 ? ParagraphSpacing - ListItemSpacing : NestedParagraphSpacing);
    }

    private static string ResolveListMarker(ListBlock list, ListItemBlock item, int index)
    {
        if (TryGetTaskListState(item, out var isChecked))
        {
            return isChecked ? "[x]" : "[ ]";
        }

        return list.IsOrdered ? $"{index}." : "\u2022";
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

    private static void RenderThematicBreak(PdfLayout layout, float x, float width, MarkdownExportStyle style)
    {
        layout.AddGap(10);
        layout.EnsureSpace(1);
        layout.AddOperation(new PdfLineOperation(x, layout.Y, x + width, layout.Y, ParseColor(style.BorderColor, SKColors.LightGray), 1));
        layout.AddGap(24);
    }

    private static void RenderTable(PdfLayout layout, Table table, float x, float width, MarkdownExportStyle style)
    {
        var rows = table.OfType<TableRow>().ToList();
        if (rows.Count == 0)
        {
            return;
        }

        var columnCount = Math.Max(1, rows.Select(row => row.OfType<TableCell>().Count()).DefaultIfEmpty(1).Max());
        var columnWidth = width / columnCount;

        layout.AddGap(4);
        foreach (var row in rows)
        {
            var cells = row.OfType<TableCell>().ToList();
            var cellLines = new List<IReadOnlyList<PdfLine>>();
            var rowHeight = 0f;
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cell = columnIndex < cells.Count ? cells[columnIndex] : null;
                var lines = CreateTableCellLines(cell, row.IsHeader, Math.Max(1, columnWidth - (TableCellPaddingX * 2)), style, layout.Resources);
                cellLines.Add(lines);
                rowHeight = Math.Max(rowHeight, (TableCellPaddingY * 2) + lines.Sum(line => line.Height));
            }

            layout.EnsureSpace(rowHeight);
            var rowY = layout.Y;
            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var cellX = x + (columnIndex * columnWidth);
                layout.AddOperation(new PdfRectOperation(
                    cellX,
                    rowY,
                    columnWidth,
                    rowHeight,
                    row.IsHeader ? ParseColor(style.TableHeaderBackgroundColor, SKColors.White) : null,
                    ParseColor(style.BorderColor, SKColors.LightGray),
                    1));

                var textY = rowY + TableCellPaddingY;
                foreach (var line in cellLines[columnIndex])
                {
                    AddLineOperations(layout, line, cellX + TableCellPaddingX, textY, columnWidth - (TableCellPaddingX * 2));
                    textY += line.Height;
                }
            }

            layout.Y += rowHeight;
        }

        layout.AddGap(18);
    }

    private static IReadOnlyList<PdfLine> CreateTableCellLines(
        TableCell? cell,
        bool isHeader,
        float width,
        MarkdownExportStyle style,
        PdfRenderResources resources)
    {
        var textStyle = CreateTextStyle(
            style.BodyFontFamily,
            (float)style.TableFontSize,
            isHeader ? 600 : 400,
            false,
            ParseColor(style.BodyColor, SKColors.Black),
            false,
            false,
            (float)style.LineHeightRatio);
        var inlines = new List<PdfInline>();
        if (cell is not null)
        {
            foreach (var child in cell)
            {
                CollectBlockText(child, inlines, textStyle, style);
            }
        }

        if (inlines.Count == 0)
        {
            inlines.Add(new PdfInline(string.Empty, textStyle, null));
        }

        return WrapInlines(inlines, width, resources);
    }

    private static void CollectBlockText(Block block, List<PdfInline> inlines, PdfTextStyle textStyle, MarkdownExportStyle style)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                if (inlines.Count > 0)
                {
                    inlines.Add(new PdfInline("\n", textStyle, null));
                }

                inlines.AddRange(CollectInlines(paragraph.Inline, textStyle, style));
                break;
            case LeafBlock leaf:
                if (inlines.Count > 0)
                {
                    inlines.Add(new PdfInline("\n", textStyle, null));
                }

                inlines.Add(new PdfInline(leaf.Lines.ToString(), textStyle, null));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    CollectBlockText(child, inlines, textStyle, style);
                }

                break;
        }
    }

    private static void RenderImageParagraph(
        PdfLayout layout,
        LinkInline imageInline,
        string? documentPath,
        float x,
        float width,
        MarkdownExportStyle style)
    {
        if (!TryLoadImage(imageInline.Url, documentPath, layout.Resources, out var bitmap))
        {
            var altText = GetInlineText(imageInline);
            RenderParagraphText(
                layout,
                [new PdfInline(string.IsNullOrWhiteSpace(altText) ? imageInline.Url ?? string.Empty : altText, CreateBodyStyle(style, 0), null)],
                x,
                width,
                ParagraphSpacing);
            return;
        }

        var imageWidth = Math.Min(width, bitmap.Width);
        var imageHeight = bitmap.Height * (imageWidth / bitmap.Width);
        if (imageHeight > layout.Geometry.ContentHeight)
        {
            var scale = layout.Geometry.ContentHeight / imageHeight;
            imageWidth *= scale;
            imageHeight = layout.Geometry.ContentHeight;
        }

        layout.AddGap(4);
        layout.EnsureSpace(imageHeight);
        layout.AddOperation(new PdfImageOperation(bitmap, x, layout.Y, imageWidth, imageHeight));
        layout.Y += imageHeight;
        layout.AddGap(18);
    }

    private static bool TryLoadImage(
        string? url,
        string? documentPath,
        PdfRenderResources resources,
        [NotNullWhen(true)] out SKBitmap? bitmap)
    {
        bitmap = null;
        try
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var imageSource = MarkdownImageSourceLoader.Load(url, documentPath);
            var bytes = imageSource.IsSvg || imageSource.IsGif
                ? MarkdownImageRasterizer.RenderToPngBytes(imageSource)
                : imageSource.Bytes;
            bitmap = SKBitmap.Decode(bytes);
            if (bitmap is null)
            {
                return false;
            }

            resources.TrackBitmap(bitmap);
            return true;
        }
        catch
        {
            bitmap?.Dispose();
            bitmap = null;
            return false;
        }
    }

    private static IReadOnlyList<PdfInline> CollectInlines(
        ContainerInline? container,
        PdfTextStyle baseStyle,
        MarkdownExportStyle exportStyle)
    {
        var inlines = new List<PdfInline>();
        AppendInlines(inlines, container, baseStyle, exportStyle);
        if (inlines.Count == 0)
        {
            inlines.Add(new PdfInline(string.Empty, baseStyle, null));
        }

        return inlines;
    }

    private static void AppendInlines(
        List<PdfInline> inlines,
        ContainerInline? container,
        PdfTextStyle currentStyle,
        MarkdownExportStyle exportStyle)
    {
        if (container is null)
        {
            return;
        }

        // 行内 <span style>：开/闭标签以兄弟节点出现，用栈把样式带给后续兄弟，段落结束自然清空。
        var spanStack = new Stack<MarkdownSpanStyle>();
        foreach (var inline in container)
        {
            AppendInline(inlines, inline, currentStyle, exportStyle, spanStack);
        }
    }

    private static PdfTextStyle WithSpan(PdfTextStyle style, MarkdownSpanStyle span, out SKColor? background)
    {
        background = MarkdownSpanStyle.TryGetColor(span.Background) is { } bg ? new SKColor(bg.R, bg.G, bg.B) : null;
        // 作者色可读性回退：与段落底色对比不足（如暗色导出配深色文字）时沿用段落色。
        var color = EnsureReadableSkia(
            MarkdownSpanStyle.TryGetColor(span.Foreground),
            ToAvaloniaColor(background),
            style.Color);
        return style with
        {
            Color = color,
            FontSize = span.FontSize is { } size ? (float)size : style.FontSize,
        };
    }

    private static global::Avalonia.Media.Color? ToAvaloniaColor(SKColor? color) =>
        color is { } value ? new global::Avalonia.Media.Color(value.Alpha, value.Red, value.Green, value.Blue) : null;

    private static SKColor EnsureReadableSkia(global::Avalonia.Media.Color? authored, global::Avalonia.Media.Color? background, SKColor fallback) =>
        MarkdownSpanStyle.EnsureReadable(authored, background, new global::Avalonia.Media.Color(255, fallback.Red, fallback.Green, fallback.Blue)) is { } readable
            ? new SKColor(readable.R, readable.G, readable.B)
            : fallback;

    private static void AppendInline(
        List<PdfInline> inlines,
        MarkdigInline inline,
        PdfTextStyle currentStyle,
        MarkdownExportStyle exportStyle,
        Stack<MarkdownSpanStyle>? spanStack = null)
    {
        switch (inline)
        {
            case LiteralInline literal:
                inlines.Add(new PdfInline(literal.Content.ToString(), WithSpanStyle(currentStyle, spanStack, out var literalBackground), literalBackground));
                break;
            case CodeInline code:
                inlines.Add(new PdfInline(
                    code.Content,
                    WithSpanStyle(currentStyle, spanStack, out _) with
                    {
                        FontFamilies = exportStyle.MonoFontFamily,
                        FontSize = (float)exportStyle.CodeFontSize,
                        Color = ParseColor(exportStyle.InlineCodeForegroundColor, currentStyle.Color)
                    },
                    ParseColor(exportStyle.InlineCodeBackgroundColor, SKColors.Transparent)));
                break;
            case LineBreakInline:
                inlines.Add(new PdfInline("\n", WithSpanStyle(currentStyle, spanStack, out _), null));
                break;
            case EmphasisInline emphasis:
                AppendInlineChildren(inlines, emphasis, CreateEmphasisStyle(currentStyle, emphasis), exportStyle, spanStack);
                break;
            case LinkInline { IsImage: true } image:
                var imageText = GetInlineText(image);
                inlines.Add(new PdfInline(string.IsNullOrWhiteSpace(imageText) ? image.Url ?? string.Empty : imageText, WithSpanStyle(currentStyle, spanStack, out _), null));
                break;
            case LinkInline link:
                var linkStyle = WithSpanStyle(currentStyle, spanStack, out _) with
                {
                    Color = ParseColor(exportStyle.LinkColor, currentStyle.Color),
                    Underline = true
                };
                var beforeCount = inlines.Count;
                AppendInlineChildren(inlines, link, linkStyle, exportStyle, spanStack);
                if (inlines.Count == beforeCount && !string.IsNullOrWhiteSpace(link.Url))
                {
                    inlines.Add(new PdfInline(link.Url, linkStyle, null));
                }

                break;
            case HtmlInline html:
                AppendSpanTag(html.Tag, spanStack);
                break;
            case ContainerInline container:
                AppendInlineChildren(inlines, container, currentStyle, exportStyle, spanStack);
                break;
        }
    }

    /// <summary>嵌套容器递归：新建子栈继承当前栈（容器结束不影响兄弟），避免跨层级串样式。</summary>
    private static void AppendInlineChildren(
        List<PdfInline> inlines,
        ContainerInline container,
        PdfTextStyle currentStyle,
        MarkdownExportStyle exportStyle,
        Stack<MarkdownSpanStyle>? spanStack)
    {
        if (spanStack is null)
        {
            AppendInlines(inlines, container, currentStyle, exportStyle);
            return;
        }

        var childStack = new Stack<MarkdownSpanStyle>(spanStack.Reverse());
        foreach (var child in container)
        {
            AppendInline(inlines, child, currentStyle, exportStyle, childStack);
        }
    }

    /// <summary>处理 span 开/闭标签：开标签压栈（外层样式叠加），闭标签弹栈；其余标签按原文输出。</summary>
    private static void AppendSpanTag(string? tag, Stack<MarkdownSpanStyle>? spanStack)
    {
        if (spanStack is null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (tag.Equals("</span>", StringComparison.OrdinalIgnoreCase))
        {
            if (spanStack.Count > 0)
            {
                spanStack.Pop();
            }

            return;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            tag, @"<span\b[^>]*\bstyle\s*=\s*""([^""]*)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var outer = spanStack.Count > 0 ? spanStack.Peek() : new MarkdownSpanStyle();
            var own = MarkdownSpanStyle.FromDeclarations(match.Groups[1].Value);
            spanStack.Push(own is null ? outer : outer.Over(own));
        }
    }

    private static PdfTextStyle WithSpanStyle(PdfTextStyle style, Stack<MarkdownSpanStyle>? spanStack, out SKColor? background)
    {
        if (spanStack is not { Count: > 0 })
        {
            background = null;
            return style;
        }

        return WithSpan(style, spanStack.Peek(), out background);
    }

    private static PdfTextStyle CreateEmphasisStyle(PdfTextStyle currentStyle, EmphasisInline emphasis)
    {
        if (emphasis.DelimiterChar == '~')
        {
            return currentStyle with { Strike = true };
        }

        return emphasis.DelimiterCount >= 2
            ? currentStyle with { Weight = 700 }
            : currentStyle with { Italic = true };
    }

    private static IReadOnlyList<PdfLine> WrapInlines(
        IReadOnlyList<PdfInline> inlines,
        float maxWidth,
        PdfRenderResources resources)
    {
        var lines = new List<PdfLine>();
        var builder = new PdfLineBuilder(resources);
        PdfInline? pendingWhitespace = null;

        foreach (var inline in ExpandInlineParts(inlines))
        {
            if (inline.Text == "\n")
            {
                FlushLine(lines, builder, force: true);
                pendingWhitespace = null;
                continue;
            }

            if (string.IsNullOrEmpty(inline.Text))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(inline.Text))
            {
                if (!builder.IsEmpty)
                {
                    pendingWhitespace = pendingWhitespace is null
                        ? inline
                        : pendingWhitespace.Value with { Text = pendingWhitespace.Value.Text + inline.Text };
                }

                continue;
            }

            var inlineWidth = resources.MeasureText(inline.Text, inline.Style);
            var pendingWidth = pendingWhitespace.HasValue && !builder.IsEmpty
                ? resources.MeasureText(pendingWhitespace.Value.Text, pendingWhitespace.Value.Style)
                : 0;

            if (!builder.IsEmpty && builder.Width + pendingWidth + inlineWidth > maxWidth)
            {
                FlushLine(lines, builder, force: false);
                pendingWhitespace = null;
            }
            else if (pendingWhitespace.HasValue && !builder.IsEmpty)
            {
                builder.Add(pendingWhitespace.Value);
                pendingWhitespace = null;
            }

            AddWrappedInline(lines, builder, inline, maxWidth, resources);
        }

        FlushLine(lines, builder, force: true);
        if (lines.Count == 0)
        {
            lines.Add(PdfLine.Empty(inlines.FirstOrDefault().Style));
        }

        return lines;
    }

    private static void AddWrappedInline(
        List<PdfLine> lines,
        PdfLineBuilder builder,
        PdfInline inline,
        float maxWidth,
        PdfRenderResources resources)
    {
        if (builder.Width + resources.MeasureText(inline.Text, inline.Style) <= maxWidth || builder.IsEmpty)
        {
            if (resources.MeasureText(inline.Text, inline.Style) <= maxWidth)
            {
                builder.Add(inline);
                return;
            }
        }

        var runBuilder = new StringBuilder();
        foreach (var rune in inline.Text.EnumerateRunes())
        {
            var runeText = rune.ToString();
            var runeWidth = resources.MeasureText(runeText, inline.Style);
            if (runBuilder.Length > 0 && builder.Width + resources.MeasureText(runBuilder.ToString(), inline.Style) + runeWidth > maxWidth)
            {
                builder.Add(inline with { Text = runBuilder.ToString() });
                FlushLine(lines, builder, force: false);
                runBuilder.Clear();
            }

            if (builder.Width + runeWidth > maxWidth && !builder.IsEmpty)
            {
                FlushLine(lines, builder, force: false);
            }

            runBuilder.Append(runeText);
        }

        if (runBuilder.Length > 0)
        {
            builder.Add(inline with { Text = runBuilder.ToString() });
        }
    }

    private static IEnumerable<PdfInline> ExpandInlineParts(IEnumerable<PdfInline> inlines)
    {
        foreach (var inline in inlines)
        {
            var text = NormalizeLineEndings(inline.Text).Replace("\t", "    ", StringComparison.Ordinal);
            var index = 0;
            while (index < text.Length)
            {
                if (text[index] == '\n')
                {
                    yield return inline with { Text = "\n" };
                    index++;
                    continue;
                }

                var start = index;
                var isWhitespace = char.IsWhiteSpace(text[index]);
                while (index < text.Length && text[index] != '\n' && char.IsWhiteSpace(text[index]) == isWhitespace)
                {
                    index++;
                }

                yield return inline with { Text = text[start..index] };
            }
        }
    }

    private static void FlushLine(List<PdfLine> lines, PdfLineBuilder builder, bool force)
    {
        if (!builder.IsEmpty || force)
        {
            lines.Add(builder.Build());
            builder.Clear();
        }
    }

    private static PdfTextStyle CreateBodyStyle(MarkdownExportStyle style, int depth)
    {
        return CreateTextStyle(
            style.BodyFontFamily,
            (float)style.BodyFontSize,
            400,
            false,
            ParseColor(depth == 0 ? style.BodyColor : style.MutedColor, SKColors.Black),
            false,
            false,
            (float)style.LineHeightRatio);
    }

    private static PdfTextStyle CreateTextStyle(
        string fontFamilies,
        float fontSize,
        int weight,
        bool italic,
        SKColor color,
        bool underline,
        bool strike,
        float lineHeightRatio)
    {
        return new PdfTextStyle(fontFamilies, fontSize, weight, italic, color, underline, strike, lineHeightRatio);
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

    private static void DrawWatermark(SKCanvas canvas, MarkdownPdfExportOptions options, PdfPageGeometry geometry, PdfRenderResources resources)
    {
        if (string.IsNullOrWhiteSpace(options.WatermarkText) || !double.IsFinite(options.WatermarkOpacity) || options.WatermarkOpacity <= 0) return;
        var text = string.Concat(options.WatermarkText.EnumerateRunes().Take(60).Select(r => r.ToString()));
        var opacity = (byte)(Math.Clamp(options.WatermarkOpacity, 0, 0.5) * 255);
        var style = CreateBodyStyle(resources.Style, 0) with { FontSize = 20, Color = new SKColor(36, 118, 107, opacity) };
        canvas.Save();
        canvas.ClipRect(new SKRect(geometry.PageMargin, geometry.ContentTop, geometry.PageWidth - geometry.PageMargin, geometry.ContentBottom));
        for (float y = geometry.ContentTop + 80; y < geometry.ContentBottom; y += 150)
        for (float x = geometry.PageMargin + 40; x < geometry.PageWidth; x += 260)
        {
            canvas.Save(); canvas.Translate(x, y); canvas.RotateDegrees(-25);
            new PdfTextOperation(text, 0, 0, resources.MeasureText(text, style), style).Draw(canvas, resources);
            canvas.Restore();
        }
        canvas.Restore();
    }

    private static void DrawHeader(SKCanvas canvas, string title, SKColor textColor, SKColor lineColor, PdfPageGeometry geometry)
    {
        var headerBottom = geometry.PageMargin + HeaderHeight;
        using var linePaint = CreateLinePaint(lineColor);
        canvas.DrawLine(geometry.PageMargin, headerBottom, geometry.PageWidth - geometry.PageMargin, headerBottom, linePaint);

        using var textPaint = CreateTextPaint(textColor);
        using var font = CreateMetadataFont(HeaderFontSize);

        var titleMaxWidth = geometry.PageWidth - (geometry.PageMargin * 2);
        var visibleTitle = TrimToWidth(title, font, textPaint, titleMaxWidth);
        canvas.DrawText(visibleTitle, geometry.PageMargin, geometry.PageMargin + 17, font, textPaint);
    }

    private static void DrawFooter(SKCanvas canvas, string title, int pageNumber, int pageCount, SKColor textColor, SKColor lineColor, PdfPageGeometry geometry)
    {
        var footerTop = geometry.PageHeight - geometry.PageMargin - FooterHeight;
        using var linePaint = CreateLinePaint(lineColor);
        canvas.DrawLine(geometry.PageMargin, footerTop, geometry.PageWidth - geometry.PageMargin, footerTop, linePaint);

        using var textPaint = CreateTextPaint(textColor);
        using var font = CreateMetadataFont(FooterFontSize);

        var baseline = footerTop + 18;
        var pageText = $"{pageNumber} / {pageCount}";
        var pageTextWidth = font.MeasureText(pageText, textPaint);
        var pageTextX = geometry.PageWidth - geometry.PageMargin - pageTextWidth;
        var titleMaxWidth = Math.Max(0, pageTextX - geometry.PageMargin - 24);
        var visibleTitle = TrimToWidth(title, font, textPaint, titleMaxWidth);

        canvas.DrawText(visibleTitle, geometry.PageMargin, baseline, font, textPaint);
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
        foreach (var family in MetadataFontFamilies)
        {
            var typeface = SKTypeface.FromFamilyName(family);
            if (typeface is not null)
            {
                return new SKFont(typeface, size);
            }
        }

        return new SKFont(SKTypeface.Default, size);
    }

    private static readonly string[] MetadataFontFamilies =
    [
        "Microsoft YaHei UI",
        "Microsoft YaHei",
        "PingFang SC",
        "Noto Sans CJK SC",
        "Noto Sans SC",
        "Source Han Sans SC",
        "SimSun",
        "Arial Unicode MS"
    ];

    private static string ResolveHeaderTitle(MarkdownExportDocument document, MarkdownPdfExportOptions options)
    {
        return MarkdownExportHeadingScanner.FindFirstHeading(document.Markdown)
               ?? Path.GetFileNameWithoutExtension(ResolveFooterTitle(document, options))
               ?? options.DefaultHeading;
    }

    private static string ResolveFooterTitle(MarkdownExportDocument document, MarkdownPdfExportOptions? options = null)
    {
        if (!string.IsNullOrWhiteSpace(document.FilePath))
        {
            var fileName = Path.GetFileName(document.FilePath);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                return fileName;
            }
        }

        return string.IsNullOrWhiteSpace(document.FileName)
            ? options?.DefaultFileName ?? "document.md"
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

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private readonly record struct PdfInline(string Text, PdfTextStyle Style, SKColor? BackgroundColor);

    private readonly record struct PdfTextRun(string Text, PdfTextStyle Style, SKColor? BackgroundColor);

    private readonly record struct PdfTextStyle(
        string FontFamilies,
        float FontSize,
        int Weight,
        bool Italic,
        SKColor Color,
        bool Underline,
        bool Strike,
        float LineHeightRatio);

    private sealed class PdfLine(IReadOnlyList<PdfTextRun> runs, float width, float height, float baselineOffset)
    {
        public IReadOnlyList<PdfTextRun> Runs { get; } = runs;

        public float Width { get; } = width;

        public float Height { get; } = height;

        public float BaselineOffset { get; } = baselineOffset;

        public static PdfLine Empty(PdfTextStyle style)
        {
            var height = Math.Max(1, style.FontSize * style.LineHeightRatio);
            return new PdfLine([], 0, height, CalculateBaselineOffset(height, style.FontSize));
        }
    }

    private sealed class PdfLineBuilder(PdfRenderResources resources)
    {
        private readonly List<PdfTextRun> _runs = [];
        private float _maxFontSize;
        private float _maxLineHeightRatio = 1.5f;

        public bool IsEmpty => _runs.Count == 0;

        public float Width { get; private set; }

        public void Add(PdfInline inline)
        {
            if (inline.Text.Length == 0)
            {
                return;
            }

            Width += resources.MeasureText(inline.Text, inline.Style);
            _maxFontSize = Math.Max(_maxFontSize, inline.Style.FontSize);
            _maxLineHeightRatio = Math.Max(_maxLineHeightRatio, inline.Style.LineHeightRatio);

            if (_runs.Count > 0
                && _runs[^1].Style == inline.Style
                && _runs[^1].BackgroundColor == inline.BackgroundColor)
            {
                var previous = _runs[^1];
                _runs[^1] = previous with { Text = previous.Text + inline.Text };
                return;
            }

            _runs.Add(new PdfTextRun(inline.Text, inline.Style, inline.BackgroundColor));
        }

        public PdfLine Build()
        {
            var fontSize = _maxFontSize <= 0 && _runs.Count > 0 ? _runs.Max(run => run.Style.FontSize) : _maxFontSize;
            if (fontSize <= 0)
            {
                fontSize = 1;
            }

            var lineHeight = Math.Max(fontSize, fontSize * _maxLineHeightRatio);
            return new PdfLine(_runs.ToList(), Width, lineHeight, CalculateBaselineOffset(lineHeight, fontSize));
        }

        public void Clear()
        {
            _runs.Clear();
            Width = 0;
            _maxFontSize = 0;
            _maxLineHeightRatio = 1.5f;
        }
    }

    private static float CalculateBaselineOffset(float lineHeight, float fontSize)
    {
        return ((lineHeight - fontSize) / 2) + (fontSize * 0.82f);
    }

    private sealed record PdfPageGeometry(float PageWidth, float PageHeight, float PageMargin)
    {
        public float ContentTop => PageMargin + HeaderHeight + HeaderGap;
        public float ContentBottom => PageHeight - PageMargin - FooterGap - FooterHeight;
        public float ContentWidth => PageWidth - PageMargin * 2;
        public float ContentHeight => ContentBottom - ContentTop;

        public static PdfPageGeometry From(MarkdownPdfExportOptions options)
        {
            var (width, height) = options.PageSize.ToUpperInvariant() switch
            {
                "LETTER" => (612f, 792f), "A5" => (420f, 595f), _ => (595f, 842f)
            };
            var margin = options.MarginMillimeters is { } mm && double.IsFinite(mm) ? (float)(Math.Clamp(mm, 0, 50) * 72 / 25.4) : 36;
            return new(width, height, margin);
        }
    }

    private sealed class PdfLayout(PdfRenderResources resources, PdfPageGeometry geometry)
    {
        public PdfRenderResources Resources { get; } = resources;
        public PdfPageGeometry Geometry { get; } = geometry;

        public List<PdfPage> Pages { get; } = [new PdfPage()];

        public float Y { get; set; } = geometry.ContentTop;

        public void AddOperation(IPdfOperation operation)
        {
            Pages[^1].Operations.Add(operation);
        }

        public void EnsureSpace(float height)
        {
            if (height > geometry.ContentHeight)
            {
                if (Y >= geometry.ContentBottom)
                {
                    NewPage();
                }

                return;
            }

            if (Y + height > geometry.ContentBottom && Pages[^1].Operations.Count > 0)
            {
                NewPage();
            }
        }

        public void AddGap(float gap)
        {
            if (gap <= 0)
            {
                return;
            }

            Y = Math.Min(geometry.ContentBottom, Y + gap);
        }

        public void NewPage()
        {
            if (Pages[^1].Operations.Count == 0)
            {
                Y = geometry.ContentTop;
                return;
            }

            Pages.Add(new PdfPage());
            Y = geometry.ContentTop;
        }

        public PdfLayoutMark Mark()
        {
            return new PdfLayoutMark(Pages.Count - 1, Y, Pages[^1].Operations.Count);
        }

        public void InsertDecorationRange(PdfLayoutMark start, PdfLayoutMark end, PdfQuoteDecoration decoration)
        {
            if (end.PageIndex < start.PageIndex)
            {
                return;
            }

            for (var pageIndex = start.PageIndex; pageIndex <= end.PageIndex; pageIndex++)
            {
                var page = Pages[pageIndex];
                var top = pageIndex == start.PageIndex ? start.Y : geometry.ContentTop;
                var bottom = pageIndex == end.PageIndex ? end.Y : geometry.ContentBottom;
                if (bottom <= top)
                {
                    continue;
                }

                var operations = new IPdfOperation[]
                {
                    new PdfRectOperation(decoration.X, top, decoration.Width, bottom - top, decoration.BackgroundColor, null, 0),
                    new PdfRectOperation(decoration.X, top, 4, bottom - top, decoration.BorderColor, null, 0)
                };
                var insertIndex = pageIndex == start.PageIndex ? Math.Min(start.OperationIndex, page.Operations.Count) : 0;
                page.Operations.InsertRange(insertIndex, operations);
            }
        }
    }

    private readonly record struct PdfLayoutMark(int PageIndex, float Y, int OperationIndex);

    private readonly record struct PdfQuoteDecoration(float X, float Width, SKColor BackgroundColor, SKColor BorderColor);

    private sealed class PdfPage
    {
        public List<IPdfOperation> Operations { get; } = [];

        public void Draw(SKCanvas canvas, PdfRenderResources resources)
        {
            foreach (var operation in Operations)
            {
                operation.Draw(canvas, resources);
            }
        }
    }

    private interface IPdfOperation
    {
        void Draw(SKCanvas canvas, PdfRenderResources resources);
    }

    private sealed record PdfTextOperation(string Text, float X, float Baseline, float Width, PdfTextStyle Style) : IPdfOperation
    {
        public void Draw(SKCanvas canvas, PdfRenderResources resources)
        {
            var cursorX = X;
            foreach (var segment in resources.CreateSegments(Text, Style))
            {
                using var font = new SKFont(segment.Typeface, Style.FontSize) { Subpixel = true };
                using var paint = CreateTextPaint(Style.Color);
                canvas.DrawText(segment.Text, cursorX, Baseline, font, paint);
                cursorX += segment.Width;
            }

            if (!Style.Underline && !Style.Strike)
            {
                return;
            }

            using var linePaint = CreateLinePaint(Style.Color);
            linePaint.StrokeWidth = Math.Max(0.5f, Style.FontSize / 18);
            if (Style.Underline)
            {
                var underlineY = Baseline + (Style.FontSize * 0.12f);
                canvas.DrawLine(X, underlineY, X + Width, underlineY, linePaint);
            }

            if (Style.Strike)
            {
                var strikeY = Baseline - (Style.FontSize * 0.32f);
                canvas.DrawLine(X, strikeY, X + Width, strikeY, linePaint);
            }
        }
    }

    private sealed record PdfLineOperation(float X1, float Y1, float X2, float Y2, SKColor Color, float StrokeWidth) : IPdfOperation
    {
        public void Draw(SKCanvas canvas, PdfRenderResources resources)
        {
            using var paint = CreateLinePaint(Color);
            paint.StrokeWidth = StrokeWidth;
            canvas.DrawLine(X1, Y1, X2, Y2, paint);
        }
    }

    private sealed record PdfRectOperation(float X, float Y, float Width, float Height, SKColor? Fill, SKColor? Stroke, float StrokeWidth) : IPdfOperation
    {
        public void Draw(SKCanvas canvas, PdfRenderResources resources)
        {
            var rect = new SKRect(X, Y, X + Width, Y + Height);
            if (Fill.HasValue)
            {
                using var fillPaint = new SKPaint
                {
                    Color = Fill.Value,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                canvas.DrawRect(rect, fillPaint);
            }

            if (Stroke.HasValue && StrokeWidth > 0)
            {
                using var strokePaint = new SKPaint
                {
                    Color = Stroke.Value,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = StrokeWidth
                };
                canvas.DrawRect(rect, strokePaint);
            }
        }
    }

    private sealed record PdfRoundRectOperation(
        float X,
        float Y,
        float Width,
        float Height,
        float Radius,
        SKColor Fill,
        SKColor? Stroke,
        float StrokeWidth) : IPdfOperation
    {
        public void Draw(SKCanvas canvas, PdfRenderResources resources)
        {
            var rect = new SKRoundRect(new SKRect(X, Y, X + Width, Y + Height), Radius, Radius);
            using var fillPaint = new SKPaint
            {
                Color = Fill,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(rect, fillPaint);

            if (Stroke.HasValue && StrokeWidth > 0)
            {
                using var strokePaint = new SKPaint
                {
                    Color = Stroke.Value,
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = StrokeWidth
                };
                canvas.DrawRoundRect(rect, strokePaint);
            }
        }
    }

    private sealed record PdfImageOperation(SKBitmap Bitmap, float X, float Y, float Width, float Height) : IPdfOperation
    {
        public void Draw(SKCanvas canvas, PdfRenderResources resources)
        {
            var destination = new SKRect(X, Y, X + Width, Y + Height);
            canvas.DrawBitmap(Bitmap, destination);
        }
    }

    private sealed class PdfRenderResources(MarkdownExportStyle style) : IDisposable
    {
        private static readonly string[] FallbackFontFamilies =
        [
            "Microsoft YaHei UI",
            "Microsoft YaHei",
            "PingFang SC",
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "Source Han Sans SC",
            "SimSun",
            "Arial Unicode MS",
            "Segoe UI",
            "Arial"
        ];

        private static readonly SKTypeface DefaultTypeface = SKTypeface.Default;

        private readonly Dictionary<TypefaceKey, SKTypeface> _typefaces = [];
        private readonly Dictionary<FallbackTypefaceKey, SKTypeface> _fallbackTypefaces = [];
        private readonly List<SKBitmap> _bitmaps = [];

        public MarkdownExportStyle Style { get; } = style;

        public float MeasureText(string text, PdfTextStyle textStyle)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var width = 0f;
            foreach (var segment in CreateSegments(text, textStyle))
            {
                width += segment.Width;
            }

            return width;
        }

        public IEnumerable<TextSegment> CreateSegments(string text, PdfTextStyle textStyle)
        {
            using var paint = CreateTextPaint(textStyle.Color);
            var currentTypeface = (SKTypeface?)null;
            var currentText = new StringBuilder();

            foreach (var rune in text.EnumerateRunes())
            {
                var typeface = GetTypeface(textStyle, rune.Value);
                if (currentTypeface is not null && !ReferenceEquals(currentTypeface, typeface))
                {
                    var segmentText = currentText.ToString();
                    yield return new TextSegment(segmentText, currentTypeface, MeasureSegment(segmentText, currentTypeface, textStyle, paint));
                    currentText.Clear();
                }

                currentTypeface = typeface;
                currentText.Append(rune.ToString());
            }

            if (currentTypeface is not null && currentText.Length > 0)
            {
                var segmentText = currentText.ToString();
                yield return new TextSegment(segmentText, currentTypeface, MeasureSegment(segmentText, currentTypeface, textStyle, paint));
            }
        }

        public void TrackBitmap(SKBitmap bitmap)
        {
            _bitmaps.Add(bitmap);
        }

        public void Dispose()
        {
            foreach (var bitmap in _bitmaps)
            {
                bitmap.Dispose();
            }

            var disposedTypefaces = new List<SKTypeface>();
            foreach (var typeface in _typefaces.Values.Concat(_fallbackTypefaces.Values))
            {
                if (ReferenceEquals(typeface, DefaultTypeface)
                    || disposedTypefaces.Any(disposed => ReferenceEquals(disposed, typeface)))
                {
                    continue;
                }

                typeface.Dispose();
                disposedTypefaces.Add(typeface);
            }
        }

        private SKTypeface GetTypeface(PdfTextStyle textStyle, int rune)
        {
            foreach (var family in GetFontFamilies(textStyle.FontFamilies))
            {
                var typeface = GetBaseTypeface(family, textStyle);
                using var font = new SKFont(typeface);
                if (font.ContainsGlyph(rune))
                {
                    return typeface;
                }
            }

            foreach (var family in FallbackFontFamilies)
            {
                var typeface = GetBaseTypeface(family, textStyle);
                using var font = new SKFont(typeface);
                if (font.ContainsGlyph(rune))
                {
                    return typeface;
                }
            }

            var fallbackKey = new FallbackTypefaceKey(textStyle.Weight, textStyle.Italic, rune);
            if (_fallbackTypefaces.TryGetValue(fallbackKey, out var fallback))
            {
                return fallback;
            }

            var slant = textStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            fallback = SKFontManager.Default.MatchCharacter(null, textStyle.Weight, (int)SKFontStyleWidth.Normal, slant, null, rune)
                       ?? DefaultTypeface;
            _fallbackTypefaces[fallbackKey] = fallback;
            return fallback;
        }

        private SKTypeface GetBaseTypeface(string family, PdfTextStyle textStyle)
        {
            var key = new TypefaceKey(family, textStyle.Weight, textStyle.Italic);
            if (_typefaces.TryGetValue(key, out var typeface))
            {
                return typeface;
            }

            var slant = textStyle.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            typeface = SKTypeface.FromFamilyName(family, textStyle.Weight, (int)SKFontStyleWidth.Normal, slant)
                       ?? DefaultTypeface;
            _typefaces[key] = typeface;
            return typeface;
        }

        private static float MeasureSegment(string text, SKTypeface typeface, PdfTextStyle textStyle, SKPaint paint)
        {
            using var font = new SKFont(typeface, textStyle.FontSize) { Subpixel = true };
            return font.MeasureText(text, paint);
        }

        private static IEnumerable<string> GetFontFamilies(string fontFamilies)
        {
            foreach (var family in fontFamilies.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var trimmed = family.Trim('\'', '"');
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    yield return trimmed;
                }
            }
        }
    }

    private readonly record struct TypefaceKey(string Family, int Weight, bool Italic);

    private readonly record struct FallbackTypefaceKey(int Weight, bool Italic, int Rune);

    private readonly record struct TextSegment(string Text, SKTypeface Typeface, float Width);
}
