using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vex.Modules.Templates.Services;

/// <summary>保存用户模板的结果。</summary>
public enum TemplateSaveResult
{
    /// <summary>新增保存成功。</summary>
    Created,

    /// <summary>同名（分组 + 名称）覆盖成功。</summary>
    Overwritten,

    /// <summary>被拒绝：随包模板目录已有同名模板，系统自带模板不可覆盖。</summary>
    BlockedByBuiltin,

    /// <summary>被拒绝：名称含非法字符或为空。</summary>
    InvalidName,
}

/// <summary>用户模板目录根的 templates.json 条目：描述 / 标准 / Tag，全部可选。</summary>
public sealed record TemplateMeta(
    string? Standard = null,
    string? Description = null,
    IReadOnlyList<string>? Tags = null);

/// <summary>templates.json 的源生成序列化上下文（NativeAOT 无反射）。</summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, TemplateMeta>))]
internal sealed partial class TemplateMetaJsonContext : JsonSerializerContext;

/// <summary>
/// 模板目录服务：模板完全由目录结构驱动，代码不写死任何模板清单、分组或标签。
/// 两个模板根：
/// + 随包根（安装目录 Templates）：只读，软件研发/运维子文件夹 + templates.json 元数据随包分发；
/// + 用户根（默认 %LocalAppData%/Vex/Templates，设置 TemplatesPath 可改）：根目录文件归「我的模板」，
///   一级子文件夹名即分组名（可自建任意分组），templates.json 存用户模板的 Tag。
/// 唯一标识 = 分组 + 名称（跨根全局）：同名覆盖仅限用户根，随包模板不可修改、覆盖与删除。
/// </summary>
public sealed class TemplateCatalog
{
    public const string UngroupedGroup = "我的模板";
    private const string MetaFileName = "templates.json";

    private readonly string _builtinRoot;
    private readonly Func<string?> _userDirectoryProvider;

    public TemplateCatalog(string builtinRoot, Func<string?> userDirectoryProvider)
    {
        _builtinRoot = builtinRoot;
        _userDirectoryProvider = userDirectoryProvider;
    }

