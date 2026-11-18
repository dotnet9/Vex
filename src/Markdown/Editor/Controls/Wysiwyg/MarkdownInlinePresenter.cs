using global::Avalonia;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Media;
using global::Avalonia.Media.TextFormatting;
using global::Avalonia.Utilities;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>保留 TextBox 的输入法、选择和光标；显示行内样式并保持源码字符索引。</summary>
internal sealed class MarkdownInlinePresenter(MarkdownBlockViewOptions options, bool plain) : TextPresenter
{
    private double _availableWidth = double.PositiveInfinity;
    protected override Size MeasureOverride(Size availableSize)
    { _availableWidth = availableSize.Width; return base.MeasureOverride(availableSize); }
    protected override Size ArrangeOverride(Size finalSize)
    { _availableWidth = finalSize.Width; return base.ArrangeOverride(finalSize); }

    protected override TextLayout CreateTextLayout()
    {
        if (plain) return base.CreateTextLayout();
        var source = Text ?? "";
        var face = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var styles = new List<ValueSpan<TextRunProperties>>();
        // 隐藏的语法仍占一个源码索引，但不能包含实际换行而影响段落排版。
        var display = Enumerable.Repeat('\u200b', source.Length).ToArray();
        var cursor = 0;
        foreach (var run in MarkdownTextRunParser.Parse(source))
        {
            var start = Math.Clamp(run.SourceStart, cursor, source.Length);
            if (start > cursor) Hide(cursor, start - cursor);
            var length = Math.Min(run.Text.Length, source.Length - start);
            if (length > 0)
            {
                run.Text.AsSpan(0, length).CopyTo(display.AsSpan(start));
                var typeface = new Typeface(run.Code ? options.CodeFontFamily : FontFamily,
                    run.Italic ? FontStyle.Italic : FontStyle, run.Bold ? FontWeight.Bold : FontWeight, FontStretch);
                var decoration = run.Strike ? TextDecorations.Strikethrough : run.LinkUrl is not null ? TextDecorations.Underline : null;
                // 样式优先级：行内代码 > <span style>（对比度不足回退主题） > 链接 > 默认前景。
                var themeTextColor = (Foreground as SolidColorBrush)?.Color ?? Colors.Black;
                var spanBackgroundColor = MarkdownSpanStyle.TryGetColor(run.Background);
                var foreground = run.Code ? options.CodeBrush
                    : MarkdownSpanStyle.EnsureReadable(
                          MarkdownSpanStyle.TryGetColor(run.Foreground),
                          spanBackgroundColor,
                          themeTextColor) is { } readable
                        ? new SolidColorBrush(readable)
                        : (run.LinkUrl is not null ? options.LinkBrush : Foreground);
                var background = run.Code ? options.CodeBackgroundBrush
                    : spanBackgroundColor is { } backgroundColor ? new SolidColorBrush(backgroundColor) : null;
                styles.Add(new(start, length, new GenericTextRunProperties(typeface, run.FontSize ?? FontSize, decoration,
                    foreground, background)));
            }
            cursor = start + length;
        }
        if (cursor < source.Length) Hide(cursor, source.Length - cursor);
        var text = new string(display);
        var compositionStart = Math.Clamp(CaretIndex, 0, source.Length);
        var compositionLength = PreeditText?.Length ?? 0;
        if (compositionLength > 0)
        {
            // 预编辑沿用光标处的富文本样式，不重新解析未提交的 Markdown。
            var inherited = styles.FirstOrDefault(s => s.Start <= compositionStart && s.Start + s.Length > compositionStart && s.Value.FontRenderingEmSize > .01).Value
                ?? styles.LastOrDefault(s => s.Start < compositionStart && s.Value.FontRenderingEmSize > .01).Value
                ?? new GenericTextRunProperties(face, FontSize, foregroundBrush: Foreground);
            var shifted = new List<ValueSpan<TextRunProperties>>();
            foreach (var span in styles)
            {
                var end = span.Start + span.Length;
                if (end <= compositionStart) shifted.Add(span);
                else if (span.Start >= compositionStart) shifted.Add(new(span.Start + compositionLength, span.Length, span.Value));
                else
                {
                    shifted.Add(new(span.Start, compositionStart - span.Start, span.Value));
                    shifted.Add(new(compositionStart + compositionLength, end - compositionStart, span.Value));
                }
            }
            shifted.Add(new(compositionStart, compositionLength, inherited));
            styles = shifted.OrderBy(s => s.Start).ToList();
            text = text.Insert(compositionStart, PreeditText!);
        }
        var selectionStart = Math.Min(SelectionStart, SelectionEnd);
        var selectionEnd = Math.Max(SelectionStart, SelectionEnd);
        var overrides = new List<ValueSpan<TextRunProperties>>();
        foreach (var span in styles)
        {
            var end = span.Start + span.Length;
            var boundaries = new[] { span.Start, end, selectionStart, selectionEnd, compositionStart, compositionStart + compositionLength }
                .Where(index => index >= span.Start && index <= end).Distinct().Order().ToArray();
            for (var i = 0; i + 1 < boundaries.Length; i++)
            {
                var start = boundaries[i]; var finish = boundaries[i + 1]; var properties = span.Value;
                var composing = compositionLength > 0 && start >= compositionStart && finish <= compositionStart + compositionLength;
                var selected = start >= selectionStart && finish <= selectionEnd && selectionStart != selectionEnd;
                if ((selected && SelectionForegroundBrush is not null || composing) && properties.FontRenderingEmSize > .01)
                    properties = new GenericTextRunProperties(properties.Typeface, properties.FontRenderingEmSize,
                        composing ? TextDecorations.Underline : properties.TextDecorations,
                        selected ? SelectionForegroundBrush : properties.ForegroundBrush,
                        selected && SelectionForegroundBrush is not null ? null : properties.BackgroundBrush,
                        properties.BaselineAlignment, properties.CultureInfo, properties.FontFeatures);
                overrides.Add(new(start, finish - start, properties));
            }
        }
        var mixedSizes = styles.Any(span => span.Value.FontRenderingEmSize > FontSize + .01);
        var layoutFontSize = mixedSizes ? MarkdownLineMetrics.StrutFontSize(face, FontSize, LineHeight) : FontSize;
        return new TextLayout(text, face, layoutFontSize, Foreground, TextAlignment, TextWrapping,
            maxWidth: _availableWidth > 0 ? _availableWidth : double.PositiveInfinity,
            flowDirection: FlowDirection, fontFeatures: FontFeatures,
            lineHeight: mixedSizes ? double.NaN : LineHeight, letterSpacing: LetterSpacing, textStyleOverrides: overrides);
        void Hide(int start, int length) => styles.Add(new(start, length,
            new GenericTextRunProperties(face, .001, foregroundBrush: Brushes.Transparent)));
    }
}
