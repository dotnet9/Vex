using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.Media;
using global::Avalonia.Media.Immutable;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>查找按块模型计数；高亮只应用于已物化文本，不改变文档与行内格式。</summary>
public partial class MarkdownViewer
{
    public static readonly StyledProperty<IBrush?> SearchHighlightBrushProperty = AvaloniaProperty.Register<MarkdownViewer, IBrush?>(
        nameof(SearchHighlightBrush), new ImmutableSolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xD5, 0x4F)));
    public static readonly StyledProperty<IBrush?> SearchCurrentBrushProperty = AvaloniaProperty.Register<MarkdownViewer, IBrush?>(
        nameof(SearchCurrentBrush), new ImmutableSolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0x98, 0x00)));
    public IBrush? SearchHighlightBrush { get => GetValue(SearchHighlightBrushProperty); set => SetValue(SearchHighlightBrushProperty, value); }
    public IBrush? SearchCurrentBrush { get => GetValue(SearchCurrentBrushProperty); set => SetValue(SearchCurrentBrushProperty, value); }
    public event EventHandler? SearchChanged;

    private string? _searchText;
    private bool _searchCaseSensitive;
    private bool _searchWholeWord;
    private readonly List<SearchMatch> _searchMatches = [];
    private int _searchCurrentIndex = -1;
    private readonly List<SearchRunEdit> _searchModified = [];
    private readonly HashSet<SelectableTextBlock> _searchPainted = [];
    private readonly HashSet<Control> _searchFormulaControls = [];
    private readonly Dictionary<SelectableTextBlock, string> _searchPlainBlocks = [];
    private bool _searchPaintQueued;
    public int MatchCount => _searchMatches.Count;
    public int CurrentMatchIndex => _searchCurrentIndex;

    /// <summary>全文明文查找，包含离屏块与代码正文；空白查询清除结果。</summary>
    public int Find(string searchText, bool caseSensitive = false, bool wholeWord = false)
    {
        if (string.IsNullOrWhiteSpace(searchText)) { ClearSearch(); return 0; }
        _searchText = searchText; _searchCaseSensitive = caseSensitive; _searchWholeWord = wholeWord;
        LayoutUpdated -= OnSearchLayoutUpdated; LayoutUpdated += OnSearchLayoutUpdated;
        ReapplySearch(); SearchChanged?.Invoke(this, EventArgs.Empty);
        return MatchCount;
    }
    public bool FindNext() => MoveSearchMatch(1);
    public bool FindPrevious() => MoveSearchMatch(-1);
    public void ClearSearch()
    {
        LayoutUpdated -= OnSearchLayoutUpdated;
        RestoreSearchInlines(); _searchText = null; _searchMatches.Clear(); _searchCurrentIndex = -1;
        SearchChanged?.Invoke(this, EventArgs.Empty);
    }
    private void ReapplySearchIfActive()
    {
        if (_searchText is null) return;
        ReapplySearch(); SearchChanged?.Invoke(this, EventArgs.Empty);
    }
    private bool MoveSearchMatch(int direction)
    {
        if (MatchCount == 0) return false;
        _searchCurrentIndex = _searchCurrentIndex < 0 ? direction > 0 ? 0 : MatchCount - 1
            : (_searchCurrentIndex + direction + MatchCount) % MatchCount;
        ScrollToBlock(_searchMatches[_searchCurrentIndex].BlockIndex);
        PaintRealizedSearch(); ApplyCurrentMatchBrush(); SearchChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
    private void ReapplySearch()
    {
        RestoreSearchInlines(); _searchMatches.Clear(); _searchCurrentIndex = -1;
        if (_searchText is null) return;
        for (var i = 0; i < _renderedModel.Blocks.Count; i++)
            foreach (var offset in FindAllMatchOffsets(_renderedModel.Blocks[i].PlainText))
                _searchMatches.Add(new SearchMatch(i, offset, _searchText.Length));
        PaintRealizedSearch();
    }
    private void OnSearchLayoutUpdated(object? sender, EventArgs e)
    {
        if (_searchPaintQueued || _searchText is null) return;
        _searchPaintQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _searchPaintQueued = false;
            if (_searchText is not null) PaintRealizedSearch();
        }, DispatcherPriority.Background);
    }
    private void PaintRealizedSearch()
    {
        if (_searchText is null) return;
        // 回收的文本不再被搜索状态引用；长文档滚动不会累积已释放的控件。
        foreach (var text in _searchPainted.Where(text => TopLevel.GetTopLevel(text) is null).ToArray()) RestoreSearchInlines(text);
        foreach (var formula in _searchFormulaControls.Where(formula => TopLevel.GetTopLevel(formula) is null).ToArray()) RestoreFormulaSearch(formula);
        for (var i = 0; i < _renderedBlocks.Count; i++)
        {
            if (!IsBlockRealized(i)) continue;
            var root = _renderedBlocks[i].Control;
            var plainText = _renderedModel.Blocks[i].PlainText;

            // 自绘文本段：高亮区间整表下发（段纯文本与查找偏移同源，段内自行夹取），不走 Run 拆分投影。
            IMarkdownSearchHighlightVisual? searchVisual = root as IMarkdownSearchHighlightVisual;
            searchVisual ??= root.GetVisualDescendants().OfType<IMarkdownSearchHighlightVisual>().FirstOrDefault();
            if (searchVisual is not null)
            {
                ApplySegmentSearch(searchVisual, i);
                continue;
            }

            var projections = MarkdownTextProjections.Create(root, plainText).ToArray();
            foreach (var text in projections.Where(item => item.TextBlock is not null).Select(item => item.TextBlock!).Distinct())
            {
                if (text.Tag as string == "CodeLineNumbers" || !_searchPainted.Add(text)) continue;
                var matches = _searchMatches.Where(match => match.BlockIndex == i).ToArray();
                var textProjections = projections.Where(item => item.TextBlock == text).ToArray();
                if (textProjections.All(item => !matches.Any(match => Overlaps(match, item.Start, item.Length)))) continue;
                if (text.Inlines is null || text.Inlines.Count == 0)
                {
                    var buffer = text.Text ?? string.Empty;
                    _searchPlainBlocks[text] = buffer;
                    text.Text = null;
                    var run = new Run(buffer);
                    text.Inlines = new InlineCollection { run };
                    PaintRun(text, run, textProjections[0].Start, matches);
                }
                else
                {
                    foreach (var projection in textProjections)
                    {
                        if (projection.Run is { } run) PaintRun(text, run, projection.Start, matches);
                    }
                }
            }

            foreach (var projection in projections.Where(item => item.TextBlock is null))
            {
                foreach (var match in _searchMatches.Where(match => match.BlockIndex == i && Overlaps(match, projection.Start, projection.Length)))
                {
                    match.FormulaControls.Add(projection.Control);
                    _searchFormulaControls.Add(projection.Control);
                }
            }
        }
        ApplyCurrentMatchBrush();
    }
    private static bool Overlaps(SearchMatch match, int start, int length) =>
        match.Offset < start + length && match.Offset + match.Length > start;
    private void ApplySegmentSearch(IMarkdownSearchHighlightVisual visual, int blockIndex)
    {
        var entries = new List<(int Start, int Length, IBrush Brush)>();
        for (var m = 0; m < _searchMatches.Count; m++)
        {
            var match = _searchMatches[m];
            if (match.BlockIndex != blockIndex) continue;
            entries.Add((match.Offset, match.Length, m == _searchCurrentIndex ? SearchCurrentBrush! : SearchHighlightBrush!));
        }
        visual.ApplySearchHighlights(entries);
    }
    private void PaintRun(SelectableTextBlock owner, Run source, int runStart, SearchMatch[] matches)
    {
        if (source.Text is not { Length: > 0 } text) return;
        var overlapping = matches.Where(match => Overlaps(match, runStart, text.Length)).ToArray();
        if (overlapping.Length == 0) return;
        var inserted = new List<Run>();
        var position = 0;
        foreach (var match in overlapping)
        {
            var start = Math.Max(0, match.Offset - runStart);
            var end = Math.Min(text.Length, match.Offset + match.Length - runStart);
            if (start > position) inserted.Add(CopyRun(source, text[position..start]));
            var marked = CopyRun(source, text[start..end]);
            inserted.Add(marked); match.Runs.Add(marked); position = end;
        }
        if (position < text.Length) inserted.Add(CopyRun(source, text[position..]));
        var inlines = owner.Inlines!;
        var collection = FindInlineCollection(inlines, source);
        var index = collection.IndexOf(source);
        source.Text = string.Empty;
        for (var n = 0; n < inserted.Count; n++) collection.Insert(index + 1 + n, inserted[n]);
        _searchModified.Add(new SearchRunEdit(owner, collection, source, text, inserted));
    }
    private static InlineCollection FindInlineCollection(InlineCollection inlines, Run target)
    {
        foreach (var inline in inlines)
        {
            if (inline == target) return inlines;
            if (inline is Span span && span.Inlines.Contains(target)) return FindInlineCollection(span.Inlines, target);
        }
        return inlines;
    }
    private static Run CopyRun(Run source, string text) => new(text)
    {
        FontFamily = source.FontFamily, FontSize = source.FontSize, FontStyle = source.FontStyle,
        FontWeight = source.FontWeight, FontStretch = source.FontStretch, Foreground = source.Foreground,
        Background = source.Background, BaselineAlignment = source.BaselineAlignment, TextDecorations = source.TextDecorations
    };
    private IEnumerable<int> FindAllMatchOffsets(string text)
    {
        var comparison = _searchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        for (var offset = text.IndexOf(_searchText!, comparison); offset >= 0; offset = text.IndexOf(_searchText!, offset + _searchText!.Length, comparison))
            if (!_searchWholeWord || IsWholeWordBoundary(text, offset, _searchText!.Length)) yield return offset;
    }
    private static bool IsWholeWordBoundary(string text, int start, int length)
    {
        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        return (start == 0 || !Word(text[start - 1])) && (start + length >= text.Length || !Word(text[start + length]));
    }
    private void ApplyCurrentMatchBrush()
    {
        foreach (var formula in _searchFormulaControls) MarkdownFormulaState.SetSearchBrush(formula, null);
        for (var i = 0; i < _searchMatches.Count; i++)
        {
            foreach (var run in _searchMatches[i].Runs) run.Background = i == _searchCurrentIndex ? SearchCurrentBrush : SearchHighlightBrush;
            foreach (var formula in _searchMatches[i].FormulaControls) MarkdownFormulaState.SetSearchBrush(formula, SearchHighlightBrush);
        }
        if (_searchCurrentIndex >= 0 && _searchCurrentIndex < _searchMatches.Count)
        {
            foreach (var formula in _searchMatches[_searchCurrentIndex].FormulaControls)
                MarkdownFormulaState.SetSearchBrush(formula, SearchCurrentBrush);
        }
    }
    private void RestoreSearchInlines(SelectableTextBlock? owner = null)
    {
        foreach (var edit in _searchModified.Where(edit => owner is null || edit.Owner == owner).ToArray())
        {
            foreach (var run in edit.Inserted) edit.Collection.Remove(run);
            edit.Source.Text = edit.OriginalText;
            foreach (var match in _searchMatches) match.Runs.RemoveAll(edit.Inserted.Contains);
            _searchModified.Remove(edit);
        }
        foreach (var (text, plain) in _searchPlainBlocks.Where(pair => owner is null || pair.Key == owner).ToArray())
        {
            text.Inlines = null; text.Text = plain; _searchPlainBlocks.Remove(text);
        }
        RestoreFormulaSearch(owner);
        if (owner is null)
        {
            // 自绘文本段的高亮区间整表替换式下发：全局清除时同步清空。
            foreach (var block in _renderedBlocks)
            {
                if (block.Control is IMarkdownSearchHighlightVisual selfHighlight) selfHighlight.ApplySearchHighlights([]);
                else if (block.Control.GetVisualDescendants().OfType<IMarkdownSearchHighlightVisual>().FirstOrDefault() is { } childHighlight) childHighlight.ApplySearchHighlights([]);
            }
        }
        if (owner is null) _searchPainted.Clear(); else _searchPainted.Remove(owner);
    }
    private void RestoreFormulaSearch(Control? owner = null)
    {
        foreach (var formula in _searchFormulaControls.Where(formula => owner is null || formula == owner).ToArray())
        {
            MarkdownFormulaState.SetSearchBrush(formula, null);
            foreach (var match in _searchMatches) match.FormulaControls.Remove(formula);
            _searchFormulaControls.Remove(formula);
        }
    }
    private sealed record SearchRunEdit(SelectableTextBlock Owner, InlineCollection Collection, Run Source, string OriginalText, List<Run> Inserted);
    private sealed record SearchMatch(int BlockIndex, int Offset, int Length)
    {
        public List<Run> Runs { get; } = [];
        public HashSet<Control> FormulaControls { get; } = [];
    }
}
