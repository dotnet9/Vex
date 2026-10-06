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
    private readonly IShellDocumentWorkflowText _text;
    private readonly IShellOverlayService _overlay;

    public ShellDocumentUtilityActions(
        IMarkdownExportService exportService,
        IShellDocumentWorkflowText text,
        IShellOverlayService overlay)
    {
        _exportService = exportService;
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
                OpenFileLocation(path);
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
                OpenFileLocation(path);
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
                OpenFileLocation(path);
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
                OpenFileLocation(path);
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
    /// 把拖入的本地图片复制到文档所在目录的 assets/ 下，返回插入 Markdown 的相对路径；
    /// 同名文件已存在时复用，不覆盖用户已有素材。
    /// </summary>
    public string? CopyImageToAssets(string? documentPath, string imagePath)
    {
        if (string.IsNullOrWhiteSpace(documentPath) || string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return null;
        }

        var documentDirectory = Path.GetDirectoryName(documentPath);
        if (string.IsNullOrWhiteSpace(documentDirectory))
        {
            return null;
        }

        var assetsDirectory = Path.Combine(documentDirectory, "assets");
        Directory.CreateDirectory(assetsDirectory);

        var fileName = Path.GetFileName(imagePath);
        var targetPath = Path.Combine(assetsDirectory, fileName);
        if (!File.Exists(targetPath))
        {
            File.Copy(imagePath, targetPath);
        }

        // 无论来源在哪，源码里统一写 assets/<文件名> 相对路径。
        return "assets/" + fileName;
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
