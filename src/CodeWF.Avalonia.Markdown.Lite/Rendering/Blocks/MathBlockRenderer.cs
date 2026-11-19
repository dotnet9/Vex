using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Syntax;
using Inline = global::Avalonia.Controls.Documents.Inline;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 数学块渲染器：受理 $$..$$ / \[..\] / MathBlock，
/// 化学式（\ce{}）降级为上下标文本，其余交给 CSharpMath。
/// </summary>
internal sealed class MathBlockRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (context is MarkdownViewer { SyntaxOptions.Math: false }) return null;
        var text = MarkdownSpecialBlocks.GetSpecialBlockText(block, sourceMarkdown) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (!TryExtractMathBlock(text, MarkdownSpecialBlocks.IsMathBlock(block), out var latex))
        {
            return null;
        }

        return RenderMath(latex, context);
    }

    private static Control RenderMath(string latex, IMarkdownRenderContext context)
    {
        if (MarkdownChemistry.TryParseLatex(latex, out var chemExpression))
        {
            if (context is MarkdownViewer { SyntaxOptions.Chemistry: false }) return context.CreateFallbackText(latex, MarkdownStyleKeys.HtmlBlock);
            return CreateChemBlock(chemExpression, context);
        }

        Control? view;
        try
        {
            view = context.CreateMathView(latex, 20, MarkdownMathLineStyle.Display);
        }
        catch
        {
            view = null;
        }

        if (view is null)
        {
            return context.CreateFallbackText(latex, MarkdownStyleKeys.HtmlBlock);
        }

        var border = new Border
        {
            Child = view,
            Padding = new Thickness(0, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        context.AddMarkdownClass(border, MarkdownStyleKeys.HtmlBlock);
        return border;
    }

    private static Control CreateChemBlock(MarkdownChemExpression expression, IMarkdownRenderContext context)
    {
        var textBlock = context.CreateSelectableText(MarkdownStyleKeys.HtmlBlock);
        textBlock.TextAlignment = TextAlignment.Center;
        textBlock.FontSize = 20;
        textBlock.LineHeight = Math.Max(28, context.ParagraphLineHeight);
        context.BindTheme(textBlock, SelectableTextBlock.ForegroundProperty, MarkdownViewer.TextBrushProperty);
        context.BindTheme(textBlock, SelectableTextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);

        foreach (var inline in CreateChemInlines(expression, 20))
        {
            textBlock.Inlines?.Add(inline);
        }

        var border = new Border
        {
            Child = textBlock,
            Padding = new Thickness(0, 8),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        context.AddMarkdownClass(border, MarkdownStyleKeys.HtmlBlock);
        return border;
    }

    internal static IEnumerable<Inline> CreateChemInlines(MarkdownChemExpression expression, double fontSize)
    {
        foreach (var chemInline in expression.Inlines)
        {
            var run = new Run(chemInline.Text);
            if (chemInline.Kind == MarkdownChemInlineKind.Subscript)
            {
                run.BaselineAlignment = BaselineAlignment.Subscript;
                run.FontSize = Math.Max(9, fontSize * 0.72);
            }
            else if (chemInline.Kind == MarkdownChemInlineKind.Superscript)
            {
                run.BaselineAlignment = BaselineAlignment.Superscript;
                run.FontSize = Math.Max(9, fontSize * 0.72);
            }

            yield return run;
        }
    }

    private static bool TryExtractMathBlock(string text, bool allowBareLatex, out string latex)
    {
        latex = string.Empty;
        var trimmed = text.Trim();
        if (trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal) && trimmed.Length > 4)
        {
            latex = trimmed[2..^2].Trim();
            return !string.IsNullOrWhiteSpace(latex);
        }

        if (trimmed.StartsWith(@"\[", StringComparison.Ordinal) && trimmed.EndsWith(@"\]", StringComparison.Ordinal) && trimmed.Length > 4)
        {
            latex = trimmed[2..^2].Trim();
            return !string.IsNullOrWhiteSpace(latex);
        }

        if (allowBareLatex && !string.IsNullOrWhiteSpace(trimmed) && !MarkdownPlainTextExtractor.IsTypeNameFallback(trimmed, typeof(Block)))
        {
            latex = trimmed;
            return true;
        }

        return false;
    }
}
