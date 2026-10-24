using System.Collections.ObjectModel;
using Avalonia.Threading;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellFilesViewModel
{
    private CancellationTokenSource? _indexCancellation;
    private IReadOnlyList<DocumentFile>? _indexedFiles;
    private readonly List<DocumentFileNode> _temporaryRoots = [];
    private long _projectionRevision;
    private bool _isIndexLoading;
    private bool _hasIndexError;
    private string? _projectedFilter;
    private bool _projectedList;
    private bool _projectedDescending;
    private string? _projectionSelectionPath;
    private bool _treeSortDescending;

    public bool IsIndexLoading => _isIndexLoading;
    public bool HasIndexError => _hasIndexError;

    private void SetIndexStatus(bool loading, bool failed)
    {
        _isIndexLoading = loading;
        _hasIndexError = failed;
        OnPropertyChanged(nameof(IsIndexLoading));
        OnPropertyChanged(nameof(HasIndexError));
        OnPropertyChanged(nameof(IsDocumentFilesEmpty));
    }

    private void ResetProjection()
    {
        _projectionRevision++;
        _indexCancellation?.Cancel();
        _indexCancellation?.Dispose();
        _indexCancellation = null;
        _indexedFiles = null;
        _projectedFilter = null;
        foreach (var node in _temporaryRoots) DetachNode(node);
        _temporaryRoots.Clear();
        foreach (var node in FlatDocumentFileNodes) node.DetachDocument();
        _previewVersions.Clear();
        _previewTasks.Clear();
        _previewValues.Clear();
        _projectionSelectionPath = null;
        _pathChangeRevisions.Clear();
        SetIndexStatus(false, false);
    }

    private void InvalidateIndexProjection()
    {
        _indexedFiles = null;
        _projectedFilter = null;
        if (IsListView || !string.IsNullOrWhiteSpace(FilterText)) RebuildWorkspaceProjection();
    }

    public void RetryIndex()
    {
        _indexedFiles = null;
        _projectedFilter = null;
        RebuildWorkspaceProjection();
    }

    private void RebuildWorkspaceProjection()
    {
        if (_workspaceFiles is null || _disposed) return;
        var filter = FilterText.Trim();
        _projectionSelectionPath = SelectedDocumentFileNode?.Path ?? _projectionSelectionPath;
        if (!IsListView && filter.Length == 0)
        {
            _indexCancellation?.Cancel();
            _projectionRevision++;
            SetIndexStatus(false, false);
            if (_treeSortDescending != _sortDescending)
            {
                foreach (var folder in Enumerate(DocumentFileNodes).Where(node => node.IsFolder).ToArray())
                    Synchronize(folder.Children, SortNodes(folder.Children).ToArray());
                _treeSortDescending = _sortDescending;
            }
            Synchronize(FilteredDocumentFileNodes, DocumentFileNodes);
            foreach (var node in FlatDocumentFileNodes) node.DetachDocument();
            FlatDocumentFileNodes.Clear();
            ClearTemporaryProjection();
            if (_projectionSelectionPath is { } selectedPath)
                SelectDocumentFileSilently(Enumerate(DocumentFileNodes).FirstOrDefault(node => PathsEqual(node.Path, selectedPath)));
            _projectedFilter = null;
            NotifyDocumentFilesChanged();
            return;
        }
        if (_projectedFilter == filter && _projectedList == IsListView && _projectedDescending == _sortDescending) return;
        _projectedFilter = filter;
        _projectedList = IsListView;
        _projectedDescending = _sortDescending;
        _indexCancellation?.Cancel();
        _indexCancellation?.Dispose();
        _indexCancellation = CancellationTokenSource.CreateLinkedTokenSource(_workspaceCancellation.Token);
        var revision = ++_projectionRevision;
        SetIndexStatus(true, false);
        // 完整索引尚未完成时不展示过期搜索结果，也不误报“无匹配项”。
        FilteredDocumentFileNodes.Clear();
        foreach (var node in FlatDocumentFileNodes) node.DetachDocument();
        FlatDocumentFileNodes.Clear();
        ClearTemporaryProjection();
        _ = BuildIndexProjectionAsync(filter, IsListView, _sortDescending, revision, _workspaceGeneration, _indexCancellation.Token);
    }

    private async Task BuildIndexProjectionAsync(string filter, bool list, bool descending, long revision, long generation, CancellationToken token)
    {
        try
        {
            if (filter.Length > 0) await Task.Delay(250, token).ConfigureAwait(false);
            var files = _indexedFiles ?? await _workspaceFiles!.GetFilesAsync(token).ConfigureAwait(false);
            var matched = await Task.Run(() => files
                .Where(file => filter.Length == 0 || file.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase).ToArray(), token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || revision != _projectionRevision) return;
                _indexedFiles = files;
                var selectedPath = SelectedDocumentFileNode?.Path ?? _projectionSelectionPath;
                if (list)
                {
                    var sorted = descending ? matched.Reverse() : matched.AsEnumerable();
                    foreach (var file in sorted)
                        FlatDocumentFileNodes.Add(new DocumentFileNode(file.Name, file.Path, false, documentFile: file));
                }
                else if (matched.Length > 0)
                {
                    foreach (var root in BuildDocumentFileNodes(matched, _workspacePath))
                    {
                        ExpandSearchProjection(root);
                        _temporaryRoots.Add(root);
                        FilteredDocumentFileNodes.Add(root);
                    }
                }
                var visible = list ? FlatDocumentFileNodes : FilteredDocumentFileNodes;
                SelectDocumentFileSilently(selectedPath is null ? null : Enumerate(visible).FirstOrDefault(node => PathsEqual(node.Path, selectedPath)));
                SetIndexStatus(false, false);
                NotifyDocumentFilesChanged();
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || revision != _projectionRevision) return;
                _projectedFilter = null;
                RebuildWorkspaceProjection();
            });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!token.IsCancellationRequested && generation == _workspaceGeneration && revision == _projectionRevision)
                    SetIndexStatus(false, true);
            });
        }
    }

    private void ClearTemporaryProjection()
    {
        foreach (var root in _temporaryRoots) DetachNode(root);
        _temporaryRoots.Clear();
    }

    private void ExpandSearchProjection(DocumentFileNode node)
    {
        node.IsExpanded = node.IsFolder;
        Synchronize(node.Children, SortNodes(node.Children).ToArray());
        foreach (var child in node.Children)
        {
            child.Parent = node;
            ExpandSearchProjection(child);
        }
    }

    private IEnumerable<DocumentFileNode> SortNodes(IEnumerable<DocumentFileNode> nodes)
    {
        var ordered = nodes.OrderBy(node => node.IsPlaceholder ? 2 : node.IsFolder ? 0 : 1);
        return _sortDescending
            ? ordered.ThenByDescending(node => node.Name, StringComparer.OrdinalIgnoreCase)
            : ordered.ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static void Synchronize(ObservableCollection<DocumentFileNode> target, IEnumerable<DocumentFileNode> source)
    {
        var desired = source.ToArray();
        var retained = desired.ToHashSet();
        foreach (var removed in target.Where(node => !retained.Contains(node)).ToArray()) target.Remove(removed);
        for (var index = 0; index < desired.Length; index++)
        {
            if (index < target.Count && ReferenceEquals(target[index], desired[index])) continue;
            var current = target.IndexOf(desired[index]);
            if (current < 0) target.Insert(index, desired[index]);
            else if (current != index) target.Move(current, index);
        }
    }
}
