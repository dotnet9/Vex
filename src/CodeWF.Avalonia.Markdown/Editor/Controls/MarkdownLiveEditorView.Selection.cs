using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

using CodeWF.Avalonia.Markdown.Core.Blocks;

public partial class MarkdownLiveEditorView
{
    private IPointer? _documentPointer;
    private (int Block, int Offset) _selectionAnchor;
    private (int Block, int Offset) _selectionEnd;
    private (int Start, int Length)? _documentSelection;
    private bool _hasSelectionAnchor;
    private int[]? _selectionContentStarts;
    private int _selectionMappedLength;

    private void InitializeDocumentSelection()
    {
        AddHandler(PointerPressedEvent, OnDocumentPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnDocumentPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_documentPointer != e.Pointer) return;
            _documentPointer = null; e.Pointer.Capture(null); e.Handled = true;
            UpdateDocumentSelection();
        }, RoutingStrategies.Tunnel);
        PointerCaptureLost += (_, _) => _documentPointer = null;
        AddHandler(TextInputEvent, (_, e) =>
        {
            if (_documentSelection is null || string.IsNullOrEmpty(e.Text)) return;
            ReplaceDocumentSelection(e.Text); e.Handled = true;
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnDocumentSelectionKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.Escape) { HideSelectionBar(); return; }
            if (SelectionTextBox is not { IsKeyboardFocusWithin: true } box) return;
            if (box.SelectionStart == box.SelectionEnd) { HideSelectionBar(); return; }
            if (FindBlockView(_blockViews[_activeIndex]) is { } block
                && block.GetTextBounds(box.SelectionEnd) is { } anchor
                && block.TranslatePoint(anchor.TopLeft, this) is { } point)
                _selectionBarAnchor = new Rect(point, anchor.Size);
            ShowSelectionBar();
        }, RoutingStrategies.Bubble, true);
    }

    private IEnumerable<MarkdownBlockView> RealizedTextBlocks => _blockHost.GetVisualDescendants()
        .OfType<MarkdownBlockView>().Where(view => _models.Contains(view.Model));

    private void OnDocumentPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            HideSelectionBar();
            UpdateContextSourceLine(e.GetPosition(this));
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.Source is not Visual visual) return;
        if (MarkdownBlockView.IsInteractiveChild(visual)) return;
        var view = (visual as MarkdownBlockView) ?? visual.GetVisualAncestors().OfType<MarkdownBlockView>().FirstOrDefault();
        if (view is null || !_models.Contains(view.Model)) return; // 表格单元格保留自身的输入逻辑。
        var textBox = (visual as TextBox) ?? visual.GetVisualAncestors().OfType<TextBox>().FirstOrDefault();
        if (textBox is not null && textBox != view.EditingInput) return; // 代码类型输入框。
        var point = (_models.IndexOf(view.Model), view.GetPointerTextOffset(e));
        var extend = e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _hasSelectionAnchor;
        if (!extend) ClearDocumentSelection();
        view.BeginEdit(point.Item2);
        if (!extend) _selectionAnchor = point;
        _selectionEnd = point; _hasSelectionAnchor = true;
        if (e.ClickCount >= 3)
        {
            _selectionAnchor = (point.Item1, 0); _selectionEnd = (point.Item1, view.CurrentText.Length);
        }
        else if (e.ClickCount == 2)
        {
            var text = view.CurrentText; var start = point.Item2; var end = start;
            while (start > 0 && IsWordCharacter(text[start - 1])) start--;
            while (end < text.Length && IsWordCharacter(text[end])) end++;
            if (end == start && end < text.Length) end++;
            _selectionAnchor = (point.Item1, start); _selectionEnd = (point.Item1, end);
        }
        UpdateDocumentSelection();
        _documentPointer = e.Pointer; e.Pointer.Capture(this); e.Handled = true;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    private void OnDocumentPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_documentPointer != e.Pointer || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // 只检查已物化的块，不为选择而构造整篇长文档。
        var y = e.GetPosition(_blockHost).Y;
        var view = RealizedTextBlocks.OrderBy(block =>
        {
            var top = block.TranslatePoint(default, _blockHost)?.Y ?? double.PositiveInfinity;
            return y < top ? top - y : Math.Max(0, y - top - block.Bounds.Height);
        }).FirstOrDefault();
        if (view is null) return;
        _selectionEnd = (_models.IndexOf(view.Model), view.GetPointerTextOffset(e));
        UpdateDocumentSelection(); e.Handled = true;
        if (_blockHost.ScrollHost is { } scroll)
        {
            var position = e.GetPosition(scroll).Y;
            if (position < 16 || position > scroll.Viewport.Height - 16)
                scroll.Offset = new Vector(scroll.Offset.X, Math.Max(0, scroll.Offset.Y + (position < 16 ? -24 : 24)));
        }
    }

    private void UpdateDocumentSelection()
    {
        var start = _selectionAnchor; var end = _selectionEnd;
        if (start.Block > end.Block || start.Block == end.Block && start.Offset > end.Offset) (start, end) = (end, start);
        if (start.Block != end.Block)
        {
            var first = GetContentStart(start.Block) + start.Offset;
            var last = GetContentStart(end.Block) + end.Offset;
            _documentSelection = (first, Math.Max(0, last - first));
        }
        else _documentSelection = null;
        foreach (var view in RealizedTextBlocks)
        {
            var index = _models.IndexOf(view.Model);
            view.SetTextSelection(index < start.Block || index > end.Block ? 0 : index == start.Block ? start.Offset : 0,
                index < start.Block || index > end.Block ? 0 : index == end.Block ? end.Offset : view.CurrentText.Length);
        }
        RaiseSelectionChanged();
        // 跨块选区直接弹条；同块选区看编辑输入框的实时选择（拖选/双击取词），空选收条。
        var sameBlock = start.Block == end.Block;
        var hasText = !sameBlock || (start.Block >= 0 && start.Block < _blockViews.Count
            && FindActiveEditor(_blockViews[start.Block]) is { } box && box.SelectionStart != box.SelectionEnd);
        if (!sameBlock || hasText)
        {
            if (RealizedTextBlocks.FirstOrDefault(view => _models.IndexOf(view.Model) == _selectionEnd.Block) is { } endpoint
                && endpoint.GetTextBounds(_selectionEnd.Offset) is { } anchor
                && endpoint.TranslatePoint(anchor.TopLeft, this) is { } editorAnchor)
                _selectionBarAnchor = new Rect(editorAnchor, anchor.Size);
            if (_documentPointer is null) ShowSelectionBar();
            else HideSelectionBar();
        }
        else HideSelectionBar();
    }

    private (int Start, int Length)? GetTableSelection(MarkdownTableView table)
    {
        var cell = table.ActiveCell ?? table.FirstCell;
        if (cell is null) return null;
        var position = table.ActiveCellPosition ?? (0, 0);
        if (FindTableCellSpan(position.Row, position.Column) is not { } span) return null;
        var box = cell.EditingInput;
        var start = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, span.Length);
        var length = Math.Clamp(Math.Abs(box.SelectionEnd - box.SelectionStart), 0, span.Length - start);
        return (span.Start + start, length);
    }

    private (int Start, int Length)? FindTableCellSpan(int row, int column)
    {
        var text = Text;
        var lineStart = GetContentStart(_activeIndex);
        var sourceRow = row;
        var firstLineEnd = text.IndexOf('\n', lineStart);
        if (row > 0 && firstLineEnd >= 0 && IsTableDelimiterLine(text, firstLineEnd + 1)) sourceRow++;
        for (var currentRow = 0; currentRow <= sourceRow && lineStart <= text.Length; currentRow++)
        {
            var lineEnd = text.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = text.Length;
            if (currentRow == sourceRow)
            {
                var cells = SplitTableLine(text, lineStart, lineEnd);
                return column >= 0 && column < cells.Count ? cells[column] : null;
            }
            lineStart = lineEnd < text.Length ? lineEnd + 1 : text.Length + 1;
        }
        return null;
    }

    private static bool IsTableDelimiterLine(string text, int start)
    {
        var end = text.IndexOf('\n', start);
        if (end < 0) end = text.Length;
        var cells = SplitTableLine(text, start, end);
        return cells.Count > 0 && cells.All(cell =>
        {
            var value = text.AsSpan(cell.Start, cell.Length).Trim();
            if (value.IsEmpty) return false;
            value = value.Trim(':');
            return value.Length >= 3 && value.IndexOfAnyExcept('-') < 0;
        });
    }

    private static List<(int Start, int Length)> SplitTableLine(string text, int start, int end)
    {
        var cells = new List<(int Start, int Length)>();
        var first = start;
        while (first < end && char.IsWhiteSpace(text[first])) first++;
        var cellStart = first < end && text[first] == '|' ? first + 1 : start;
        for (var i = cellStart; i < end; i++)
        {
            if (text[i] == '\\' && i + 1 < end) { i++; continue; }
            if (text[i] != '|') continue;
            AddTableCell(cells, text, cellStart, i);
            cellStart = i + 1;
        }
        var lastContent = end;
        if (cellStart >= end) return cells;
        while (lastContent > cellStart && char.IsWhiteSpace(text[lastContent - 1])) lastContent--;
        if (lastContent <= cellStart || text[lastContent - 1] != '|')
            AddTableCell(cells, text, cellStart, end);
        return cells;
    }

    private static void AddTableCell(List<(int Start, int Length)> cells, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start])) start++;
        while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
        cells.Add((start, end - start));
    }

    private int GetContentStart(int index)
    {
        EnsureSelectionMap();
        return index >= 0 && index < _selectionContentStarts!.Length ? _selectionContentStarts[index] : _selectionMappedLength;
    }

    private void EnsureSelectionMap()
    {
        if (_selectionContentStarts is not null) return;
        var text = Text; var cursor = 0;
        _selectionMappedLength = text.Length;
        _selectionContentStarts = new int[_models.Count];
        for (var i = 0; i < _models.Count; i++)
        {
            var content = _models[i].Kind == MarkdownBlockKind.Raw ? _models[i].Raw : _models[i].Text;
            if (content.Length == 0)
            {
                _selectionContentStarts[i] = _textBuffer.GetGroupBounds(i)?.Start ?? Math.Min(cursor, text.Length);
                cursor = _selectionContentStarts[i];
                continue;
            }
            var found = text.IndexOf(content, Math.Min(cursor, text.Length), StringComparison.Ordinal);
            if (found < 0) found = Math.Min(cursor, text.Length);
            _selectionContentStarts[i] = found;
            cursor = found + content.Length;
        }
    }

    private void ClearDocumentSelection()
    {
        _documentSelection = null; _hasSelectionAnchor = false;
        HideSelectionBar();
        foreach (var view in RealizedTextBlocks) view.SetTextSelection(0, 0);
    }

    private void OnDocumentSelectionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && _selectionBarPopup is { IsOpen: true })
        {
            HideSelectionBar();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Apps || e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift)
            ContextSourceLine = _activeIndex >= 0 && _activeIndex < _models.Count ? Math.Max(1, _models[_activeIndex].SourceLine) : 1;
        if (e.Source is Visual visual && visual.GetVisualAncestors().OfType<TextBox>().FirstOrDefault() is { } input
            && !RealizedTextBlocks.Any(view => view.EditingInput == input)) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.A)
        {
            SelectDocument(); e.Handled = true; return;
        }
        if (_documentSelection is null) return;
        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.C or Key.X or Key.V)
        {
            ApplyDocumentSelectionAction(e.Key == Key.C ? MarkdownEditorAction.Copy : e.Key == Key.X ? MarkdownEditorAction.Cut : MarkdownEditorAction.Paste);
            e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Back or Key.Delete or Key.Enter)
        {
            ReplaceDocumentSelection(e.Key == Key.Enter ? "\n" : string.Empty); e.Handled = true;
        }
        else if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Left or Key.Right or Key.Escape)
        {
            var range = _documentSelection.Value;
            ClearDocumentSelection(); FocusSourceOffset(e.Key == Key.Right ? range.Start + range.Length : range.Start); e.Handled = true;
        }
    }

    private void SelectDocument()
    {
        if (_models.Count == 0) return;
        ClearDocumentSelection();
        _selectionAnchor = (0, 0); _selectionEnd = (_models.Count - 1, _models[^1].Kind == MarkdownBlockKind.Raw ? _models[^1].Raw.Length : _models[^1].Text.Length);
        _hasSelectionAnchor = true; UpdateDocumentSelection();
        // 全选包括围栏、列表前缀与文档末尾的换行。
        _documentSelection = (0, Text.Length);
    }

    private bool ApplyDocumentSelectionAction(MarkdownEditorAction action)
    {
        if (action == MarkdownEditorAction.SelectAll) { SelectDocument(); return true; }
        if (_documentSelection is null) return false;
        switch (action)
        {
            case MarkdownEditorAction.Copy: _ = CopyDocumentSelectionAsync(false); return true;
            case MarkdownEditorAction.Cut: _ = CopyDocumentSelectionAsync(true); return true;
            case MarkdownEditorAction.Paste: _ = PasteDocumentSelectionAsync(); return true;
            case MarkdownEditorAction.CopyPlainText: _ = CopyPlainTextAsync(); return true;
            case MarkdownEditorAction.Bold: ReplaceDocumentSelection("**" + SelectedText + "**"); return true;
            case MarkdownEditorAction.Italic: ReplaceDocumentSelection("*" + SelectedText + "*"); return true;
            case MarkdownEditorAction.Strikethrough: ReplaceDocumentSelection("~~" + SelectedText + "~~"); return true;
            case MarkdownEditorAction.Highlight: ReplaceDocumentSelection("==" + SelectedText + "=="); return true;
            case MarkdownEditorAction.Superscript: ReplaceDocumentSelection("^" + SelectedText + "^"); return true;
            case MarkdownEditorAction.Subscript: ReplaceDocumentSelection("~" + SelectedText + "~"); return true;
            case MarkdownEditorAction.InlineCode: ReplaceDocumentSelection("`" + SelectedText + "`"); return true;
            default: return false;
        }
    }

    private async Task CopyDocumentSelectionAsync(bool cut)
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        var original = Text; var range = _documentSelection;
        await clipboard.SetTextAsync(SelectedText);
        if (cut && Text == original && _documentSelection == range) ReplaceDocumentSelection(string.Empty);
    }

    private async Task PasteDocumentSelectionAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        var original = Text; var range = _documentSelection;
        var text = await clipboard.TryGetTextAsync();
        if (text is not null && Text == original && _documentSelection == range) ReplaceDocumentSelection(text);
    }

    private void ReplaceDocumentSelection(string replacement)
    {
        if (_documentSelection is not { } range) return;
        var previous = CaptureSnapshot(); var text = Text.Remove(range.Start, range.Length).Insert(range.Start, replacement);
        _restoringHistory = true;
        try { SetText(text); }
        finally { _restoringHistory = false; }
        _lastSnapshot = previous;
        FocusSourceOffset(range.Start + replacement.Length); RaiseChanged();
    }

    private void FocusSourceOffset(int offset)
    {
        var index = 0;
        for (var i = 0; i < _models.Count; i++) { if (GetContentStart(i) > offset) break; index = i; }
        FocusBlock(index);
        if (ActiveTextBox is { } input) input.CaretIndex = input.SelectionStart = input.SelectionEnd = Math.Clamp(offset - GetContentStart(index), 0, input.Text?.Length ?? 0);
    }
}
