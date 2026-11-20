using CodeWF.Avalonia.Markdown.Core.Blocks;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>
/// 即时编辑的全文缓存：块模型列表（与宿主视图共享同一实例）是唯一真相，全文文本是派生缓存。
/// <para>
/// 纯文本编辑的热路径只重算被编辑组（共享同一 <see cref="MarkdownBlockSource"/> 的连续块）的片段，
/// 拼接回缓存文本；结构变化（拆块/合并/转换/增删）走整体重算。组区间由
/// <see cref="MarkdownBlockParser.WriteWithSpans"/> 记录，保证缓存文本与
/// <see cref="MarkdownBlockParser.Write"/> 的输出逐字符一致。
/// </para>
/// </summary>
internal sealed class MarkdownLiveTextBuffer(List<MarkdownBlockModel> models)
{
    private string _text = string.Empty;
    private List<MarkdownGroupSpan> _spans = [];
    private int _lineCount = 1;

    /// <summary>当前全文（与 <see cref="MarkdownBlockParser.Write"/> 对同一模型列表的输出一致）。</summary>
    public string Text => _text;

    /// <summary>全文行数（按「1 + 换行数」口径，与整篇扫描一致）。</summary>
    public int LineCount => _lineCount;

    /// <summary>模型整体重建（载入文档或结构变化后）：整篇重算并记录组区间。</summary>
    public void Reset()
    {
        (_text, _spans) = MarkdownBlockParser.WriteWithSpans(models);
        _lineCount = 1 + _text.AsSpan().Count('\n');
    }

    /// <summary>
    /// 只重算 <paramref name="modelIndex"/> 所在组的片段并拼接回全文。
    /// 返回 <c>false</c> 表示该组无法增量（生成组或区间簿记缺失），调用方应走 <see cref="Reset"/>；
    /// 返回 <c>true</c> 表示已按增量处理——片段与缓存一致时文本引用不变，
    /// 调用方可用引用比较（<see cref="object.ReferenceEquals"/>）判断全文是否变化。
    /// </summary>
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
        var oldSegment = _text.Substring(span.Start, span.Length);
        if (string.Equals(oldSegment, segment, StringComparison.Ordinal)) return true;

        _text = string.Concat(_text.AsSpan(0, span.Start), segment, _text.AsSpan(span.Start + span.Length));
        var delta = segment.Length - span.Length;
        _spans[spanIndex] = span with { Length = segment.Length };
        for (var i = spanIndex + 1; i < _spans.Count; i++)
        {
            _spans[i] = _spans[i] with { Start = _spans[i].Start + delta };
        }

        _lineCount += segment.AsSpan().Count('\n') - oldSegment.AsSpan().Count('\n');
        return true;
    }

    /// <summary>块下标所在组的区间（增量维护选区映射等派生数据用）；组不存在时返回 null。</summary>
    public (int FirstBlockIndex, int LastBlockIndex, int Start, int OldLength)? GetGroupBounds(int modelIndex)
    {
        if (modelIndex < 0 || modelIndex >= models.Count) return null;
        var spanIndex = FindSpanIndex(modelIndex);
        if (spanIndex < 0) return null;
        var span = _spans[spanIndex];
        var lastModel = (spanIndex + 1 < _spans.Count ? _spans[spanIndex + 1].FirstBlockIndex : models.Count) - 1;
        return (span.FirstBlockIndex, lastModel, span.Start, span.Length);
    }

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
}