    /// <summary>随包模板根探测：优先 macOS 应用包 Resources，其次安装目录。</summary>
    public static string ResolveBuiltinRoot()
    {
        var resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "Templates"));
        return OperatingSystem.IsMacOS() && Directory.Exists(resources)
            ? resources
            : Path.Combine(AppContext.BaseDirectory, "Templates");
    }

    /// <summary>用户模板目录默认值：%LocalAppData%/Vex/Templates（与恢复草稿同一数据根）。</summary>
    public static string DefaultUserDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vex", "Templates");

    /// <summary>解析用户模板目录：设置 TemplatesPath 为空用默认值。</summary>
    public string ResolveUserDirectory()
    {
        var configured = _userDirectoryProvider();
        return string.IsNullOrWhiteSpace(configured)
            ? DefaultUserDirectory
            : configured!;
    }

    /// <summary>加载全部模板：随包在前，用户在后（按名称排序）。</summary>
    public IReadOnlyList<TemplateItem> LoadAll()
    {
        var items = new List<TemplateItem>();
        items.AddRange(LoadRoot(_builtinRoot, isBuiltin: true));
        items.AddRange(LoadRoot(ResolveUserDirectory(), isBuiltin: false));
        return items;
    }

    /// <summary>
    /// 扫描一个模板根：一级子文件夹名即分组名，根目录文件归「我的模板」（不递归）；
    /// 根内 templates.json 提供描述 / 标准 / Tag；目录不存在视为空；单个坏文件进 errors 不拖垮整体。
    /// </summary>
    public IReadOnlyList<TemplateItem> LoadRoot(string root, bool isBuiltin, List<string>? errors = null)
    {
        var items = new List<TemplateItem>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return items;
        var meta = LoadMeta(root);
        foreach (var folder in Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal))
        {
            var group = Path.GetFileName(folder);
            foreach (var file in Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal))
            {
                AppendFile(items, meta, root, group, file, isBuiltin, errors);
            }
        }
        foreach (var file in Directory.EnumerateFiles(root, "*.md").Order(StringComparer.Ordinal))
        {
            AppendFile(items, meta, root, UngroupedGroup, file, isBuiltin, errors);
        }
        return items;
    }

    private static void AppendFile(
        List<TemplateItem> items, Dictionary<string, TemplateMeta> meta,
        string root, string group, string file, bool isBuiltin, List<string>? errors)
    {
        try
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var identity = group + "/" + name;
            meta.TryGetValue(identity, out var itemMeta);
            items.Add(new TemplateItem(
                group, name,
                Standard: itemMeta?.Standard ?? string.Empty,
                Description: itemMeta?.Description ?? string.Empty,
                itemMeta?.Tags ?? [],
                File.ReadAllText(file),
                IsBuiltin: isBuiltin));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors?.Add(Path.GetFileName(file) + "：" + ex.Message);
        }
    }

    /// <summary>随包根存在同名模板时返回 true：该身份被只读根占用，不可覆盖、修改或删除。</summary>
    public bool IsProtectedIdentity(string group, string name)
    {
        var folder = group == UngroupedGroup ? _builtinRoot : Path.Combine(_builtinRoot, group);
        return File.Exists(Path.Combine(folder, name + ".md"));
    }

    /// <summary>
    /// 保存用户模板（写入用户根，新增或同名覆盖）。随包根同名身份返回
    /// <see cref="TemplateSaveResult.BlockedByBuiltin"/>；分组为「我的模板」写目录根，其余写同名子文件夹（自动创建）；
    /// Tag 写用户根 templates.json。
    /// </summary>
    public TemplateSaveResult SaveUserTemplate(
        string group, string name, IReadOnlyList<string> tags, string body)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return TemplateSaveResult.InvalidName;
        if (IsProtectedIdentity(group, name))
            return TemplateSaveResult.BlockedByBuiltin;

        var userDirectory = ResolveUserDirectory();
        Directory.CreateDirectory(userDirectory);
        var folder = group == UngroupedGroup ? userDirectory : Path.Combine(userDirectory, group);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".md");
        var overwritten = File.Exists(path);
        File.WriteAllText(path, body);

        var root = userDirectory;
        var meta = LoadMeta(root);
        var identity = group + "/" + name;
        if (tags.Count > 0) meta[identity] = new TemplateMeta(Tags: [.. tags]);
        else meta.Remove(identity);
        SaveMeta(root, meta);
        return overwritten ? TemplateSaveResult.Overwritten : TemplateSaveResult.Created;
    }

    /// <summary>删除用户模板文件与元数据记录；随包身份直接拒绝；文件不存在返回 false。</summary>
    public bool DeleteUserTemplate(string group, string name)
    {
        if (IsProtectedIdentity(group, name)) return false;
        var userDirectory = ResolveUserDirectory();
        var folder = group == UngroupedGroup ? userDirectory : Path.Combine(userDirectory, group);
        var path = Path.Combine(folder, name + ".md");
        if (!File.Exists(path)) return false;
        File.Delete(path);
        var meta = LoadMeta(userDirectory);
        if (meta.Remove(group + "/" + name)) SaveMeta(userDirectory, meta);
        return true;
    }

    /// <summary>用户模板文件路径（删除确认浮层展示用）。</summary>
    public string GetUserTemplatePath(string group, string name)
    {
        var userDirectory = ResolveUserDirectory();
        var folder = group == UngroupedGroup ? userDirectory : Path.Combine(userDirectory, group);
        return Path.Combine(folder, name + ".md");
    }

    /// <summary>加载根内 templates.json（键 = 分组/名称）；文件缺失或损坏返回空字典。</summary>
    public Dictionary<string, TemplateMeta> LoadMeta(string root)
    {
        var path = Path.Combine(root, MetaFileName);
        try
        {
            if (!File.Exists(path)) return [];
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, TemplateMetaJsonContext.Default.DictionaryStringTemplateMeta) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return [];
        }
    }

    private void SaveMeta(string root, Dictionary<string, TemplateMeta> meta)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, MetaFileName), JsonSerializer.Serialize(meta, TemplateMetaJsonContext.Default.DictionaryStringTemplateMeta));
    }
}
