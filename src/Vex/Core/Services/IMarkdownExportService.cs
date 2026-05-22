using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IMarkdownExportService
{
    Task<string?> ExportHtmlAsync(DocumentSnapshot document);
}
