using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

public sealed class AutoSaveDraftService : IAutoSaveDraftService, IDisposable
{
    private static readonly Encoding DraftEncoding = new UTF8Encoding(false);
    private readonly object _sync = new();
    private readonly string _session = Guid.NewGuid().ToString("N");
    private readonly Dictionary<string, List<string>> _restoredPaths = new();
    private AutoSaveDraft? _pendingDraft;
    private Timer? _timer;
    private FileStream? _sessionLock;
    private bool _completed;
    private bool _disposed;
    public string StorageDirectory { get; init; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vex", "Drafts");
    public string? LastError { get; private set; }
    public event EventHandler? StateChanged;

    public DocumentSnapshot? TryRestore(DocumentSnapshot document)
    {
        if (!Directory.Exists(StorageDirectory)) return null;
        var key = GetDocumentKey(document);
        DocumentSnapshot? recovery = null;
        var candidates = new List<string>();
        try
        {
            // 每个进程独立保存，打开文件或新建窗口不会覆盖尚未查看的恢复数据。
            foreach (var path in Directory.EnumerateFiles(StorageDirectory, key + ".*.json").OrderByDescending(File.GetLastWriteTimeUtc))
            {
                if (path == GetDraftPath(document) || !IsAbandonedSession(path)) continue;
                try
                {
                    var draft = JsonSerializer.Deserialize(File.ReadAllText(path, DraftEncoding), AutoSaveDraftJsonContext.Default.AutoSaveDraft);
                    if (draft?.Markdown is null) throw new InvalidDataException("Invalid recovery draft: " + path);
                    candidates.Add(path);
                    if (!MarkdownTextComparer.EqualsNormalizedLineEndings(draft.Markdown, document.Markdown))
                        recovery ??= document with { Markdown = draft.Markdown, FileName = draft.FileName };
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
                { SetError(exception.Message); }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { SetError(exception.Message); }
        if (candidates.Count > 0) _restoredPaths[key] = candidates;
        return recovery;
    }

    public void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown)
    {
        if (MarkdownTextComparer.EqualsNormalizedLineEndings(markdown, lastSavedMarkdown)) { Clear(document); return; }
        lock (_sync)
        {
            if (_completed || _disposed) return;
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
            catch (DirectoryNotFoundException) { } // 首次运行尚无草稿目录，清理已满足要求。
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { SetError(exception.Message); }
        }
    }

    public void ClearRestored(DocumentSnapshot document)
    {
        lock (_sync)
        {
            try { ArchiveRecovery(GetDocumentKey(document)); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { SetError(exception.Message); }
        }
    }

    public bool Flush()
    {
        lock (_sync)
        {
            if (_disposed) return _completed;
            if (_pendingDraft is not { } draft) return true;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            try
            {
                Directory.CreateDirectory(StorageDirectory);
                if (_sessionLock is null)
                {
                    _sessionLock = new FileStream(SessionPath(_session), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                _sessionLock.Position = 0;
                _sessionLock.Write("active"u8);
                _sessionLock.SetLength(_sessionLock.Position);
                _sessionLock.Flush(true);
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

    public bool CompleteSession()
    {
        lock (_sync)
        {
            if (_completed) return true;
            if (_disposed) return false;
            try
            {
                // 正常退出同时结束尚未查看的恢复提示；归档保留内容，避免误删历史备份。
                foreach (var key in _restoredPaths.Keys.ToArray()) ArchiveRecovery(key);
                if (Directory.Exists(StorageDirectory))
                    foreach (var path in Directory.EnumerateFiles(StorageDirectory, "*." + _session + ".json*")) File.Delete(path);
                if (_sessionLock is not null)
                {
                    _sessionLock.Position = 0;
                    _sessionLock.SetLength(0);
                    _sessionLock.Write("closed"u8);
                    _sessionLock.Flush(true);
                }
                _completed = true;
                _pendingDraft = null;
                _timer?.Dispose(); _timer = null;
                _sessionLock?.Dispose(); _sessionLock = null;
                SetError(null);
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { SetError(exception.Message); return false; }
        }
    }

    private void ArchiveRecovery(string key)
    {
        if (!_restoredPaths.TryGetValue(key, out var paths)) return;
        foreach (var path in paths)
            if (File.Exists(path)) File.Move(path, path + ".resolved", true);
        _restoredPaths.Remove(key);
    }

    private bool IsAbandonedSession(string draftPath)
    {
        var session = Path.GetFileNameWithoutExtension(draftPath).Split('.').Last();
        if (!Guid.TryParseExact(session, "N", out _)) return false;
        try
        {
            // 只有带新会话标记的记录能证明异常退出；旧格式备份保留在磁盘，不再自动提示。
            if (!File.Exists(SessionPath(session))) return false;
            using var marker = new FileStream(SessionPath(session), FileMode.Open, FileAccess.Read, FileShare.None);
            using var reader = new StreamReader(marker);
            return reader.ReadToEnd() == "active";
        }
        catch (IOException) { return false; } // 活跃窗口持有独占锁，不能当作崩溃恢复。
    }

    private string SessionPath(string session) => Path.Combine(StorageDirectory, session + ".session");

    public void Dispose()
    {
        lock (_sync)
        {
            _disposed = true;
            _pendingDraft = null;
            _timer?.Dispose(); _timer = null;
            _sessionLock?.Dispose(); _sessionLock = null;
        }
    }

    private void SetError(string? error) { LastError = error; StateChanged?.Invoke(this, EventArgs.Empty); }
    private string GetDraftPath(DocumentSnapshot document) => Path.Combine(StorageDirectory, GetDocumentKey(document) + "." + _session + ".json");
    private static string GetDocumentKey(DocumentSnapshot document) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        document.FilePath is { Length: > 0 } path ? Path.GetFullPath(path) : "__untitled__"))).ToLowerInvariant();
}
