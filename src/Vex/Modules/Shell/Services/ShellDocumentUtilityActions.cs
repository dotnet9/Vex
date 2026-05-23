using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public sealed class ShellDocumentUtilityActions : IShellDocumentUtilityActions
{
    private readonly IMarkdownExportService _exportService;
    private readonly IShellDocumentWorkflowText _text;

    public ShellDocumentUtilityActions(IMarkdownExportService exportService, IShellDocumentWorkflowText text)
    {
        _exportService = exportService;
        _text = text;
    }

    public void ShowProperties(ShellDialogsViewModel dialogs, ShellDocumentInfoViewModel documentInfo)
    {
        dialogs.ShowPropertiesPanel();
        _text.PublishPropertiesSummary(
            documentInfo.CurrentDocumentTitle,
            documentInfo.DocumentStateText,
            documentInfo.CurrentEncodingText,
            documentInfo.PropertySizeText,
            documentInfo.PropertyLocationText);
    }

    public async Task ExportAsync(DocumentSnapshot document, string markdown, string? format)
    {
        if (format?.Equals("HTML", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportHtmlAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishHtmlExportCanceled();
            }
            else
            {
                _text.PublishExportedHtmlTo(Path.GetFileName(path));
            }

            return;
        }

        if (format?.Equals("PDF", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportPdfAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishPdfExportCanceled();
            }
            else
            {
                _text.PublishExportedPdfTo(Path.GetFileName(path));
            }

            return;
        }

        if (format?.Equals("PNG", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportPngAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishPngExportCanceled();
            }
            else
            {
                _text.PublishExportedPngTo(Path.GetFileName(path));
            }

            return;
        }

        _text.PublishExportNotImplemented(format);
    }

    public async Task CopyHtmlAsync(DocumentSnapshot document, string markdown, string? target)
    {
        var copied = await _exportService.CopyHtmlAsync(document with { Markdown = markdown }, target);
        if (copied)
        {
            _text.PublishCopiedHtmlToPlatform(target);
        }
        else
        {
            _text.PublishCopyHtmlUnavailable();
        }
    }

    public async Task PrintAsync(DocumentSnapshot document, string markdown)
    {
        var path = await _exportService.OpenHtmlPrintPreviewAsync(document with { Markdown = markdown });
        _text.PublishPrintPreviewResult(path is null);
    }

    public void WordCount(ShellDialogsViewModel dialogs, MarkdownStatistics statistics)
    {
        dialogs.ShowStatisticsPanel();
        _text.PublishStatisticsSummary(statistics);
    }
}
