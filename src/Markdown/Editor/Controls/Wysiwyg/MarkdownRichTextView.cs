using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Media.TextFormatting;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>
/// 富文本显示单元：按 <see cref="MarkdownTextRun"/> 渲染粗体/斜体/删除线/行内代码/链接，
/// 并提供“点击处 → 纯文本偏移”的命中测试，供所见即所得视图点入时定位光标。
/// </summary>
internal sealed class MarkdownRichTextView : TextBlock
{
    private IReadOnlyList<MarkdownTextRun> _runs = [];
    private MarkdownBlockViewOptions _options = new();
    private double _fontSize = 15;

    public MarkdownRichTextView()
    {
        TextWrapping = TextWrapping.Wrap;
        VerticalAlignment = VerticalAlignment.Top;
    }

    /// <summary>注入块视图配色（由 <see cref="MarkdownBlockView"/> 传入）。</summary>
    public void BindPalette(MarkdownBlockViewOptions options) => _options = options;

    public double RunFontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            FontSize = value;
            ApplyRuns();
        }
    }

    /// <summary>设置富文本内容。</summary>
    public void SetRuns(IReadOnlyList<MarkdownTextRun> runs)
    {
        _runs = runs;
        ApplyRuns();
    }

    /// <summary>把控件坐标映射为纯文本偏移（用于点入编辑时定位光标）。</summary>
    public int GetTextOffset(Point point)
    {
        var plain = MarkdownTextRunParser.ToPlainText(_runs);
        if (plain.Length == 0 || TextLayout is not { } layout)
        {
            return 0;
        }

        return Math.Clamp(layout.HitTestPoint(point).TextPosition, 0, plain.Length);
    }

    private void ApplyRuns()
    {
        var inlines = new InlineCollection();
        foreach (var run in _runs)
        {
            if (run.Text.Length == 0)
            {
                continue;
            }

            var textRun = new Run(run.Text)
            {
                FontSize = run.FontSize ?? _fontSize,
            };

            // 样式优先级：行内代码 > <span style>（对比度不足回退主题） > 链接 > 默认。
            var spanBackgroundColor = MarkdownSpanStyle.TryGetColor(run.Background);
            if (run.Code)
            {
                textRun.Foreground = _options.CodeBrush;
            }
            else if (MarkdownSpanStyle.EnsureReadable(
                         MarkdownSpanStyle.TryGetColor(run.Foreground),
                         spanBackgroundColor,
                         (_options.TextBrush as SolidColorBrush)?.Color ?? Colors.Black) is { } spanForeground)
            {
                textRun.Foreground = new SolidColorBrush(spanForeground);
            }
            else
            {
                textRun.Foreground = run.LinkUrl is null ? _options.TextBrush : _options.LinkBrush;
            }

            if (spanBackgroundColor is { } backgroundColor)
            {
                textRun.Background = new SolidColorBrush(backgroundColor);
            }

            if (run.Bold)
            {
                textRun.FontWeight = FontWeight.SemiBold;
            }

            if (run.Italic)
            {
                textRun.FontStyle = FontStyle.Italic;
            }

            if (run.LinkUrl is { Length: > 0 })
            {
                textRun.TextDecorations = CreateDecoration(TextDecorationLocation.Underline);
            }
            else if (run.Strike)
            {
                textRun.TextDecorations = CreateDecoration(TextDecorationLocation.Strikethrough);
            }

            if (run.Code)
            {
                textRun.FontFamily = _options.CodeFontFamily;
                textRun.Background = _options.CodeBackgroundBrush;
            }

            inlines.Add(textRun);
        }

        Inlines = inlines;
    }

    private static TextDecorationCollection CreateDecoration(TextDecorationLocation location)
    {
        var collection = new TextDecorationCollection();
        collection.Add(new TextDecoration { Location = location });
        return collection;
    }
}
