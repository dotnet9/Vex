using System.Net;
using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

    public async Task<string?> OpenHtmlPrintPreviewAsync(DocumentSnapshot document)
    {
        var folder = Path.Combine(Path.GetTempPath(), "Vex", "PrintPreview");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(document.FileName)}-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(path, BuildHtml(document), Utf8NoBom);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return path;
    }

    private string BuildHtml(DocumentSnapshot document)
    {
        var title = WebUtility.HtmlEncode(Path.GetFileNameWithoutExtension(document.FileName));
        var body = Markdig.Markdown.ToHtml(document.Markdown ?? string.Empty, Pipeline);
        var language = WebUtility.HtmlEncode(_localizer.Culture.Name);
        return $$"""
            <!doctype html>
            <html lang="{{language}}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{title}}</title>
              <style>
                body { margin: 0; color: #1f2937; background: #ffffff; font-family: "Inter", "Microsoft YaHei UI", "Segoe UI", sans-serif; line-height: 1.72; }
                article { max-width: 860px; margin: 0 auto; padding: 48px 32px 72px; }
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
              </style>
            </head>
            <body>
              <article>
            {{body}}
              </article>
            </body>
            </html>
            """;
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

    private static Window? GetMainWindow()
    {
        return Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
    }
}
