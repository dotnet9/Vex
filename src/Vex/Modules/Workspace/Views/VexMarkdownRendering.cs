using CodeWF.Avalonia.Markdown;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;

namespace Vex.Modules.Workspace.Views;

/// <summary>Vex 的文档排版策略，通过库的公开渲染扩展点接入所有文档视图。</summary>
internal static class VexMarkdownRendering
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;
        VexDocumentTypography.Register();
        MarkdownViewer.RegisterTableToolsFactory((_, data) => TableTools.Create(data));
        MarkdownViewer.RegisterImageControlFactory((context, source, alt) =>
        {
            var image = new MarkdownImage
            {
                Source = source, AltText = alt, ImageBasePath = context.ImageBasePath,
                ImageSourceResolver = context.ImageSourceResolver, Sizing = MarkdownImageSizing.FillAvailableWidth
            };
            context.AddMarkdownClass(image, MarkdownStyleKeys.Image);
            return image;
        });
    }
}
