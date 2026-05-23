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
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The bundled help document was not found.", path);
        }

        Open(path);
        return Task.CompletedTask;
    }

    public Task OpenLocalizedDocumentAsync(string documentName, string cultureName)
    {
        return OpenDocumentAsync(ResolveLocalizedDocumentName(documentName, cultureName));
    }

    private static void Open(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
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
        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            yield return $"{documentName}.{cultureName}.md";
            var dashIndex = cultureName.IndexOf('-', StringComparison.Ordinal);
            if (dashIndex > 0)
            {
                yield return $"{documentName}.{cultureName[..dashIndex]}.md";
            }
        }

        yield return $"{documentName}.zh-CN.md";
    }
}
