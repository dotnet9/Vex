using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CodeWF.Toolkit.Core.FileExtensions;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class DocumentService : IDocumentService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private readonly IDocumentFileFactory _documentFileFactory;
    private readonly IAppLocalizer _localizer;
    private readonly EditorPreferences _preferences;

    public DocumentService(IDocumentFileFactory documentFileFactory, IAppLocalizer localizer, EditorPreferences preferences)
    {
        _documentFileFactory = documentFileFactory;
        _localizer = localizer;
        _preferences = preferences;
    }

    public string? LastOpenedFolderPath { get; private set; }

    public DocumentSnapshot CreateNew()
    {
        var markdown = $"# {_localizer.Get(VexL.DocumentDefaultHeading)}\n\n{_localizer.Get(VexL.DocumentDefaultBody)}\n";

        var newline = _preferences.DefaultNewline == "CRLF" ? "\r\n" : _preferences.DefaultNewline == "LF" ? "\n" : Environment.NewLine;
        markdown = markdown.Replace("\n", newline);
        return new DocumentSnapshot(null, _localizer.Get(VexL.DocumentDefaultFileName), markdown, ResolveEncoding(_preferences.DefaultEncoding), true);
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

    /// <summary>导入 HTML/PDF/Word：拾取源文件、转换为 Markdown、选择保存位置后落盘并返回快照。</summary>
    public async Task<DocumentSnapshot?> ImportAsync(string format)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var picker = new FilePickerOpenOptions
        {
            Title = _localizer.Get(VexL.MenuImport),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(format.ToUpperInvariant())
            {
                Patterns = [format switch { "PDF" => "*.pdf", "Word" => "*.docx", _ => "*.html;*.htm" }],
            }],
        };
        var sources = await owner.StorageProvider.OpenFilePickerAsync(picker);
        var sourcePath = sources.Count > 0 ? sources[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        var markdown = format switch
        {
            "PDF" => DocumentImportService.FromPdf(sourcePath),
            "Word" => DocumentImportService.FromWord(sourcePath),
            _ => DocumentImportService.FromHtml(await File.ReadAllTextAsync(sourcePath)),
        };
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new InvalidDataException(_localizer.Get(VexL.ImportEmptyResult));
        }

        var saved = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _localizer.Get(VexL.ImportMarkdownTitle),
            SuggestedFileName = Path.GetFileNameWithoutExtension(sourcePath),
            DefaultExtension = "md",
            FileTypeChoices = CreateMarkdownFileTypes(),
        });
        var target = saved?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        await File.WriteAllTextAsync(target, markdown, Utf8NoBom);
        return await OpenPathAsync(target);
    }

    public async Task<DocumentSnapshot> OpenPathAsync(string path, string? encodingName = null)
    {
        var encoding = ResolveEncoding(encodingName);
        using var reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true);
        var markdown = await reader.ReadToEndAsync();
        return new DocumentSnapshot(path, Path.GetFileName(path), markdown, reader.CurrentEncoding, false);
    }

    public async Task<DocumentSnapshot> ReloadAsync(DocumentSnapshot document)
    {
        if (document.FilePath is not { Length: > 0 } path)
        {
            throw new InvalidOperationException(_localizer.Get(VexL.DocumentDetailFilePathMissing));
        }

        var markdown = await FileHelper.SafeReadAllTextAsync(path, document.Encoding);
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

    // 逐路径写入锁：同一文档的保存/另存串行，避免并发保存交叉写坏内容。
    private static readonly Dictionary<string, SemaphoreSlim> WriteLocks = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object WriteLocksGate = new();

    private static SemaphoreSlim WriteLock(string path)
    {
        lock (WriteLocksGate)
        {
            if (!WriteLocks.TryGetValue(path, out var semaphore))
            {
                semaphore = new SemaphoreSlim(1, 1);
                WriteLocks[path] = semaphore;
            }
            return semaphore;
        }
    }

    public async Task<DocumentSnapshot?> SaveAsync(DocumentSnapshot document)
    {
        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return await SaveAsAsync(document);
        }

        var semaphore = WriteLock(document.FilePath);
        await semaphore.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(document.FilePath, document.Markdown, document.Encoding);
        }
        finally
        {
            semaphore.Release();
        }
        return document with { IsNew = false };
    }

    public async Task<bool> TryAutoSaveAsync(DocumentSnapshot document, string lastSavedMarkdown)
    {
        if (document.FilePath is not { } path || !File.Exists(path)) return false;
        // 校验和写入持有同一文件句柄；Windows 上禁止其他写入及替换，避免校验后覆盖外部修改。
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, useAsync: true);
        using (var reader = new StreamReader(stream, document.Encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
            if (await reader.ReadToEndAsync() != lastSavedMarkdown) return false;
        var preamble = document.Encoding.GetPreamble();
        var bytes = document.Encoding.GetBytes(document.Markdown);
        stream.Position = 0;
        await stream.WriteAsync(preamble);
        await stream.WriteAsync(bytes);
        stream.SetLength(stream.Position);
        await stream.FlushAsync();
        return true;
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

        var saveLock = WriteLock(path);
        await saveLock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(path, document.Markdown, document.Encoding);
        }
        finally
        {
            saveLock.Release();
        }
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
        if (path.Equals(targetPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
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
