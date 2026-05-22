using System.Diagnostics;
using Vex.Core.Services;

namespace Vex.Modules.Help.Services;

public sealed class HelpService : IHelpService
{
    private static readonly string DocumentsFolder = Path.Combine(AppContext.BaseDirectory, "docs");

    public Task OpenWebsiteAsync()
    {
        Open("https://codewf.com");
        return Task.CompletedTask;
    }

    public Task OpenFeedbackAsync()
    {
        Open("https://github.com/dotnet9/Vex/issues");
        return Task.CompletedTask;
    }

    public Task OpenDocumentAsync(string fileName)
    {
        var path = Path.Combine(DocumentsFolder, fileName);
        if (File.Exists(path))
        {
            Open(path);
        }

        return Task.CompletedTask;
    }

    private static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }
}
