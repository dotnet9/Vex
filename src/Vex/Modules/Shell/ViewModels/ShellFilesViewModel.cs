using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Regions;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellFilesViewModel : ReactiveObject, IRegionTabItem
{
    private DocumentFileNode? _selectedDocumentFileNode;
    private DocumentFileNode? _contextDocumentFileNode;
    private readonly IAppSettingsStore? _settingsStore;
    private bool _isListView;
    private bool _sortDescending;

    public ShellFilesViewModel(IAppSettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore;
        _isListView = settingsStore?.Current.FileListView ?? false;
        _sortDescending = settingsStore?.Current.FileSortDescending ?? false;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
    }

    public string? TitleKey { get; } = VexL.SidebarFiles;

    public ObservableCollection<DocumentFileNode> DocumentFileNodes { get; } = [];

    // 侧栏过滤（对应原型「侧栏内搜索」）：TreeView 绑定过滤后的列表，主列表保留用于选中恢复
    public ObservableCollection<DocumentFileNode> FilteredDocumentFileNodes { get; } = [];
    public ObservableCollection<DocumentFileNode> FlatDocumentFileNodes { get; } = [];
    public bool IsListView => _isListView;
    public bool IsTreeView => !_isListView;
    public bool CanCollapseAll => IsTreeView && Enumerate(FilteredDocumentFileNodes).Any(node => node.IsFolder && node.IsExpanded);
    public bool HasPathCommandTarget => (_contextDocumentFileNode ?? SelectedDocumentFileNode) is not null;
    public void NewDocument() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.NewDocument));
    public void QuickOpen() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.QuickOpen));
    public void RefreshFiles() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.RefreshFiles));
    public void ClearFilter() => FilterText = string.Empty;
    public void ToggleListView()
    {
        _isListView = !_isListView;
        OnPropertyChanged(nameof(IsListView)); OnPropertyChanged(nameof(IsTreeView)); OnPropertyChanged(nameof(CanCollapseAll));
        _settingsStore?.Update(settings => settings with { FileListView = _isListView });
    }
    public void ToggleSort()
    {
        _sortDescending = !_sortDescending;
        RebuildFilteredNodes();
        _settingsStore?.Update(settings => settings with { FileSortDescending = _sortDescending });
    }
    public void CollapseAll()
    {
        foreach (var node in Enumerate(FilteredDocumentFileNodes).Where(node => node.IsFolder)) node.IsExpanded = false;
        OnPropertyChanged(nameof(CanCollapseAll));
    }
    private static IEnumerable<DocumentFileNode> Enumerate(IEnumerable<DocumentFileNode> nodes)
    {
        foreach (var node in nodes) { yield return node; foreach (var child in Enumerate(node.Children)) yield return child; }
    }
    public void OpenInNewWindow()
    {
        if ((_contextDocumentFileNode ?? SelectedDocumentFileNode) is not { } node) return;
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

    public bool HasDocumentFiles => FilteredDocumentFileNodes.Count > 0;

    public bool IsDocumentFilesEmpty => !HasDocumentFiles;

    public DocumentFileNode? SelectedDocumentFileNode
    {
        get => _selectedDocumentFileNode;
        set
        {
            var previousSelection = _selectedDocumentFileNode?.DocumentFile;
            if (SetProperty(ref _selectedDocumentFileNode, value))
            {
                OnPropertyChanged(nameof(HasSelectedDocumentFile));
                OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
                OnPropertyChanged(nameof(HasPathCommandTarget));
                if (value?.DocumentFile is { } file)
                {
                    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new DocumentFileOpenRequestedCommand(file, previousSelection));
                }
            }
        }
    }

    public bool HasSelectedDocumentFile => SelectedDocumentFileNode?.DocumentFile is not null;

    public bool HasDocumentFileCommandTarget => GetDocumentFileCommandTarget() is not null;

    /// <summary>请求打开一个 Markdown 文件夹，由 Shell 协调层处理未保存确认和文件读取。</summary>
    public void OpenFolderAsync() => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.OpenFolder));

    public void OpenSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        var previousSelection = SelectedDocumentFileNode?.DocumentFile;
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
        if ((_contextDocumentFileNode ?? SelectedDocumentFileNode) is not { } node)
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
        _contextDocumentFileNode = documentFileNode;
        OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
        OnPropertyChanged(nameof(HasPathCommandTarget));
    }

    [EventHandler]
    public void ApplyDocumentFilesChanged(DocumentFilesChangedCommand command)
    {
        // 文件列表只接收 Shell 发布的快照，不直接调用主 ViewModel 的文件打开流程。
        _workspacePath = command.WorkspaceRootPath;
        OnPropertyChanged(nameof(WorkspaceName));
        var expansion = Enumerate(DocumentFileNodes).Where(node => node.IsFolder).ToDictionary(node => node.Path, node => node.IsExpanded);
        DocumentFileNodes.Clear();
        foreach (var node in BuildDocumentFileNodes(command.Files, command.WorkspaceRootPath))
        {
            DocumentFileNodes.Add(node);
        }

        foreach (var node in Enumerate(DocumentFileNodes).Where(node => node.IsFolder))
        {
            if (expansion.TryGetValue(node.Path, out var expanded)) node.IsExpanded = expanded;
            node.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanCollapseAll));
        }

        RebuildFilteredNodes();
        SelectDocumentFileSilently(FindDocumentFileNode(DocumentFileNodes, command.SelectedFile));
        NotifyDocumentFilesChanged();
    }

    [EventHandler]
    public void ApplyDocumentFileSelectionChanged(DocumentFileSelectionChangedCommand command)
    {
        // 未保存确认被取消时静默恢复旧选择，避免再次触发打开文件请求。
        SelectDocumentFileSilently(FindDocumentFileNode(DocumentFileNodes, command.SelectedFile));
    }

    private void SelectDocumentFileSilently(DocumentFileNode? documentFileNode)
    {
        if (SetProperty(ref _selectedDocumentFileNode, documentFileNode, nameof(SelectedDocumentFileNode)))
        {
            OnPropertyChanged(nameof(HasSelectedDocumentFile));
            OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
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
        FilteredDocumentFileNodes.Clear();
        FlatDocumentFileNodes.Clear();
        var filter = _filterText?.Trim();
        foreach (var node in _sortDescending ? DocumentFileNodes.Reverse() : DocumentFileNodes)
        {
            if (BuildFilteredNode(node, filter) is { } filtered)
            {
                FilteredDocumentFileNodes.Add(filtered);
            }
        }

        var flat = Enumerate(DocumentFileNodes).Where(node => node.IsDocument && (string.IsNullOrEmpty(filter)
            || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) || node.Preview.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        foreach (var node in _sortDescending ? flat.OrderByDescending(node => node.Name, StringComparer.OrdinalIgnoreCase) : flat.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase))
            FlatDocumentFileNodes.Add(node);

        NotifyDocumentFilesChanged();
    }

    private DocumentFileNode? BuildFilteredNode(DocumentFileNode node, string? filter)
    {
        if (string.IsNullOrEmpty(filter) && !_sortDescending)
        {
            return node;
        }

        if (node.IsFolder)
        {
            var children = (_sortDescending ? node.Children.Reverse() : node.Children)
                .Select(child => BuildFilteredNode(child, filter))
                .OfType<DocumentFileNode>()
                .ToList();
            return children.Count > 0
                ? CreateFilteredFolder(node, children, !string.IsNullOrEmpty(filter))
                : null;
        }

        var matches = string.IsNullOrEmpty(filter) || node.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || node.Preview.Contains(filter, StringComparison.OrdinalIgnoreCase);
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
        if (files.Count == 0)
        {
            return [];
        }

        var rootPath = ResolveWorkspaceRootPath(files, workspaceRootPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return files
                .OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                .Select(file => new DocumentFileNode(file.Name, file.Path, false, documentFile: file))
                .ToList();
        }

        var root = new MutableDocumentFolderNode(
            Path.GetFileName(rootPath) is { Length: > 0 } rootName ? rootName : rootPath,
            rootPath);

        foreach (var file in files.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase))
        {
            AddFileToTree(root, rootPath, file);
        }

        return BuildChildren(root);
    }

    private static void AddFileToTree(MutableDocumentFolderNode root, string rootPath, DocumentFile file)
    {
        var current = root;
        var directory = SafeDirectoryName(file.Path);
        var relativeDirectory = ResolveRelativeDirectory(rootPath, directory);
        foreach (var part in SplitRelativePath(relativeDirectory))
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
