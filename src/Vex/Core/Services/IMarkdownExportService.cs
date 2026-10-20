using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IMarkdownExportService
{
    Task<string?> ExportMarkdownAsync(DocumentSnapshot document);

    Task<string?> ExportHtmlAsync(DocumentSnapshot document);

    Task<string?> ExportPdfAsync(DocumentSnapshot document);

    Task<string?> ExportPngAsync(DocumentSnapshot document);

    Task<string?> ExportWordAsync(DocumentSnapshot document);

    Task<bool> CopyHtmlAsync(DocumentSnapshot document, string? target);

    Task<string?> OpenHtmlPrintPreviewAsync(DocumentSnapshot document);
}
