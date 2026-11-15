using Vex.Modules.Templates;
using Vex.Modules.Templates.Services;
using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// 模板目录服务（目录驱动）：随包根只读保护、用户根扫描与覆盖语义、templates.json 元数据容错。
/// 随包根 / 用户根都用临时目录模拟，不触碰用户数据与安装目录。
/// </summary>
public sealed class TemplateCatalogTests : IDisposable
{
    private readonly string _builtinRoot;
    private readonly string _userRoot;
    private readonly TemplateCatalog _catalog;

    public TemplateCatalogTests()
    {
        _builtinRoot = Path.Combine(Path.GetTempPath(), "vex-template-tests", Guid.NewGuid().ToString("N"), "builtin");
        _userRoot = Path.Combine(Path.GetTempPath(), "vex-template-tests", Guid.NewGuid().ToString("N"), "user");
        Directory.CreateDirectory(Path.Combine(_builtinRoot, "软件研发"));
        File.WriteAllText(Path.Combine(_builtinRoot, "软件研发", "需求文档.md"), "# 需求文档\n\n随包正文。");
        File.WriteAllText(Path.Combine(_builtinRoot, "templates.json"),
            """{ "软件研发/需求文档": { "standard": "团队通用", "description": "需求模板。", "tags": ["需求", "PRD"] } }""");
        _catalog = new TemplateCatalog(_builtinRoot, () => _userRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_builtinRoot)!, recursive: true); } catch { }
        try { Directory.Delete(Path.GetDirectoryName(_userRoot)!, recursive: true); } catch { }
    }

    [Fact]
    public void LoadRoot_reads_builtin_subfolders_as_groups_and_meta()
    {
        var builtins = _catalog.LoadRoot(_builtinRoot, isBuiltin: true);
        var item = Assert.Single(builtins);
        Assert.Equal("软件研发", item.Group);
        Assert.Equal("需求文档", item.Name);
        Assert.True(item.IsBuiltin);
        Assert.Equal("团队通用", item.Standard);
        Assert.Equal("需求模板。", item.Description);
        Assert.Equal(["需求", "PRD"], item.Tags);
    }

    [Fact]
    public void LoadRoot_maps_user_root_files_to_ungrouped()
    {
        Directory.CreateDirectory(_userRoot);
        File.WriteAllText(Path.Combine(_userRoot, "随手记.md"), "# 随手记");
        var item = Assert.Single(_catalog.LoadRoot(_userRoot, isBuiltin: false));
        Assert.Equal(TemplateCatalog.UngroupedGroup, item.Group);
        Assert.False(item.IsBuiltin);
        Assert.Equal(string.Empty, item.Standard);
    }

    [Fact]
    public void LoadAll_combines_both_roots()
    {
        Directory.CreateDirectory(_userRoot);
        File.WriteAllText(Path.Combine(_userRoot, "随手记.md"), "# 随手记");
        var all = _catalog.LoadAll();
        Assert.Equal(2, all.Count);
        Assert.True(all[0].IsBuiltin);
        Assert.False(all[1].IsBuiltin);
    }

    [Fact]
    public void Protected_identity_blocks_save_delete()
    {
        Assert.True(_catalog.IsProtectedIdentity("软件研发", "需求文档"));
        Assert.False(_catalog.IsProtectedIdentity("软件研发", "新模板"));

        var save = _catalog.SaveUserTemplate("软件研发", "需求文档", [], "覆盖内置");
        Assert.Equal(TemplateSaveResult.BlockedByBuiltin, save);
        Assert.Empty(_catalog.LoadRoot(_userRoot, isBuiltin: false));

        Assert.False(_catalog.DeleteUserTemplate("软件研发", "需求文档"));
    }

    [Fact]
    public void Same_group_and_name_across_roots_is_blocked()
    {
        // 用户根自建同名分组 + 同名文件 = 与随包根身份撞车，同样阻断。
        Directory.CreateDirectory(Path.Combine(_userRoot, "软件研发"));
        File.WriteAllText(Path.Combine(_userRoot, "软件研发", "需求文档.md"), "用户版");
        var save = _catalog.SaveUserTemplate("软件研发", "需求文档", [], "覆盖内置");
        Assert.Equal(TemplateSaveResult.BlockedByBuiltin, save);
    }

    [Fact]
    public void Save_creates_file_and_meta_then_overwrite_updates()
    {
        Assert.Equal(TemplateSaveResult.Created, _catalog.SaveUserTemplate(TemplateCatalog.UngroupedGroup, "部署手册", ["部署"], "# 第一版"));
        Assert.True(File.Exists(Path.Combine(_userRoot, "部署手册.md")));

        var meta = _catalog.LoadMeta(_userRoot);
        Assert.True(meta.TryGetValue(TemplateCatalog.UngroupedGroup + "/部署手册", out var entry));
        Assert.Equal(["部署"], entry!.Tags);

        Assert.Equal(TemplateSaveResult.Overwritten, _catalog.SaveUserTemplate(TemplateCatalog.UngroupedGroup, "部署手册", ["部署", "v2"], "# 第二版"));
        var items = _catalog.LoadRoot(_userRoot, isBuiltin: false);
        var item = Assert.Single(items);
        Assert.Equal("# 第二版", item.Body);
        Assert.Equal(["部署", "v2"], item.Tags);
    }

    [Fact]
    public void Save_to_subfolder_groups_by_folder_name()
    {
        _catalog.SaveUserTemplate("周报", "十一月第二周", [], "# 周报");
        Assert.True(File.Exists(Path.Combine(_userRoot, "周报", "十一月第二周.md")));
        var item = Assert.Single(_catalog.LoadRoot(_userRoot, isBuiltin: false));
        Assert.Equal("周报", item.Group);
    }

    [Fact]
    public void Delete_removes_file_and_meta_and_refuses_missing()
    {
        _catalog.SaveUserTemplate(TemplateCatalog.UngroupedGroup, "部署手册", [], "# 部署手册");
        Assert.True(_catalog.DeleteUserTemplate(TemplateCatalog.UngroupedGroup, "部署手册"));
        Assert.Empty(_catalog.LoadMeta(_userRoot));
        Assert.False(_catalog.DeleteUserTemplate(TemplateCatalog.UngroupedGroup, "不存在"));
    }

    [Fact]
    public void Save_rejects_invalid_name()
    {
        Assert.Equal(TemplateSaveResult.InvalidName, _catalog.SaveUserTemplate(TemplateCatalog.UngroupedGroup, "bad:name", [], "x"));
        Assert.Equal(TemplateSaveResult.InvalidName, _catalog.SaveUserTemplate(TemplateCatalog.UngroupedGroup, "  ", [], "x"));
    }

    [Fact]
    public void LoadMeta_survives_missing_or_corrupt_file()
    {
        Assert.Empty(_catalog.LoadMeta(_userRoot));
        Directory.CreateDirectory(_userRoot);
        File.WriteAllText(Path.Combine(_userRoot, "templates.json"), "{ not json");
        Assert.Empty(_catalog.LoadMeta(_userRoot));
    }

    [Fact]
    public void ResolveBuiltinRoot_points_under_base_directory_on_windows()
    {
        if (!OperatingSystem.IsWindows()) return;
        Assert.True(TemplateCatalog.ResolveBuiltinRoot().EndsWith("Templates", StringComparison.Ordinal));
    }
}
