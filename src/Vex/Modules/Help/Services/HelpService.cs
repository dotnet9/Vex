using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Vex.Core.Services;
using Vex.Modules.Help.Views;

namespace Vex.Modules.Help.Services;

public sealed class HelpService : IHelpService
{
    private static readonly string DocumentsFolder = Path.Combine(AppContext.BaseDirectory, "docs");
    private readonly IEditorAppearanceState _appearanceState;
    private readonly IAppLocalizer _localizer;

    public HelpService(IEditorAppearanceState appearanceState, IAppLocalizer localizer)
    {
        _appearanceState = appearanceState;
        _localizer = localizer;
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

    public Task OpenLocalizedDocumentAsync(string documentName, string cultureName)
    {
        return OpenDocumentAsync(ResolveLocalizedDocumentName(documentName, cultureName));
    }

    public Task ShowDocumentWindowAsync(string title, string fileName)
    {
        var markdown = ReadDocument(fileName);
        ShowWindow(new MarkdownDocumentWindow(
            title,
            markdown,
            _appearanceState.TypographyTheme,
            _appearanceState.TypographySize));
        return Task.CompletedTask;
    }

    public Task ShowLocalizedDocumentWindowAsync(string title, string documentName, string cultureName)
    {
        return ShowDocumentWindowAsync(title, ResolveLocalizedDocumentName(documentName, cultureName));
    }

    public Task ShowAboutWindowAsync()
    {
        ShowWindow(new AboutWindow());
        return Task.CompletedTask;
    }

    private static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    private string ReadDocument(string fileName)
    {
        var path = Path.Combine(DocumentsFolder, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(_localizer.Get(VexL.HelpDetailDocumentNotFound), path);
        }

        return File.ReadAllText(path);
    }

    private static void ShowWindow(Window window)
    {
        if (GetMainWindow() is { } owner)
        {
            window.Show(owner);
            return;
        }

        window.Show();
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }

    private static string ResolveLocalizedDocumentName(string documentName, string cultureName)
    {
        foreach (var candidate in EnumerateDocumentCandidates(documentName, cultureName))
        {
            if (File.Exists(Path.Combine(DocumentsFolder, candidate)))
            {
                return candidate;
            }
        }

        return $"{documentName}.zh-CN.md";
    }

    private static IEnumerable<string> EnumerateDocumentCandidates(string documentName, string cultureName)
    {
        var isChineseCulture = false;
        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            yield return $"{documentName}.{cultureName}.md";
            var dashIndex = cultureName.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                yield return $"{documentName}.{cultureName[..dashIndex]}.md";
            }

            isChineseCulture = cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        }

        if (!isChineseCulture)
        {
            yield return $"{documentName}.en-US.md";
        }

        yield return $"{documentName}.zh-CN.md";
    }
}
