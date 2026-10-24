using Vex.Core.Models;

namespace Vex.Modules.Shell.Services;

public interface IShellDocumentWorkflowText
{
    void PublishDroppedItemUnavailable();

    void PublishDropMarkdownOrTextFile();

    void PublishNewDocumentCreated();

    void PublishDocumentClosed();

    void PublishChooseDocumentFromLoadedFolder();

    void PublishLoadedMarkdownFiles(int count);

    void PublishSaved(string fileName);

    void PublishSavedAs(string fileName);

    void PublishSaveAllResult(bool isStillModified);

    void PublishFileDeleted();

    void PublishRenamedFile(string fileName);

    void PublishOpenFileBeforeEncoding();

    void PublishReopenedWithEncoding(string encodingName);

    void PublishOpened(string fileName);

    void PublishExternalFileReloaded(string fileName);

    void PublishPropertiesSummary(
        string title,
        string state,
        string encoding,
        string size,
        string location);

    void PublishHtmlExportCanceled();

    void PublishExportedHtmlTo(string fileName);

    void PublishExportedMarkdownTo(string fileName);

    void PublishPdfExportCanceled();

    void PublishExportedPdfTo(string fileName);

    void PublishPngExportCanceled();

    void PublishExportedPngTo(string fileName);

    void PublishExportedWordTo(string fileName);

    void PublishExportNotImplemented(string? format);

    string CopyTargetName(string? target);

    void PublishCopiedHtmlToPlatform(string? target);

    void PublishCopyHtmlUnavailable();

    void PublishPrintPreviewResult(bool isCanceled);

    void PublishStatisticsSummary(MarkdownStatistics statistics);

    void PublishRecentFileUnavailable();

    void PublishRecentFileRemovedMissing();

    void PublishSaveCanceledActionIncomplete();
}
