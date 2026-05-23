using Vex.Core.Models;

namespace Vex.Modules.Shell.Services;

public interface IShellDocumentWorkflowText
{
    string UnsavedDocumentFallback { get; }

    string TitleSaveChanges { get; }

    string TitleBeforeOpeningFolder { get; }

    string TitleBeforeOpening { get; }

    string TitleBeforeSwitchingFiles { get; }

    string TitleBeforeDeleting { get; }

    string TitleBeforeReopening { get; }

    string TitleBeforeOpeningRecent { get; }

    string TitleBeforeClosingVex { get; }

    string BeforeOpeningDroppedFolder(string documentName);

    string BeforeOpeningFile(string documentName, string fileName);

    string BeforeNewDocument(string documentName);

    string BeforeClosingDocument(string documentName);

    string BeforeOpeningAnotherFile(string documentName);

    string BeforeOpeningFolder(string documentName);

    string BeforeSwitchingFile(string documentName, string fileName);

    string BeforeDeleting(string documentName);

    string BeforeReopeningWithEncoding(string documentName, string encodingName);

    string BeforeOpeningRecent(string documentName, string recentName);

    string BeforeClosingVex(string documentName);

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

    void PublishPngExportCanceled();

    void PublishExportedPngTo(string fileName);

    void PublishExportNotImplemented(string? format);

    void PublishPrintPreviewResult(bool isCanceled);

    void PublishStatisticsSummary(MarkdownStatistics statistics);

    void PublishRecentFileUnavailable();

    void PublishRecentFileRemovedMissing();

    void PublishSaveCanceledActionIncomplete();
}
