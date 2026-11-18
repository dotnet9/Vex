using global::Avalonia.Controls;

using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;

using CSharpMath.Atom;

namespace CodeWF.Avalonia.Markdown.MathRendering;

/// <summary>
/// 数学渲染能力接入扩展：注册后 $..$ / $$..$$ 公式使用 CSharpMath 排版；
/// 不注册时由 Core 降级为原文渲染。
/// </summary>
public static class CodeWFMarkdownMathExtensions
{
    public static void UseMath() =>
        MarkdownViewer.RegisterMathViewFactory(static (context, latex, fontSize, lineStyle) =>
        {
            try
            {
                var view = new MarkdownMathView
                {
                    LaTeX = MathLatexNormalizer.NormalizeLatex(latex),
                    FontSize = (float)fontSize,
                    LineStyle = lineStyle switch
                    {
                        MarkdownMathLineStyle.Display => LineStyle.Display,
                        MarkdownMathLineStyle.Script => LineStyle.Script,
                        MarkdownMathLineStyle.ScriptScript => LineStyle.ScriptScript,
                        _ => LineStyle.Text
                    },
                    DisplayErrorInline = false
                };
                context.BindTheme(view, MarkdownMathView.ForegroundProperty, MarkdownViewer.TextBrushProperty);
                return view;
            }
            catch
            {
                return null;
            }
        });
}
