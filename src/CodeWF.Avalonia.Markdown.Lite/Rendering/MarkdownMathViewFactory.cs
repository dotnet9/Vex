using global::Avalonia.Controls;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 数学块线型（镜像 CSharpMath.Atom.LineStyle，Core 不依赖 CSharpMath）。
/// </summary>
public enum MarkdownMathLineStyle
{
    Display,
    Text,
    Script,
    ScriptScript
}

/// <summary>
/// 数学视图工厂委托：返回数学排版控件；返回 null 表示无法渲染
/// （调用方使用 Core 的原文降级）。
/// </summary>
public delegate Control? MarkdownMathViewFactory(
    IMarkdownRenderContext context,
    string latex,
    double fontSize,
    MarkdownMathLineStyle lineStyle);
