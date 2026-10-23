using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;

namespace Vex.Modules.Help.Views;

/// <summary>
/// 帮助文档只读窗口（更新日志、鸣谢）：对应原型的 dlg-doc 独立窗体，
/// 以 MarkdownViewer 呈现随附文档，不接入编辑与保存链路。
/// </summary>
public sealed class HelpDocumentWindow : Window
{
    public HelpDocumentWindow(string title, string markdown)
    {
        Title = title;
        Width = 860;
        Height = 680;
        MinWidth = 520;
        MinHeight = 360;
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC");
        var viewer = new MarkdownViewer
        {
            Markdown = markdown,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(28, 22, 28, 32),
        };
        viewer.Bind(BackgroundProperty, this.GetResourceObservable("VexPreviewBackgroundBrush"));
        var scroll = new ScrollViewer { Content = viewer };
        scroll.Bind(BackgroundProperty, this.GetResourceObservable("VexPreviewBackgroundBrush"));
        Content = scroll;
        this.Bind(BackgroundProperty, this.GetResourceObservable("VexWindowBackgroundBrush"));
    }
}
