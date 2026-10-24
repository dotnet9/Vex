using Avalonia.Threading;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellFilesViewModel
{
    private readonly Dictionary<string, long> _pathChangeRevisions = new(PathComparer);
    private void OnWorkspaceFilesChanged(object? sender, WorkspaceFilesChangedEventArgs change)
    {
        void ApplyChange()
        {
            if (_disposed || _workspaceFiles is null || change.Generation != _workspaceFiles.Generation) return;
            var changedSession = _workspaceGeneration != change.Generation;
            EnsureWorkspaceSession();
            if (changedSession) return;
            foreach (var path in change.ChangedPaths)
                _pathChangeRevisions[path] = _pathChangeRevisions.GetValueOrDefault(path) + 1;
            var candidates = Enumerate(DocumentFileNodes).Where(node => !node.IsPlaceholder
                && change.ChangedPaths.Any(path => PathsEqual(path, node.Path))).ToArray();
            _ = RemoveMissingNodesAsync(candidates, _workspaceGeneration, _workspaceCancellation.Token);
            var affected = change.DirectoryPaths.ToHashSet(PathComparer);
            foreach (var node in Enumerate(DocumentFileNodes).Where(node => node.IsFolder).ToArray())
            {
                if (!change.IsRecovery && !affected.Contains(node.Path)) continue;
                node.IsInvalidated = true;
                if (_nodeLoads.TryGetValue(node, out var state))
                {
                    state.Revision++;
                    state.RefreshPending = true;
                }
                if (node.IsExpanded && node.LoadState != DocumentFileNode.DirectoryLoadState.NotLoaded)
                    _ = EnsureDirectoryLoadedAsync(node, refresh: true);
            }
            var changedDocuments = Enumerate(DocumentFileNodes).Concat(Enumerate(FilteredDocumentFileNodes)).Concat(FlatDocumentFileNodes)
                .Where(node => node.IsDocument && (change.IsRecovery || change.ChangedPaths.Any(path => IsSameOrParentDirectory(path, node.Path))))
                .Distinct().ToArray();
            foreach (var group in changedDocuments.GroupBy(node => node.Path, PathComparer))
            {
                InvalidatePreview(group.Key);
                foreach (var node in group) node.SetPreview(string.Empty);
            }
            if (changedDocuments.Length > 0)
            {
                _previewRevision++;
                OnPropertyChanged(nameof(PreviewRevision));
            }
            if (_fileFactory is not null) _ = RefreshMetadataAsync(changedDocuments, _workspaceGeneration, _workspaceCancellation.Token);
            InvalidateIndexProjection();
        }
        if (Dispatcher.UIThread.CheckAccess()) ApplyChange();
        else Dispatcher.UIThread.Post(ApplyChange);
    }

    private async Task RemoveMissingNodesAsync(DocumentFileNode[] nodes, long generation, CancellationToken token)
    {
        var versions = nodes.ToDictionary(node => node.Path, node => _pathChangeRevisions.GetValueOrDefault(node.Path), PathComparer);
        try
        {
            var removed = await Task.Run(() => nodes.Where(node => node.IsFolder ? !Directory.Exists(node.Path) : !File.Exists(node.Path)).ToArray(), token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration) return;
                foreach (var node in removed)
                {
                    if (!IsAttached(node) || versions[node.Path] != _pathChangeRevisions.GetValueOrDefault(node.Path)) continue;
                    if (node.Parent is not { } parent)
                    {
                        node.LoadState = DocumentFileNode.DirectoryLoadState.Failed;
                        node.IsInvalidated = true;
                        SetPlaceholder(node, failed: true);
                        continue;
                    }
                    if (SelectedDocumentFileNode is { } selected && IsNodeInSubtree(node, selected)) SelectDocumentFileSilently(parent);
                    if (_contextDocumentFileNode is { } context && IsNodeInSubtree(node, context)) ClearContextDocumentFile();
                    parent.Children.Remove(node);
                    DetachNode(node);
                }
                NotifyDocumentFilesChanged();
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task RefreshMetadataAsync(DocumentFileNode[] nodes, long generation, CancellationToken token)
    {
        var revisions = nodes.Select(node => node.Path).Distinct(PathComparer)
            .ToDictionary(path => path, path => _previewVersions.GetValueOrDefault(path), PathComparer);
        try
        {
            var metadata = await Task.Run(() => nodes.Select(node => node.Path).Distinct(PathComparer)
                .ToDictionary(path => path, path => _fileFactory!.GetModifiedText(path), PathComparer), token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration) return;
                foreach (var node in nodes)
                    if (revisions[node.Path] == _previewVersions.GetValueOrDefault(node.Path)
                        && node.DocumentFile is { } file && metadata.TryGetValue(node.Path, out var text)) file.ModifiedText = text;
            });
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"File metadata failed: {error.Message}");
        }
    }

    /// <summary>仅显式定位结果时逐层加载，普通快照及监控刷新不主动展开路径。</summary>
    public async Task RevealDocumentAsync(DocumentFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || _workspaceFiles is null || _workspacePath is null) return;
        var generation = _workspaceGeneration;
        var document = SafeFullPath(file.Path);
        if (document is null || !IsSameOrParentDirectory(_workspacePath, document)) return;
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _workspaceCancellation.Token);
            var parts = Path.GetRelativePath(_workspacePath, document)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            var current = DocumentFileNodes.FirstOrDefault();
            foreach (var part in parts)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (current is not { IsFolder: true } || generation != _workspaceGeneration) return;
                current.IsExpanded = true;
                await EnsureDirectoryLoadedAsync(current);
                if (generation != _workspaceGeneration || cancellation.IsCancellationRequested) return;
                current = current.Children.FirstOrDefault(node => !node.IsPlaceholder && PathComparer.Equals(node.Name, part));
            }
            if (current is not null && PathsEqual(current.Path, document))
            {
                var visible = IsListView ? FlatDocumentFileNodes : FilteredDocumentFileNodes;
                SelectDocumentFileSilently(Enumerate(visible).FirstOrDefault(node => node.IsDocument && PathsEqual(node.Path, document)) ?? current);
                _projectionSelectionPath = document;
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_workspaceFiles is not null) _workspaceFiles.Changed -= OnWorkspaceFilesChanged;
        CodeWF.Toolkit.EventBus.EventBus.Default.Unsubscribe(this);
        _workspaceCancellation.Cancel();
        _workspaceCancellation.Dispose();
        ResetProjection();
        foreach (var root in DocumentFileNodes) DetachNode(root);
    }
}
