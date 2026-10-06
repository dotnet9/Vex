using System.Diagnostics;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Help.Services;

public sealed class HelpService : IHelpService
{
    private static readonly string DocumentsFolder = Path.Combine(AppContext.BaseDirectory, "docs");
    private readonly IEditorAppearanceState _appearanceState;
    private readonly IAppLocalizer _localizer;
    private readonly IShellOverlayService _overlay;

    public HelpService(
        IEditorAppearanceState appearanceState,
        IAppLocalizer localizer,
        IShellOverlayService overlay)
    {
        _appearanceState = appearanceState;
        _localizer = localizer;
        _overlay = overlay;
    }

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
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(_localizer.Get(VexL.HelpDetailDocumentNotFound), path);
        }

        Open(path);
        return Task.CompletedTask;
    }

    public Task ShowDocumentWindowAsync(string title, string fileName)
    {
        var path = GetDocumentPath(fileName);
        var markdown = File.ReadAllText(path);
        return _overlay.ShowDocumentAsync(
            title,
            markdown,
            path,
            _appearanceState.TypographyTheme,
            _appearanceState.TypographySize);
    }

    public Task ShowAboutWindowAsync() => _overlay.ShowAboutAsync();

    private static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    private string GetDocumentPath(string fileName)
    {
        var path = Path.Combine(DocumentsFolder, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(_localizer.Get(VexL.HelpDetailDocumentNotFound), path);
        }

        return path;
    }




}
