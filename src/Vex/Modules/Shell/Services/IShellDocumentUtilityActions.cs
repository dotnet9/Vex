using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public interface IShellDocumentUtilityActions
{
    void ShowProperties(ShellDialogsViewModel dialogs, ShellDocumentInfoViewModel documentInfo);

    Task ExportAsync(DocumentSnapshot document, string markdown, string? format);

    Task PrintAsync(DocumentSnapshot document, string markdown);

    void WordCount(ShellDialogsViewModel dialogs, MarkdownStatistics statistics);
}
