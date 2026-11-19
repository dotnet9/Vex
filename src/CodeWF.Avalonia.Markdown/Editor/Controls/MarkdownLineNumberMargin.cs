using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>
/// 源码行号边距：普通行次色，光标当前行强调色加粗（原型 workbench.css 的
/// .line-numbers div.current）。不响应指针（原型 .line-numbers pointer-events:none）。
/// </summary>
public sealed class MarkdownLineNumberMargin : AbstractMargin
{
    private static readonly Typeface NormalTypeface =
        new(new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI"), FontStyle.Normal, FontWeight.Normal);

    private static readonly Typeface CurrentTypeface =
        new(new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI"), FontStyle.Normal, FontWeight.SemiBold);

    private int _currentLine = -1;

    /// <summary>行号字号（原型 .line-numbers 12px）。</summary>
    public double FontSize { get; set; } = 12;

    /// <summary>行号列右侧留白（数字右对齐到正文起排前的余量）。</summary>
    public double RightPadding { get; set; } = 14;

    /// <summary>普通行号颜色；null 回落编辑器前景。</summary>
    public IBrush? LineNumbersBrush { get; set; }

    /// <summary>当前行号颜色；null 回落普通行号色。</summary>
    public IBrush? CurrentLineBrush { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var digits = Document is { LineCount: > 0 } document
            ? (int)Math.Floor(Math.Log10(document.LineCount)) + 1
            : 1;
        var sample = new FormattedText(
            new string('9', digits), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            NormalTypeface, FontSize, LineNumbersBrush ?? Brushes.Gray);
        return new Size(sample.Width + RightPadding, sample.Height);
    }

    public override void Render(DrawingContext context)
    {
        var textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
        {
            return;
        }

        foreach (var visualLine in textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            var isCurrent = lineNumber == _currentLine;
            var formatted = new FormattedText(
                lineNumber.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                isCurrent ? CurrentTypeface : NormalTypeface, FontSize,
                isCurrent ? CurrentLineBrush ?? LineNumbersBrush ?? Brushes.Gray
                          : LineNumbersBrush ?? Brushes.Gray);
            context.DrawText(formatted, GetTextOrigin(visualLine, formatted));
        }
    }

    internal Point GetTextOrigin(VisualLine visualLine, FormattedText formatted)
    {
        var firstLine = visualLine.TextLines[0];
        // 行号与正文可以不同字号；按首个折行的基线对齐，不能直接共用行顶。
        var baseline = visualLine.GetTextLineVisualYPosition(firstLine, VisualYPosition.Baseline);
        return new Point(
            Bounds.Width - RightPadding - formatted.Width,
            baseline - TextView.VerticalOffset - formatted.Baseline);
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (TextArea is not null)
        {
            TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);

        if (TextArea is not null)
        {
            TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            _currentLine = TextArea.Caret.Line;
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnDocumentChanged(TextDocument oldDocument, TextDocument newDocument)
    {
        base.OnDocumentChanged(oldDocument, newDocument);
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override void OnTextViewVisualLinesChanged()
    {
        base.OnTextViewVisualLinesChanged();
        InvalidateVisual();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        var line = TextArea?.Caret.Line ?? -1;
        if (line == _currentLine)
        {
            return;
        }

        _currentLine = line;
        InvalidateVisual();
    }
}
