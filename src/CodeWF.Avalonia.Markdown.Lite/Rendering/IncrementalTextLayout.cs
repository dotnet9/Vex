using System.Collections;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 文本片段的逐行增量排版：与 <see cref="TextLayout"/> 结果相同（行高、对齐、空行、命中测试），
/// 但可以从任意差异位置重新排版——流式追加/编辑时只从差异点所在行的前一行重新整形断行，
/// 之前的行（及其绘制）保持不变，长段落不再整段重排。
/// 基于 StreamMark 的 SegmentLayout（MIT，© StreamMark contributors）移植适配。
/// </summary>
/// <remarks>
/// 从行首重新开始等价于整段排版：行首本身是断行机会，前一行的断行只取决于它自己的文字与紧随其后的字符，
/// 因此从差异点所在行的前一行开始即可覆盖禁则（如行首不能是句号）把前一行末字拉到下一行的情况。
/// 两端对齐依赖 Avalonia 内部的词间对齐实现，整段交给 <see cref="TextLayout"/> 排版后接管其行。
/// </remarks>
public sealed class IncrementalTextLayout : IDisposable
{
    private readonly List<Line> _lines = [];
    private readonly LineList _textLines;
    private FlowDirection _flowDirection;
    private int _textLength;

    public IncrementalTextLayout()
    {
        _textLines = new LineList(_lines);
    }

    /// <summary>所有行（按显示顺序）。</summary>
    public IReadOnlyList<TextLine> TextLines => _textLines;

    public double Height { get; private set; }

    public double Width { get; private set; }

    public double WidthIncludingTrailingWhitespace { get; private set; }

    /// <summary>排版宽度（不换行时为无穷大）。</summary>
    public double MaxWidth { get; private set; } = double.NaN;

    internal int LineCount => _lines.Count;

    internal double LineTop(int index) => _lines[index].Top;

    internal int LineStart(int index) => _lines[index].Start;

    internal int LineLength(int index) => _lines[index].Length;

    internal bool IsEmptyLine(int index) => _lines[index].IsEmpty;

    internal TextLine LineAt(int index) => _lines[index].Text;

    /// <summary>
    /// 排版。<paramref name="from"/> 是新文本与上次排版文本第一个不同的位置（0 或宽度变化时整段重排）。
    /// 返回第一个被替换的行号（之前的行对象未变）。
    /// </summary>
    public int Format(ITextSource source, int textLength, double maxWidth, TextParagraphProperties fixedHeight,
        TextParagraphProperties natural, int from, TextRunCache? cache)
    {
        _flowDirection = fixedHeight.FlowDirection;
        var restart = 0;
        if (from > 0 && _lines.Count > 0 && maxWidth.Equals(MaxWidth) && fixedHeight.TextAlignment != TextAlignment.Justify)
        {
            restart = Math.Max(0, LineIndexOf(from) - 1);
            // 末尾的空行（硬换行之后）没有自己的文字：从产生它的那一行开始
            while (restart > 0 && _lines[restart].IsEmpty)
            {
                restart--;
            }
        }
        var position = restart < _lines.Count ? _lines[restart].Start : 0;
        for (var i = restart; i < _lines.Count; i++)
        {
            _lines[i].Text.Dispose();
        }
        _lines.RemoveRange(restart, _lines.Count - restart);
        MaxWidth = maxWidth;
        _textLength = textLength;

        if (fixedHeight.TextAlignment == TextAlignment.Justify)
        {
            AdoptJustified(source, maxWidth, fixedHeight, natural, cache);
        }
        else
        {
            FormatFrom(source, position, textLength, maxWidth, fixedHeight, natural, restart == 0 ? cache : null);
        }
        if (_lines.Count == 0)
        {
            AddEmptyLine(0, maxWidth, fixedHeight);
        }
        UpdateMetrics(restart);
        return restart;
    }

