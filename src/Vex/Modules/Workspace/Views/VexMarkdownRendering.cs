using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
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
        MarkdownViewer.RegisterImageControlFactory((context, source, alt) =>
        {
            var image = new ResponsiveMarkdownImage
            {
                Source = source, AltText = alt, ImageBasePath = context.ImageBasePath
            };
            context.AddMarkdownClass(image, MarkdownStyleKeys.Image);
            return image;
        });
    }
}

/// <summary>保留库的异步加载、动图和点击预览，只改变正文中的图片布局。</summary>
internal sealed class ResponsiveMarkdownImage : MarkdownImage
{
    private ContentControl? _host;
    private Image? _content;
    private double _aspectRatio;
    protected override Type StyleKeyOverride => typeof(MarkdownImage);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<ContentControl>("PART_ContentHost");
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_host?.Content is Image image)
        {
            if (!ReferenceEquals(_content, image))
            {
                _content = image;
                // 动图的 Source 由动画控件管理；库已给出首帧的等比例宽高。
                _aspectRatio = image.Width > 0 && image.Height > 0 ? image.Height / image.Width : 0;
            }
            if (_aspectRatio > 0 && double.IsFinite(availableSize.Width))
            {
                var width = Math.Max(1, availableSize.Width - image.Margin.Left - image.Margin.Right);
                image.MaxWidth = double.PositiveInfinity;
                image.MaxHeight = double.PositiveInfinity;
                image.Width = width;
                image.Height = width * _aspectRatio;
                image.Stretch = Stretch.Uniform;
            }
        }
        return base.MeasureOverride(availableSize);
    }
}
