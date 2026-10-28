using Avalonia.Threading;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellFilesViewModel
{
    private readonly Dictionary<string, long> _previewVersions = new(PathComparer);
    private readonly Dictionary<string, Task> _previewTasks = new(PathComparer);
    private readonly Dictionary<string, string> _previewValues = new(PathComparer);
    private long _previewRevision;

    public long PreviewRevision => _previewRevision;

    private void InvalidatePreview(string path)
    {
        _previewVersions[path] = _previewVersions.GetValueOrDefault(path) + 1;
        _previewValues.Remove(path);
    }

    /// <summary>仅实际打开提示或列表项进入可见区域时请求摘要。</summary>
    public Task RequestPreviewAsync(DocumentFileNode node)
    {
        if (_workspaceFiles is null || !node.IsDocument || _disposed) return Task.CompletedTask;
        if (_previewValues.TryGetValue(node.Path, out var cached))
        {
            node.SetPreview(cached);
            return Task.CompletedTask;
        }
        if (_previewTasks.TryGetValue(node.Path, out var existing)) return existing;
        var task = LoadPreviewAsync(node.Path, _workspaceGeneration, _previewVersions.GetValueOrDefault(node.Path), _workspaceCancellation.Token);
        _previewTasks[node.Path] = task;
        return task;
    }

    private async Task LoadPreviewAsync(string path, long generation, long revision, CancellationToken token)
    {
        // 保证调用者先登记任务，防止服务缓存同步完成时留存已完成任务。
        await Task.Yield();
        try
        {
            var preview = await _workspaceFiles!.GetPreviewAsync(path, token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested || generation != _workspaceGeneration || revision != _previewVersions.GetValueOrDefault(path)) return;
                _previewValues[path] = preview;
                foreach (var node in Enumerate(DocumentFileNodes).Concat(Enumerate(FilteredDocumentFileNodes)).Concat(FlatDocumentFileNodes)
                             .Where(node => node.IsDocument && PathsEqual(node.Path, path)).Distinct())
                    node.SetPreview(preview);
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // 摘要失败不阻断文件打开，下次显示时允许重试。
            System.Diagnostics.Debug.WriteLine($"File preview failed: {path}: {error.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation == _workspaceGeneration) _previewTasks.Remove(path);
                if (!token.IsCancellationRequested && generation == _workspaceGeneration && revision != _previewVersions.GetValueOrDefault(path))
                {
                    _previewRevision++;
                    OnPropertyChanged(nameof(PreviewRevision));
                }
            });
        }
    }
}
