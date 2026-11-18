using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeWF.Avalonia.MindView.I18n;
using CodeWF.Toolkit.Lang;
using System.Globalization;

namespace CodeWF.Avalonia.MindView;

public static partial class MindMapDocumentCodec
{
    private const int RootMarkdownHeadingLevel = 1;

    public static string ToMarkdown(MindMapNode root)
    {
        var lines = new List<string>();
        WriteMarkdownHeadingNode(root, RootMarkdownHeadingLevel, lines);
        return string.Join(Environment.NewLine, lines);
    }

    public static MindMapNode FromMarkdown(string markdown)
    {
        MindMapNode? root = null;
        MindMapNode? lastNode = null;
        var rootLevel = 1;
        var stack = new Dictionary<int, MindMapNode>();
        var listStack = new Dictionary<int, MindMapNode>();
        MindMapNode? listBaseNode = null;
        var leadingNotes = new List<string>();

        foreach (var rawLine in SplitLines(markdown))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (TryParseHeading(line, out var level, out var title))
            {
                if (root is null)
                {
                    root = new MindMapNode(title);
                    rootLevel = level;
                    stack[level] = root;
                    lastNode = root;
                    listBaseNode = root;
                    listStack.Clear();
                    continue;
                }

                var node = new MindMapNode(title);
                FindMarkdownParent(level, rootLevel, root, stack).Children.Add(node);
                stack[level] = node;
                lastNode = node;
                listBaseNode = node;
                listStack.Clear();

                foreach (var staleLevel in stack.Keys.Where(key => key > level).ToList())
                {
                    stack.Remove(staleLevel);
                }

                continue;
            }

            if (TryParseMarkdownListItem(line, out var indent, out title))
            {
                if (root is null)
                {
                    root = new MindMapNode(title);
                    rootLevel = 0;
                    stack[rootLevel] = root;
                    listStack[indent] = root;
                    listBaseNode = root;
                    lastNode = root;
                    continue;
                }

                var node = new MindMapNode(title);
                var parent = FindMarkdownListParent(indent, listBaseNode ?? lastNode ?? root, listStack);
                parent.Children.Add(node);
                lastNode = node;

                foreach (var staleIndent in listStack.Keys.Where(key => key >= indent).ToList())
                {
                    listStack.Remove(staleIndent);
                }

                listStack[indent] = node;
                continue;
            }

            if (lastNode is not null)
            {
                lastNode.Note = AppendNote(lastNode.Note, line.Trim());
            }
            else
            {
                leadingNotes.Add(line.Trim());
            }
        }

        root ??= new MindMapNode(string.Empty);
        if (leadingNotes.Count > 0)
        {
            root.Note = AppendNote(root.Note, string.Join(Environment.NewLine, leadingNotes));
        }

