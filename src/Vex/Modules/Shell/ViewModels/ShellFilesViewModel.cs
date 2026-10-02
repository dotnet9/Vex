using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Regions;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellFilesViewModel : ReactiveObject, IRegionTabItem
{
    private DocumentFileNode? _selectedDocumentFileNode;
    private DocumentFileNode? _contextDocumentFileNode;

    public ShellFilesViewModel()
    {
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    public string? TitleKey { get; } = VexL.SidebarFiles;

    public ObservableCollection<DocumentFileNode> DocumentFileNodes { get; } = [];

    public bool HasDocumentFiles => DocumentFileNodes.Count > 0;

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
                if (value?.DocumentFile is { } file)
                {
                    CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileOpenRequestedCommand(file, previousSelection));
                }
            }
        }
    }

    public bool HasSelectedDocumentFile => SelectedDocumentFileNode?.DocumentFile is not null;

    public bool HasDocumentFileCommandTarget => GetDocumentFileCommandTarget() is not null;

    public void OpenSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        var previousSelection = SelectedDocumentFileNode?.DocumentFile;
        ClearContextDocumentFile();
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileOpenRequestedCommand(file, previousSelection));
    }

    public void RenameSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileRenameRequestedCommand(file));
    }

    public void OpenSelectedFileLocation()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileOpenLocationRequestedCommand(file));
    }

    public void DeleteSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        CodeWF.EventBus.EventBus.Default.Publish(new DocumentFileDeleteRequestedCommand(file));
    }

    public void SelectDocumentFileForContextMenu(DocumentFileNode documentFileNode)
    {
        _contextDocumentFileNode = documentFileNode;
        OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
    }

    [EventHandler]
    public void ApplyDocumentFilesChanged(DocumentFilesChangedCommand command)
    {
        // 文件列表只接收 Shell 发布的快照，不直接调用主 ViewModel 的文件打开流程。
        DocumentFileNodes.Clear();
        foreach (var node in BuildDocumentFileNodes(command.Files, command.WorkspaceRootPath))
        {
            DocumentFileNodes.Add(node);
        }

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
        _contextDocumentFileNode?.DocumentFile ?? SelectedDocumentFileNode?.DocumentFile;

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
        if (path.Equals(directory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                  + Path.DirectorySeparatorChar;
        return path.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
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
            return Path.GetFullPath(left).Equals(Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
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
            new(StringComparer.OrdinalIgnoreCase);

        public List<DocumentFile> Files { get; } = [];
    }
}
