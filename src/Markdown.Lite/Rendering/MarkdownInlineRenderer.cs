using System.Text.RegularExpressions;
using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using Inline = global::Avalonia.Controls.Documents.Inline;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 内联转换引擎：Markdig 内联树 → Avalonia Inline 集合
/// （字面量、行内代码、行内公式/化学、注音、强调、链接、图片、脚注引用、HTML）。
/// </summary>
internal sealed class MarkdownInlineRenderer(IMarkdownRenderContext context)
{
    private readonly IMarkdownRenderContext _context = context;

    public IEnumerable<Inline> ConvertInlines(ContainerInline? container, bool stripTaskPrefix = false)
    {
        if (container?.FirstChild is null)
        {
            yield break;
        }

        // 行内 <span style> 以兄弟节点形式出现在扁平序列里，需要成对配对后包裹子内容。
        var nodes = new List<Markdig.Syntax.Inlines.Inline>();
        for (var node = container.FirstChild; node is not null; node = node.NextSibling)
        {
            nodes.Add(node);
        }

        var strip = stripTaskPrefix;
        foreach (var inline in ConvertRange(nodes, 0, nodes.Count, ref strip))
        {
            yield return inline;
        }
    }

    private List<Inline> ConvertRange(List<Markdig.Syntax.Inlines.Inline> nodes, int from, int to, ref bool stripTaskPrefix)
    {
        var inlines = new List<Inline>();
        for (var index = from; index < to;)
        {
            if (nodes[index] is HtmlInline opening && IsSpanOpenTag(opening.Tag))
            {
                var close = FindSpanClose(nodes, index + 1, to);
                var innerEnd = close < 0 ? to : close;
                var span = CreateStyledSpan(ParseSpanStyle(opening.Tag));
                span.Inlines.AddRange(ConvertRange(nodes, index + 1, innerEnd, ref stripTaskPrefix));
                inlines.Add(span);
                index = close < 0 ? to : close + 1;
                continue;
            }

            if (nodes[index] is HtmlInline orphan && orphan.Tag?.Equals("</span>", StringComparison.OrdinalIgnoreCase) == true)
            {
                // 无配对开标签的闭标签：丢弃标签本身，内容照常排版。
                index++;
                continue;
            }

            inlines.AddRange(ConvertInline(nodes[index], ref stripTaskPrefix));
            index++;
        }

        return inlines;
    }