        return root;
    }

    public static string ToOpml(MindMapNode root)
    {
        var document = new XDocument(
            new XElement(
                "opml",
                new XAttribute("version", "2.0"),
                new XElement("head", new XElement("title", GetTitle(root))),
                new XElement("body", ToOpmlOutline(root))));

        return document.ToString();
    }

    public static MindMapNode FromOpml(string opml)
    {
        var document = XDocument.Parse(opml);
        var body = document.Root?.Elements().FirstOrDefault(IsBodyElement);
        var outlines = body?.Elements().Where(IsOutlineElement).ToList() ?? [];

        if (outlines.Count == 1)
        {
            return FromOpmlOutline(outlines[0]);
        }

        var title = document.Root?
            .Elements().FirstOrDefault(IsHeadElement)?
            .Elements().FirstOrDefault(element => element.Name.LocalName == "title")?
            .Value;
        var root = new MindMapNode(string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim());

        foreach (var outline in outlines)
        {
            root.Children.Add(FromOpmlOutline(outline));
        }

        return root;
    }

    public static byte[] ToXMind(MindMapNode root)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteArchiveEntry(archive, "content.json", CreateXMindContentJson(root));
            WriteArchiveEntry(archive, "metadata.json", CreateXMindMetadataJson());
            WriteArchiveEntry(archive, "manifest.json", CreateXMindManifestJson());
        }

        return memory.ToArray();
    }

    public static MindMapNode FromXMind(byte[] xmind)
    {
        using var memory = new MemoryStream(xmind);
        using var archive = new ZipArchive(memory, ZipArchiveMode.Read);

        var contentJson = archive.GetEntry("content.json");
        if (contentJson is not null)
        {
            using var stream = contentJson.Open();
            using var document = JsonDocument.Parse(stream);
            return FromXMindJson(document.RootElement);
        }

        var contentXml = archive.GetEntry("content.xml");
        if (contentXml is not null)
        {
            using var stream = contentXml.Open();
            var document = XDocument.Load(stream);
            return FromXMindXml(document);
        }

        throw new InvalidDataException(GetResource(MindViewL.XMindContentMissing, "No recognizable XMind content was found."));
    }

    private static MindMapNode CreateMetadataNode(MindMapFileFormat format, string? filePath, string? detail = null)
    {
        var title = GetFileTitle(filePath, GetFormatName(format));
        var node = new MindMapNode(title)
        {
            Note = CreateMetadataNote(format, filePath, detail)
        };
        return node;
    }

    private static string CreateMetadataNote(MindMapFileFormat format, string? filePath, string? detail)
    {
        var lines = new List<string>
        {
            FormatResource(MindViewL.MetadataFormatLabel, "Format: {0}", GetFormatName(format))
        };

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            lines.Add(FormatResource(MindViewL.MetadataPathLabel, "Path: {0}", filePath));
        }

        if (!string.IsNullOrWhiteSpace(detail))
        {
            lines.Add(detail);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string GetFormatName(MindMapFileFormat format)
    {
        return MindMapFileFormatRegistry.GetDisplayName(format);
    }

    private static string GetResource(string key, string fallback)
    {
        var value = MindViewLocalization.GetResource(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static string FormatResource(string key, string fallbackFormat, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, GetResource(key, fallbackFormat), args);
    }

    private static string GetFileTitle(string? filePath, string fallback)
    {
        return string.IsNullOrWhiteSpace(filePath)
            ? fallback
            : Path.GetFileNameWithoutExtension(filePath);
    }

    private static bool LooksLikeZip(byte[] bytes)
    {
        return bytes.Length >= 4 && bytes[0] == 'P' && bytes[1] == 'K';
    }

    private static string DecodeUtf8(byte[]? bytes)
    {
        return bytes is null || bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
    }

    private static string StripHtml(string value)
    {
        return CleanText(value);
    }

    private static string CleanText(string? value, string fallback = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var decoded = WebUtility.HtmlDecode(value);
        var withoutTags = Regex.Replace(decoded, "<.*?>", " ");
        var normalized = Regex.Replace(withoutTags, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static void WriteMarkdownHeadingNode(MindMapNode node, int level, List<string> lines)
    {
        var headingLevel = Math.Max(RootMarkdownHeadingLevel, level);
        lines.Add($"{new string('#', headingLevel)} {GetTitle(node)}");
        WriteMarkdownNote(node.Note, string.Empty, lines);

        foreach (var child in node.Children)
        {
            WriteMarkdownHeadingNode(child, headingLevel + 1, lines);
        }
    }

    private static void WriteMarkdownNote(string note, string indent, List<string> lines)
    {
        if (!string.IsNullOrWhiteSpace(note))
        {
            foreach (var noteLine in SplitLines(note))
            {
                if (!string.IsNullOrWhiteSpace(noteLine))
                {
                    lines.Add($"{indent}{noteLine.TrimEnd()}");
                }
            }
        }
    }

    private static MindMapNode FindMarkdownParent(
        int level,
        int rootLevel,
        MindMapNode root,
        IReadOnlyDictionary<int, MindMapNode> stack)
    {
        if (level <= rootLevel)
        {
            return root;
        }

        var parentLevel = level - 1;
        while (parentLevel > rootLevel && !stack.ContainsKey(parentLevel))
        {
            parentLevel--;
        }

        return stack.TryGetValue(parentLevel, out var parent) ? parent : root;
    }

    private static MindMapNode FindMarkdownListParent(
        int indent,
        MindMapNode fallback,
        IReadOnlyDictionary<int, MindMapNode> listStack)
    {
        MindMapNode? parent = null;
        var parentIndent = int.MinValue;
        foreach (var (candidateIndent, candidateNode) in listStack)
        {
            if (candidateIndent < indent && candidateIndent > parentIndent)
            {
                parentIndent = candidateIndent;
                parent = candidateNode;
            }
        }

        return parent ?? fallback;
    }

    private static bool TryParseMarkdownListItem(string line, out int indent, out string title)
    {
        indent = 0;
        title = string.Empty;

        var index = 0;
        while (index < line.Length && char.IsWhiteSpace(line[index]) && line[index] != '\r' && line[index] != '\n')
        {
            indent += line[index] == '\t' ? 4 : 1;
            index++;
        }

        if (index >= line.Length)
        {
            return false;
        }

        var match = Regex.Match(line[index..], @"^(?:[-*+]|\d+[.)])(?:\s+(.*))?$");
        if (!match.Success)
        {
            return false;
        }

        title = Regex.Replace(match.Groups[1].Value.Trim(), @"^\[[ xX]\]\s+", string.Empty);
        return true;
    }

    private static bool TryParseHeading(string line, out int level, out string title)
    {
        level = 0;
        title = string.Empty;

        var trimmed = line.TrimStart();
        while (level < trimmed.Length && trimmed[level] == '#')
        {
            level++;
        }

        if (level == 0)
        {
            return false;
        }

        if (level == trimmed.Length)
        {
            title = string.Empty;
            return true;
        }

        if (!char.IsWhiteSpace(trimmed[level]))
        {
            level = 0;
            return false;
        }

        title = trimmed[level..].Trim();
        return true;
    }

    private static XElement ToOpmlOutline(MindMapNode node)
    {
        var outline = new XElement("outline", new XAttribute("text", GetTitle(node)));
        if (!string.IsNullOrWhiteSpace(node.Note))
        {
            outline.SetAttributeValue("_note", node.Note);
        }

        foreach (var child in node.Children)
        {
            outline.Add(ToOpmlOutline(child));
        }

        return outline;
    }

    private static MindMapNode FromOpmlOutline(XElement outline)
    {
        var title = outline.Attribute("text")?.Value ?? outline.Attribute("title")?.Value;
        var node = new MindMapNode(string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim())
        {
            Note = outline.Attribute("_note")?.Value ?? outline.Attribute("note")?.Value ?? string.Empty
        };

        foreach (var child in outline.Elements().Where(IsOutlineElement))
        {
            node.Children.Add(FromOpmlOutline(child));
        }

        return node;
    }

    private static string CreateXMindContentJson(MindMapNode root)
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WriteString("id", CreateId());
            writer.WriteString("class", "sheet");
            writer.WriteString("title", GetTitle(root));
            writer.WritePropertyName("rootTopic");
            WriteXMindTopic(writer, root);
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static string CreateXMindMetadataJson()
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("creator");
            writer.WriteStartObject();
            writer.WriteString("name", "CodeWF.Avalonia.MindView");
            writer.WriteString("version", "1.0");
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static string CreateXMindManifestJson()
    {
        using var memory = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memory, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("file-entries");
            writer.WriteStartObject();
            writer.WritePropertyName("content.json");
            writer.WriteStartObject();
            writer.WriteEndObject();
            writer.WritePropertyName("metadata.json");
            writer.WriteStartObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(memory.ToArray());
    }

    private static void WriteXMindTopic(Utf8JsonWriter writer, MindMapNode node)
    {
        writer.WriteStartObject();
        writer.WriteString("id", CreateId());
        writer.WriteString("title", GetTitle(node));

        if (!string.IsNullOrWhiteSpace(node.Note))
        {
            writer.WritePropertyName("notes");
            writer.WriteStartObject();
            writer.WritePropertyName("plain");
            writer.WriteStartObject();
            writer.WriteString("content", node.Note);
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        if (node.Children.Count > 0)
        {
            writer.WritePropertyName("children");
            writer.WriteStartObject();
            writer.WritePropertyName("attached");
            writer.WriteStartArray();

            foreach (var child in node.Children)
            {
                WriteXMindTopic(writer, child);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static MindMapNode FromXMindJson(JsonElement rootElement)
    {
        JsonElement sheet;
        if (rootElement.ValueKind == JsonValueKind.Array)
        {
            sheet = rootElement.EnumerateArray().FirstOrDefault();
        }
        else
        {
            sheet = rootElement;
        }

        if (sheet.ValueKind != JsonValueKind.Object
            || !sheet.TryGetProperty("rootTopic", out var rootTopic))
        {
            throw new InvalidDataException(GetResource(
                MindViewL.XMindJsonMissingRootTopic,
                "XMind content.json does not contain rootTopic."));
        }

        return FromXMindJsonTopic(rootTopic);
    }

    private static MindMapNode FromXMindJsonTopic(JsonElement topic)
    {
        var node = new MindMapNode(CleanText(GetJsonString(topic, "title")))
        {
            Note = GetXMindJsonNote(topic)
        };

        if (topic.TryGetProperty("children", out var children)
            && children.TryGetProperty("attached", out var attached)
            && attached.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in attached.EnumerateArray())
            {
                node.Children.Add(FromXMindJsonTopic(child));
            }
        }

        return node;
    }

    private static string GetXMindJsonNote(JsonElement topic)
    {
        if (topic.TryGetProperty("notes", out var notes)
            && notes.TryGetProperty("plain", out var plain)
            && plain.TryGetProperty("content", out var content)
            && content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static MindMapNode FromXMindXml(XDocument document)
    {
        var topic = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "sheet")?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "topic");

        if (topic is null)
        {
            throw new InvalidDataException(GetResource(
                MindViewL.XMindXmlMissingRootTopic,
                "XMind content.xml does not contain a root topic."));
        }

        return FromXMindXmlTopic(topic);
    }

    private static MindMapNode FromXMindXmlTopic(XElement topic)
    {
        var title = topic.Elements().FirstOrDefault(element => element.Name.LocalName == "title")?.Value;
        var node = new MindMapNode(string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim())
        {
            Note = topic
                .Elements()
                .FirstOrDefault(element => element.Name.LocalName == "notes")?
                .Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "plain")?
                .Value
                .Trim() ?? string.Empty
        };

        var attachedTopics = topic
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == "children")?
            .Elements()
            .Where(element => element.Name.LocalName == "topics"
                && (string?)element.Attribute("type") == "attached")
            .SelectMany(element => element.Elements().Where(child => child.Name.LocalName == "topic"))
            ?? [];

        foreach (var child in attachedTopics)
        {
            node.Children.Add(FromXMindXmlTopic(child));
        }

        return node;
    }

    private static void WriteArchiveEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string GetTitle(MindMapNode node)
    {
        return node.Title.Trim();
    }

    private static string CreateId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static bool IsHeadElement(XElement element)
    {
        return element.Name.LocalName == "head";
    }

    private static bool IsBodyElement(XElement element)
    {
        return element.Name.LocalName == "body";
    }

    private static bool IsOutlineElement(XElement element)
    {
        return element.Name.LocalName == "outline";
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static string AppendNote(string existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return addition;
        }

        return $"{existing}{Environment.NewLine}{addition}";
    }
}
