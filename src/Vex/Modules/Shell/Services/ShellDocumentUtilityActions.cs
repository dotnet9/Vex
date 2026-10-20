using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

namespace Vex.Modules.Shell.Services;

public sealed class ShellDocumentUtilityActions : IShellDocumentUtilityActions
{
    private readonly IMarkdownExportService _exportService;
    private readonly EditorPreferences _preferences;
    private readonly IShellDocumentWorkflowText _text;
    private readonly IShellOverlayService _overlay;

    public ShellDocumentUtilityActions(
        IMarkdownExportService exportService,
        EditorPreferences preferences,
        IShellDocumentWorkflowText text,
        IShellOverlayService overlay)
    {
        _exportService = exportService;
        _preferences = preferences;
        _text = text;
        _overlay = overlay;
    }

    public void ShowProperties(ShellDocumentInfoViewModel documentInfo)
    {
        _ = _overlay.ShowPropertiesAsync(documentInfo);
        _text.PublishPropertiesSummary(
            documentInfo.CurrentDocumentTitle,
            documentInfo.DocumentStateText,
            documentInfo.CurrentEncodingText,
            documentInfo.PropertySizeText,
            documentInfo.PropertyLocationText);
    }

    public async Task ExportAsync(DocumentSnapshot document, string markdown, string? format)
    {
        if (format?.Equals("Markdown", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportMarkdownAsync(document with { Markdown = markdown });
            if (path is not null)
            {
                _text.PublishExportedMarkdownTo(Path.GetFileName(path));
                if (_preferences.ExportOpen) OpenFileLocation(path);
            }
            return;
        }
        if (format?.Equals("HTML", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportHtmlAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishHtmlExportCanceled();
            }
            else
            {
                _text.PublishExportedHtmlTo(Path.GetFileName(path));
                if (_preferences.ExportOpen) OpenFileLocation(path);
            }

            return;
        }

        if (format?.Equals("PDF", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportPdfAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishPdfExportCanceled();
            }
            else
            {
                _text.PublishExportedPdfTo(Path.GetFileName(path));
                if (_preferences.ExportOpen) OpenFileLocation(path);
            }

            return;
        }

        if (format?.Equals("PNG", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportPngAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishPngExportCanceled();
            }
            else
            {
                _text.PublishExportedPngTo(Path.GetFileName(path));
                if (_preferences.ExportOpen) OpenFileLocation(path);
            }

            return;
        }

        if (format?.Equals("Word", StringComparison.OrdinalIgnoreCase) == true
            || format?.Equals("DOCX", StringComparison.OrdinalIgnoreCase) == true)
        {
            var path = await _exportService.ExportWordAsync(document with { Markdown = markdown });
            if (path is null)
            {
                _text.PublishExportNotImplemented(format);
            }
            else
            {
                _text.PublishExportedWordTo(Path.GetFileName(path));
                if (_preferences.ExportOpen) OpenFileLocation(path);
            }

            return;
        }

        _text.PublishExportNotImplemented(format);
    }

    public async Task CopyHtmlAsync(DocumentSnapshot document, string markdown, string? target)
    {
        var copied = await _exportService.CopyHtmlAsync(document with { Markdown = markdown }, target);
        if (copied)
        {
            _text.PublishCopiedHtmlToPlatform(target);
        }
        else
        {
            _text.PublishCopyHtmlUnavailable();
        }
    }

    public async Task PrintAsync(DocumentSnapshot document, string markdown)
    {
        var path = await _exportService.OpenHtmlPrintPreviewAsync(document with { Markdown = markdown });
        _text.PublishPrintPreviewResult(path is null);
    }


    public void WordCount(ShellDocumentInfoViewModel documentInfo)
    {
        _ = _overlay.ShowStatisticsAsync(documentInfo);
        _text.PublishStatisticsSummary(documentInfo.Statistics);
    }



    /// <summary>
    /// 按图片偏好准备资源路径；同名内容不同的图片使用新文件名，不覆盖已有素材。
    /// </summary>
    public string? CopyImageToAssets(string? documentPath, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var documentDirectory = string.IsNullOrWhiteSpace(documentPath) ? null : Path.GetDirectoryName(Path.GetFullPath(documentPath));
        var targetPath = Path.GetFullPath(imagePath);
        if (_preferences.ImageCopy && documentDirectory is not null)
        {
            var folder = string.IsNullOrWhiteSpace(_preferences.ImageFolder) ? "assets" : _preferences.ImageFolder.Trim();
            if (Path.IsPathRooted(folder)) throw new IOException("Image folder must be relative to the document.");
            var assetsDirectory = Path.GetFullPath(Path.Combine(documentDirectory, folder));
            var relativeFolder = Path.GetRelativePath(documentDirectory, assetsDirectory);
            if (relativeFolder == ".." || relativeFolder.StartsWith(".." + Path.DirectorySeparatorChar))
                throw new IOException("Image folder must remain within the document directory.");
            Directory.CreateDirectory(assetsDirectory);
            var fileName = Path.GetFileName(imagePath);
            targetPath = Path.Combine(assetsDirectory, fileName);
            var suffix = 1;
            while (File.Exists(targetPath) && !SameImage(imagePath, targetPath))
                targetPath = Path.Combine(assetsDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}-{suffix++}{Path.GetExtension(fileName)}");
            if (!File.Exists(targetPath)) File.Copy(imagePath, targetPath);
        }
        if (!_preferences.ImageRelative || documentDirectory is null) return new Uri(targetPath).AbsoluteUri;
        var relative = Path.GetRelativePath(documentDirectory, targetPath);
        if (Path.IsPathRooted(relative)) return new Uri(targetPath).AbsoluteUri;
        return string.Join('/', relative.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    }

    private static bool SameImage(string left, string right)
    {
        if (string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return true;
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
        using var source = File.OpenRead(left);
        using var target = File.OpenRead(right);
        return System.Security.Cryptography.SHA256.HashData(source).AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(target));
    }
    private static void OpenFileLocation(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
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
            Process.Start("xdg-open", $"\"{directory}\"");
        }
    }


}
