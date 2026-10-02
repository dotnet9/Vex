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

    public string? LastOpenedFolderPath { get; private set; }

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

    public async Task<DocumentSnapshot> ReloadAsync(DocumentSnapshot document)
    {
        if (document.FilePath is not { Length: > 0 } path)
        {
            throw new InvalidOperationException(_localizer.Get(VexL.DocumentDetailFilePathMissing));
        }

        var markdown = await File.ReadAllTextAsync(path, document.Encoding);
        return document with
        {
            FilePath = path,
            FileName = Path.GetFileName(path),
            Markdown = markdown,
            IsNew = false
        };
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
        LastOpenedFolderPath = string.IsNullOrWhiteSpace(folder) ? null : Path.GetFullPath(folder);
        return string.IsNullOrWhiteSpace(folder)
            ? []
            : await OpenFolderPathAsync(folder);
    }

    public Task<IReadOnlyList<DocumentFile>> OpenFolderPathAsync(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            LastOpenedFolderPath = null;
            return Task.FromResult<IReadOnlyList<DocumentFile>>([]);
        }

        LastOpenedFolderPath = Path.GetFullPath(folder);

        return Task.Run<IReadOnlyList<DocumentFile>>(() =>
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                ReturnSpecialDirectories = false
            };

            return Directory.EnumerateFiles(folder, "*.*", options)
                // 与文件选择器保持一致，文件夹扫描也识别常见 Markdown 扩展名。
                .Where(IsSupportedDocumentPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => _documentFileFactory.Create(path, folder))
                .ToList();
        });
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

    public Task<string> RenameAsync(string path, string newName)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            throw new FileNotFoundException(_localizer.Get(VexL.RenameDetailFileNotFound), path);
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(_localizer.Get(VexL.RenameDetailDirectoryUnavailable));
        }

        var targetName = NormalizeRenameTargetName(path, newName);
        var targetPath = Path.Combine(directory, targetName);
        if (path.Equals(targetPath, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(path);
        }

        if (File.Exists(targetPath))
        {
            throw new IOException(_localizer.Format(VexL.RenameDetailDuplicateFileFormat, targetName));
        }

        File.Move(path, targetPath);
        return Task.FromResult(targetPath);
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

    private string NormalizeRenameTargetName(string path, string newName)
    {
        var trimmed = newName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException(_localizer.Get(VexL.RenameDetailEmptyFileName), nameof(newName));
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(_localizer.Get(VexL.RenameDetailInvalidFileName), nameof(newName));
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(trimmed)))
        {
            trimmed += Path.GetExtension(path);
        }

        if (!IsSupportedExtension(Path.GetExtension(trimmed)))
        {
            throw new ArgumentException(_localizer.Get(VexL.RenameDetailUnsupportedExtension), nameof(newName));
        }

        return trimmed;
    }

    private static bool IsSupportedExtension(string extension)
    {
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".mdown", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);
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
