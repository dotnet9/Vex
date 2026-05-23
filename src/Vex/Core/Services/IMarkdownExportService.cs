using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IMarkdownExportService
{
    Task<string?> ExportHtmlAsync(DocumentSnapshot document);

    Task<string?> ExportPngAsync(DocumentSnapshot document);

    Task<string?> OpenHtmlPrintPreviewAsync(DocumentSnapshot document);
}