    private void FormatFrom(ITextSource source, int position, int textLength, double maxWidth, TextParagraphProperties fixedHeight,
        TextParagraphProperties natural, TextRunCache? cache)
    {
        var formatter = TextFormatter.Current;
        TextLineBreak? previous = null;
        while (position < textLength)
        {
            var line = formatter.FormatLine(source, position, maxWidth, fixedHeight, previous, cache);
            if (line is null)
            {
                break;
            }
            if (Overflows(line))
            {
                // 固定行高会把整行裁到该高度：行内有更高的对象（图片、大公式）时这一行用自然行高
                line.Dispose();
                line = formatter.FormatLine(source, position, maxWidth, natural, null, null);
                if (line is null)
                {
                    break;
                }
            }
            if (line.Length <= 0)
            {
                line.Dispose();
                break;
            }
            _lines.Add(new Line(line, position, line.Length, IsEmpty: false));
            position += line.Length;
            previous = line.TextLineBreak;
            if (previous?.TextEndOfLine is TextEndOfParagraph)
            {
                return;
            }
        }
        // 文字以换行结束：后面还有一个空行（光标、选区落在这里）
        if (_lines.Count > 0 && _lines[^1].Text.NewLineLength > 0 && position >= textLength)
        {
            AddEmptyLine(position, maxWidth, fixedHeight);
        }
    }

    private void AdoptJustified(ITextSource source, double maxWidth, TextParagraphProperties fixedHeight, TextParagraphProperties natural, TextRunCache? cache)
    {
#pragma warning disable CS0618
        var layout = new TextLayout(source, fixedHeight, maxWidth: maxWidth, textRunCache: cache);
        if (layout.TextLines.Any(Overflows))
        {
            layout.Dispose();
            layout = new TextLayout(source, natural, maxWidth: maxWidth);
        }
#pragma warning restore CS0618
        // 接管各行（之后由本对象释放）；TextLayout 自身只持有行，不需要单独释放
        foreach (var line in layout.TextLines)
        {
            _lines.Add(new Line(line, line.FirstTextSourceIndex, line.Length, IsEmpty: line.Length == 0));
        }
    }

    private static bool Overflows(TextLine line)
    {
        foreach (var run in line.TextRuns)
        {
            if (run is DrawableTextRun drawable and not ShapedTextRun && drawable.Size.Height > line.Height + 0.5)
            {
                return true;
            }
        }
        return false;
    }

    private void AddEmptyLine(int position, double maxWidth, TextParagraphProperties properties)
    {
        // 空行由 TextLayout 对空文本源生成（TextFormatter 不公开创建空行的方法）；行对象之后由本对象释放
        var empty = new TextLayout(EmptySource.Instance, properties, maxWidth: double.IsFinite(maxWidth) ? maxWidth : double.PositiveInfinity);
        _lines.Add(new Line(empty.TextLines[0], position, 0, IsEmpty: true));
    }

    private void UpdateMetrics(int from)
    {
        var top = from > 0 ? _lines[from - 1].Top + _lines[from - 1].Text.Height : 0;
        for (var i = from; i < _lines.Count; i++)
        {
            _lines[i] = _lines[i] with { Top = top };
            top += _lines[i].Text.Height;
        }
        Height = top;
        double width = 0, withWhitespace = 0;
        foreach (var line in _lines)
        {
            width = Math.Max(width, line.Text.Width);
            withWhitespace = Math.Max(withWhitespace, line.Text.WidthIncludingTrailingWhitespace);
        }
        Width = width;
        WidthIncludingTrailingWhitespace = withWhitespace;
    }

    /// <summary>包含源码位置 <paramref name="position"/> 的行（超出末尾时为最后一行）。</summary>
    internal int LineIndexOf(int position)
    {
        int lo = 0, hi = _lines.Count - 1, result = 0;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (_lines[mid].Start <= position)
            {
                result = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return result;
    }

    private int LineIndexAtY(double y)
    {
        int lo = 0, hi = _lines.Count - 1, result = _lines.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (_lines[mid].Top + _lines[mid].Text.Height > y)
            {
                result = mid;
                hi = mid - 1;
            }
            else
            {
                lo = mid + 1;
            }
        }
        return result;
    }

    // ---- 命中测试：与 TextLayout 的同名方法语义一致 ----

    public Rect HitTestTextPosition(int textPosition)
    {
        if (_lines.Count == 0)
        {
            return default;
        }
        if (textPosition < 0)
        {
            textPosition = _textLength;
        }
        var index = LineIndexOf(textPosition);
        // 位置正好在一行末尾（不是最后一行）时属于下一行
        while (index + 1 < _lines.Count && _lines[index].Start + _lines[index].Length <= textPosition)
        {
            index++;
        }
        var line = _lines[index];
        if (line.IsEmpty)
        {
            return new Rect(line.Text.Start, line.Top, 0, line.Text.Height);
        }
        var hit = new CharacterHit(textPosition);
        var startX = line.Text.GetDistanceFromCharacterHit(hit);
        var endX = line.Text.GetDistanceFromCharacterHit(line.Text.GetNextCaretCharacterHit(hit));
        return new Rect(startX, line.Top, endX - startX, line.Text.Height);
    }

