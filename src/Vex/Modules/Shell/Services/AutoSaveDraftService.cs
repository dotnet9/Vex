using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class AutoSaveDraftService : IAutoSaveDraftService
{
    private static readonly Encoding DraftEncoding = new UTF8Encoding(false);
    private readonly object _sync = new();
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, string> _restoredPaths = new();
    private AutoSaveDraft? _pendingDraft;
    private Timer? _timer;
    public string StorageDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vex", "Drafts");
    public string? LastError { get; private set; }
    public event EventHandler? StateChanged;

    public DocumentSnapshot? TryRestore(DocumentSnapshot document)
    {
        if (!Directory.Exists(StorageDirectory)) return null;
        var key = GetDocumentKey(document);
        try
        {
            // 每个进程独立保存，打开文件或新建窗口不会覆盖尚未查看的恢复数据。
            foreach (var path in Directory.EnumerateFiles(StorageDirectory, key + ".*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                if (path == GetDraftPath(document)) continue;
                try
                {
                    var draft = JsonSerializer.Deserialize(File.ReadAllText(path, DraftEncoding), AutoSaveDraftJsonContext.Default.AutoSaveDraft);
                    if (draft?.Markdown is null) throw new InvalidDataException("Invalid recovery draft: " + path);
                    if (MarkdownTextComparer.EqualsNormalizedLineEndings(draft.Markdown, document.Markdown)) continue;
                    _restoredPaths[key] = path;
                    return document with { Markdown = draft.Markdown };
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
                { SetError(exception.Message); }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { SetError(exception.Message); }
        return null;
    }

    public void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown)
    {
        if (MarkdownTextComparer.EqualsNormalizedLineEndings(markdown, lastSavedMarkdown)) { Clear(document); return; }
        lock (_sync)
        {
            _pendingDraft = AutoSaveDraft.FromDocument(document, markdown, GetDraftPath(document));
            _timer ??= new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(TimeSpan.FromSeconds(1.5), Timeout.InfiniteTimeSpan);
        }
    }

    public void Clear(DocumentSnapshot document)
    {
        lock (_sync)
        {
            var path = GetDraftPath(document);
            if (_pendingDraft?.Path == path) { _pendingDraft = null; _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); }
            try { File.Delete(path); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { SetError(exception.Message); }
        }
    }

    public void ClearRestored(DocumentSnapshot document)
    {
        var key = GetDocumentKey(document);
        if (!_restoredPaths.TryGetValue(key, out var path)) return;
        try { File.Delete(path); _restoredPaths.Remove(key); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { SetError(exception.Message); }
    }

    public bool Flush()
    {
        lock (_sync)
        {
            if (_pendingDraft is not { } draft) return true;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            try
            {
                Directory.CreateDirectory(StorageDirectory);
                var json = JsonSerializer.Serialize(draft, AutoSaveDraftJsonContext.Default.AutoSaveDraft);
                File.WriteAllText(draft.Path + ".tmp", json, DraftEncoding);
                File.Move(draft.Path + ".tmp", draft.Path, true);
                _pendingDraft = null;
                SetError(null);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 保留待写快照供重试；失败不能伪装成已落盘。
                SetError(exception.Message);
                return false;
            }
        }
    }

    private void SetError(string? error) { LastError = error; StateChanged?.Invoke(this, EventArgs.Empty); }
    private string GetDraftPath(DocumentSnapshot document) => Path.Combine(StorageDirectory, GetDocumentKey(document) + "." + _session + ".json");
    private static string GetDocumentKey(DocumentSnapshot document) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        document.FilePath is { Length: > 0 } path ? Path.GetFullPath(path) : "__untitled__"))).ToLowerInvariant();
}
