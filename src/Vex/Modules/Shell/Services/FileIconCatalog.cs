using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Vex.Modules.Shell.Services;

/// <summary>按文件名、扩展名和文件夹展开状态解析本地图标，并共享已解码位图。</summary>
public sealed class FileIconCatalog : IDisposable
{
    private readonly string _root;
    private readonly Dictionary<string, string> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string>> _maps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _lightMaps = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Bitmap?> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _imageLock = new();
    private bool _disposed;

    // 转换器共用一个目录，应用退出时释放解码后的非托管图片。
    public static FileIconCatalog Default { get; } = CreateDefault();

    public FileIconCatalog(string root)
    {
        _root = Path.GetFullPath(root);
        LoadTheme(Path.Combine(_root, "icon-theme.json"));
        LoadTheme(Path.Combine(_root, "local-overrides.json"));
    }

    /// <summary>未知文件或资源不可用时返回 null，由视图显示原有矢量图标。</summary>
    public IImage? Resolve(string name, bool isFolder, bool expanded = false, bool light = false, bool isRoot = false)
    {
        var path = ResolveIconFilePath(name, isFolder, expanded, light, isRoot);
        if (path is null) return null;

        lock (_imageLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_images.TryGetValue(path, out var image)) return image;
            try
            {
                using var stream = File.OpenRead(path);
                image = new Bitmap(stream);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Trace.TraceWarning("File icon could not be decoded: {0}: {1}", path, ex.Message);
                image = null;
            }
            _images[path] = image;
            return image;
        }
    }

    /// <summary>解析实际图片路径，供资源完整性及映射验证使用。</summary>
    public string? ResolveIconFilePath(string name, bool isFolder, bool expanded = false, bool light = false, bool isRoot = false)
    {
        string? id;
        if (isFolder)
        {
            var section = expanded ? "folderNamesExpanded" : "folderNames";
            id = isRoot ? FindMapping(expanded ? "rootFolderNamesExpanded" : "rootFolderNames", name, light) : null;
            id ??= FindMapping(section, name, light);
            id ??= isRoot ? (expanded ? "folder-root-open" : "folder-root") : (expanded ? "folder-open" : "folder");
        }
        else
        {
            id = FindMapping("fileNames", name, light);
            // 从第一个点开始尝试，确保 axaml.cs 等复合扩展名先于 cs。
            for (var dot = name.IndexOf('.'); id is null && dot >= 0; dot = name.IndexOf('.', dot + 1))
            {
                id = FindMapping("fileExtensions", name[(dot + 1)..], light);
            }
        }

        if (id is null || !_definitions.TryGetValue(id, out var relative)) return null;
        try
        {
            var path = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = Path.TrimEndingDirectorySeparator(_root) + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
                && File.Exists(path) ? path : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    private string? FindMapping(string section, string key, bool light)
    {
        if (light && _lightMaps.TryGetValue(section, out var lightMap) && lightMap.TryGetValue(key, out var lightId)) return lightId;
        return _maps.TryGetValue(section, out var map) && map.TryGetValue(key, out var id) ? id : null;
    }

    private void LoadTheme(string path)
    {
        if (!File.Exists(path)) return;
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("iconDefinitions", out var definitions) && definitions.ValueKind == JsonValueKind.Object)
            {
                foreach (var definition in definitions.EnumerateObject())
                {
                    if (definition.Value.ValueKind == JsonValueKind.Object
                        && definition.Value.TryGetProperty("iconPath", out var iconPath)
                        && iconPath.ValueKind == JsonValueKind.String && iconPath.GetString() is { Length: > 0 } relative)
                    {
                        _definitions[definition.Name] = relative;
                    }
                }
            }
            ReadMappings(root, _maps);
            // 本地覆盖优先于上游浅色规则，避免自定义映射只在深色下生效。
            if (Path.GetFileName(path) == "local-overrides.json") ReadMappings(root, _lightMaps);
            if (root.TryGetProperty("light", out var light)) ReadMappings(light, _lightMaps);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Trace.TraceWarning("File icon theme could not be loaded: {0}: {1}", path, ex.Message);
        }
    }

    private static void ReadMappings(JsonElement root, Dictionary<string, Dictionary<string, string>> maps)
    {
        if (root.ValueKind != JsonValueKind.Object) return;
        foreach (var section in new[] { "fileNames", "fileExtensions", "folderNames", "folderNamesExpanded", "rootFolderNames", "rootFolderNamesExpanded" })
        {
            if (!root.TryGetProperty(section, out var values) || values.ValueKind != JsonValueKind.Object) continue;
            if (!maps.TryGetValue(section, out var map)) maps[section] = map = new(StringComparer.OrdinalIgnoreCase);
            foreach (var value in values.EnumerateObject())
            {
                if (value.Value.ValueKind == JsonValueKind.String && value.Value.GetString() is { Length: > 0 } id) map[value.Name] = id;
            }
        }
    }

    private static FileIconCatalog CreateDefault()
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "file-icons"));
        var root = OperatingSystem.IsMacOS() && Directory.Exists(resources) ? resources : Path.Combine(AppContext.BaseDirectory, "file-icons");
        var catalog = new FileIconCatalog(root);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => catalog.Dispose();
        return catalog;
    }

    public void Dispose()
    {
        lock (_imageLock)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var image in _images.Values) image?.Dispose();
            _images.Clear();
        }
    }
}