    public IEnumerable<Rect> HitTestTextRange(int start, int length)
    {
        if (start + length <= 0 || _lines.Count == 0)
        {
            return [];
        }
        var result = new List<Rect>();
        for (var i = LineIndexOf(Math.Max(0, start)); i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (line.IsEmpty)
            {
                continue;
            }
            if (line.Start + line.Length <= start)
            {
                continue;
            }
            foreach (var bounds in line.Text.GetTextBounds(start, length))
            {
                if (result.Count > 0 && result[^1] is var last && MathUtilitiesAreClose(last.Right, bounds.Rectangle.Left) && MathUtilitiesAreClose(last.Top, line.Top))
                {
                    result[^1] = last.WithWidth(last.Width + bounds.Rectangle.Width);
                }
                else
                {
                    result.Add(bounds.Rectangle.WithY(line.Top));
                }
                foreach (var run in bounds.TextRunBounds)
                {
                    start += run.Length;
                    length -= run.Length;
                }
            }
            if (line.Start + line.Length >= start + length)
            {
                break;
            }
        }
        return result;
    }

    public TextHitTestResult HitTestPoint(in Point point)
    {
        if (_lines.Count == 0)
        {
            return default;
        }
        var index = LineIndexAtY(point.Y);
        var line = _lines[index];
        if (line.IsEmpty)
        {
            return new TextHitTestResult(new CharacterHit(line.Start), line.Start, false, point.Y > Height);
        }
        var characterHit = line.Text.GetCharacterHitFromDistance(point.X);
        return GetHitTestResult(line, characterHit, new Point(point.X, point.Y - line.Top), point.Y);
    }

    private TextHitTestResult GetHitTestResult(Line line, CharacterHit characterHit, Point local, double y)
    {
        var textLine = line.Text;
        var (x, ly) = local;
        var isInside = x >= 0 && x <= textLine.Width && ly >= 0 && ly <= textLine.Height;
        var lastTrailingIndex = 0;
        if (_flowDirection == FlowDirection.LeftToRight)
        {
            lastTrailingIndex = textLine.FirstTextSourceIndex + textLine.Length;
            if (x >= textLine.Width && textLine.Length > 0 && textLine.NewLineLength > 0)
            {
                lastTrailingIndex -= textLine.NewLineLength;
            }
            if (textLine.TextLineBreak?.TextEndOfLine is { } endOfLine)
            {
                lastTrailingIndex -= endOfLine.Length;
            }
        }
        else
        {
            if (x <= textLine.WidthIncludingTrailingWhitespace - textLine.Width && textLine.Length > 0 && textLine.NewLineLength > 0)
            {
                lastTrailingIndex += textLine.NewLineLength;
            }
            if (textLine.TextLineBreak?.TextEndOfLine is { } endOfLine)
            {
                lastTrailingIndex += endOfLine.Length;
            }
        }
        var textPosition = characterHit.FirstCharacterIndex + characterHit.TrailingLength;
        var isTrailing = lastTrailingIndex == textPosition && characterHit.TrailingLength > 0 || y > Height;
        if (textPosition == textLine.FirstTextSourceIndex + textLine.Length)
        {
            textPosition -= textLine.NewLineLength;
        }
        if (textLine.NewLineLength > 0 && textPosition + textLine.NewLineLength == characterHit.FirstCharacterIndex + characterHit.TrailingLength)
        {
            characterHit = new CharacterHit(characterHit.FirstCharacterIndex);
        }
        return new TextHitTestResult(characterHit, textPosition, isInside, isTrailing);
    }

    private static bool MathUtilitiesAreClose(double a, double b) => Math.Abs(a - b) < 1e-6 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

    public void Dispose()
    {
        foreach (var line in _lines)
        {
            line.Text.Dispose();
        }
        _lines.Clear();
    }

    private readonly record struct Line(TextLine Text, int Start, int Length, bool IsEmpty)
    {
        public double Top { get; init; }
    }

    private sealed class LineList(List<Line> lines) : IReadOnlyList<TextLine>
    {
        public TextLine this[int index] => lines[index].Text;

        public int Count => lines.Count;

        public IEnumerator<TextLine> GetEnumerator()
        {
            foreach (var line in lines)
            {
                yield return line.Text;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class EmptySource : ITextSource
    {
        public static readonly EmptySource Instance = new();

        public TextRun? GetTextRun(int textSourceIndex) => null;
    }
}
