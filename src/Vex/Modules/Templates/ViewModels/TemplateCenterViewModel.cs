using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Templates.Services;

namespace Vex.Modules.Templates.ViewModels;

/// <summary>模板中心弹层的显示面板。</summary>
public enum TemplateViewMode
{
    /// <summary>模板选择（分组/Tag/搜索/预览/使用）。</summary>
    Picker,

    /// <summary>另存当前文档为模板。</summary>
    SaveForm,

    /// <summary>修改用户模板（名称/分组/Tag/内容）。</summary>
    EditForm,
}

/// <summary>
/// 模板中心：纯 Markdown 模板（无元数据），分组对应模板目录子文件夹，Tag 存 tags.json；
/// 唯一标识 = 分组 + 名称，同名覆盖仅限我的模板，系统自带模板（软件研发/运维）不可修改与覆盖。
/// 单弹层三面板（Picker/SaveForm/EditForm），与原型 layer-templates / layer-template-save / layer-template-edit 对齐。
/// </summary>
public sealed class TemplateCenterViewModel : ReactiveObject, IDialogContext
{
    private readonly TemplateCatalog _catalog;
    private readonly IAppLocalizer _localizer;
    private readonly ITemplateDocumentTarget _target;
    private readonly Func<string, Task<bool>> _confirmDelete;
    private readonly IShellToastPublisher _toasts;
    private readonly string _userDirectory;

    private string _selectedGroup = "all";
    private string? _selectedTag;
    private string _searchText = string.Empty;
    private TemplateItem? _selectedItem;
    private string _previewMarkdown = string.Empty;
    private TemplateViewMode _mode = TemplateViewMode.Picker;
    private string _saveName = string.Empty;
    private string _pendingBody = string.Empty;
    private string _previewName = string.Empty;
    private string _previewDescription = string.Empty;
    private System.Collections.ObjectModel.ObservableCollection<string> _previewTags = [];
    private string _saveGroup = TemplateCatalog.UngroupedGroup;
    private string _saveTags = string.Empty;
    private string _saveHint = string.Empty;
    private string _errorText = string.Empty;
    private TemplateItem? _editing;
    private string _editName = string.Empty;
    private string _editGroup = TemplateCatalog.UngroupedGroup;
    private string _editTags = string.Empty;
    private string _editContent = string.Empty;

    public TemplateCenterViewModel(
        TemplateCatalog catalog,
        IAppLocalizer localizer,
        ITemplateDocumentTarget target,
        Func<string, Task<bool>> confirmDelete,
        IShellToastPublisher toasts,
        TemplateViewMode initialMode = TemplateViewMode.Picker,
        string? initialSourceFile = null)
    {
        _catalog = catalog;
        _localizer = localizer;
        _target = target;
        _confirmDelete = confirmDelete;
        _toasts = toasts;
        _userDirectory = catalog.ResolveUserDirectory();
        Mode = initialMode;
        if (Mode == TemplateViewMode.SaveForm) PrepareSaveForm(initialSourceFile);
        else RefreshAll();
    }

    /// <summary>准备另存面板字段：sourceFilePath 非空以该文件为正文（文件列表入口），否则取当前文档。</summary>
    private void PrepareSaveForm(string? sourceFilePath = null)
    {
        string name;
        if (sourceFilePath is { Length: > 0 } path && File.Exists(path))
        {
            _pendingBody = File.ReadAllText(path);
            name = System.IO.Path.GetFileName(path);
        }
        else
        {
            _pendingBody = _target.Markdown;
            name = _target.CurrentDocumentName;
        }
        SaveName = name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? name[..^3] : name;
        SaveGroup = TemplateCatalog.UngroupedGroup;
        SaveTags = string.Empty;
        ErrorText = string.Empty;
        RefreshSaveHint();
        RefreshAll();
    }

    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    public TemplateViewMode Mode
    {
        get => _mode;
        set
        {
            this.RaiseAndSetIfChanged(ref _mode, value);
            this.RaisePropertyChanged(nameof(IsPicker));
            this.RaisePropertyChanged(nameof(IsSaveForm));
            this.RaisePropertyChanged(nameof(IsEditForm));
        }
    }

