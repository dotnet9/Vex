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

        _text.PublishExportNotImplemented(format);
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
