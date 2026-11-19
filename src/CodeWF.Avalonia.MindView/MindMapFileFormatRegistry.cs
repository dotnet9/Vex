using CodeWF.Avalonia.MindView.I18n;
using CodeWF.Toolkit.Lang;

namespace CodeWF.Avalonia.MindView;

/// <summary>
/// Central registry for file-format metadata used by import/export, pickers, and previews.
/// </summary>
public static class MindMapFileFormatRegistry
{
    private static readonly MindMapFileFormatDescriptor[] Descriptors =
    [
        new(MindMapFileFormat.Markdown, "Markdown", ["md", "markdown"], "md", canWrite: true, isText: true, requiresBinary: false, mimeTypes: ["text/markdown", "text/plain"]),
        new(MindMapFileFormat.Opml, "OPML", ["opml"], "opml", canWrite: true, isText: true, requiresBinary: false, mimeTypes: ["text/x-opml", "application/xml", "text/xml"]),
        new(MindMapFileFormat.XMind, "XMind", ["xmind"], "xmind", canWrite: true, isText: false, requiresBinary: true, mimeTypes: ["application/octet-stream"]),
        new(MindMapFileFormat.Xml, "XML", ["xml"], "xml", canWrite: false, isText: true, requiresBinary: false, mimeTypes: ["application/xml", "text/xml"]),
        new(MindMapFileFormat.FreeMind, "FreeMind", ["mm"], "mm", canWrite: false, isText: true, requiresBinary: false),
        new(MindMapFileFormat.MindManager, "MindManager", ["mmap"], "mmap", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.MindNode, "MindNode", ["mindnode"], "mindnode", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.MindMaster, "MindMaster", ["emmx", "eddx", "mind"], "emmx", canWrite: false, isText: false, requiresBinary: true),
        new(
            MindMapFileFormat.BaiduMindMap,
            "Baidu Mind Map",
            ["km"],
            "km",
            canWrite: false,
            isText: true,
            requiresBinary: false,
            displayNameResourceKey: MindViewL.BaiduMindMapFormat),
        new(MindMapFileFormat.MindNow, "MindNow", ["mindnow"], "mindnow", canWrite: false, isText: false, requiresBinary: true),
        new(
            MindMapFileFormat.Image,
            "Image",
            ["png", "jpg", "jpeg", "gif"],
            "png",
            canWrite: false,
            isText: false,
            requiresBinary: false,
            displayNameResourceKey: MindViewL.ImageFormat),
        new(MindMapFileFormat.Svg, "SVG", ["svg"], "svg", canWrite: false, isText: true, requiresBinary: false),
        new(MindMapFileFormat.WebP, "WebP", ["webp"], "webp", canWrite: false, isText: false, requiresBinary: false),
        new(MindMapFileFormat.Pdf, "PDF", ["pdf"], "pdf", canWrite: false, isText: false, requiresBinary: false),
        new(MindMapFileFormat.Word, "Word", ["doc", "docx"], "docx", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.Excel, "Excel", ["xls", "xlsx"], "xlsx", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.PowerPoint, "PowerPoint", ["ppt", "pptx"], "pptx", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.PlainText, "TXT", ["txt"], "txt", canWrite: false, isText: true, requiresBinary: false, mimeTypes: ["text/plain"]),
        new(MindMapFileFormat.TextBundle, "TextBundle", ["textbundle"], "textbundle", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.Html, "HTML", ["html", "htm"], "html", canWrite: false, isText: true, requiresBinary: false, mimeTypes: ["text/html"]),
        new(MindMapFileFormat.Json, "JSON", ["json"], "json", canWrite: false, isText: true, requiresBinary: false, mimeTypes: ["application/json"]),
        new(MindMapFileFormat.Yaml, "YAML", ["yml", "yaml"], "yml", canWrite: false, isText: true, requiresBinary: false),
        new(MindMapFileFormat.Csv, "CSV", ["csv"], "csv", canWrite: false, isText: true, requiresBinary: false, mimeTypes: ["text/csv"]),
        new(MindMapFileFormat.DrawIo, "draw.io XML", ["drawio", "drawio.xml", "dio", "xml"], "drawio", canWrite: false, isText: true, requiresBinary: false, mimeTypes: ["application/xml", "text/xml"]),
        new(MindMapFileFormat.Visio, "Visio", ["vsd", "vsdx"], "vsdx", canWrite: false, isText: false, requiresBinary: true),
        new(MindMapFileFormat.Gliffy, "Gliffy", ["gliffy"], "gliffy", canWrite: false, isText: true, requiresBinary: false),
        new(MindMapFileFormat.Lucid, "Lucid", ["lucid"], "lucid", canWrite: false, isText: true, requiresBinary: false)
    ];

