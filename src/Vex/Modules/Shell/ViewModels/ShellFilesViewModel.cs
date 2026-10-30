using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Regions;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellFilesViewModel : ReactiveObject, IRegionTabItem, IDisposable
{
    private DocumentFileNode? _selectedDocumentFileNode;
    private DocumentFileNode? _contextDocumentFileNode;
    private DocumentFile? _currentDocumentFile;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly IDocumentFileFactory? _fileFactory;
    private bool _isListView;
    private string _sortKey = "group";

    public ShellFilesViewModel(IAppSettingsStore? settingsStore = null, IWorkspaceFileService? workspaceFiles = null, IDocumentFileFactory? fileFactory = null)
    {
        _settingsStore = settingsStore;
        _fileFactory = fileFactory;
        _isListView = settingsStore?.Current.FileListView ?? false;
        _sortKey = settingsStore?.Current.FileSortKey ?? "group";
        _workspaceFiles = workspaceFiles;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
        if (workspaceFiles is not null)
        {
            workspaceFiles.Changed += OnWorkspaceFilesChanged;
            EnsureWorkspaceSession();
        }
    }

    public string? TitleKey { get; } = VexL.SidebarFiles;

    public ObservableCollection<DocumentFileNode> DocumentFileNodes { get; } = [];

    // 侧栏过滤（对应原型「侧栏内搜索」）：TreeView 绑定过滤后的列表，主列表保留用于选中恢复
    public ObservableCollection<DocumentFileNode> FilteredDocumentFileNodes { get; } = [];
    public ObservableCollection<DocumentFileNode> FlatDocumentFileNodes { get; } = [];
    public bool IsListView => _isListView;
    public bool IsTreeView => !_isListView;
    public bool CanCollapseAll => IsTreeView && Enumerate(FilteredDocumentFileNodes).Any(node => node.IsFolder && node.IsExpanded);
    public bool HasPathCommandTarget => (_contextDocumentFileNode ?? SelectedDocumentFileNode) is { IsPlaceholder: false };
    public void NewDocument() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.NewDocument));
    public void QuickOpen() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.QuickOpen));
    public void RefreshFiles() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.RefreshFiles));
    public void ClearFilter() => FilterText = string.Empty;
    public void ToggleListView()
    {
        _isListView = !_isListView;
        OnPropertyChanged(nameof(IsListView)); OnPropertyChanged(nameof(IsTreeView)); OnPropertyChanged(nameof(CanCollapseAll));
        RebuildFilteredNodes();
        _settingsStore?.Update(settings => settings with { FileListView = _isListView });
    }
    public string SortKey => _sortKey;

    // 五键排序的勾选态（原型 .files-sort-item[aria-checked=true]）：MenuFlyout 的 Radio 勾选按此刷新。
    public bool IsSortKeyGroup => _sortKey == "group";
    public bool IsSortKeyNaturalDesc => _sortKey == "natural-desc";
    public bool IsSortKeyNameAsc => _sortKey == "name-asc";
    public bool IsSortKeyModified => _sortKey == "modified";
    public bool IsSortKeyCreated => _sortKey == "created";

    public void SetSortKey(string key)
    {
        if (SortKey == key) return;
        _sortKey = key;
        RebuildFilteredNodes();
        RebuildWorkspaceProjection();
        _settingsStore?.Update(settings => settings with { FileSortKey = key });
        OnPropertyChanged(nameof(SortKey));
        OnPropertyChanged(nameof(IsSortKeyGroup));
        OnPropertyChanged(nameof(IsSortKeyNaturalDesc));
        OnPropertyChanged(nameof(IsSortKeyNameAsc));
        OnPropertyChanged(nameof(IsSortKeyModified));
        OnPropertyChanged(nameof(IsSortKeyCreated));
    }

    public void CollapseAll()
    {
        // 操作源节点；搜索生成的临时展开副本不应覆盖真实折叠偏好。
        _batchExpansionChange = true;
        try
        {
            foreach (var node in Enumerate(DocumentFileNodes).Where(node => node.IsFolder)) node.IsExpanded = false;
            foreach (var node in Enumerate(FilteredDocumentFileNodes).Where(node => node.IsFolder)) node.IsExpanded = false;
        }
        finally { _batchExpansionChange = false; }
        SaveExpansionState();
        RebuildFilteredNodes();
        OnPropertyChanged(nameof(CanCollapseAll));
    }
    private static IEnumerable<DocumentFileNode> Enumerate(IEnumerable<DocumentFileNode> nodes)
    {
        foreach (var node in nodes) { yield return node; foreach (var child in Enumerate(node.Children)) yield return child; }
    }
    public void OpenInNewWindow()
    {
        if ((_contextDocumentFileNode ?? SelectedDocumentFileNode) is not { IsPlaceholder: false } node) return;
        ClearContextDocumentFile();
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.NewWindow, node.Path));
    }

    private string? _workspacePath;
    public string WorkspaceName => _workspacePath is { Length: > 0 } path ? Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) : CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(VexL.SidebarFiles) ?? string.Empty;

    private string _filterText = string.Empty;

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                RebuildFilteredNodes();
            }
        }
    }

    public bool HasDocumentFiles => IsListView ? FlatDocumentFileNodes.Count > 0 : FilteredDocumentFileNodes.Count > 0;

    /// <summary>列表视图文件超过阈值时改用树形视图的提示态（原型同款）。</summary>
    public bool IsListOverflow => IsListView && FlatDocumentFileNodes.Count > 200;

    public bool IsDocumentFilesEmpty => !HasDocumentFiles && !IsIndexLoading && !HasIndexError;

    public DocumentFileNode? SelectedDocumentFileNode
    {
        get => _selectedDocumentFileNode;
        set
        {
            var previousSelection = _selectedDocumentFileNode?.DocumentFile ?? _currentDocumentFile;
            if (SetProperty(ref _selectedDocumentFileNode, value))
            {
                OnPropertyChanged(nameof(HasSelectedDocumentFile));
                OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
                OnPropertyChanged(nameof(HasPathCommandTarget));
                OnPropertyChanged(nameof(SelectedListDocumentFileNode));
                if (value?.DocumentFile is { } file)
                {
                    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new DocumentFileOpenRequestedCommand(file, previousSelection));
                }
            }
        }
    }

    public bool HasSelectedDocumentFile => SelectedDocumentFileNode?.DocumentFile is not null;

    // 隐藏的 ListBox 不能把 TreeView 选中的文件夹反向清空。
    public DocumentFileNode? SelectedListDocumentFileNode
    {
        get => SelectedDocumentFileNode is { IsDocument: true } node ? node : null;
        set { if (IsListView) SelectedDocumentFileNode = value; }
    }

    public bool HasDocumentFileCommandTarget => GetDocumentFileCommandTarget() is not null;

    /// <summary>请求打开一个 Markdown 文件夹，由 Shell 协调层处理未保存确认和文件读取。</summary>
    public void OpenFolderAsync() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.OpenFolder));

    public void OpenSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        var previousSelection = SelectedDocumentFileNode?.DocumentFile ?? _currentDocumentFile;
        ClearContextDocumentFile();
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new DocumentFileOpenRequestedCommand(file, previousSelection));
    }

    public void RenameSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new DocumentFileRenameRequestedCommand(file));
    }

    public void OpenSelectedFileLocation()
    {
        if ((_contextDocumentFileNode ?? SelectedDocumentFileNode) is not { IsPlaceholder: false } node)
        {
            return;
        }

        ClearContextDocumentFile();
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.OpenPathLocation, node.Path));
    }

    public void DeleteSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new DocumentFileDeleteRequestedCommand(file));
    }

    public void SelectDocumentFileForContextMenu(DocumentFileNode documentFileNode)
    {
        if (documentFileNode.IsPlaceholder) return;
        _contextDocumentFileNode = documentFileNode;
        OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
        OnPropertyChanged(nameof(HasPathCommandTarget));
    }

    [EventHandler]
    public void ApplyDocumentFilesChanged(DocumentFilesChangedCommand command)
    {
        if (_workspaceFiles is not null)
        {
            ApplyWorkspaceDocumentFilesChanged(command);
            return;
        }
        // 文件列表只接收 Shell 发布的快照，不直接调用主 ViewModel 的文件打开流程。
        var sameWorkspace = _workspacePath is not null && command.WorkspaceRootPath is not null
            && PathsEqual(_workspacePath, command.WorkspaceRootPath);
        var expandedPaths = sameWorkspace
            ? Enumerate(DocumentFileNodes).Where(node => node.IsFolder && node.IsExpanded)
                .Select(node => NavigationExpansionState.PathKey(node.Path)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var selectedFolderPath = SelectedDocumentFileNode is { IsFolder: true } selectedFolder
            && sameWorkspace ? selectedFolder.Path : null;
        _workspacePath = command.WorkspaceRootPath;
        _currentDocumentFile = command.SelectedFile;
        OnPropertyChanged(nameof(WorkspaceName));
        DocumentFileNodes.Clear();
        foreach (var node in BuildDocumentFileNodes(command.Files, command.WorkspaceRootPath))
        {
            DocumentFileNodes.Add(node);
        }

        foreach (var node in Enumerate(DocumentFileNodes).Where(node => node.IsFolder))
        {
            var pathKey = NavigationExpansionState.PathKey(node.Path);
            // 新目录默认折叠；同一工作区刷新只恢复用户本次手动展开的目录。
            node.IsExpanded = sameWorkspace
                ? expandedPaths.Contains(pathKey)
                : ReferenceEquals(node, DocumentFileNodes.FirstOrDefault());
            node.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(DocumentFileNode.IsExpanded)) return;
                var key = NavigationExpansionState.Encode(NavigationExpansionState.PathKey(node.Path));
                if (!_batchExpansionChange) _settingsStore?.Update(settings => settings with
                {
                    CollapsedFolders = NavigationExpansionState.Set(settings.CollapsedFolders, key, !node.IsExpanded)
                });
                OnPropertyChanged(nameof(CanCollapseAll));
            };
        }

        RebuildFilteredNodes();
        if (command.FocusWorkspaceRoot && IsTreeView)
        {
            FilterText = string.Empty;
            var root = FilteredDocumentFileNodes.FirstOrDefault(node => node.IsFolder);
            SelectDocumentFileSilently(root);
        }
        else
        {
            var retainedFolder = selectedFolderPath is null ? null
                : Enumerate(FilteredDocumentFileNodes).FirstOrDefault(node => node.IsFolder && PathsEqual(node.Path, selectedFolderPath));
            SelectDocumentFileSilently(retainedFolder ?? FindDocumentFileNode(DocumentFileNodes, command.SelectedFile));
        }
        NotifyDocumentFilesChanged();
    }

    [EventHandler]
    public void ApplyDocumentFileSelectionChanged(DocumentFileSelectionChangedCommand command)
    {
        if (_workspaceFiles is not null && !Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyDocumentFileSelectionChanged(command));
            return;
        }
        // 未保存确认被取消时静默恢复旧选择，避免再次触发打开文件请求。
        _currentDocumentFile = command.SelectedFile;
        if (_workspaceFiles is not null)
        {
            if (command.RevealPath) _ = RevealDocumentAsync(command.SelectedFile);
            else SelectDocumentFileSilently(FindDocumentFileNode(DocumentFileNodes, command.SelectedFile));
            return;
        }
        SelectDocumentFileSilently(FindDocumentFileNode(DocumentFileNodes, command.SelectedFile));
    }

    private void SelectDocumentFileSilently(DocumentFileNode? documentFileNode)
    {
        if (SetProperty(ref _selectedDocumentFileNode, documentFileNode, nameof(SelectedDocumentFileNode)))
        {
            OnPropertyChanged(nameof(HasSelectedDocumentFile));
            OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
            OnPropertyChanged(nameof(HasPathCommandTarget));
            OnPropertyChanged(nameof(SelectedListDocumentFileNode));
        }
    }

    private DocumentFile? GetDocumentFileCommandTarget() =>
        (_contextDocumentFileNode ?? SelectedDocumentFileNode)?.DocumentFile;

    private void ClearContextDocumentFile()
    {
        if (_contextDocumentFileNode is null)
        {
            return;
        }

        _contextDocumentFileNode = null;
        OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
    }

    private void NotifyDocumentFilesChanged()
    {
        OnPropertyChanged(nameof(HasDocumentFiles));
        OnPropertyChanged(nameof(IsDocumentFilesEmpty));
        OnPropertyChanged(nameof(CanCollapseAll));
    }

    // 按名称 / 摘要过滤文档；文件夹保留仍有命中的子树。命中文档节点复用主列表实例，保证选中绑定不受影响。
    private void RebuildFilteredNodes()
    {
        if (_workspaceFiles is not null)
        {
            RebuildWorkspaceProjection();
            return;
        }
        var selectedPath = SelectedDocumentFileNode?.Path;
        FilteredDocumentFileNodes.Clear();
        FlatDocumentFileNodes.Clear();
        var filter = _filterText?.Trim();
        var descending = _sortKey == "natural-desc";
        foreach (var node in descending ? DocumentFileNodes.Reverse() : DocumentFileNodes)
        {
            if (BuildFilteredNode(node, filter) is { } filtered)
            {
                FilteredDocumentFileNodes.Add(filtered);
            }
        }

        var flat = Enumerate(DocumentFileNodes).Where(node => IsListView && node.IsDocument && (string.IsNullOrEmpty(filter)
            || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        var ordered = _sortKey switch
        {
            "natural-desc" => flat.OrderBy(node => node.Name, Core.Services.NaturalStringComparer.Descending),
            "modified" => flat.OrderBy(node => node.ModifiedTime ?? DateTimeOffset.MaxValue),
            "created" => flat.OrderBy(node => node.CreatedTime ?? DateTimeOffset.MaxValue),
            _ => flat.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase),
        };
        foreach (var node in ordered)
        {
            node.FolderTag = FolderTagFor(node.Path);
            FlatDocumentFileNodes.Add(node);
        }
        OnPropertyChanged(nameof(IsListOverflow));

        SelectDocumentFileSilently(selectedPath is null ? null
            : Enumerate(FilteredDocumentFileNodes).FirstOrDefault(node => PathsEqual(node.Path, selectedPath)));
        NotifyDocumentFilesChanged();
    }

    private DocumentFileNode? BuildFilteredNode(DocumentFileNode node, string? filter)
    {
        if (string.IsNullOrEmpty(filter) && _sortKey == "group")
        {
            return node;
        }

        if (node.IsFolder)
        {
            var children = (_sortKey == "natural-desc" ? node.Children.Reverse() : node.Children)
                .Select(child => BuildFilteredNode(child, filter))
                .OfType<DocumentFileNode>()
                .ToList();
            return children.Count > 0 || string.IsNullOrEmpty(filter)
                ? CreateFilteredFolder(node, children, !string.IsNullOrEmpty(filter))
                : null;
        }

        var matches = string.IsNullOrEmpty(filter) || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        return matches ? node : null;
    }

    private DocumentFileNode CreateFilteredFolder(DocumentFileNode source, IReadOnlyList<DocumentFileNode> children, bool expand)
    {
        var node = new DocumentFileNode(source.Name, source.Path, true, children) { IsExpanded = expand || source.IsExpanded };
        node.PropertyChanged += (_, _) => { if (!expand) source.IsExpanded = node.IsExpanded; OnPropertyChanged(nameof(CanCollapseAll)); };
        return node;
    }

    private static IReadOnlyList<DocumentFileNode> BuildDocumentFileNodes(
        IReadOnlyList<DocumentFile> files,
        string? workspaceRootPath)
    {
        var rootPath = ResolveWorkspaceRootPath(files, workspaceRootPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return files
                .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .Select(file => new DocumentFileNode(file.Name, file.Path, false, documentFile: file))
                .ToList();
        }

        var root = new MutableDocumentFolderNode(
            Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } rootName ? rootName : rootPath,
            rootPath);

        foreach (var file in files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
        {
            AddFileToTree(root, rootPath, file);
        }

        return [new DocumentFileNode(root.Name, root.Path, true, BuildChildren(root))];
    }

    private static void AddFileToTree(MutableDocumentFolderNode root, string rootPath, DocumentFile file)
    {
        var current = root;
        var directory = SafeDirectoryName(file.Path);
        var relativeDirectory = ResolveRelativeDirectory(rootPath, directory);
        var parts = SplitRelativePath(relativeDirectory);
        if (parts.Any(part => part.StartsWith(".", StringComparison.Ordinal))) return;
        foreach (var part in parts)
        {
            var nextPath = Path.Combine(current.Path, part);
            if (!current.Folders.TryGetValue(part, out var next))
            {
                next = new MutableDocumentFolderNode(part, nextPath);
                current.Folders.Add(part, next);
            }

            current = next;
        }

        current.Files.Add(file);
    }

    private static IReadOnlyList<DocumentFileNode> BuildChildren(MutableDocumentFolderNode folder)
    {
        var nodes = new List<DocumentFileNode>();
        foreach (var childFolder in folder.Folders.Values)
        {
            nodes.Add(new DocumentFileNode(
                childFolder.Name,
                childFolder.Path,
                true,
                BuildChildren(childFolder)));
        }

        foreach (var file in folder.Files.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase))
        {
            nodes.Add(new DocumentFileNode(file.Name, file.Path, false, documentFile: file));
        }

        return nodes;
    }

    private static string? ResolveWorkspaceRootPath(IReadOnlyList<DocumentFile> files, string? workspaceRootPath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceRootPath))
        {
            var fullWorkspaceRootPath = SafeFullPath(workspaceRootPath);
            if (fullWorkspaceRootPath is not null)
            {
                return fullWorkspaceRootPath;
            }
        }

        var directories = files
            .Select(file => SafeDirectoryName(file.Path))
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(directory => SafeFullPath(directory!))
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Select(directory => directory!)
            .ToArray();

        return directories.Length == 0 ? null : FindCommonDirectory(directories);
    }

    private static string? FindCommonDirectory(IReadOnlyList<string> directories)
    {
        var common = directories[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var directory in directories.Skip(1))
        {
            var candidate = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            while (!IsSameOrParentDirectory(common, candidate))
            {
                var parent = Path.GetDirectoryName(common);
                if (string.IsNullOrWhiteSpace(parent))
                {
                    return null;
                }

                common = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        return common;
    }

    private static bool IsSameOrParentDirectory(string directory, string path)
    {
        if (path.Equals(directory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return true;
        }

        var normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    /// <summary>列表行目录标签：文件相对工作区根的子目录（正斜杠，原型 treePath 口径）；根目录文件返回空。</summary>
    private string FolderTagFor(string? filePath)
    {
        if (_workspacePath is not { Length: > 0 } root || string.IsNullOrEmpty(filePath)) return string.Empty;
        var relative = ResolveRelativeDirectory(root, SafeDirectoryName(filePath));
        return relative is null ? string.Empty : relative.Replace('\\', '/');
    }

    private static string? ResolveRelativeDirectory(string rootPath, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            var relativeDirectory = Path.GetRelativePath(rootPath, directory);
            if (relativeDirectory == "."
                || relativeDirectory.StartsWith("..", StringComparison.Ordinal)
                || Path.IsPathRooted(relativeDirectory))
            {
                return null;
            }

            return relativeDirectory;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> SplitRelativePath(string? relativePath)
    {
        return string.IsNullOrWhiteSpace(relativePath)
            ? []
            : relativePath
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
    }

    private static string? SafeDirectoryName(string path)
    {
        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static DocumentFileNode? FindDocumentFileNode(
        IEnumerable<DocumentFileNode> nodes,
        DocumentFile? documentFile)
    {
        if (documentFile is null)
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (node.DocumentFile is { } file && PathsEqual(file.Path, documentFile.Path))
            {
                return node;
            }

            var child = FindDocumentFileNode(node.Children, documentFile);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return left.Equals(right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }

    private sealed class MutableDocumentFolderNode
    {
        public MutableDocumentFolderNode(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }

        public string Path { get; }

        public SortedDictionary<string, MutableDocumentFolderNode> Folders { get; } =
            new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        public List<DocumentFile> Files { get; } = [];
    }
}