    public bool IsPicker => Mode == TemplateViewMode.Picker;
    public bool IsSaveForm => Mode == TemplateViewMode.SaveForm;
    public bool IsEditForm => Mode == TemplateViewMode.EditForm;

    public string Title => Mode switch
    {
        TemplateViewMode.SaveForm => _localizer.Get(VexL.TemplateSaveTitle),
        TemplateViewMode.EditForm => _localizer.Get(VexL.TemplateEditTitle),
        _ => _localizer.Get(VexL.TemplateCenterTitle),
    };

    /// <summary>分组选项：Key 为目录语义键（all/dev/ops/my 或用户子文件夹名），DisplayName 为本地化显示。</summary>
    public sealed record TemplateGroupOption(string Key, string DisplayName);

    /// <summary>分组选项：all 固定第一，dev/ops 固定，用户自定义子文件夹动态，my 固定最后。</summary>
    public ObservableCollection<TemplateGroupOption> Groups { get; } = [new("all", string.Empty)];

    public string SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedGroup, value);
            SelectedTag = null;
            RefreshTagsAndItems();
        }
    }

    public ObservableCollection<string> TagFilters { get; } = [];

    public string? SelectedTag
    {
        get => _selectedTag;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedTag, value);
            RefreshItems();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            RefreshTagsAndItems();
        }
    }

    public ObservableCollection<TemplateItem> Items { get; } = [];

    /// <summary>分组横排 ListBox 的选中索引（绑定层用；-1 = 未选中）。</summary>
    public int SelectedGroupIndex
    {
        get
        {
            var index = -1;
            for (var i = 0; i < Groups.Count; i++)
            {
                if (Groups[i].Key != _selectedGroup) continue;
                index = i;
                break;
            }
            return index;
        }
        set
        {
            if (value < 0 || value >= Groups.Count) return;
            SelectedGroup = Groups[value].Key;
        }
    }

    /// <summary>Tag 横排 ListBox 的选中索引；-1 = 不按 Tag 过滤。</summary>
    public int SelectedTagIndex
    {
        get => _selectedTag is null ? -1 : TagFilters.IndexOf(_selectedTag);
        set => SelectedTag = value >= 0 && value < TagFilters.Count ? TagFilters[value] : null;
    }

    public TemplateItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedItem, value);
            PreviewMarkdown = value?.Body ?? string.Empty;
            PreviewName = value?.Name ?? string.Empty;
            PreviewDescription = value?.Description ?? string.Empty;
            PreviewTags = new System.Collections.ObjectModel.ObservableCollection<string>(value?.Tags ?? []);
            this.RaisePropertyChanged(nameof(CanApply));
            this.RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public string PreviewMarkdown
    {
        get => _previewMarkdown;
        set => this.RaiseAndSetIfChanged(ref _previewMarkdown, value);
    }

    /// <summary>预览头部：模板名（固定不随内容滚动）。</summary>
    public string PreviewName { get => _previewName; set => this.RaiseAndSetIfChanged(ref _previewName, value); }

    /// <summary>预览头部：描述（固定不随内容滚动）。</summary>
    public string PreviewDescription { get => _previewDescription; set => this.RaiseAndSetIfChanged(ref _previewDescription, value); }

    /// <summary>预览头部：标签（固定不随内容滚动）。</summary>
    public System.Collections.ObjectModel.ObservableCollection<string> PreviewTags
    {
        get => _previewTags;
        private set => this.RaiseAndSetIfChanged(ref _previewTags, value);
    }

    /// <summary>当前文档处于编辑视图才可应用模板（原型同款守卫）。</summary>
    public bool CanApply => _target.IsEditing && SelectedItem is not null;

    public bool HasSelection => SelectedItem is not null;

    public string SaveName { get => _saveName; set { this.RaiseAndSetIfChanged(ref _saveName, value); RefreshSaveHint(); } }

    public ObservableCollection<string> SaveGroupOptions { get; } = [];

    /// <summary>另存分组下拉选中索引（绑定层用）。</summary>
    public int SaveGroupIndex
    {
        get => SaveGroupOptions.IndexOf(_saveGroup);
        set
        {
            if (value < 0 || value >= SaveGroupOptions.Count) return;
            SaveGroup = SaveGroupOptions[value];
        }
    }

    public string SaveGroup
    {
        get => _saveGroup;
        set
        {
            this.RaiseAndSetIfChanged(ref _saveGroup, value);
            this.RaisePropertyChanged(nameof(SaveGroupIndex));
            RefreshSaveHint();
        }
    }

    public string SaveTags { get => _saveTags; set => this.RaiseAndSetIfChanged(ref _saveTags, value); }

    public string SaveHint { get => _saveHint; set => this.RaiseAndSetIfChanged(ref _saveHint, value); }

    public bool HasSaveHint => SaveHint.Length > 0;

    public bool HasError => ErrorText.Length > 0;

    public string ErrorText { get => _errorText; set { this.RaiseAndSetIfChanged(ref _errorText, value); this.RaisePropertyChanged(nameof(HasError)); } }

    public string EditName { get => _editName; set => this.RaiseAndSetIfChanged(ref _editName, value); }

    public ObservableCollection<string> EditGroupOptions { get; } = [];

    /// <summary>编辑分组下拉选中索引（绑定层用）。</summary>
    public int EditGroupIndex
    {
        get => EditGroupOptions.IndexOf(_editGroup);
        set
        {
            if (value < 0 || value >= EditGroupOptions.Count) return;
            EditGroup = EditGroupOptions[value];
        }
    }

    public string EditGroup
    {
        get => _editGroup;
        set
        {
            this.RaiseAndSetIfChanged(ref _editGroup, value);
            this.RaisePropertyChanged(nameof(EditGroupIndex));
        }
    }

    public string EditTags { get => _editTags; set => this.RaiseAndSetIfChanged(ref _editTags, value); }

    public string EditContent { get => _editContent; set => this.RaiseAndSetIfChanged(ref _editContent, value); }

    public string GroupDisplay(string group) => group == "all" ? _localizer.Get(VexL.TemplateGroupAll) : group;

    /// <summary>分组显示名 → 选项键（另存/编辑弹层的分组下拉）。</summary>
    public string? GroupKeyOf(string display)
    {
        foreach (var option in SaveGroupOptions)
        {
            if (GroupDisplay(option) == display) return option;
        }
        return null;
    }

    /// <summary>刷新分组与另存/编辑下拉；Picker 态同时刷新 Tag 与列表。</summary>
    public void RefreshAll()
    {
        var all = _catalog.LoadAll();
        Groups.Clear();
        Groups.Add(new TemplateGroupOption("all", _localizer.Get(VexL.TemplateGroupAll)));
        foreach (var group in all.Select(item => item.Group).Distinct().Order(StringComparer.Ordinal))
        {
            if (group != TemplateCatalog.UngroupedGroup) Groups.Add(new TemplateGroupOption(group, group));
        }
        Groups.Add(new TemplateGroupOption(TemplateCatalog.UngroupedGroup, _localizer.Get(VexL.TemplateGroupMy)));
        FillGroupOptions(SaveGroupOptions, SaveGroup);
        FillGroupOptions(EditGroupOptions, EditGroup);
        this.RaisePropertyChanged(nameof(SaveGroupIndex));
        this.RaisePropertyChanged(nameof(EditGroupIndex));
        RefreshTagsAndItems();
        this.RaisePropertyChanged(nameof(SelectedGroupIndex));
        this.RaisePropertyChanged(nameof(SelectedTagIndex));
    }

    private void FillGroupOptions(ObservableCollection<string> options, string preferred)
    {
        options.Clear();
        options.Add(TemplateCatalog.UngroupedGroup);
        foreach (var group in Groups)
        {
            if (group.Key != "all" && group.Key != TemplateCatalog.UngroupedGroup && !options.Contains(group.Key)) options.Add(group.Key);
        }
        if (!options.Contains(preferred)) options.Add(preferred);
    }

    private void RefreshTagsAndItems()
    {
        var visible = VisibleTemplates();
        var tags = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in visible)
        {
            foreach (var tag in item.Tags)
            {
                tags[tag] = tags.GetValueOrDefault(tag) + 1;
            }
        }
        TagFilters.Clear();
        foreach (var tag in tags.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key))
        {
            TagFilters.Add(tag);
        }
        if (_selectedTag is { } keep && !TagFilters.Contains(keep)) _selectedTag = null;
        this.RaisePropertyChanged(nameof(SelectedTag));
        RefreshItems();
    }

    private IReadOnlyList<TemplateItem> VisibleTemplates()
    {
        var all = _catalog.LoadAll();
        return all.Where(item =>
        {
            if (_selectedGroup != "all" && item.Group != _selectedGroup) return false;
            if (_selectedTag is { } tag && !item.Tags.Contains(tag)) return false;
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            var haystack = item.Name + " " + item.Description + " " + string.Join(' ', item.Tags);
            return haystack.Contains(_searchText.Trim(), StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    private void RefreshItems()
    {
        var visible = VisibleTemplates();
        Items.Clear();
        foreach (var item in visible) Items.Add(item);
        if (SelectedItem is not { } current || !visible.Contains(current))
        {
            SelectedItem = visible.Count > 0 ? visible[0] : null;
        }
    }

    /// <summary>进入另存面板：预填当前文档名（去扩展名），正文为空则提示并留在选择面板。</summary>
    public void OpenSaveForm() => OpenSaveForm(null);

    /// <summary>另存当前文档为模板（正文取当前文档）。</summary>
    public void OpenSaveForm(string? sourceFilePath)
    {
        if (sourceFilePath is not { Length: > 0 })
        {
            if (string.IsNullOrWhiteSpace(_target.Markdown))
            {
                _toasts.Publish(_localizer.Get(VexL.TemplateEmptyBodyToast));
                return;
            }
        }
        PrepareSaveForm(sourceFilePath);
        Mode = TemplateViewMode.SaveForm;
        this.RaisePropertyChanged(nameof(Title));
    }

    /// <summary>同名冲突提示（内置同名阻断，用户同名提示覆盖）；返回保存按钮是否可用。</summary>
    private bool RefreshSaveHint()
    {
        var name = SaveName.Trim();
        var group = SaveGroup;
        if (_catalog.IsProtectedIdentity(group, name))
        {
            SaveHint = _localizer.Get(VexL.TemplateSaveHintBuiltin);
            return false;
        }
        if (_catalog.LoadAll().Any(item => item.Group == group && item.Name == name && !item.IsBuiltin))
        {
            SaveHint = _localizer.Get(VexL.TemplateSaveHintOverwrite);
            return true;
        }
        SaveHint = string.Empty;
        return true;
    }

    /// <summary>另存为模板：唯一标识 = 分组 + 名称；同名覆盖我的模板，内置同名阻断。</summary>
    public async Task SaveCurrentAsync()
    {
        var name = SaveName.Trim();
        ErrorText = string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ErrorText = _localizer.Get(VexL.TemplateSaveInvalidName);
            return;
        }
        var result = _catalog.SaveUserTemplate( SaveGroup, name, ParseTags(SaveTags), _target.Markdown);
        if (result == TemplateSaveResult.BlockedByBuiltin)
        {
            ErrorText = _localizer.Get(VexL.TemplateSaveHintBuiltin);
            return;
        }
        SelectedGroup = SaveGroup;
        Mode = TemplateViewMode.Picker;
        RefreshAll();
        this.RaisePropertyChanged(nameof(Title));
        var toastKey = result == TemplateSaveResult.Overwritten ? VexL.TemplateOverwrittenToast : VexL.TemplateSavedToast;
        _toasts.Publish(_localizer.Format(toastKey, name));
        await Task.CompletedTask;
    }

    /// <summary>进入修改面板：仅用户模板可改。</summary>
    public void OpenEditForm(TemplateItem item)
    {
        if (!item.IsCustom()) return;
        _editing = item;
        EditName = item.Name;
        EditGroup = item.Group;
        EditTags = string.Join(", ", item.Tags);
        EditContent = item.Body;
        ErrorText = string.Empty;
        FillGroupOptions(EditGroupOptions, item.Group);
        Mode = TemplateViewMode.EditForm;
        this.RaisePropertyChanged(nameof(Title));
    }

    /// <summary>保存修改：改名/换分组撞内置阻断，撞其他用户模板提示先删除。</summary>
    public async Task SaveEditAsync()
    {
        if (_editing is not { } item) return;
        var name = EditName.Trim();
        ErrorText = string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            ErrorText = _localizer.Get(VexL.TemplateSaveInvalidName);
            return;
        }
        if (_catalog.IsProtectedIdentity(EditGroup, name))
        {
            ErrorText = _localizer.Get(VexL.TemplateSaveHintBuiltin);
            return;
        }
        var clash = _catalog.LoadAll()
            .Any(other => other.Identity != item.Identity && other.Group == EditGroup && other.Name == name);
        if (clash)
        {
            ErrorText = _localizer.Get(VexL.TemplateEditClashHint);
            return;
        }
        var identityChanged = item.Group != EditGroup || item.Name != name;
        if (identityChanged)
        {
            // 分组或名称变化：写新文件，删除旧文件，Tag 迁移。
            _catalog.SaveUserTemplate( EditGroup, name, ParseTags(EditTags), EditContent);
            _catalog.DeleteUserTemplate( item.Group, item.Name);
        }
        else
        {
            _catalog.SaveUserTemplate( EditGroup, name, ParseTags(EditTags), EditContent);
        }
        SelectedGroup = EditGroup;
        Mode = TemplateViewMode.Picker;
        RefreshAll();
        this.RaisePropertyChanged(nameof(Title));
        _toasts.Publish(_localizer.Format(VexL.TemplateEditSavedToast, name));
        await Task.CompletedTask;
    }

    /// <summary>删除用户模板：统一删除确认浮层，确认后删文件与 Tag 记录。</summary>
    public async Task DeleteAsync(TemplateItem item)
    {
        if (!item.IsCustom()) return;
        var path = _catalog.GetUserTemplatePath( item.Group, item.Name);
        if (!await _confirmDelete(path)) return;
        if (!_catalog.DeleteUserTemplate( item.Group, item.Name)) return;
        if (ReferenceEquals(SelectedItem, item) || SelectedItem == item) SelectedItem = null;
        RefreshAll();
        _toasts.Publish(_localizer.Format(VexL.TemplateDeletedToast, item.Name));
    }

    /// <summary>删除当前编辑的用户模板（编辑面板的删除按钮）。</summary>
    public async Task DeleteEditingAsync()
    {
        if (_editing is { } item) await DeleteAsync(item);
    }

    /// <summary>以模板内容新建文档（未保存确认流 + 默认文件名 = 模板名.md）。</summary>
    public async Task UseNewDocumentAsync()
    {
        if (SelectedItem is not { } item) return;
        await _target.NewDocumentFromTemplateAsync(item.Name + ".md", item.Body);
        Close();
    }

    /// <summary>应用到当前文档：整体替换正文（撤销经自动保存快照与编辑器撤销栈）；非编辑视图阻断并提示。</summary>
    public void ApplyToCurrentDocument()
    {
        if (SelectedItem is not { } item) return;
        if (!_target.IsEditing)
        {
            _toasts.Publish(_localizer.Get(VexL.TemplateApplyBlockedToast), NotificationType.Warning);
            return;
        }
        _target.ApplyMarkdown(item.Body);
        Close();
        _toasts.Publish(_localizer.Format(VexL.TemplateAppliedToast, item.Name));
    }

    /// <summary>取消表单回选择面板（Picker 无操作直接关弹层）。</summary>
    public void Cancel()
    {
        if (Mode == TemplateViewMode.Picker) { Close(); return; }
        Mode = TemplateViewMode.Picker;
        this.RaisePropertyChanged(nameof(Title));
        RefreshAll();
    }

    private static string[] ParseTags(string value) => value
        .Split([',', '，', ';', '；', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.Ordinal)
        .Take(8)
        .ToArray();
}

/// <summary>用户模板判定扩展（内置模板不可编辑/删除）。</summary>
public static class TemplateItemExtensions
{
    public static bool IsCustom(this TemplateItem item) => !item.IsBuiltin;
}
