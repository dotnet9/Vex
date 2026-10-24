using System.ComponentModel;
using Avalonia.Threading;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellFilesViewModel
{
    private readonly IWorkspaceFileService? _workspaceFiles;
    private readonly Dictionary<DocumentFileNode, NodeLoad> _nodeLoads = [];
    private CancellationTokenSource _workspaceCancellation = new();
    private long _workspaceGeneration = -1;
    private bool _batchExpansionChange;
    private bool _disposed;

    private void EnsureWorkspaceSession()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(EnsureWorkspaceSession);
            return;
        }
        if (_disposed || _workspaceFiles is null || _workspaceGeneration == _workspaceFiles.Generation) return;
        _workspaceCancellation.Cancel();
        _workspaceCancellation.Dispose();
        _workspaceCancellation = new CancellationTokenSource();
        foreach (var root in DocumentFileNodes) DetachNode(root);
        _nodeLoads.Clear();
        ResetProjection();
        DocumentFileNodes.Clear();
        FilteredDocumentFileNodes.Clear();
        FlatDocumentFileNodes.Clear();
        ClearContextDocumentFile();
        _workspaceGeneration = _workspaceFiles.Generation;
        _workspacePath = _workspaceFiles.RootPath;
        OnPropertyChanged(nameof(WorkspaceName));
        SelectDocumentFileSilently(null);
        if (_workspacePath is { Length: > 0 } path)
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var root = CreateDirectoryNode(string.IsNullOrEmpty(name) ? path : name, path, null);
            DocumentFileNodes.Add(root);
            _batchExpansionChange = true;
            try { root.IsExpanded = true; }
            finally { _batchExpansionChange = false; }
            SelectDocumentFileSilently(root);
        }
        RebuildWorkspaceProjection();
    }

    private void ApplyWorkspaceDocumentFilesChanged(DocumentFilesChangedCommand command)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ApplyWorkspaceDocumentFilesChanged(command));
            return;
        }
        EnsureWorkspaceSession();
        _currentDocumentFile = command.SelectedFile;
        if (command.FocusWorkspaceRoot && IsTreeView)
        {
            FilterText = string.Empty;
            SelectDocumentFileSilently(DocumentFileNodes.FirstOrDefault());
        }
        else if (SelectedDocumentFileNode is null)
        {
            SelectDocumentFileSilently(FindDocumentFileNode(DocumentFileNodes, command.SelectedFile));
        }
        NotifyDocumentFilesChanged();
    }

    private DocumentFileNode CreateDirectoryNode(string name, string path, DocumentFileNode? parent)
    {
        var node = new DocumentFileNode(name, path, true) { Parent = parent };
        node.Children.Add(DocumentFileNode.CreatePlaceholder());
        node.Children[0].Parent = node;
        node.PropertyChanged += OnDirectoryNodeChanged;
        return node;
    }

    private void OnDirectoryNodeChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(DocumentFileNode.IsExpanded) || sender is not DocumentFileNode node) return;
        if (node.IsExpanded) _ = EnsureDirectoryLoadedAsync(node);
        if (!_batchExpansionChange) SaveExpansionState();
        OnPropertyChanged(nameof(CanCollapseAll));
    }

    private void SaveExpansionState()
    {
        _settingsStore?.Update(settings =>
        {
            var value = settings.CollapsedFolders;
            foreach (var folder in Enumerate(DocumentFileNodes).Where(node => node.IsFolder))
                value = NavigationExpansionState.Set(value, NavigationExpansionState.Encode(NavigationExpansionState.PathKey(folder.Path)), !folder.IsExpanded);
            return settings with { CollapsedFolders = value };
        });
    }

    public Task RetryDirectoryAsync(DocumentFileNode? node)
    {
        if (node?.IsPlaceholder == true) node = node.Parent;
        if (node is not { IsFolder: true }) return Task.CompletedTask;
        node.IsInvalidated = true;
        return EnsureDirectoryLoadedAsync(node, refresh: true);
    }

    private Task EnsureDirectoryLoadedAsync(DocumentFileNode node, bool refresh = false)
    {
        if (_disposed || _workspaceFiles is null || !node.IsFolder || !IsAttached(node)) return Task.CompletedTask;
        if (!_nodeLoads.TryGetValue(node, out var state))
        {
            state = new NodeLoad(CancellationTokenSource.CreateLinkedTokenSource(_workspaceCancellation.Token));
            _nodeLoads.Add(node, state);
        }
        if (state.Task is { IsCompleted: false }) return state.Task;
        if (!refresh && !node.IsInvalidated && node.LoadState == DocumentFileNode.DirectoryLoadState.Loaded) return Task.CompletedTask;
        state.RefreshPending = false;
        state.Task = LoadDirectoryAsync(node, state, refresh || node.IsInvalidated);
        return state.Task;
    }

    private async Task LoadDirectoryAsync(DocumentFileNode node, NodeLoad state, bool refresh)
    {
        var generation = _workspaceGeneration;
        var revision = state.Revision;
        var token = state.Cancellation.Token;
        // 延后到任务登记后启动，兼容同步命中的目录缓存。
        await Task.Yield();
        if (token.IsCancellationRequested) return;
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || !IsAttached(node)) return;
                node.LoadState = DocumentFileNode.DirectoryLoadState.Loading;
                SetPlaceholder(node, failed: false);
            });
            token.ThrowIfCancellationRequested();
            var snapshot = await _workspaceFiles!.GetDirectoryAsync(node.Path, refresh, token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || !IsAttached(node)) return;
                if (revision != state.Revision || !_workspaceFiles.IsCurrent(snapshot))
                {
                    node.IsInvalidated = true;
                    state.RefreshPending = true;
                    return;
                }
                ApplyDirectorySnapshot(node, snapshot);
                node.LoadState = DocumentFileNode.DirectoryLoadState.Loaded;
                node.IsInvalidated = false;
                RebuildWorkspaceProjection();
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || !IsAttached(node)) return;
                node.IsInvalidated = true;
                state.RefreshPending = true;
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || !IsAttached(node)) return;
                node.LoadState = DocumentFileNode.DirectoryLoadState.Failed;
                node.IsInvalidated = true;
                SetPlaceholder(node, failed: true);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                state.Task = null;
                if (!token.IsCancellationRequested && generation == _workspaceGeneration && state.RefreshPending && node.IsExpanded && IsAttached(node))
                    _ = EnsureDirectoryLoadedAsync(node, refresh: true);
            });
        }
    }

    private static void SetPlaceholder(DocumentFileNode node, bool failed)
    {
        // 已有内容在刷新期间保留，首次失败仍保留可点击的重试入口。
        foreach (var placeholder in node.Children.Where(child => child.IsPlaceholder).ToArray()) node.Children.Remove(placeholder);
        if (failed || node.Children.Count == 0)
        {
            var placeholder = DocumentFileNode.CreatePlaceholder(failed);
            placeholder.Parent = node;
            node.Children.Add(placeholder);
        }
    }

    private void ApplyDirectorySnapshot(DocumentFileNode parent, WorkspaceDirectorySnapshot snapshot)
    {
        var existing = parent.Children.Where(node => !node.IsPlaceholder)
            .ToDictionary(node => node.Path, PathComparer);
        var desired = new List<DocumentFileNode>();
        foreach (var entry in snapshot.Entries)
        {
            if (!existing.TryGetValue(entry.Path, out var node) || node.IsFolder != entry.IsDirectory || node.Name != entry.Name)
            {
                node = entry.IsDirectory ? CreateDirectoryNode(entry.Name, entry.Path, parent)
                    : new DocumentFileNode(entry.Name, entry.Path, false, documentFile: entry.DocumentFile) { Parent = parent };
            }
            else if (!entry.IsDirectory) node.UpdateDocumentFile(entry.DocumentFile);
            desired.Add(node);
        }
        desired = SortNodes(desired).ToList();
        var retained = desired.ToHashSet();
        foreach (var removed in parent.Children.Where(node => !retained.Contains(node)).ToArray())
        {
            if (SelectedDocumentFileNode is { } selected && IsNodeInSubtree(removed, selected)) SelectDocumentFileSilently(parent);
            if (_contextDocumentFileNode is { } context && IsNodeInSubtree(removed, context)) ClearContextDocumentFile();
            parent.Children.Remove(removed);
            DetachNode(removed);
        }
        for (var index = 0; index < desired.Count; index++)
        {
            var node = desired[index];
            if (index < parent.Children.Count && ReferenceEquals(parent.Children[index], node)) continue;
            var current = parent.Children.IndexOf(node);
            if (current < 0) parent.Children.Insert(index, node);
            else if (current != index) parent.Children.Move(current, index);
        }
    }

    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private bool IsAttached(DocumentFileNode node)
    {
        while (node.Parent is { } parent) node = parent;
        return DocumentFileNodes.Contains(node);
    }

    private static bool IsNodeInSubtree(DocumentFileNode parent, DocumentFileNode node)
    {
        for (DocumentFileNode? current = node; current is not null; current = current.Parent)
            if (ReferenceEquals(current, parent)) return true;
        return false;
    }

    private void DetachNode(DocumentFileNode node)
    {
        node.PropertyChanged -= OnDirectoryNodeChanged;
        node.DetachDocument();
        if (_nodeLoads.Remove(node, out var state))
        {
            state.Cancellation.Cancel();
            state.Cancellation.Dispose();
        }
        foreach (var child in node.Children) DetachNode(child);
        node.Parent = null;
    }

    private sealed class NodeLoad(CancellationTokenSource cancellation)
    {
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public Task? Task { get; set; }
        public long Revision { get; set; }
        public bool RefreshPending { get; set; }
    }
}
