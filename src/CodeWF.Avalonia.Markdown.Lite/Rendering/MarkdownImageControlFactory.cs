using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 交互式视觉标记：实现它的控件在文本选择路由中不参与选区
/// （如图片查看控件）。能力包控件实现此接口即可获得同等路由行为。
/// </summary>
public interface IMarkdownInteractiveVisual
{
}

/// <summary>
/// 图片控件工厂委托：返回图片查看控件；返回 null 表示无图片能力
/// （调用方使用 Core 的替代文本降级渲染）。
/// </summary>
public delegate Control? MarkdownImageControlFactory(
    IMarkdownRenderContext context,
    string source,
    string altText);
