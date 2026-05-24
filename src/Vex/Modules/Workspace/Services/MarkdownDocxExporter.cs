using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Xml.Linq;
using Avalonia.Media.Imaging;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Vex.Core.Models;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace Vex.Modules.Workspace.Services;

internal static class MarkdownDocxExporter
{
    private const string StylesRelationshipId = "rIdStyles";
    private const long EmuPerPixelAt96Dpi = 9525;
    private const long MaxImageWidthEmu = 9026L * 635L;
    private const int MaxSvgRasterDimension = 4096;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace ContentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";
    private static readonly XNamespace Wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

    public static void Export(DocumentSnapshot document, string path, MarkdownExportStyle style)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var context = new DocxExportContext(document.FilePath);
        var documentXml = BuildDocument(document, style, context);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", BuildContentTypes());
        WriteEntry(archive, "_rels/.rels", BuildPackageRelationships());
        WriteEntry(archive, "word/_rels/document.xml.rels", BuildDocumentRelationships(context.ImageParts));
        WriteEntry(archive, "word/document.xml", documentXml);
        WriteEntry(archive, "word/styles.xml", BuildStyles(style));
        foreach (var image in context.ImageParts)
        {
            WriteEntry(archive, $"word/{image.Target}", image.Bytes);
        }
    }

    private static XDocument BuildContentTypes()
    {
        return new XDocument(
            new XElement(ContentTypes + "Types",
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "png"),
                    new XAttribute("ContentType", "image/png")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "jpg"),
                    new XAttribute("ContentType", "image/jpeg")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "jpeg"),
                    new XAttribute("ContentType", "image/jpeg")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "gif"),
                    new XAttribute("ContentType", "image/gif")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "bmp"),
                    new XAttribute("ContentType", "image/bmp")),
                new XElement(ContentTypes + "Default",
                    new XAttribute("Extension", "webp"),
                    new XAttribute("ContentType", "image/webp")),
                new XElement(ContentTypes + "Override",
                    new XAttribute("PartName", "/word/document.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")),
                new XElement(ContentTypes + "Override",
                    new XAttribute("PartName", "/word/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"))));
    }

    private static XDocument BuildPackageRelationships()
    {
        return new XDocument(
            new XElement(Rel + "Relationships",
                new XElement(Rel + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "word/document.xml"))));
    }

    private static XDocument BuildDocumentRelationships(IReadOnlyList<DocxImagePart> images)
    {
        var relationships = new XElement(Rel + "Relationships",
            new XElement(Rel + "Relationship",
                new XAttribute("Id", StylesRelationshipId),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                new XAttribute("Target", "styles.xml")));

        foreach (var image in images)
        {
            relationships.Add(new XElement(Rel + "Relationship",
                new XAttribute("Id", image.RelationshipId),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"),
                new XAttribute("Target", image.Target)));
        }

        return new XDocument(relationships);
    }

    private static XDocument BuildDocument(DocumentSnapshot document, MarkdownExportStyle style, DocxExportContext context)
    {
        var parsed = Markdown.Parse(document.Markdown ?? string.Empty, Pipeline);
        var body = new XElement(W + "body");
        foreach (var block in parsed)
        {
            AddBlock(body, block, style, context, 0);
        }

        if (!body.Elements().Any())
        {
            body.Add(CreateParagraph(string.Empty));
        }

        body.Add(CreateSectionProperties());
        return new XDocument(
            new XElement(W + "document",
                new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "r", R.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "wp", Wp.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "a", A.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "pic", Pic.NamespaceName),
                body));
    }

    private static XDocument BuildStyles(MarkdownExportStyle style)
    {
        return new XDocument(
            new XElement(W + "styles",
                new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName),
                new XElement(W + "docDefaults",
                    new XElement(W + "rPrDefault",
                        CreateStyleRunProperties(style, style.BodyFontFamily, style.BodyFontSize, style.BodyColor)),
                    new XElement(W + "pPrDefault",
                        new XElement(W + "pPr", CreateParagraphSpacing(style)))),
                CreateNormalStyle(style),
                CreateHeadingStyle("Heading1", "heading 1", style.Heading1FontSize, style.HeadingColor, style),
                CreateHeadingStyle("Heading2", "heading 2", style.Heading2FontSize, style.HeadingColor, style),
                CreateHeadingStyle("Heading3", "heading 3", style.Heading3FontSize, style.HeadingColor, style),
                CreateHeadingStyle("Heading4", "heading 4", style.Heading4FontSize, style.HeadingColor, style),
                CreateHeadingStyle("Heading5", "heading 5", style.Heading5FontSize, style.HeadingColor, style),
                CreateHeadingStyle("Heading6", "heading 6", style.Heading6FontSize, style.HeadingColor, style)));
    }

    private static XElement CreateNormalStyle(MarkdownExportStyle style)
    {
        return new XElement(W + "style",
            new XAttribute(W + "type", "paragraph"),
            new XAttribute(W + "default", "1"),
            new XAttribute(W + "styleId", "Normal"),
            new XElement(W + "name", new XAttribute(W + "val", "Normal")),
            new XElement(W + "qFormat"),
            new XElement(W + "pPr", CreateParagraphSpacing(style)),
            CreateStyleRunProperties(style, style.BodyFontFamily, style.BodyFontSize, style.BodyColor));
    }

    private static XElement CreateHeadingStyle(
        string styleId,
        string name,
        double fontSize,
        string color,
        MarkdownExportStyle style)
    {
        return new XElement(W + "style",
            new XAttribute(W + "type", "paragraph"),
            new XAttribute(W + "styleId", styleId),
            new XElement(W + "name", new XAttribute(W + "val", name)),
            new XElement(W + "basedOn", new XAttribute(W + "val", "Normal")),
            new XElement(W + "next", new XAttribute(W + "val", "Normal")),
            new XElement(W + "uiPriority", new XAttribute(W + "val", "9")),
            new XElement(W + "qFormat"),
            new XElement(W + "pPr",
                new XElement(W + "spacing",
                    new XAttribute(W + "before", "360"),
                    new XAttribute(W + "after", "200"))),
            CreateStyleRunProperties(style, style.BodyFontFamily, fontSize, color, bold: true));
    }

    private static XElement CreateStyleRunProperties(
        MarkdownExportStyle style,
        string fontFamily,
        double fontSize,
        string color,
        bool bold = false)
    {
        var runProperties = new XElement(W + "rPr",
            CreateRunFonts(fontFamily),
            new XElement(W + "color", new XAttribute(W + "val", CssColorToWordColor(color))),
            new XElement(W + "sz", new XAttribute(W + "val", ToHalfPoints(fontSize))),
            new XElement(W + "szCs", new XAttribute(W + "val", ToHalfPoints(fontSize))));

        if (bold)
        {
            runProperties.AddFirst(new XElement(W + "b"));
        }

        return runProperties;
    }

    private static XElement CreateParagraphSpacing(MarkdownExportStyle style)
    {
        return new XElement(W + "spacing",
            new XAttribute(W + "after", "160"),
            new XAttribute(W + "line", ToLineSpacing(style.LineHeightRatio)),
            new XAttribute(W + "lineRule", "auto"));
    }

    private static void AddBlock(
        XElement body,
        Block block,
        MarkdownExportStyle style,
        DocxExportContext context,
        int depth)
    {
        switch (block)
        {
            case HeadingBlock heading:
                body.Add(CreateHeading(heading, style, context));
                break;
            case ParagraphBlock paragraph:
                body.Add(CreateParagraph(paragraph.Inline, style, context, depth));
                break;
            case ListBlock list:
                AddList(body, list, style, context, depth);
                break;
            case QuoteBlock quote:
                foreach (var child in quote)
                {
                    AddBlock(body, child, style, context, depth + 1);
                }

                break;
            case CodeBlock code:
                AddCodeBlock(body, code, style);
                break;
            case ThematicBreakBlock:
                body.Add(CreateHorizontalRule(style));
                break;
            case Table table:
                body.Add(CreateTable(table, style, context));
                break;
            case HtmlBlock html:
                body.Add(CreateParagraph(html.Lines.ToString()));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AddBlock(body, child, style, context, depth);
                }

                break;
        }
    }

    private static XElement CreateHeading(HeadingBlock heading, MarkdownExportStyle style, DocxExportContext context)
    {
        var paragraph = CreateParagraph(heading.Inline, style, context, 0);
        paragraph.AddFirst(
            new XElement(W + "pPr",
                new XElement(W + "pStyle",
                    new XAttribute(W + "val", $"Heading{Math.Clamp(heading.Level, 1, 6)}"))));
        foreach (var run in paragraph.Elements(W + "r"))
        {
            AddRunProperty(run, new XElement(W + "b"));
        }

        return paragraph;
    }

    private static XElement CreateParagraph(
        ContainerInline? inline,
        MarkdownExportStyle style,
        DocxExportContext context,
        int depth)
    {
        var paragraph = new XElement(W + "p");
        if (depth > 0)
        {
            paragraph.Add(new XElement(W + "pPr",
                new XElement(W + "ind", new XAttribute(W + "left", Math.Min(depth, 4) * 360))));
        }

        foreach (var run in CreateRuns(inline, style, context))
        {
            paragraph.Add(run);
        }

        if (!paragraph.Elements(W + "r").Any())
        {
            paragraph.Add(CreateTextRun(string.Empty));
        }

        return paragraph;
    }

    private static XElement CreateParagraph(string text)
    {
        return new XElement(W + "p", CreateTextRun(text));
    }

    private static void AddList(
        XElement body,
        ListBlock list,
        MarkdownExportStyle style,
        DocxExportContext context,
        int depth)
    {
        var index = string.IsNullOrWhiteSpace(list.OrderedStart)
            ? 1
            : int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var marker = ResolveListMarker(list, item, index);
            if (list.IsOrdered)
            {
                index++;
            }

            var paragraph = new XElement(W + "p",
                new XElement(W + "pPr",
                    new XElement(W + "ind", new XAttribute(W + "left", Math.Min(depth + 1, 5) * 360))),
                CreateTextRun($"{marker} "));
            var firstParagraph = item.OfType<ParagraphBlock>().FirstOrDefault();
            if (firstParagraph is not null)
            {
                foreach (var run in CreateRuns(firstParagraph.Inline, style, context))
                {
                    paragraph.Add(run);
                }
            }

            body.Add(paragraph);
            foreach (var child in item.Where(child => !ReferenceEquals(child, firstParagraph)))
            {
                AddBlock(body, child, style, context, depth + 1);
            }
        }
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

    private static void AddCodeBlock(XElement body, CodeBlock code, MarkdownExportStyle style)
    {
        var lines = code.Lines.ToString().ReplaceLineEndings("\n").Split('\n');
        foreach (var line in lines)
        {
            var paragraph = CreateParagraph(line);
            foreach (var run in paragraph.Elements(W + "r"))
            {
                AddRunProperty(run,
                    CreateRunFonts(style.MonoFontFamily),
                    new XElement(W + "color", new XAttribute(W + "val", CssColorToWordColor(style.CodeForegroundColor))),
                    new XElement(W + "shd",
                        new XAttribute(W + "val", "clear"),
                        new XAttribute(W + "fill", CssColorToWordColor(style.CodeBackgroundColor))));
            }

            body.Add(paragraph);
        }
    }

    private static XElement CreateHorizontalRule(MarkdownExportStyle style)
    {
        return new XElement(W + "p",
            new XElement(W + "pPr",
                new XElement(W + "pBdr",
                    new XElement(W + "bottom",
                        new XAttribute(W + "val", "single"),
                        new XAttribute(W + "sz", "6"),
                        new XAttribute(W + "space", "1"),
                        new XAttribute(W + "color", CssColorToWordColor(style.BorderColor))))));
    }

    private static XElement CreateTable(Table table, MarkdownExportStyle style, DocxExportContext context)
    {
        var element = new XElement(W + "tbl",
            new XElement(W + "tblPr",
                new XElement(W + "tblBorders",
                    CreateBorder("top", style.BorderColor),
                    CreateBorder("left", style.BorderColor),
                    CreateBorder("bottom", style.BorderColor),
                    CreateBorder("right", style.BorderColor),
                    CreateBorder("insideH", style.BorderColor),
                    CreateBorder("insideV", style.BorderColor))));

        foreach (var row in table.OfType<TableRow>())
        {
            var rowElement = new XElement(W + "tr");
            foreach (var cell in row.OfType<TableCell>())
            {
                var cellElement = new XElement(W + "tc");
                if (row.IsHeader)
                {
                    cellElement.Add(new XElement(W + "tcPr",
                        new XElement(W + "shd",
                            new XAttribute(W + "fill", CssColorToWordColor(style.TableHeaderBackgroundColor)))));
                }

                foreach (var child in cell)
                {
                    AddCellBlock(cellElement, child, style, context);
                }

                if (!cellElement.Elements(W + "p").Any())
                {
                    cellElement.Add(CreateParagraph(string.Empty));
                }

                rowElement.Add(cellElement);
            }

            element.Add(rowElement);
        }

        return element;
    }

    private static XElement CreateBorder(string name, string color)
    {
        return new XElement(W + name,
            new XAttribute(W + "val", "single"),
            new XAttribute(W + "sz", "4"),
            new XAttribute(W + "space", "0"),
            new XAttribute(W + "color", CssColorToWordColor(color)));
    }

    private static void AddCellBlock(XElement cell, Block block, MarkdownExportStyle style, DocxExportContext context)
    {
        switch (block)
        {
            case ParagraphBlock paragraph:
                cell.Add(CreateParagraph(paragraph.Inline, style, context, 0));
                break;
            case LeafBlock leaf:
                cell.Add(CreateParagraph(leaf.Lines.ToString()));
                break;
            case ContainerBlock container:
                foreach (var child in container)
                {
                    AddCellBlock(cell, child, style, context);
                }

                break;
        }
    }

    private static IEnumerable<XElement> CreateRuns(ContainerInline? inline, MarkdownExportStyle style, DocxExportContext context)
    {
        if (inline is null)
        {
            yield break;
        }

        var current = inline.FirstChild;
        while (current is not null)
        {
            foreach (var run in CreateRun(current, style, context))
            {
                yield return run;
            }

            current = current.NextSibling;
        }
    }

    private static IEnumerable<XElement> CreateRun(
        MarkdigInline inline,
        MarkdownExportStyle style,
        DocxExportContext context)
    {
        switch (inline)
        {
            case LiteralInline literal:
                yield return CreateTextRun(literal.Content.ToString());
                break;
            case CodeInline code:
                var codeRun = CreateTextRun(code.Content);
                AddRunProperty(codeRun,
                    CreateRunFonts(style.MonoFontFamily),
                    new XElement(W + "color", new XAttribute(W + "val", CssColorToWordColor(style.CodeForegroundColor))),
                    new XElement(W + "shd",
                        new XAttribute(W + "val", "clear"),
                        new XAttribute(W + "fill", CssColorToWordColor(style.InlineCodeBackgroundColor))));
                yield return codeRun;
                break;
            case LineBreakInline:
                yield return new XElement(W + "r", new XElement(W + "br"));
                break;
            case EmphasisInline emphasis:
                foreach (var run in CreateRuns(emphasis, style, context))
                {
                    AddRunProperty(run, emphasis.DelimiterCount >= 2 ? new XElement(W + "b") : new XElement(W + "i"));
                    yield return run;
                }

                break;
            case LinkInline { IsImage: true } image:
                if (context.TryCreateImageRun(image, out var imageRun))
                {
                    yield return imageRun;
                }
                else
                {
                    yield return CreateTextRun(GetInlineText(image));
                }

                break;
            case LinkInline link:
                foreach (var run in CreateRuns(link, style, context))
                {
                    AddRunProperty(run,
                        new XElement(W + "color", new XAttribute(W + "val", CssColorToWordColor(style.LinkColor))),
                        new XElement(W + "u", new XAttribute(W + "val", "single")));
                    yield return run;
                }

                if (!string.IsNullOrWhiteSpace(link.Url))
                {
                    yield return CreateTextRun($" ({WebUtility.HtmlDecode(link.Url)})");
                }

                break;
            case TaskList taskList:
                yield return CreateTextRun(taskList.Checked ? "[x] " : "[ ] ");
                break;
            case HtmlInline html:
                yield return CreateTextRun(html.Tag);
                break;
            case ContainerInline nested:
                foreach (var run in CreateRuns(nested, style, context))
                {
                    yield return run;
                }

                break;
        }
    }

    private static XElement CreateTextRun(string text)
    {
        return new XElement(W + "r",
            new XElement(W + "t",
                new XAttribute(Xml + "space", "preserve"),
                text));
    }

    private static XElement CreateImageRun(
        string relationshipId,
        string fileName,
        string description,
        int imageId,
        long widthEmu,
        long heightEmu)
    {
        return new XElement(W + "r",
            new XElement(W + "drawing",
                new XElement(Wp + "inline",
                    new XAttribute("distT", "0"),
                    new XAttribute("distB", "0"),
                    new XAttribute("distL", "0"),
                    new XAttribute("distR", "0"),
                    new XElement(Wp + "extent",
                        new XAttribute("cx", widthEmu),
                        new XAttribute("cy", heightEmu)),
                    new XElement(Wp + "effectExtent",
                        new XAttribute("l", "0"),
                        new XAttribute("t", "0"),
                        new XAttribute("r", "0"),
                        new XAttribute("b", "0")),
                    new XElement(Wp + "docPr",
                        new XAttribute("id", imageId),
                        new XAttribute("name", $"Picture {imageId}"),
                        new XAttribute("descr", description)),
                    new XElement(Wp + "cNvGraphicFramePr",
                        new XElement(A + "graphicFrameLocks",
                            new XAttribute("noChangeAspect", "1"))),
                    new XElement(A + "graphic",
                        new XElement(A + "graphicData",
                            new XAttribute("uri", Pic.NamespaceName),
                            new XElement(Pic + "pic",
                                new XElement(Pic + "nvPicPr",
                                    new XElement(Pic + "cNvPr",
                                        new XAttribute("id", imageId),
                                        new XAttribute("name", fileName),
                                        new XAttribute("descr", description)),
                                    new XElement(Pic + "cNvPicPr")),
                                new XElement(Pic + "blipFill",
                                    new XElement(A + "blip",
                                        new XAttribute(R + "embed", relationshipId)),
                                    new XElement(A + "stretch",
                                        new XElement(A + "fillRect"))),
                                new XElement(Pic + "spPr",
                                    new XElement(A + "xfrm",
                                        new XElement(A + "off",
                                            new XAttribute("x", "0"),
                                            new XAttribute("y", "0")),
                                        new XElement(A + "ext",
                                            new XAttribute("cx", widthEmu),
                                            new XAttribute("cy", heightEmu))),
                                    new XElement(A + "prstGeom",
                                        new XAttribute("prst", "rect"),
                                        new XElement(A + "avLst")))))))));
    }

    private static void AddRunProperty(XElement run, params XElement[] properties)
    {
        var runProperties = run.Element(W + "rPr");
        if (runProperties is null)
        {
            runProperties = new XElement(W + "rPr");
            run.AddFirst(runProperties);
        }

        foreach (var property in properties)
        {
            runProperties.Add(property);
        }
    }

    private static XElement CreateRunFonts(string fontFamily)
    {
        var primary = ResolvePrimaryFontFamily(fontFamily);
        var eastAsia = ResolveEastAsiaFontFamily(fontFamily);
        return new XElement(W + "rFonts",
            new XAttribute(W + "ascii", primary),
            new XAttribute(W + "hAnsi", primary),
            new XAttribute(W + "eastAsia", eastAsia),
            new XAttribute(W + "cs", primary));
    }

    private static XElement CreateSectionProperties()
    {
        return new XElement(W + "sectPr",
            new XElement(W + "pgSz",
                new XAttribute(W + "w", "11906"),
                new XAttribute(W + "h", "16838")),
            new XElement(W + "pgMar",
                new XAttribute(W + "top", "1440"),
                new XAttribute(W + "right", "1440"),
                new XAttribute(W + "bottom", "1440"),
                new XAttribute(W + "left", "1440"),
                new XAttribute(W + "header", "720"),
                new XAttribute(W + "footer", "720"),
                new XAttribute(W + "gutter", "0")));
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

    private static bool TryResolveLocalImagePath(string? url, string? documentPath, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(url)
            || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!uri.IsFile)
            {
                return false;
            }

            path = uri.LocalPath;
            return File.Exists(path);
        }

        foreach (var candidate in EnumerateLocalImagePathCandidates(url))
        {
            if (Path.IsPathRooted(candidate))
            {
                path = candidate;
                if (File.Exists(path))
                {
                    return true;
                }
            }
        }

        var baseDirectory = string.IsNullOrWhiteSpace(documentPath) ? null : Path.GetDirectoryName(documentPath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return false;
        }

        foreach (var candidate in EnumerateLocalImagePathCandidates(url))
        {
            path = Path.GetFullPath(Path.Combine(baseDirectory, candidate));
            if (File.Exists(path))
            {
                return true;
            }
        }

        path = string.Empty;
        return false;
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

    private static bool TryGetWordImageContentType(string path, out string extension, out string contentType)
    {
        extension = Path.GetExtension(path).ToLowerInvariant();
        contentType = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => string.Empty
        };

        return contentType.Length > 0;
    }

    private static bool TryGetDataImageBytes(string? url, out byte[] bytes, out string extension)
    {
        bytes = [];
        extension = string.Empty;
        if (string.IsNullOrWhiteSpace(url)
            || !url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commaIndex = url.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex < 0)
        {
            return false;
        }

        var metadata = url[..commaIndex];
        if (!metadata.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        extension = metadata.ToLowerInvariant() switch
        {
            var value when value.Contains("image/png", StringComparison.Ordinal) => ".png",
            var value when value.Contains("image/jpeg", StringComparison.Ordinal) => ".jpg",
            var value when value.Contains("image/gif", StringComparison.Ordinal) => ".gif",
            var value when value.Contains("image/bmp", StringComparison.Ordinal) => ".bmp",
            var value when value.Contains("image/webp", StringComparison.Ordinal) => ".webp",
            var value when value.Contains("image/svg+xml", StringComparison.Ordinal) => ".svg",
            _ => ".png"
        };

        try
        {
            bytes = Convert.FromBase64String(url[(commaIndex + 1)..]);
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            bytes = [];
            extension = string.Empty;
            return false;
        }
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

        var prefix = System.Text.Encoding.UTF8.GetString(bytes, 0, length).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
               || prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
               && prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] RenderSvgToPngBytes(byte[] svgBytes)
    {
        using var svg = new Svg.Skia.SKSvg();
        using var svgStream = new MemoryStream(svgBytes);
        var picture = svg.Load(svgStream) ?? svg.Picture;
        if (picture is null)
        {
            throw new InvalidDataException("SVG picture could not be loaded.");
        }

        var bounds = picture.CullRect;
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
        var scale = Math.Min(1d, MaxSvgRasterDimension / (double)Math.Max(width, height));
        var scaledWidth = Math.Max(1, (int)Math.Ceiling(width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Ceiling(height * scale));

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(
            scaledWidth,
            scaledHeight,
            SkiaSharp.SKColorType.Rgba8888,
            SkiaSharp.SKAlphaType.Premul));
        if (surface is null)
        {
            throw new InvalidDataException("SVG rendering surface could not be created.");
        }

        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Transparent);
        canvas.Scale((float)scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();

        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? throw new InvalidDataException("SVG picture could not be encoded.");
    }

    private static bool TryNormalizeImageForWord(
        byte[] sourceBytes,
        string sourceExtension,
        out byte[] bytes,
        out string extension,
        out int pixelWidth,
        out int pixelHeight)
    {
        bytes = sourceBytes;
        extension = sourceExtension.ToLowerInvariant();
        pixelWidth = 0;
        pixelHeight = 0;

        try
        {
            if (extension == ".svg" || IsSvgBytes(sourceBytes))
            {
                bytes = RenderSvgToPngBytes(sourceBytes);
                extension = ".png";
            }
            else if (extension == ".webp")
            {
                using var input = new MemoryStream(sourceBytes);
                using var bitmap = new Bitmap(input);
                using var output = new MemoryStream();
                bitmap.Save(output);
                bytes = output.ToArray();
                extension = ".png";
            }

            using var stream = new MemoryStream(bytes);
            using var decoded = new Bitmap(stream);
            pixelWidth = decoded.PixelSize.Width;
            pixelHeight = decoded.PixelSize.Height;
            return pixelWidth > 0 && pixelHeight > 0 && TryGetWordImageContentType($"image{extension}", out _, out _);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException
                                   or NotSupportedException
                                   or InvalidDataException)
        {
            bytes = [];
            extension = string.Empty;
            pixelWidth = 0;
            pixelHeight = 0;
            return false;
        }
    }

    private static (long Width, long Height) CalculateImageSizeEmu(int pixelWidth, int pixelHeight)
    {
        var width = Math.Max(1L, pixelWidth * EmuPerPixelAt96Dpi);
        var height = Math.Max(1L, pixelHeight * EmuPerPixelAt96Dpi);
        if (width <= MaxImageWidthEmu)
        {
            return (width, height);
        }

        var ratio = (double)MaxImageWidthEmu / width;
        return (MaxImageWidthEmu, Math.Max(1L, (long)Math.Round(height * ratio)));
    }

    private static string CssColorToWordColor(string color)
    {
        return color.TrimStart('#').Length >= 6
            ? color.TrimStart('#')[..6]
            : "000000";
    }

    private static string ToHalfPoints(double points)
    {
        return Math.Max(1, (int)Math.Round(points * 2d)).ToString(CultureInfo.InvariantCulture);
    }

    private static string ToLineSpacing(double lineHeightRatio)
    {
        return Math.Max(240, (int)Math.Round(240d * lineHeightRatio)).ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolvePrimaryFontFamily(string fontFamily)
    {
        return EnumerateFontFamilies(fontFamily).FirstOrDefault() ?? "Arial";
    }

    private static string ResolveEastAsiaFontFamily(string fontFamily)
    {
        var families = EnumerateFontFamilies(fontFamily).ToArray();
        foreach (var preferred in new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Source Han Sans SC", "SimSun" })
        {
            if (families.Any(family => family.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
            {
                return preferred;
            }
        }

        return families.FirstOrDefault() ?? "Microsoft YaHei UI";
    }

    private static IEnumerable<string> EnumerateFontFamilies(string fontFamily)
    {
        foreach (var family in fontFamily.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = family.Trim('\'', '"');
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                yield return normalized;
            }
        }
    }

    private static void WriteEntry(ZipArchive archive, string path, XDocument document)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        document.Save(stream);
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private sealed class DocxExportContext(string? documentPath)
    {
        private int _imageIndex;

        public List<DocxImagePart> ImageParts { get; } = [];

        public bool TryCreateImageRun(LinkInline image, out XElement run)
        {
            run = null!;
            byte[] sourceBytes;
            string sourceExtension;
            if (TryGetDataImageBytes(image.Url, out var dataBytes, out var dataExtension))
            {
                sourceBytes = dataBytes;
                sourceExtension = dataExtension;
            }
            else if (TryResolveLocalImagePath(image.Url, documentPath, out var path))
            {
                try
                {
                    sourceBytes = File.ReadAllBytes(path);
                    sourceExtension = Path.GetExtension(path);
                }
                catch (Exception ex) when (ex is IOException
                                           or UnauthorizedAccessException
                                           or ArgumentException
                                           or NotSupportedException
                                           or PathTooLongException)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            if (!TryNormalizeImageForWord(sourceBytes, sourceExtension, out var bytes, out var extension, out var pixelWidth, out var pixelHeight))
            {
                return false;
            }

            var imageId = ++_imageIndex;
            var relationshipId = $"rIdImage{imageId}";
            var fileName = $"image{imageId}{extension}";
            var target = $"media/{fileName}";
            var description = GetInlineText(image);
            if (string.IsNullOrWhiteSpace(description))
            {
                description = image.Url ?? fileName;
            }

            var (widthEmu, heightEmu) = CalculateImageSizeEmu(pixelWidth, pixelHeight);
            ImageParts.Add(new DocxImagePart(relationshipId, target, bytes));
            run = CreateImageRun(relationshipId, fileName, description, imageId, widthEmu, heightEmu);
            return true;
        }
    }

    private sealed record DocxImagePart(string RelationshipId, string Target, byte[] Bytes);
}
