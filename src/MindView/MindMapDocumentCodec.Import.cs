using System.Diagnostics;
using CodeWF.Avalonia.MindView.I18n;

namespace CodeWF.Avalonia.MindView;

public static partial class MindMapDocumentCodec
{
    private delegate MindMapNode DocumentImportStrategy(DocumentImportContext context);

    private readonly record struct DocumentImportContext(
        MindMapFileFormat Format,
        string? TextContent,
        byte[]? BinaryContent,
        string? FilePath)
    {
        public string TextOrBinaryUtf8 => TextContent ?? DecodeUtf8(BinaryContent);
    }

    private static readonly IReadOnlyDictionary<MindMapFileFormat, DocumentImportStrategy> DocumentImportStrategies =
        new Dictionary<MindMapFileFormat, DocumentImportStrategy>
        {
            [MindMapFileFormat.Markdown] = context => FromMarkdown(context.TextContent ?? string.Empty),
            [MindMapFileFormat.Opml] = context => FromOpml(context.TextContent ?? string.Empty),
            [MindMapFileFormat.XMind] = context => FromXMind(context.BinaryContent ?? []),
            [MindMapFileFormat.FreeMind] = context => FromFreeMind(context.TextOrBinaryUtf8),
            [MindMapFileFormat.BaiduMindMap] = context => FromJson(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Json] = context => FromJson(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Yaml] = context => FromYaml(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Csv] = context => FromCsv(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.PlainText] = context => FromPlainText(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Html] = context => FromHtml(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Svg] = context => FromSvg(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Xml] = context => FromXml(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.DrawIo] = context => FromDrawIo(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Gliffy] = context => FromJson(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Lucid] = context => FromJson(context.TextOrBinaryUtf8, context.FilePath),
            [MindMapFileFormat.Word] = context => FromOpenXmlPackage(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.Excel] = context => FromOpenXmlPackage(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.PowerPoint] = context => FromOpenXmlPackage(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.TextBundle] = context => FromTextBundle(context.BinaryContent, context.FilePath),
            [MindMapFileFormat.MindManager] = context => FromArchiveOrMetadata(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.MindNode] = context => FromArchiveOrMetadata(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.MindMaster] = context => FromArchiveOrMetadata(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.MindNow] = context => FromArchiveOrText(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.Visio] = context => FromOpenXmlPackage(context.BinaryContent, context.Format, context.FilePath),
            [MindMapFileFormat.Image] = context => CreateMetadataNode(context.Format, context.FilePath),
            [MindMapFileFormat.WebP] = context => CreateMetadataNode(context.Format, context.FilePath),
            [MindMapFileFormat.Pdf] = context => CreateMetadataNode(context.Format, context.FilePath)
        };

    public static MindMapNode FromDocument(
        MindMapFileFormat format,
        string? textContent,
        byte[]? binaryContent,
        string? filePath = null)
    {
        try
        {
            var context = new DocumentImportContext(format, textContent, binaryContent, filePath);
            return DocumentImportStrategies.TryGetValue(format, out var strategy)
                ? strategy(context)
                : CreateMetadataNode(format, filePath);
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"Mind map document import failed. format={format}: {exception}");
            return CreateMetadataNode(
                format,
                filePath,
                FormatResource(MindViewL.ParseFailed, "Parse failed: {0}", exception.Message));
        }
    }

}
