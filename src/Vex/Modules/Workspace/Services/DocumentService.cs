using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class DocumentService : IDocumentService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public DocumentSnapshot CreateNew()
    {
        const string markdown = """
            # Untitled

            极简之力，妙笔成章。
            """;

        return new DocumentSnapshot(null, "Untitled.md", markdown, Utf8NoBom, true);
    }

    public async Task<DocumentSnapshot?> OpenAsync()
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown",
            AllowMultiple = false,
            FileTypeFilter = MarkdownFileTypes
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        return !string.IsNullOrWhiteSpace(path) ? await OpenPathAsync(path) : null;
    }

    public async Task<DocumentSnapshot> OpenPathAsync(string path, string? encodingName = null)
    {
        var encoding = ResolveEncoding(encodingName);
        var markdown = await File.ReadAllTextAsync(path, encoding);
        return new DocumentSnapshot(path, Path.GetFileName(path), markdown, encoding, false);
    }

    public async Task<IReadOnlyList<DocumentFile>> OpenFolderAsync()
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return [];
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open Folder",
            AllowMultiple = false
        });

        var folder = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        return string.IsNullOrWhiteSpace(folder)
            ? []
            : await OpenFolderPathAsync(folder);
    }

    public Task<IReadOnlyList<DocumentFile>> OpenFolderPathAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return Task.FromResult<IReadOnlyList<DocumentFile>>([]);
        }

        IReadOnlyList<DocumentFile> files = Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase)
                           || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(300)
            .Select(path => new DocumentFile(path, folder))
            .ToList();
        return Task.FromResult(files);
    }

    public async Task<DocumentSnapshot?> SaveAsync(DocumentSnapshot document)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return await SaveAsAsync(document);
        }

        await File.WriteAllTextAsync(document.FilePath, document.Markdown, document.Encoding);
        return document with { IsNew = false };
    }

    public async Task<DocumentSnapshot?> SaveAsAsync(DocumentSnapshot document)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Markdown",
            SuggestedFileName = document.FileName,
            DefaultExtension = "md",
            FileTypeChoices = MarkdownFileTypes
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        await File.WriteAllTextAsync(path, document.Markdown, document.Encoding);
        return document with
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            IsNew = false
        };
    }

    public Task DeleteAsync(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task OpenFileLocationAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.CompletedTask;
        }

        var target = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
        {
            return Task.CompletedTask;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-R \"{path}\"");
        }
        else
        {
            Process.Start("xdg-open", $"\"{target}\"");
        }

        return Task.CompletedTask;
    }

    private static Encoding ResolveEncoding(string? encodingName)
    {
        if (string.IsNullOrWhiteSpace(encodingName) || encodingName.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
        {
            return Utf8NoBom;
        }

        if (encodingName.Equals("utf-8-bom", StringComparison.OrdinalIgnoreCase))
        {
            return new UTF8Encoding(true);
        }

        return Encoding.GetEncoding(encodingName);
    }

    private static IReadOnlyList<FilePickerFileType> MarkdownFileTypes { get; } =
    [
        new("Markdown")
        {
            Patterns = ["*.md", "*.markdown", "*.mdown"]
        },
        new("Text")
        {
            Patterns = ["*.txt"]
        },
        FilePickerFileTypes.All
    ];

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
