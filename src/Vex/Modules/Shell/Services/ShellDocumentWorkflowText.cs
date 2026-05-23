using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Services;

// 主窗口文档流程的状态栏和确认框文案集中在这里，避免 MainWindowViewModel 因多语言格式化继续膨胀。
public sealed class ShellDocumentWorkflowText : IShellDocumentWorkflowText
{
    private readonly IAppLocalizer _localizer;
    private readonly IShellStatusPublisher _statusPublisher;

    public ShellDocumentWorkflowText(IAppLocalizer localizer, IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
        _statusPublisher = statusPublisher;
    }

    public string UnsavedDocumentFallback => Text(VexL.UnsavedDocument);

    public string TitleSaveChanges => Text(VexL.UnsavedTitleSaveChanges);

    public string TitleBeforeOpeningFolder => Text(VexL.UnsavedTitleBeforeOpeningFolder);

    public string TitleBeforeOpening => Text(VexL.UnsavedTitleBeforeOpening);

    public string TitleBeforeSwitchingFiles => Text(VexL.UnsavedTitleBeforeSwitchingFiles);

    public string TitleBeforeDeleting => Text(VexL.UnsavedTitleBeforeDeleting);

    public string TitleBeforeReopening => Text(VexL.UnsavedTitleBeforeReopening);

    public string TitleBeforeOpeningRecent => Text(VexL.UnsavedTitleBeforeOpeningRecent);

    public string TitleBeforeClosingVex => Text(VexL.UnsavedTitleBeforeClosingVex);

    public string BeforeOpeningDroppedFolder(string documentName) =>
        Format(VexL.UnsavedMessageBeforeOpeningDroppedFolderFormat, documentName);

    public string BeforeOpeningFile(string documentName, string fileName) =>
        Format(VexL.UnsavedMessageBeforeOpeningFileFormat, documentName, fileName);

    public string BeforeNewDocument(string documentName) =>
        Format(VexL.UnsavedMessageBeforeNewDocumentFormat, documentName);

    public string BeforeClosingDocument(string documentName) =>
        Format(VexL.UnsavedMessageBeforeClosingDocumentFormat, documentName);

    public string BeforeOpeningAnotherFile(string documentName) =>
        Format(VexL.UnsavedMessageBeforeOpeningAnotherFileFormat, documentName);

    public string BeforeOpeningFolder(string documentName) =>
        Format(VexL.UnsavedMessageBeforeOpeningFolderFormat, documentName);

    public string BeforeSwitchingFile(string documentName, string fileName) =>
        Format(VexL.UnsavedMessageBeforeSwitchingFileFormat, documentName, fileName);

    public string BeforeDeleting(string documentName) =>
        Format(VexL.UnsavedMessageBeforeDeletingFormat, documentName);

    public string BeforeReopeningWithEncoding(string documentName, string encodingName) =>
        Format(VexL.UnsavedMessageBeforeReopeningWithEncodingFormat, documentName, encodingName);

    public string BeforeOpeningRecent(string documentName, string recentName) =>
        Format(VexL.UnsavedMessageBeforeOpeningRecentFormat, documentName, recentName);

    public string BeforeClosingVex(string documentName) =>
        Format(VexL.UnsavedMessageBeforeClosingVexFormat, documentName);

    public void PublishDroppedItemUnavailable() => Publish(VexL.StatusDroppedItemUnavailable);

    public void PublishDropMarkdownOrTextFile() => Publish(VexL.StatusDropMarkdownOrTextFile);

    public void PublishNewDocumentCreated() => Publish(VexL.StatusNewDocumentCreated);

    public void PublishDocumentClosed() => Publish(VexL.StatusDocumentClosed);

    public void PublishChooseDocumentFromLoadedFolder() => Publish(VexL.StatusChooseDocumentFromLoadedFolder);

    public void PublishLoadedMarkdownFiles(int count)
    {
        if (count == 0)
        {
            Publish(VexL.StatusNoMarkdownFilesLoaded);
            return;
        }

        PublishFormat(VexL.StatusLoadedMarkdownFilesFormat, count);
    }

    public void PublishSaved(string fileName) => PublishFormat(VexL.StatusSavedFileFormat, fileName);

    public void PublishSavedAs(string fileName) => PublishFormat(VexL.StatusSavedAsFileFormat, fileName);

    public void PublishSaveAllResult(bool isStillModified)
    {
        Publish(isStillModified ? VexL.StatusSaveAllCanceledModified : VexL.StatusSavedCurrentDocumentNoMultiSave);
    }

    public void PublishFileDeleted() => Publish(VexL.StatusFileDeleted);

    public void PublishRenamedFile(string fileName) => PublishFormat(VexL.StatusRenamedFileFormat, fileName);

    public void PublishOpenFileBeforeEncoding() => Publish(VexL.StatusOpenFileBeforeEncoding);

    public void PublishReopenedWithEncoding(string encodingName) =>
        PublishFormat(VexL.StatusReopenedWithEncodingFormat, encodingName);

    public void PublishOpened(string fileName) => PublishFormat(VexL.StatusOpenedFileFormat, fileName);

    public void PublishExternalFileReloaded(string fileName) => PublishFormat(VexL.StatusExternalFileReloadedFormat, fileName);

    public void PublishPropertiesSummary(
        string title,
        string state,
        string encoding,
        string size,
        string location)
    {
        PublishFormat(VexL.StatusPropertiesSummaryFormat, title, state, encoding, size, location);
    }

    public void PublishHtmlExportCanceled() => Publish(VexL.StatusHtmlExportCanceled);

    public void PublishExportedHtmlTo(string fileName) => PublishFormat(VexL.StatusExportedHtmlToFormat, fileName);

    public void PublishPngExportCanceled() => Publish(VexL.StatusPngExportCanceled);

    public void PublishExportedPngTo(string fileName) => PublishFormat(VexL.StatusExportedPngToFormat, fileName);

    public void PublishExportNotImplemented(string? format)
    {
        var displayFormat = string.IsNullOrWhiteSpace(format)
            ? Text(VexL.ExportFormatDocument)
            : format.ToUpperInvariant();
        PublishFormat(VexL.StatusExportNotImplementedFormat, displayFormat);
    }

    public void PublishPrintPreviewResult(bool isCanceled)
    {
        Publish(isCanceled ? VexL.StatusPrintPreviewCanceled : VexL.StatusOpenedHtmlPrintPreview);
    }

    public void PublishStatisticsSummary(MarkdownStatistics statistics)
    {
        PublishFormat(
            VexL.StatusStatisticsSummaryFormat,
            statistics.Words,
            statistics.Characters,
            statistics.Lines,
            statistics.ReadingMinutes);
    }

    public void PublishRecentFileUnavailable() => Publish(VexL.StatusRecentFileUnavailable);

    public void PublishRecentFileRemovedMissing() => Publish(VexL.StatusRecentFileRemovedMissing);

    public void PublishSaveCanceledActionIncomplete() => Publish(VexL.StatusSaveCanceledActionIncomplete);

    private string Text(string key) => _localizer.Get(key);

    private string Format(string key, params object?[] args) => _localizer.Format(key, args);

    private void Publish(string key) => _statusPublisher.PublishResource(key);

    private void PublishFormat(string key, params object?[] args) => _statusPublisher.PublishResourceFormat(key, args);
}
