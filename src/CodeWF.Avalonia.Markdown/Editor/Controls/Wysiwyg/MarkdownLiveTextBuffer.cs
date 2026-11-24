using CodeWF.Avalonia.Markdown.Core.Blocks;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>
/// 即时编辑的全文缓存：块模型列表（与宿主视图共享同一实例）是唯一真相，全文文本是派生缓存。
/// 纯文本编辑只重算被编辑组，正文以分段 rope 保存；仅在读取全文的边界物化为字符串。
/// </summary>
internal sealed class MarkdownLiveTextBuffer(List<MarkdownBlockModel> models)
{
    private MarkdownTextRope _rope = MarkdownTextRope.FromString(string.Empty);
    private string? _text = string.Empty;
    private List<MarkdownGroupSpan> _spans = [];
    private SpanStartIndex _spanStarts = new(0);
    private int _length;
    private int _lineCount = 1;

    public string Text => _text ??= _rope.ToString();
    public int TextLength => _length;
    public int LineCount => _lineCount;
    public int Version { get; private set; }
    internal bool IsTextMaterialized => _text is not null;

    public void Reset()
    {
        var (text, spans) = MarkdownBlockParser.WriteWithSpans(models);
        var changed = !_rope.ContentEquals(text);
        _text = text;
        _rope = MarkdownTextRope.FromString(text);
        _spans = spans;
        _spanStarts = new SpanStartIndex(_spans.Count);
        _length = text.Length;
        _lineCount = 1 + text.AsSpan().Count('\n');
        if (changed) Version++;
    }

    public bool Splice(int modelIndex)
    {
        if (modelIndex < 0 || modelIndex >= models.Count || _spans.Count == 0) return false;
        var spanIndex = FindSpanIndex(modelIndex);
        if (spanIndex < 0) return false;
        var span = _spans[spanIndex];
        if (span.Source is null || span.Source.IsGenerated) return false;

        var lastModel = (spanIndex + 1 < _spans.Count ? _spans[spanIndex + 1].FirstBlockIndex : models.Count) - 1;
        var group = new List<MarkdownBlockModel>(lastModel - span.FirstBlockIndex + 1);
        for (var i = span.FirstBlockIndex; i <= lastModel; i++) group.Add(models[i]);
        var segment = MarkdownBlockParser.WriteGroupSegment(group, span.Source, isLast: spanIndex == _spans.Count - 1);
        var start = GetSpanStart(spanIndex);
        var oldSegment = GetText(start, span.Length);
        if (string.Equals(oldSegment, segment, StringComparison.Ordinal)) return true;

        _rope = _rope.Replace(start, span.Length, segment);
        _text = null;
        var delta = segment.Length - span.Length;
        _length += delta;
        _spans[spanIndex] = span with { Length = segment.Length };
        if (delta != 0) _spanStarts.AddSuffix(spanIndex + 1, delta);
        _lineCount += segment.AsSpan().Count('\n') - oldSegment.AsSpan().Count('\n');
        Version++;
        return true;
    }

    public (int FirstBlockIndex, int LastBlockIndex, int Start, int OldLength)? GetGroupBounds(int modelIndex)
    {
        if (modelIndex < 0 || modelIndex >= models.Count) return null;
        var spanIndex = FindSpanIndex(modelIndex);
        if (spanIndex < 0) return null;
        var span = _spans[spanIndex];
        var lastModel = (spanIndex + 1 < _spans.Count ? _spans[spanIndex + 1].FirstBlockIndex : models.Count) - 1;
        return (span.FirstBlockIndex, lastModel, GetSpanStart(spanIndex), span.Length);
    }

    public string GetText(int start, int length)
    {
        start = Math.Clamp(start, 0, _length);
        length = Math.Clamp(length, 0, _length - start);
        return _text?.Substring(start, length) ?? _rope.Substring(start, length);
    }

    public (int Line, int Column) GetLineColumn(int offset) => _rope.GetLineColumn(offset);

    internal (Func<string> Materializer, int Length) CaptureText()
    {
        var rope = _rope;
        return (() => rope.ToString(), _length);
    }

    private int GetSpanStart(int spanIndex) => _spans[spanIndex].Start + _spanStarts.Get(spanIndex);

    private int FindSpanIndex(int modelIndex)
    {
        var lo = 0;
        var hi = _spans.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (_spans[mid].FirstBlockIndex <= modelIndex) lo = mid;
            else hi = mid - 1;
        }
        if (lo < 0 || lo >= _spans.Count) return -1;
        var lastModel = (lo + 1 < _spans.Count ? _spans[lo + 1].FirstBlockIndex : models.Count) - 1;
        return modelIndex >= _spans[lo].FirstBlockIndex && modelIndex <= lastModel ? lo : -1;
    }

    private sealed class SpanStartIndex(int count)
    {
        private readonly int[] _tree = new int[count + 1];

        public void AddSuffix(int start, int delta) => Add(start, delta);

        public int Get(int index)
        {
            var value = 0;
            for (var i = index + 1; i > 0; i -= i & -i) value += _tree[i];
            return value;
        }

        private void Add(int index, int delta)
        {
            for (var i = index + 1; i < _tree.Length; i += i & -i) _tree[i] += delta;
        }
    }
}
