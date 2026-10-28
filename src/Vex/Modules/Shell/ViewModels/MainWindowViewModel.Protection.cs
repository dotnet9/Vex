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
        await RequestUnsavedConfirmationAsync(() =>
        {
            // 独立未保存文档，来源路径绝不作为恢复文档的保存目标。
            // 「.恢复」这一段是显示与另存为默认名的一部分，随界面语言走，所以取资源里的整条文件名格式。
            ApplyDocument(new DocumentSnapshot(null, _localizer.Format(VexL.RecoveryFileName, Path.GetFileNameWithoutExtension(draft.FileName)),
                draft.Markdown, new UTF8Encoding(false), true), restoreDraft: false);
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
        if (Protection.External is not { } incoming) return;
        var path = _document.FilePath;
        var choice = await _overlays.ShowDocumentReviewAsync(new ShellDocumentReviewModel(_localizer, false,
            incoming.FilePath ?? incoming.FileName, Markdown, incoming.Markdown,
            async () => { await _documentService.SaveAsAsync(_document with { Markdown = Markdown }); }));
        if (choice == DocumentReviewChoice.Cancel || !ReferenceEquals(Protection.External, incoming) || _document.FilePath != path) return;
        Protection.External = null;
        if (choice == DocumentReviewChoice.Keep) return;
        _document = incoming;
        _lastSavedMarkdown = incoming.Markdown;
        // 通过统一内容通道载入，保留已有历史，撤销可找回载入前的内容。
        Markdown = incoming.Markdown;
        RefreshDocumentInfo();
        RefreshDocumentFileInList(path!);
    }
}
