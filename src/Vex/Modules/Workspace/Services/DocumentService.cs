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
    private readonly IDocumentFileFactory _documentFileFactory;
    private readonly IAppLocalizer _localizer;

    public DocumentService(IDocumentFileFactory documentFileFactory, IAppLocalizer localizer)
    {
        _documentFileFactory = documentFileFactory;
        _localizer = localizer;
    }

    public DocumentSnapshot CreateNew()
    {
        var markdown = $"# {_localizer.Get(VexL.DocumentDefaultHeading)}\n\n{_localizer.Get(VexL.DocumentDefaultBody)}\n";

        return new DocumentSnapshot(null, _localizer.Get(VexL.DocumentDefaultFileName), markdown, Utf8NoBom, true);
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
            Title = _localizer.Get(VexL.DialogOpenMarkdownTitle),
            AllowMultiple = false,
            FileTypeFilter = CreateMarkdownFileTypes()
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
            Title = _localizer.Get(VexL.DialogOpenFolderTitle),
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
            // 与文件选择器保持一致，文件夹扫描也识别常见 Markdown 扩展名。
            .Where(IsSupportedDocumentPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(300)
            .Select(path => _documentFileFactory.Create(path, folder))
            .ToList();
        return Task.FromResult(files);
    }

    public bool IsSupportedDocumentPath(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
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
            Title = _localizer.Get(VexL.DialogSaveMarkdownTitle),
            SuggestedFileName = document.FileName,
            DefaultExtension = "md",
            FileTypeChoices = CreateMarkdownFileTypes()
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

    private IReadOnlyList<FilePickerFileType> CreateMarkdownFileTypes()
    {
        return
        [
            new(_localizer.Get(VexL.FileTypeMarkdown))
            {
                Patterns = ["*.md", "*.markdown", "*.mdown"]
            },
            new(_localizer.Get(VexL.FileTypeText))
            {
                Patterns = ["*.txt"]
            },
            FilePickerFileTypes.All
        ];
    }

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
