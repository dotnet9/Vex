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
    private AutoSaveDraft? _pendingDraft;
    private Timer? _timer;

    public DocumentSnapshot? TryRestore(DocumentSnapshot document)
    {
        try
        {
            var path = GetDraftPath(document);
            if (!File.Exists(path))
            {
                return null;
            }

            var json = File.ReadAllText(path, DraftEncoding);
            var draft = JsonSerializer.Deserialize(json, AutoSaveDraftJsonContext.Default.AutoSaveDraft);
            if (draft is null || draft.Markdown is null
                || MarkdownTextComparer.EqualsNormalizedLineEndings(draft.Markdown, document.Markdown))
            {
                Clear(document);
                return null;
            }

            if (DiskFileChangedAfterDraft(document, draft))
            {
                Clear(document);
                return null;
            }

            return document with { Markdown = draft.Markdown };
        }
        catch
        {
            return null;
        }
    }

    public void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown)
    {
        if (MarkdownTextComparer.EqualsNormalizedLineEndings(markdown, lastSavedMarkdown))
        {
            Clear(document);
            return;
        }

        lock (_sync)
        {
            _pendingDraft = AutoSaveDraft.FromDocument(document, markdown, GetDraftPath(document));
            _timer ??= new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(TimeSpan.FromSeconds(1.5), Timeout.InfiniteTimeSpan);
        }
    }

    public void Clear(DocumentSnapshot document)
    {
        try
        {
            var path = GetDraftPath(document);
            lock (_sync)
            {
                if (_pendingDraft?.Path == path)
                {
                    _pendingDraft = null;
                    _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    public void Flush()
    {
        AutoSaveDraft? draft;
        lock (_sync)
        {
            draft = _pendingDraft;
            _pendingDraft = null;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        if (draft is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(draft.Path)!);
            var json = JsonSerializer.Serialize(draft, AutoSaveDraftJsonContext.Default.AutoSaveDraft);
            var tempPath = draft.Path + ".tmp";
            File.WriteAllText(tempPath, json, DraftEncoding);
            File.Move(tempPath, draft.Path, true);
        }
        catch
        {
        }
    }

    private static bool DiskFileChangedAfterDraft(DocumentSnapshot document, AutoSaveDraft draft)
    {
        if (document.FilePath is not { Length: > 0 } path || !File.Exists(path) || draft.SourceLastWriteTimeUtc is null)
        {
            return false;
        }

        var currentWriteTime = new DateTimeOffset(File.GetLastWriteTimeUtc(path));
        return currentWriteTime > draft.SourceLastWriteTimeUtc.Value.AddSeconds(1);
    }

    private static string GetDraftPath(DocumentSnapshot document)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Vex",
            "Drafts");
        return Path.Combine(root, GetDocumentKey(document) + ".json");
    }

    private static string GetDocumentKey(DocumentSnapshot document)
    {
        var identity = document.FilePath is { Length: > 0 } path
            ? Path.GetFullPath(path)
            : "__untitled__";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}
