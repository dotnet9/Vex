using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

internal sealed class MarkdownSearchHighlighter : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private IReadOnlyList<MarkdownSearchMatch> _matches = [];
    private int _currentIndex = -1;

    internal MarkdownSearchHighlighter(TextEditor editor)
    {
        _editor = editor;
        _editor.TextArea.TextView.BackgroundRenderers.Add(this);
    }

    internal int MatchCount => _matches.Count;
    internal int CurrentMatchIndex => _currentIndex;
    public KnownLayer Layer => KnownLayer.Background;

    internal void Detach() => _editor.TextArea.TextView.BackgroundRenderers.Remove(this);

    internal void SetMatches(IReadOnlyList<MarkdownSearchMatch> matches, int currentIndex)
    {
        _matches = matches;
        _currentIndex = matches.Count == 0 ? -1 : currentIndex;
        _editor.TextArea.TextView.Redraw();
    }

    internal IBrush? BackgroundBrushAt(int offset) => GetBackgroundBrush(offset);

    internal IReadOnlyList<HighlightRectangle> GetHighlightRectangles(VisualLine line)
    {
        List<HighlightRectangle> rectangles = [];
        for (var index = 0; index < _matches.Count; index++)
        {
            var match = _matches[index];
            var start = Math.Max(match.Index, line.StartOffset);
            var end = Math.Min(match.Index + match.Length, line.LastDocumentLine.EndOffset);
            if (end <= start)
            {
                continue;
            }

            var startColumn = line.GetVisualColumn(start);
            var endColumn = line.GetVisualColumn(end);
            var top = line.GetVisualPosition(startColumn, VisualYPosition.LineTop);
            var bottom = line.GetVisualPosition(endColumn, VisualYPosition.LineBottom);
            rectangles.Add(new HighlightRectangle(
                new Rect(top.X, top.Y, Math.Max(0, bottom.X - top.X), Math.Max(0, bottom.Y - top.Y)),
                GetBackgroundBrush(start)));
        }

        return rectangles;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
        {
            return;
        }

        foreach (var line in textView.VisualLines)
        {
            foreach (var highlight in GetHighlightRectangles(line))
            {
                drawingContext.DrawRectangle(highlight.Brush, null, highlight.Bounds);
            }
        }
    }

    private IBrush? GetBackgroundBrush(int offset)
    {
        for (var index = 0; index < _matches.Count; index++)
        {
            var match = _matches[index];
            if (offset >= match.Index && offset < match.Index + match.Length)
            {
                return index == _currentIndex ? CurrentBrush : HighlightBrush;
            }
        }

        return null;
    }

    private static readonly IBrush HighlightBrush =
        new ImmutableSolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xD5, 0x4F));
    private static readonly IBrush CurrentBrush =
        new ImmutableSolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0x98, 0x00));

    internal readonly record struct HighlightRectangle(Rect Bounds, IBrush? Brush);
}
