using System.Diagnostics;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Help.Services;

public sealed class HelpService : IHelpService
{
    private readonly IAppLocalizer _localizer;
    private readonly IShellOverlayService _overlay;
    private readonly IDocumentWindowLauncher _windows;

    public HelpService(
        IAppLocalizer localizer,
        IShellOverlayService overlay,
        IDocumentWindowLauncher windows)
    {
        _localizer = localizer;
        _overlay = overlay;
        _windows = windows;
    }

    public Task OpenWebsiteAsync()
    {
        Open(ProjectLinks.Website);
        return Task.CompletedTask;
    }

    public Task OpenFeedbackAsync()
    {
        Open("https://github.com/dotnet9/Vex/issues");
        return Task.CompletedTask;
    }

    public Task OpenDocumentAsync(string fileName)
    {
        var path = Path.Combine(BundledDocuments.Folder, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(_localizer.Get(VexL.HelpDetailDocumentNotFound), path);
        }

        _windows.Open(path);
        return Task.CompletedTask;
    }

    public Task ShowAboutWindowAsync() => _overlay.ShowAboutAsync();

    public Task ShowUpdatesAsync() => _overlay.ShowUpdatesAsync();

    private static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

}
