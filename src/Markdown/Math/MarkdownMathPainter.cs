using Avalonia.Media;

using CSharpMath.Rendering.FrontEnd;

namespace CodeWF.Avalonia.Markdown.MathRendering;

/// <summary>
/// 数学排版器：把输出目标换成 Avalonia 12 兼容画布
/// （官方 <c>MathPainter</c> 固定绑定官方的 <c>AvaloniaCanvas</c>）。
/// 供阅读视图（MarkdownMathView）与即时编辑行内叠绘（MarkdownInlinePresenter）共用。
/// </summary>
internal sealed class MarkdownMathPainter : MathPainter<AvaloniaMathCanvas, Color>
{
    public override ICanvas WrapCanvas(AvaloniaMathCanvas canvas) => canvas;

    public override System.Drawing.Color WrapColor(Color color) =>
        System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);

    public override Color UnwrapColor(System.Drawing.Color color) =>
        Color.FromArgb(color.A, color.R, color.G, color.B);
}
