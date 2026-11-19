using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace CodeWF.Avalonia.MindView;

public static partial class MindMapDocumentCodec
{
    private static MindMapNode FromTextBundle(byte[]? bytes, string? filePath)
    {
        if (bytes is null || !LooksLikeZip(bytes))
        {
            return CreateMetadataNode(MindMapFileFormat.TextBundle, filePath);
        }

        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(item =>
            item.FullName.EndsWith("text.md", StringComparison.OrdinalIgnoreCase)
            || item.FullName.EndsWith("text.markdown", StringComparison.OrdinalIgnoreCase)
            || item.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase));
        return entry is null
            ? CreateMetadataNode(MindMapFileFormat.TextBundle, filePath)
            : FromMarkdown(ReadArchiveEntryText(entry));
    }

    private static MindMapNode FromOpenXmlPackage(byte[]? bytes, MindMapFileFormat format, string? filePath)
    {
        if (bytes is null || !LooksLikeZip(bytes))
        {
            return CreateMetadataNode(format, filePath);
        }

        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        return format switch
        {
            MindMapFileFormat.Word => FromWordArchive(archive, filePath),
            MindMapFileFormat.Excel => FromExcelArchive(archive, filePath),
            MindMapFileFormat.PowerPoint => FromPowerPointArchive(archive, filePath),
            MindMapFileFormat.Visio => FromVisioArchive(archive, filePath),
            _ => FromArchiveEntries(archive, format, filePath)
        };
    }

    private static MindMapNode FromWordArchive(ZipArchive archive, string? filePath)
    {
        var root = new MindMapNode(GetFileTitle(filePath, "Word"));
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return root;
        }

        var document = XDocument.Parse(ReadArchiveEntryText(entry));
        foreach (var paragraph in document
                     .Descendants()
                     .Where(element => element.Name.LocalName == "p")
                     .Select(paragraph => CleanText(string.Concat(paragraph
                         .Descendants()
                         .Where(text => text.Name.LocalName == "t")
                         .Select(text => text.Value))))
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Take(120))
        {
            root.Children.Add(new MindMapNode(paragraph));
        }

        return root;
    }

    private static MindMapNode FromPowerPointArchive(ZipArchive archive, string? filePath)
    {
        var root = new MindMapNode(GetFileTitle(filePath, "PowerPoint"));
        foreach (var entry in archive.Entries
                     .Where(entry => entry.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase)
                         && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(entry => entry.FullName)
                     .Take(120))
        {
            var document = XDocument.Parse(ReadArchiveEntryText(entry));
            var title = CleanText(string.Join(" ", document
                .Descendants()
                .Where(element => element.Name.LocalName == "t")
                .Select(element => element.Value)), Path.GetFileNameWithoutExtension(entry.FullName));
            root.Children.Add(new MindMapNode(title));
        }

        return root;
    }

    private static MindMapNode FromExcelArchive(ZipArchive archive, string? filePath)
    {
        var root = new MindMapNode(GetFileTitle(filePath, "Excel"));
        var sharedStrings = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStrings is not null)
        {
            var document = XDocument.Parse(ReadArchiveEntryText(sharedStrings));
            foreach (var value in document
                         .Descendants()
                         .Where(element => element.Name.LocalName == "t")
                         .Select(element => CleanText(element.Value))
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Take(200))
            {
                root.Children.Add(new MindMapNode(value));
            }
        }

        return root;
    }

    private static MindMapNode FromVisioArchive(ZipArchive archive, string? filePath)
    {
        return FromArchiveEntries(archive, MindMapFileFormat.Visio, filePath);
    }

    private static MindMapNode FromArchiveOrMetadata(byte[]? bytes, MindMapFileFormat format, string? filePath)
    {
        if (bytes is null || !LooksLikeZip(bytes))
        {
            return CreateMetadataNode(format, filePath);
        }

        using var memory = new MemoryStream(bytes);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        return FromArchiveEntries(archive, format, filePath);
    }

    private static MindMapNode FromArchiveOrText(byte[]? bytes, MindMapFileFormat format, string? filePath)
    {
        if (bytes is null)
        {
            return CreateMetadataNode(format, filePath);
        }

        if (LooksLikeZip(bytes))
        {
            return FromArchiveOrMetadata(bytes, format, filePath);
        }

        var text = DecodeUtf8(bytes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return CreateMetadataNode(format, filePath);
        }

        return text.TrimStart().StartsWith('{') || text.TrimStart().StartsWith('[')
            ? FromJson(text, filePath)
            : FromPlainText(text, filePath);
    }

    private static MindMapNode FromArchiveEntries(ZipArchive archive, MindMapFileFormat format, string? filePath)
    {
        foreach (var entry in archive.Entries.Where(IsReadableArchiveEntry).OrderBy(entry => entry.FullName).Take(20))
        {
            var text = ReadArchiveEntryText(entry);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var extension = Path.GetExtension(entry.FullName).ToLowerInvariant();
            try
            {
                return extension switch
                {
                    ".json" => FromJson(text, filePath),
                    ".md" or ".markdown" => FromMarkdown(text),
                    ".opml" => FromOpml(text),
                    ".xml" => FromXml(text, filePath),
                    ".html" or ".htm" => FromHtml(text, filePath),
                    ".txt" => FromPlainText(text, filePath),
                    _ => FromPlainText(text, filePath)
                };
            }
            catch
            {
                // 继续尝试包内下一个文本文件。
            }
        }

        return CreateMetadataNode(format, filePath);
    }

    private static bool IsReadableArchiveEntry(ZipArchiveEntry entry)
    {
        var extension = Path.GetExtension(entry.FullName).ToLowerInvariant();
        return extension is ".json" or ".xml" or ".md" or ".markdown" or ".opml" or ".html" or ".htm" or ".txt";
    }

    private static string ReadArchiveEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