    private static readonly IReadOnlyDictionary<MindMapFileFormat, MindMapFileFormatDescriptor> DescriptorsByFormat =
        Descriptors.ToDictionary(descriptor => descriptor.Format);

    private static readonly IReadOnlyList<(string Extension, MindMapFileFormatDescriptor Descriptor)> ExtensionDescriptors =
        Descriptors
            .SelectMany(descriptor => descriptor.Extensions.Select(extension => (extension, descriptor)))
            .ToArray();

    public static IReadOnlyList<MindMapFileFormatDescriptor> ReadableFormats => Descriptors;

    public static IReadOnlyList<MindMapFileFormatDescriptor> WritableFormats { get; } =
        Descriptors.Where(descriptor => descriptor.CanWrite).ToArray();

    public static IReadOnlyList<string> ReadablePatterns { get; } =
        Descriptors.SelectMany(descriptor => descriptor.Patterns).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> ReadableMimeTypes { get; } =
        Descriptors.SelectMany(descriptor => descriptor.MimeTypes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> WritablePatterns { get; } =
        WritableFormats.SelectMany(descriptor => descriptor.Patterns).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static IReadOnlyList<string> WritableMimeTypes { get; } =
        WritableFormats.SelectMany(descriptor => descriptor.MimeTypes).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    public static MindMapFileFormatDescriptor GetDescriptor(MindMapFileFormat format)
    {
        return DescriptorsByFormat.TryGetValue(format, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(format), format, null);
    }

    public static MindMapFileFormat GetFormatFromPath(
        string filePath,
        MindMapFileFormat fallback = MindMapFileFormat.Markdown)
    {
        return TryGetDescriptorFromPath(filePath, out var descriptor)
            ? descriptor.Format
            : fallback;
    }

    public static bool IsSupportedFile(string filePath)
    {
        return TryGetDescriptorFromPath(filePath, out _);
    }

    public static string GetDisplayName(MindMapFileFormat format)
    {
        return DescriptorsByFormat.TryGetValue(format, out var descriptor)
            ? descriptor.DisplayName
            : format.ToString();
    }

    public static string GetDefaultExtension(MindMapFileFormat format)
    {
        return DescriptorsByFormat.TryGetValue(format, out var descriptor)
            ? descriptor.DefaultExtension
            : "md";
    }

    public static bool CanWriteFormat(MindMapFileFormat format)
    {
        return DescriptorsByFormat.TryGetValue(format, out var descriptor) && descriptor.CanWrite;
    }

    public static bool RequiresBinaryContent(MindMapFileFormat format)
    {
        return DescriptorsByFormat.TryGetValue(format, out var descriptor) && descriptor.RequiresBinary;
    }

    public static bool IsTextFormat(MindMapFileFormat format)
    {
        return DescriptorsByFormat.TryGetValue(format, out var descriptor) && descriptor.IsText;
    }

    public static bool PathMatchesFormat(string filePath, MindMapFileFormat format)
    {
        if (!DescriptorsByFormat.TryGetValue(format, out var descriptor))
        {
            return false;
        }

        var fileName = Path.GetFileName(filePath);
        return descriptor.Extensions.Any(extension =>
            fileName.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetDescriptorFromPath(
        string filePath,
        out MindMapFileFormatDescriptor descriptor)
    {
        var fileName = Path.GetFileName(filePath);
        foreach (var item in ExtensionDescriptors.OrderByDescending(item => item.Extension.Length))
        {
            if (fileName.EndsWith($".{item.Extension}", StringComparison.OrdinalIgnoreCase))
            {
                descriptor = item.Descriptor;
                return true;
            }
        }

        descriptor = null!;
        return false;
    }
}

public sealed class MindMapFileFormatDescriptor(
    MindMapFileFormat format,
    string displayName,
    IReadOnlyList<string> extensions,
    string defaultExtension,
    bool canWrite,
    bool isText,
    bool requiresBinary,
    IReadOnlyList<string>? mimeTypes = null,
    string? displayNameResourceKey = null)
{
    public MindMapFileFormat Format { get; } = format;

    public string DisplayName => displayNameResourceKey is null
        ? displayName
        : GetResource(displayNameResourceKey, displayName);

    public IReadOnlyList<string> Extensions { get; } = extensions;

    public string DefaultExtension { get; } = defaultExtension;

    public bool CanWrite { get; } = canWrite;

    public bool IsText { get; } = isText;

    public bool RequiresBinary { get; } = requiresBinary;

    public IReadOnlyList<string> MimeTypes { get; } = mimeTypes ?? [];

    public IReadOnlyList<string> Patterns { get; } =
        extensions.Select(extension => $"*.{extension}").ToArray();

    private static string GetResource(string key, string fallback)
    {
        var value = MindViewLocalization.GetResource(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
