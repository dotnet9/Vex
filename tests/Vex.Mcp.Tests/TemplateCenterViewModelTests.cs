using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls.Notifications;
using CodeWF.Toolkit.Lang;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Templates.Services;
using Vex.Modules.Templates.ViewModels;
using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// 模板中心 ViewModel 全交互集成测试：真实 TemplateCatalog + 临时双根目录，
/// 覆盖新建、应用、另存、覆盖、内置保护、编辑、删除、持久化与边界。
/// </summary>
public sealed class TemplateCenterViewModelTests : IDisposable
{
    private readonly string _builtinRoot;
    private readonly string _userRoot;
    private readonly FakeLocalizer _localizer = new();
    private readonly FakeTarget _target = new() { Markdown = "# 当前文档", CurrentDocumentName = "草稿.md", IsEditing = true };
    private readonly FakeToasts _toasts = new();
    private bool _confirmDeleteResult = true;
    private readonly List<string> _deleteConfirmations = [];

    public TemplateCenterViewModelTests()
    {
        _builtinRoot = NewDir();
        _userRoot = NewDir();
        Directory.CreateDirectory(Path.Combine(_builtinRoot, "软件研发"));
        Directory.CreateDirectory(Path.Combine(_builtinRoot, "运维"));
        File.WriteAllText(Path.Combine(_builtinRoot, "软件研发", "需求文档.md"), "# 需求文档");
        File.WriteAllText(Path.Combine(_builtinRoot, "软件研发", "接口文档.md"), "# 接口文档");
        File.WriteAllText(Path.Combine(_builtinRoot, "运维", "巡检报告.md"), "# 巡检报告");
        File.WriteAllText(Path.Combine(_builtinRoot, "templates.json"),
            """{"软件研发/需求文档":{"standard":"团队通用","description":"需求模板。","tags":["需求","PRD"]}}""");
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_builtinRoot)!, recursive: true); } catch { }
        try { Directory.Delete(Path.GetDirectoryName(_userRoot)!, recursive: true); } catch { }
    }

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "vex-vm-tests", Guid.NewGuid().ToString("N"));

    private TemplateCatalog CreateCatalog() => new(_builtinRoot, () => _userRoot);

    private TemplateCenterViewModel CreateVm(TemplateViewMode mode = TemplateViewMode.Picker)
    {
        return new TemplateCenterViewModel(CreateCatalog(), _localizer, _target,
            async path => { _deleteConfirmations.Add(path); await Task.Yield(); return _confirmDeleteResult; },
            _toasts, mode);
    }

    [Fact]
    public void Picker_initial_state_loads_builtins_selects_first_and_renders_preview()
    {
        var vm = CreateVm();
        Assert.Equal(3, vm.Items.Count); // 随包根 3 套
        Assert.NotNull(vm.SelectedItem);
        Assert.Equal(vm.Items[0].Body, vm.PreviewMarkdown);
        Assert.True(vm.CanApply); // fake 初始为编辑视图且已选中首项

        // 分组聚合：all + 软件研发 + 运维 + 我的模板
        Assert.Equal(["all", "软件研发", "运维", "我的模板"], vm.Groups.Select(g => g.Key));
        // Tag 聚合含随包元数据
        Assert.Contains("需求", vm.TagFilters);
        Assert.Contains("PRD", vm.TagFilters);
    }

    [Fact]
    public void Apply_blocked_when_not_editing_allowed_when_editing()
    {
        _target.IsEditing = false;
        var vm = CreateVm();
        vm.SelectedItem = vm.Items[0];
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;
        vm.ApplyToCurrentDocument();
        Assert.Empty(_target.Applied);
        Assert.Equal(0, closed);
        Assert.Contains(_toasts.Messages, m => m.Contains("不能应用"));

        _target.IsEditing = true;
        vm.SelectedItem = vm.Items[0];
        var applied = vm.Items[0].Body;
        vm.ApplyToCurrentDocument();
        Assert.Equal(applied, Assert.Single(_target.Applied));
        Assert.Single(_target.Applied);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void UseNewDocument_passes_template_name_and_body_then_closes()
    {
        var vm = CreateVm();
        vm.SelectedItem = vm.Items[0];
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;
        vm.UseNewDocumentAsync();
        var (name, body) = Assert.Single(_target.NewDocuments);
        Assert.Equal(vm.Items[0].Name + ".md", name);
        Assert.Equal(vm.Items[0].Body, body);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Group_and_tag_and_search_filters_work()
    {
        var vm = CreateVm();
        Assert.Equal(3, vm.Items.Count);

        vm.SelectedGroupIndex = IndexOfKey(vm.Groups, "软件研发");
        Assert.Equal(2, vm.Items.Count);
        vm.SelectedGroupIndex = IndexOfKey(vm.Groups, TemplateCatalog.UngroupedGroup);
        Assert.Empty(vm.Items);
        Assert.Null(vm.SelectedItem);
        Assert.Equal(string.Empty, vm.PreviewMarkdown);

        vm.SelectedGroupIndex = 0; // 全部
        vm.SelectedTagIndex = vm.TagFilters.IndexOf("PRD");
        Assert.All(vm.Items, item => Assert.Contains("PRD", item.Tags));

        vm.SelectedTagIndex = -1;
        vm.SearchText = "接口";
        Assert.All(vm.Items, item => Assert.Contains("接口", item.Name + item.Description + string.Join(' ', item.Tags)));
    }

    [Fact]
    public void OpenSaveForm_prefills_name_and_blocks_empty_document()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        Assert.Equal(TemplateViewMode.SaveForm, vm.Mode);
        Assert.Equal("草稿", vm.SaveName);
        Assert.Equal(TemplateCatalog.UngroupedGroup, vm.SaveGroup);

        _target.Markdown = "   ";
        vm.OpenSaveForm();
        Assert.Contains(_toasts.Messages, m => m.Contains("空"));
    }

    [Fact]
    public void SaveCurrent_creates_file_meta_and_returns_to_picker()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveName = "部署手册";
        vm.SaveTags = "部署, 手册, 部署"; // 重复去重
        vm.SaveCurrentAsync();
        Assert.Equal(TemplateViewMode.Picker, vm.Mode);
        var path = Path.Combine(_userRoot, "部署手册.md");
        Assert.True(File.Exists(path));
        var meta = CatalogMeta();
        Assert.Equal(["部署", "手册"], meta["我的模板/部署手册"]!.Tags);
        Assert.Contains(vm.Items, item => item.Name == "部署手册");
        Assert.Contains(_toasts.Messages, m => m.Contains("部署手册"));
    }

    [Fact]
    public void SaveCurrent_overwrite_hint_and_overwrite_flow()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveName = "部署手册";
        Assert.True(string.IsNullOrEmpty(vm.SaveHint)); // 首次保存无提示
        vm.SaveCurrentAsync();
        Assert.Equal(TemplateViewMode.Picker, vm.Mode);

        // 再次另存同名 → 覆盖提示
        vm.OpenSaveForm();
        vm.SaveName = "部署手册";
        Assert.Contains("覆盖", vm.SaveHint);
        var countBefore = CatalogMeta().Count;
        vm.SaveCurrentAsync();
        Assert.Equal(TemplateViewMode.Picker, vm.Mode);
        Assert.Equal(countBefore, CatalogMeta().Count); // 条数不变 = 覆盖
        Assert.Contains(_toasts.Messages, m => m.Contains("覆盖"));
    }

    [Fact]
    public void SaveCurrent_blocks_builtin_identity_with_error()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveGroup = "软件研发";
        vm.SaveName = "需求文档";
        vm.SaveCurrentAsync();
        Assert.Contains("不可覆盖", vm.ErrorText);
        Assert.False(File.Exists(Path.Combine(_userRoot, "软件研发", "需求文档.md")));
    }

    [Fact]
    public void SaveCurrent_rejects_invalid_name()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveName = "bad:name";
        vm.SaveCurrentAsync();
        Assert.False(string.IsNullOrEmpty(vm.ErrorText));
    }

    [Fact]
    public void Edit_flow_prefills_updates_and_blocks_clashes()
    {
        // 准备一个用户模板
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveName = "部署手册";
        vm.SaveTags = "部署";
        vm.SaveCurrentAsync();
        var item = vm.Items.First(i => i.Name == "部署手册");

        vm.OpenEditForm(item);
        Assert.Equal(TemplateViewMode.EditForm, vm.Mode);
        Assert.Equal("部署手册", vm.EditName);
        Assert.Equal("部署", vm.EditTags);
        Assert.Equal(item.Body, vm.EditContent);

        // 改名 + 改 Tag 保存
        vm.EditName = "部署手册 V2";
        vm.EditTags = "部署, v2";
        vm.SaveEditAsync();
        Assert.Equal(TemplateViewMode.Picker, vm.Mode);
        Assert.Contains(vm.Items, i => i.Name == "部署手册 V2" && i.Tags.Contains("v2"));
        Assert.False(File.Exists(Path.Combine(_userRoot, "部署手册.md")));
        Assert.True(File.Exists(Path.Combine(_userRoot, "部署手册 V2.md")));

        // 改名撞内置
        vm.OpenEditForm(vm.Items.First(i => i.Name == "部署手册 V2"));
        vm.EditName = "需求文档";
        vm.EditGroup = "软件研发";
        vm.SaveEditAsync();
        Assert.Contains("不可覆盖", vm.ErrorText);

        // 改名撞同分组下其他用户模板
        _catalogSeedUser("软件研发", "部署手册");
        vm.EditGroup = "软件研发";
        vm.SaveEditAsync();
        Assert.Contains("同名", vm.ErrorText);
    }

    [Fact]
    public async Task Delete_confirms_then_removes_file_and_meta()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveName = "临时模板";
        vm.SaveCurrentAsync();
        var item = vm.Items.First(i => i.Name == "临时模板");

        _confirmDeleteResult = false;
        await vm.DeleteAsync(item);
        Assert.True(File.Exists(Path.Combine(_userRoot, "临时模板.md")));

        _confirmDeleteResult = true;
        await vm.DeleteAsync(item);
        Assert.False(File.Exists(Path.Combine(_userRoot, "临时模板.md")));
        Assert.Contains(_toasts.Messages, m => m.Contains("已删除"));
        Assert.Equal(2, _deleteConfirmations.Count); // 拒绝 + 确认各一次
    }

    [Fact]
    public void Cancel_returns_from_form_and_closes_from_picker()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.Cancel();
        Assert.Equal(TemplateViewMode.Picker, vm.Mode);
        var closed = 0;
        vm.RequestClose += (_, _) => closed++;
        vm.Cancel();
        Assert.Equal(1, closed);
    }

    [Fact]
    public void User_custom_groups_appear_in_groups_and_save_options()
    {
        Directory.CreateDirectory(Path.Combine(_userRoot, "周报"));
        File.WriteAllText(Path.Combine(_userRoot, "周报", "十一月.md"), "# 周报");
        var vm = CreateVm();
        Assert.Contains(vm.Groups, g => g.Key == "周报");
        Assert.Contains(vm.SaveGroupOptions, g => g == "周报");
        vm.SelectedGroupIndex = IndexOfKey(vm.Groups, "周报");
        Assert.Single(vm.Items);
    }

    [Fact]
    public void Persistence_across_catalog_instances()
    {
        var vm = CreateVm();
        vm.OpenSaveForm();
        vm.SaveName = "持久化模板";
        vm.SaveTags = "持久";
        vm.SaveCurrentAsync();
        // 新 Catalog 实例（模拟应用重启）仍能读到
        var fresh = CreateVm();
        Assert.Contains(fresh.Items, i => i.Name == "持久化模板" && i.Tags.Contains("持久"));
    }

    [Fact]
    public void Title_reflects_mode()
    {
        var vm = CreateVm();
        Assert.Contains("模板中心", vm.Title);
        vm.OpenSaveForm();
        Assert.Contains("另存为模板", vm.Title);
    }

    /* ---------- 基建 ---------- */

    private static int IndexOfKey(System.Collections.ObjectModel.ObservableCollection<TemplateCenterViewModel.TemplateGroupOption> groups, string key)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            if (groups[i].Key == key) return i;
        }
        return -1;
    }

    private Dictionary<string, TemplateMeta> CatalogMeta() => CreateCatalog().LoadMeta(_userRoot);

    private void _catalogSeedUser(string group, string name)
    {
        Directory.CreateDirectory(Path.Combine(_userRoot, group));
        File.WriteAllText(Path.Combine(_userRoot, group, name + ".md"), "# " + name);
    }

    private sealed class FakeTarget : ITemplateDocumentTarget
    {
        public string Markdown { get; set; } = string.Empty;
        public string CurrentDocumentName { get; set; } = string.Empty;
        public bool IsEditing { get; set; }
        public List<string> Applied { get; } = [];
        public List<(string Name, string Body)> NewDocuments { get; } = [];
        public void ApplyMarkdown(string markdown) { Markdown = markdown; Applied.Add(markdown); }
        public Task NewDocumentFromTemplateAsync(string fileName, string markdown)
        {
            NewDocuments.Add((fileName, markdown));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeToasts : IShellToastPublisher
    {
        public List<string> Messages { get; } = [];
        public void Publish(string message, NotificationType type = NotificationType.Success) => Messages.Add(message);
        public void Install(Avalonia.Controls.Primitives.VisualLayerManager? layerManager) { }
        public void Uninstall() { }
    }

    private sealed class FakeLocalizer : IAppLocalizer
    {
        // 内置测试关心的中文文案（与 zh-CN.json 同源子集），使文案断言可读。
        private static readonly Dictionary<string, string> Zh = new()
        {
            ["Vex.VexL.TemplateCenterTitle"] = "模板中心",
            ["Vex.VexL.TemplateSaveTitle"] = "另存为模板",
            ["Vex.VexL.TemplateEditTitle"] = "修改模板",
            ["Vex.VexL.TemplateGroupAll"] = "全部",
            ["Vex.VexL.TemplateGroupDev"] = "软件研发",
            ["Vex.VexL.TemplateGroupOps"] = "运维",
            ["Vex.VexL.TemplateGroupMy"] = "我的模板",
            ["Vex.VexL.TemplateSaveHintOverwrite"] = "「我的模板」中已有同名模板，保存将覆盖其内容。",
            ["Vex.VexL.TemplateSaveHintBuiltin"] = "此分组已有同名系统模板，系统模板不可覆盖，请换个名称或分组。",
            ["Vex.VexL.TemplateSaveInvalidName"] = "名称不能包含非法字符",
            ["Vex.VexL.TemplateSavedToast"] = "已保存为模板：{0}",
            ["Vex.VexL.TemplateOverwrittenToast"] = "已覆盖模板：{0}",
            ["Vex.VexL.TemplateEditClashHint"] = "该分组下已有同名模板，请先删除或换一个名称。",
            ["Vex.VexL.TemplateEditSavedToast"] = "模板已更新：{0}",
            ["Vex.VexL.TemplateDeletedToast"] = "已删除模板：{0}",
            ["Vex.VexL.TemplateEmptyBodyToast"] = "当前文档是空的，没有可保存的内容",
            ["Vex.VexL.TemplateAppliedToast"] = "已应用模板「{0}」，Ctrl+Z 可撤销",
            ["Vex.VexL.TemplateApplyBlockedToast"] = "非编辑视图下不能应用模板",
        };

        public event EventHandler<EventArgs>? CultureChanged { add { } remove { } }
        public CultureInfo Culture => CultureInfo.InvariantCulture;
        public void SetCulture(string cultureName) { }
        public string Get(string key) => Zh.GetValueOrDefault(key, key);
        public string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
    }
}