    private static readonly Regex SpanOpenPattern = new(@"^<span[\s>/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SpanStylePattern = new(@"\bstyle\s*=\s*""([^""]*)""|\bstyle\s*=\s*'([^']*)'", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LineBreakTagPattern = new(@"^<br\s*/?>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsSpanOpenTag(string? tag)
    {
        return tag is not null && SpanOpenPattern.IsMatch(tag);
    }

    private static int FindSpanClose(List<Markdig.Syntax.Inlines.Inline> nodes, int from, int to)
    {
        var depth = 1;
        for (var index = from; index < to; index++)
        {
            if (nodes[index] is not HtmlInline html)
            {
                continue;
            }

            if (IsSpanOpenTag(html.Tag))
            {
                depth++;
            }
            else if (html.Tag?.Equals("</span>", StringComparison.OrdinalIgnoreCase) == true)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static MarkdownSpanStyle? ParseSpanStyle(string? tag)
    {
        if (tag is null)
        {
            return null;
        }

        var match = SpanStylePattern.Match(tag);
        if (!match.Success)
        {
            return null;
        }

        return MarkdownSpanStyle.FromDeclarations(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }

    private Span CreateStyledSpan(MarkdownSpanStyle? style)
    {
        var span = new Span();
        if (style is null)
        {
            return span;
        }

        // 作者色可读性回退：与主题文字对比不足（如暗色主题配深色文字）时弃用作者色。
        var themeText = (_context as MarkdownViewer)?.TextBrush is SolidColorBrush themeBrush ? themeBrush.Color : default(Color?);
        var background = MarkdownSpanStyle.TryGetColor(style.Background);
        if (MarkdownSpanStyle.EnsureReadable(MarkdownSpanStyle.TryGetColor(style.Foreground), background,
                themeText ?? Color.FromRgb(0x33, 0x33, 0x33)) is { } foreground)
        {
            span.Foreground = new SolidColorBrush(foreground);
        }

        if (background is { } backgroundColor)
        {
            span.Background = new SolidColorBrush(backgroundColor);
        }

        if (style.FontSize is { } fontSize)
        {
            span.FontSize = fontSize;
        }

        return span;
    }

    private IReadOnlyList<Inline> ConvertInline(Markdig.Syntax.Inlines.Inline inline, ref bool stripTaskPrefix)
    {
        var result = new List<Inline>();
        if (TryGetMathInlineLatex(inline, out var mathLatex))
        {
            stripTaskPrefix = false;
            result.Add(CreateMathInline(mathLatex));
            return result;
        }

        switch (inline)
        {
            case LiteralInline literal:
                var literalText = literal.Content.ToString();
                if (stripTaskPrefix && MarkdownTaskListHelper.TryStripTaskPrefix(literalText, out var stripped))
                {
                    stripTaskPrefix = false;
                    if (!string.IsNullOrWhiteSpace(stripped))
                    {
                        result.Add(new Run(stripped.TrimStart()));
                    }

                    return result;
                }

                stripTaskPrefix = false;
                result.AddRange(CreateLiteralInlines(literalText));
                return result;

            case LineBreakInline lineBreak:
                stripTaskPrefix = false;
                result.Add(!lineBreak.IsHard && _context is MarkdownViewer { SyntaxOptions.SoftBreaks: false }
                    ? new Run(" ") : new LineBreak());
                return result;

            case CodeInline code:
                stripTaskPrefix = false;
                result.Add(CreateInlineCode(code.Content));
                return result;

            case TaskList:
                stripTaskPrefix = true;
                return result;

            case FootnoteLink footnoteLink:
                stripTaskPrefix = false;
                result.Add(CreateFootnoteLink(footnoteLink));
                return result;

            case EmphasisInline emphasis:
                stripTaskPrefix = false;
                result.Add(CreateEmphasis(emphasis));
                return result;

            case LinkInline { IsImage: true } image:
                stripTaskPrefix = false;
                result.Add(CreateImage(image));
                return result;

            case LinkInline link:
                stripTaskPrefix = false;
                result.Add(CreateLink(link));
                return result;

            case HtmlInline html:
                stripTaskPrefix = false;
                result.AddRange(CreateHtmlInline(html.Tag));
                return result;

            case ContainerInline container:
                stripTaskPrefix = false;
                result.Add(CreateContainerSpan(container));
                return result;

            default:
                stripTaskPrefix = false;
                var text = inline.ToString() ?? string.Empty;
                if (!MarkdownPlainTextExtractor.IsTypeNameFallback(text, inline.GetType()))
                {
                    result.Add(new Run(text));
                }

                return result;
        }
    }

    private static bool TryGetMathInlineLatex(Markdig.Syntax.Inlines.Inline inline, out string latex)
    {
        latex = string.Empty;
        string text;
        if (inline is MathInline mathInline)
        {
            text = mathInline.Content.ToString().Trim();
        }
        else
        {
            if (!inline.GetType().Name.Contains("Math", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            text = inline.ToString()?.Trim() ?? string.Empty;
            if (MarkdownPlainTextExtractor.IsTypeNameFallback(text, inline.GetType()))
            {
                return false;
            }
        }

        text = TrimInlineMathDelimiters(text);
        if (string.IsNullOrWhiteSpace(text) || MarkdownPlainTextExtractor.IsTypeNameFallback(text, inline.GetType()))
        {
            return false;
        }

        latex = text;
        return true;
    }

    private static string TrimInlineMathDelimiters(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("$$", StringComparison.Ordinal) && trimmed.EndsWith("$$", StringComparison.Ordinal) && trimmed.Length > 4)
        {
            return trimmed[2..^2].Trim();
        }

        if (trimmed.StartsWith('$') && trimmed.EndsWith('$') && trimmed.Length > 2)
        {
            return trimmed[1..^1].Trim();
        }

        if (trimmed.StartsWith(@"\(", StringComparison.Ordinal) && trimmed.EndsWith(@"\)", StringComparison.Ordinal) && trimmed.Length > 4)
        {
            return trimmed[2..^2].Trim();
        }

        return trimmed;
    }

    private IEnumerable<Inline> CreateLiteralInlines(string text)
    {
        var index = 0;
        while (index < text.Length)
        {
            if (_context is not MarkdownViewer { SyntaxOptions.Math: false } && TryReadInlineMath(text, index, out var latex, out var mathEnd))
            {
                yield return CreateMathInline(latex);
                index = mathEnd;
                continue;
            }

            if (TryReadRuby(text, index, out var rubyText, out var rubyAnnotation, out var rubyEnd))
            {
                yield return CreateRubyInline(rubyText, rubyAnnotation);
                index = rubyEnd;
                continue;
            }

            var next = FindNextSpecialInline(text, index + 1);
            yield return new Run(text[index..next]);
            index = next;
        }
    }

    private IEnumerable<Inline> CreateHtmlInline(string html)
    {
        if (LineBreakTagPattern.IsMatch(html))
        {
            yield return new LineBreak();
            yield break;
        }

        // <a>/<span> 标签本身不渲染：链接由 LinkInline 呈现，span 样式由 ConvertRange 配对包裹。
        if (Regex.IsMatch(html, @"^</?(a|span)\b", RegexOptions.IgnoreCase))
        {
            yield break;
        }

        yield return new Run(html);
    }

    private static int FindNextSpecialInline(string text, int start)
    {
        var dollar = text.IndexOf('$', start);
        var ruby = text.IndexOf('{', start);
        return (dollar, ruby) switch
        {
            (-1, -1) => text.Length,
            (-1, _) => ruby,
            (_, -1) => dollar,
            _ => Math.Min(dollar, ruby)
        };
    }

    private static bool TryReadInlineMath(string text, int start, out string latex, out int end)
    {
        latex = string.Empty;
        end = start;
        if (text[start] != '$' || start + 1 >= text.Length || text[start + 1] == '$')
        {
            return false;
        }

        var close = text.IndexOf('$', start + 1);
        if (close <= start + 1)
        {
            return false;
        }

        latex = text[(start + 1)..close].Trim();
        end = close + 1;
        return !string.IsNullOrWhiteSpace(latex);
    }

    private static bool TryReadRuby(string text, int start, out string rubyText, out string annotation, out int end)
    {
        rubyText = string.Empty;
        annotation = string.Empty;
        end = start;
        if (text[start] != '{')
        {
            return false;
        }

        var separator = text.IndexOf('|', start + 1);
        var close = text.IndexOf('}', start + 1);
        if (separator < 0 || close < 0 || separator > close)
        {
            return false;
        }

        rubyText = text[(start + 1)..separator];
        annotation = text[(separator + 1)..close];
        end = close + 1;
        return !string.IsNullOrWhiteSpace(rubyText) && !string.IsNullOrWhiteSpace(annotation);
    }

    private Inline CreateMathInline(string latex)
    {
        if (_context is MarkdownViewer { SyntaxOptions.Math: false }) return new Run("$" + latex + "$");
        if (_context is MarkdownViewer { SyntaxOptions.Chemistry: false }
            && (latex.Contains(@"\ce{", StringComparison.Ordinal) || latex.Contains(@"\pu{", StringComparison.Ordinal))) return new Run(latex);
        if (MarkdownChemistry.TryParseLatex(latex, out var chemExpression))
        {
            var span = new Span();
            foreach (var inline in MathBlockRenderer.CreateChemInlines(chemExpression, _context.ParagraphFontSize))
            {
                span.Inlines.Add(inline);
            }

            return span;
        }

        try
        {
            var view = _context.CreateMathView(latex, _context.ParagraphFontSize, MarkdownMathLineStyle.Text);
            if (view is null)
            {
                return new Run($"${latex}$");
            }

            view.VerticalAlignment = VerticalAlignment.Center;
            return CreateInlineContainer(view);
        }
        catch
        {
            return new Run($"${latex}$");
        }
    }

    private Inline CreateRubyInline(string text, string annotation)
    {
        var annotations = annotation.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (annotations.Length == text.Length)
        {
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < text.Length; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                var top = CreateRubyTextBlock(annotations[i], Math.Max(8, _context.ParagraphFontSize * 0.46), Math.Max(8, _context.ParagraphLineHeight * 0.34));
                var bottom = CreateRubyTextBlock(text[i].ToString(), _context.ParagraphFontSize, Math.Max(12, _context.ParagraphLineHeight * 0.66));
                _context.BindTheme(top, TextBlock.ForegroundProperty, MarkdownViewer.MutedTextBrushProperty);
                _context.BindTheme(bottom, TextBlock.ForegroundProperty, MarkdownViewer.TextBrushProperty);
                _context.BindTheme(top, TextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
                _context.BindTheme(bottom, TextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);

                Grid.SetRow(top, 0);
                Grid.SetColumn(top, i);
                Grid.SetRow(bottom, 1);
                Grid.SetColumn(bottom, i);
                grid.Children.Add(top);
                grid.Children.Add(bottom);
            }

            return CreateInlineContainer(grid);
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        };

        var topFallback = CreateRubyTextBlock(annotation, Math.Max(9, _context.ParagraphFontSize * 0.62), Math.Max(10, _context.ParagraphLineHeight * 0.45));
        var bottomFallback = CreateRubyTextBlock(text, _context.ParagraphFontSize, _context.ParagraphLineHeight * 0.72);
        _context.BindTheme(topFallback, TextBlock.ForegroundProperty, MarkdownViewer.MutedTextBrushProperty);
        _context.BindTheme(bottomFallback, TextBlock.ForegroundProperty, MarkdownViewer.TextBrushProperty);
        _context.BindTheme(topFallback, TextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        _context.BindTheme(bottomFallback, TextBlock.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);

        panel.Children.Add(topFallback);
        panel.Children.Add(bottomFallback);
        return CreateInlineContainer(panel);
    }

    private static TextBlock CreateRubyTextBlock(string text, double fontSize, double lineHeight)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            LineHeight = lineHeight,
            Margin = new Thickness(1, 0)
        };
    }

    private Span CreateContainerSpan(ContainerInline container)
    {
        var span = new Span();
        foreach (var inline in ConvertInlines(container))
        {
            span.Inlines.Add(inline);
        }

        return span;
    }

    private Span CreateEmphasis(EmphasisInline emphasis)
    {
        var span = new Span();
        foreach (var inline in ConvertInlines(emphasis))
        {
            span.Inlines.Add(inline);
        }

        FontWeight? fontWeight = null;
        FontStyle? fontStyle = null;
        TextDecorationCollection? textDecorations = null;
        if (emphasis.DelimiterCount >= 2 && emphasis.DelimiterChar is '*' or '_')
        {
            fontWeight = FontWeight.Bold;
        }
        else if (emphasis.DelimiterChar is '*' or '_')
        {
            fontStyle = FontStyle.Italic;
        }

        if (emphasis.DelimiterChar == '~' && emphasis.DelimiterCount >= 2)
        {
            textDecorations = TextDecorations.Strikethrough;
        }

        if (emphasis.DelimiterChar is '^' or '~' && emphasis.DelimiterCount == 1)
        {
            span.BaselineAlignment = emphasis.DelimiterChar == '^' ? BaselineAlignment.Superscript : BaselineAlignment.Subscript;
            span.FontSize = _context.ParagraphFontSize * 0.75;
        }
        if (emphasis.DelimiterChar == '=' && emphasis.DelimiterCount == 2)
            span.Background = new SolidColorBrush(Color.FromArgb(100, 255, 213, 79));

        foreach (var child in span.Inlines)
        {
            ApplyInlineTextStyle(child, fontWeight, fontStyle, textDecorations);
        }

        return span;
    }

    private static void ApplyInlineTextStyle(
        Inline inline,
        FontWeight? fontWeight,
        FontStyle? fontStyle,
        TextDecorationCollection? textDecorations)
    {
        if (fontWeight.HasValue)
        {
            inline.FontWeight = fontWeight.Value;
        }

        if (fontStyle.HasValue)
        {
            inline.FontStyle = fontStyle.Value;
        }

        if (textDecorations is not null)
        {
            inline.TextDecorations = textDecorations;
        }

        if (inline is Span span)
        {
            foreach (var child in span.Inlines)
            {
                ApplyInlineTextStyle(child, fontWeight, fontStyle, textDecorations);
            }
        }
    }

    private Inline CreateInlineCode(string code)
    {
        var run = new Run(code)
        {
            FontFamily = _context.CodeFontFamily,
            BaselineAlignment = BaselineAlignment.Center
        };
        _context.BindTheme(run, TextElement.ForegroundProperty, MarkdownViewer.AccentBrushProperty);
        _context.BindTheme(run, TextElement.BackgroundProperty, MarkdownViewer.InlineCodeBackgroundBrushProperty);
        _context.BindTheme(run, TextElement.FontFamilyProperty, MarkdownViewer.CodeFontFamilyProperty);
        _context.BindTheme(run, TextElement.FontSizeProperty, MarkdownViewer.ParagraphFontSizeProperty);
        return run;
    }

    private Inline CreateImage(LinkInline imageInline)
    {
        var altText = MarkdownPlainTextExtractor.ExtractPlainText(imageInline);
        if (imageInline.Url is { } source && _context.CreateImageControl(source, altText) is { } imageControl)
        {
            return CreateInlineContainer(imageControl);
        }

        return new Run(string.IsNullOrWhiteSpace(altText) ? "[image]" : $"[image] {altText}");
    }

    private Inline CreateFootnoteLink(FootnoteLink footnoteLink)
    {
        var text = footnoteLink.IsBackLink
            ? "^"
            : $"[{Math.Max(1, footnoteLink.Footnote?.Order ?? footnoteLink.Index)}]";
        var run = new Run(text)
        {
            BaselineAlignment = BaselineAlignment.Superscript,
            FontSize = Math.Max(9, _context.ParagraphFontSize * 0.72)
        };
        _context.BindTheme(run, TextElement.ForegroundProperty, MarkdownViewer.AccentBrushProperty);
        _context.BindTheme(run, TextElement.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        return run;
    }

    private Inline CreateLink(LinkInline linkInline)
    {
        var text = MarkdownPlainTextExtractor.ExtractPlainText(linkInline);
        if (string.IsNullOrWhiteSpace(text))
        {
            text = linkInline.Url ?? string.Empty;
        }

        var span = new Span
        {
            TextDecorations = TextDecorations.Underline
        };
        _context.BindTheme(span, TextElement.ForegroundProperty, MarkdownViewer.AccentBrushProperty);
        _context.BindTheme(span, TextElement.FontFamilyProperty, MarkdownViewer.ContentFontFamilyProperty);
        _context.BindTheme(span, TextElement.FontSizeProperty, MarkdownViewer.ParagraphFontSizeProperty);

        foreach (var inline in ConvertInlines(linkInline))
        {
            span.Inlines.Add(inline);
        }

        if (span.Inlines.Count == 0)
        {
            span.Inlines.Add(new Run(text));
        }

        return span;
    }

    private static InlineUIContainer CreateInlineContainer(Control control)
    {
        return new InlineUIContainer(control);
    }
}
