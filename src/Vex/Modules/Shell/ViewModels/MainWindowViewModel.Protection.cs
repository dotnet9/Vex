using System.Text;
using Vex.Core.Models;
using Vex.Modules.Shell.Views;

namespace Vex.Modules.Shell.ViewModels;

public sealed partial class MainWindowViewModel
{
    private DocumentSnapshot? _recoverySource;
    private void DiscoverRecoveryDraft(DocumentSnapshot document)
    {
        Protection.Recovery = _settingsStore.Current.RecoveryDrafts == false ? null : _drafts.TryRestore(document);
        Protection.DraftError = _drafts.LastError;
    }

    public async Task ReviewRecoveryAsync()
    {
        if (Protection.Recovery is not { } draft) return;
        var source = _document;
        var choice = await _overlays.ShowDocumentReviewAsync(new ShellDocumentReviewModel(_localizer, true,
            draft.FilePath ?? draft.FileName, Markdown, draft.Markdown));
        if (choice != DocumentReviewChoice.Load || !ReferenceEquals(Protection.Recovery, draft)) return;
        await RequestUnsavedConfirmationAsync(_text.TitleSaveChanges, _text.BeforeClosingDocument(_document.FileName), () =>
        {
            // 独立未保存文档，来源路径绝不作为恢复文档的保存目标。
            ApplyDocument(new DocumentSnapshot(null, Path.GetFileNameWithoutExtension(draft.FileName) + ".恢复.md", draft.Markdown, new UTF8Encoding(false), true), restoreDraft: false);
            _recoverySource = source;
            _lastSavedMarkdown = string.Empty;
            Protection.Recovery = null;
            RefreshDocumentInfo();
            _drafts.QueueSave(_document, Markdown, _lastSavedMarkdown);
            _drafts.Flush();
            return Task.CompletedTask;
        });
    }

    private void CompleteRecoveredSave()
    {
        if (_recoverySource is not { } source) return;
        _drafts.ClearRestored(source);
        _recoverySource = null;
    }

    public void RetryDraft() { _drafts.Flush(); Protection.DraftError = _drafts.LastError; }

    public async Task CompareExternalAsync()
    {
        while (Protection.External is { } incoming)
        {
            CommitPendingEditsRequested?.Invoke(this, EventArgs.Empty);
            var path = _document.FilePath;
            var documentVersion = _documentOpenVersion;
            var editRevision = _documentVersion;
            var candidateRevision = _externalCandidateRevision;
            var eventRevision = Interlocked.Read(ref _fileEventRevision);
            var choice = await _overlays.ShowDocumentReviewAsync(new ShellDocumentReviewModel(_localizer, false,
                incoming.FilePath ?? incoming.FileName, Markdown, incoming.Markdown,
                async () => { CommitPendingEditsRequested?.Invoke(this, EventArgs.Empty); await _documentService.SaveAsAsync(_document with { Markdown = Markdown }); }));
            if (choice == DocumentReviewChoice.Cancel || documentVersion != _documentOpenVersion || _document.FilePath != path) return;
            CommitPendingEditsRequested?.Invoke(this, EventArgs.Empty);
            // 弹窗期间输入或磁盘候选变化，旧决定失效，重新展示两个最新版本。
            if (editRevision != _documentVersion || candidateRevision != _externalCandidateRevision
                || !ReferenceEquals(Protection.External, incoming)) continue;
            if (eventRevision != Interlocked.Read(ref _fileEventRevision))
            {
                await ReloadWatchedFileAsync();
                continue;
            }
            if (Protection.HasFileReadError) return;
            Protection.External = null;
            if (choice == DocumentReviewChoice.Keep) return;
            _document = incoming;
            _lastSavedMarkdown = incoming.Markdown;
            // 通过统一内容通道载入，保留已有历史，撤销可找回载入前的内容。
            Markdown = incoming.Markdown;
            RefreshDocumentInfo();
            RefreshDocumentFileInList(path!);
            return;
        }
    }
}
