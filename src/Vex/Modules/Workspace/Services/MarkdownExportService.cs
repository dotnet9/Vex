using System.Globalization;
using System.Net;
using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Markdig;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Workspace.Services;

public sealed class MarkdownExportService : IMarkdownExportService
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();
    private static readonly MarkdownPdfRenderer PdfRenderer = new();
    private static readonly MarkdownPngRenderer PngRenderer = new();
    private static readonly DataFormat<string> HtmlMimeFormat = DataFormat.CreateStringPlatformFormat("text/html");
    private static readonly DataFormat<string> MacHtmlFormat = DataFormat.CreateStringPlatformFormat("public.html");
    private static readonly DataFormat<string> WindowsHtmlFormat = DataFormat.CreateStringPlatformFormat("HTML Format");
    private readonly IAppLocalizer _localizer;

    public MarkdownExportService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<string?> ExportHtmlAsync(DocumentSnapshot document)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _localizer.Get(VexL.DialogExportHtmlTitle),
            SuggestedFileName = Path.ChangeExtension(document.FileName, ".html"),
            DefaultExtension = "html",
            FileTypeChoices = CreateHtmlFileTypes()
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        await File.WriteAllTextAsync(path, BuildHtml(document), Utf8NoBom);
        return path;
    }

    public async Task<string?> ExportPdfAsync(DocumentSnapshot document)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _localizer.Get(VexL.DialogExportPdfTitle),
            SuggestedFileName = Path.ChangeExtension(document.FileName, ".pdf"),
            DefaultExtension = "pdf",
            FileTypeChoices = CreatePdfFileTypes()
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        PdfRenderer.Render(document, path);
        return path;
    }

    public async Task<string?> ExportPngAsync(DocumentSnapshot document)
    {
        var owner = GetMainWindow();
        if (owner is null)
        {
            return null;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = _localizer.Get(VexL.DialogExportPngTitle),
            SuggestedFileName = Path.ChangeExtension(document.FileName, ".png"),
            DefaultExtension = "png",
            FileTypeChoices = CreatePngFileTypes()
        });

        var path = file?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        using var bitmap = PngRenderer.Render(document);
        bitmap.Save(path);
        return path;
    }

    public async Task<bool> CopyHtmlAsync(DocumentSnapshot document, string? target)
    {
        var clipboard = GetMainWindow()?.Clipboard;
        if (clipboard is null)
        {
            return false;
        }

        var html = BuildHtml(document, HtmlDocumentMode.Export, target, includeFragmentMarkers: true);
        var transfer = CreateHtmlClipboardData(document.Markdown ?? string.Empty, html);
        await clipboard.SetDataAsync(transfer);
        return true;
    }

    public async Task<string?> OpenHtmlPrintPreviewAsync(DocumentSnapshot document)
    {
        var folder = Path.Combine(Path.GetTempPath(), "Vex", "PrintPreview");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(document.FileName)}-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(path, BuildHtml(document, HtmlDocumentMode.PrintPreview), Utf8NoBom);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return path;
    }

    private string BuildHtml(
        DocumentSnapshot document,
        HtmlDocumentMode mode = HtmlDocumentMode.Export,
        string? target = null,
        bool includeFragmentMarkers = false)
    {
        var title = WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(document.FileName));
        var body = Markdig.Markdown.ToHtml(document.Markdown ?? string.Empty, Pipeline);
        var language = WebUtility.HtmlEncode(_localizer.Culture.Name);
        var layout = ResolveSocialLayout(target);
        var targetName = WebUtility.HtmlEncode(layout.TargetName);
        var printStyles = mode == HtmlDocumentMode.PrintPreview ? PrintPreviewStyles : string.Empty;
        var printScript = mode == HtmlDocumentMode.PrintPreview ? PrintPreviewScript : string.Empty;
        var startFragment = includeFragmentMarkers ? "              <!--StartFragment-->" : string.Empty;
        var endFragment = includeFragmentMarkers ? "              <!--EndFragment-->" : string.Empty;
        return $$"""
            <!doctype html>
            <html lang="{{language}}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="vex-copy-target" content="{{targetName}}">
              <title>{{title}}</title>
              <style>
                body { margin: 0; color: #1f2937; background: #ffffff; font-family: "Inter", "Microsoft YaHei UI", "Segoe UI", sans-serif; line-height: 1.72; }
                article { max-width: {{layout.MaxWidth}}px; margin: 0 auto; padding: {{layout.PaddingTop}}px {{layout.PaddingX}}px {{layout.PaddingBottom}}px; }
                h1, h2, h3, h4, h5, h6 { color: #111827; line-height: 1.28; margin: 1.35em 0 .55em; }
                h1 { font-size: 2.1rem; }
                h2 { font-size: 1.65rem; border-bottom: 1px solid #e5e7eb; padding-bottom: .25em; }
                p, ul, ol, blockquote, pre, table { margin: 0 0 1em; }
                code { background: #f3f4f6; border-radius: 4px; padding: .12em .32em; font-family: "Cascadia Mono", Consolas, monospace; }
                pre { overflow: auto; background: #111827; color: #f9fafb; border-radius: 8px; padding: 16px; }
                pre code { background: transparent; color: inherit; padding: 0; }
                blockquote { border-left: 4px solid #d1d5db; color: #4b5563; padding-left: 16px; }
                table { border-collapse: collapse; width: 100%; }
                th, td { border: 1px solid #e5e7eb; padding: 8px 10px; }
                th { background: #f9fafb; }
                img { max-width: 100%; }
                {{printStyles}}
              </style>
              {{printScript}}
            </head>
            <body>
            {{startFragment}}
              <article data-vex-copy-target="{{targetName}}" style="max-width: {{layout.MaxWidth}}px; margin: 0 auto; padding: {{layout.PaddingTop}}px {{layout.PaddingX}}px {{layout.PaddingBottom}}px; color: #1f2937; background: #ffffff; font-family: Inter, 'Microsoft YaHei UI', 'Segoe UI', sans-serif; line-height: 1.72;">
            {{body}}
              </article>
            {{endFragment}}
            </body>
            </html>
            """;
    }

    private const string PrintPreviewStyles = """
                @page { margin: 16mm 18mm; }
                @media print {
                  html, body { background: #ffffff; }
                  body { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
                  article { max-width: none !important; padding: 0 !important; }
                  h1, h2, h3, h4, h5, h6 { break-after: avoid; page-break-after: avoid; }
                  pre, blockquote, table, img { break-inside: avoid; page-break-inside: avoid; }
                  table { page-break-inside: auto; }
                  thead { display: table-header-group; }
                  tr { break-inside: avoid; page-break-inside: avoid; }
                  a { color: #111827; text-decoration: underline; }
                }
    """;

    private const string PrintPreviewScript = """
              <script>
                window.addEventListener("load", () => window.setTimeout(() => window.print(), 250));
              </script>
    """;

    private static DataTransfer CreateHtmlClipboardData(string text, string html)
    {
        var item = new DataTransferItem();
        item.SetText(text);
        item.Set(HtmlMimeFormat, html);
        item.Set(MacHtmlFormat, html);
        item.Set(WindowsHtmlFormat, BuildWindowsClipboardHtml(html));

        var transfer = new DataTransfer();
        transfer.Add(item);
        return transfer;
    }

    private static string BuildWindowsClipboardHtml(string html)
    {
        const string StartMarker = "<!--StartFragment-->";
        const string EndMarker = "<!--EndFragment-->";
        const string HeaderFormat = "Version:1.0\r\nStartHTML:{0:0000000000}\r\nEndHTML:{1:0000000000}\r\nStartFragment:{2:0000000000}\r\nEndFragment:{3:0000000000}\r\n";

        var startMarkerIndex = html.IndexOf(StartMarker, StringComparison.Ordinal);
        var endMarkerIndex = html.IndexOf(EndMarker, StringComparison.Ordinal);
        if (startMarkerIndex < 0 || endMarkerIndex < 0 || endMarkerIndex < startMarkerIndex)
        {
            return html;
        }

        var blankHeader = string.Format(CultureInfo.InvariantCulture, HeaderFormat, 0, 0, 0, 0);
        var startHtml = Encoding.UTF8.GetByteCount(blankHeader);
        var endHtml = startHtml + Encoding.UTF8.GetByteCount(html);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(html[..(startMarkerIndex + StartMarker.Length)]);
        var endFragment = startHtml + Encoding.UTF8.GetByteCount(html[..endMarkerIndex]);
        var header = string.Format(CultureInfo.InvariantCulture, HeaderFormat, startHtml, endHtml, startFragment, endFragment);
        return header + html;
    }

    private static SocialCopyLayout ResolveSocialLayout(string? target)
    {
        return target?.Trim().ToLowerInvariant() switch
        {
            "wechat" or "weixin" => new("wechat", 760, 40, 28, 64),
            "zhihu" => new("zhihu", 740, 40, 28, 64),
            "juejin" => new("juejin", 820, 44, 32, 68),
            _ => new("document", 860, 48, 32, 72)
        };
    }

    private sealed record SocialCopyLayout(string TargetName, int MaxWidth, int PaddingTop, int PaddingX, int PaddingBottom);

    private enum HtmlDocumentMode
    {
        Export,
        PrintPreview
    }

    private IReadOnlyList<FilePickerFileType> CreateHtmlFileTypes()
    {
        return
        [
            new(_localizer.Get(VexL.FileTypeHtml))
            {
                Patterns = ["*.html", "*.htm"]
            },
            FilePickerFileTypes.All
        ];
    }

    private IReadOnlyList<FilePickerFileType> CreatePdfFileTypes()
    {
        return
        [
            new(_localizer.Get(VexL.FileTypePdf))
            {
                Patterns = ["*.pdf"]
            },
            FilePickerFileTypes.All
        ];
    }

    private IReadOnlyList<FilePickerFileType> CreatePngFileTypes()
    {
        return
        [
            new(_localizer.Get(VexL.FileTypePng))
            {
                Patterns = ["*.png"]
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
