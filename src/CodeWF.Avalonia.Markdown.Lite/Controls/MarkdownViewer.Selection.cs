using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Rendering;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

public partial class MarkdownViewer
{
    private void OnViewerPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_documentHost is null
            || e.GetCurrentPoint(this).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed
            || IsLinkPointerSource(e)
            || IsInteractiveSelectionSource(e.Source as Visual))
        {
            return;
        }

        var documentPoint = e.GetPosition(_documentHost);
        // Ctrl+C 的复制处理挂在本控件的 Tunnel KeyDown 上，要求键盘焦点在本控件子树内。
        // 块内原生选择（SelectableTextBlock）不取焦点，宿主焦点也未必落在阅读区——
        // 点击落进阅读区时把焦点收进来，之后 Ctrl+C（选区 → 块内原生 → 整篇兜底）才有路由可走。
        // 不能就地 Focus：Avalonia 原生点击焦点逻辑会在 bubble 阶段清除「点击非可聚焦区」
        // 的焦点（与即时编辑视图同一坑），延后到输入处理完成后再落焦点。
        if (!IsKeyboardFocusWithin)
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => Focus());
        }
        var extending = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_selectionController.Begin(documentPoint, extending))
        {
            _pointerSelectionState = new MarkdownPointerSelectionState(e.GetPosition(this));
            // 表格内的可选文本块不交原生选择（跨格拖选由控制器接管，原生单格选择会截断跨格）；
            // 链接命中检测仍用 FindSelectableTextBlock，不受此排除影响。
            _nativeSelectionTextBlock = FindSelectableTextBlock(e.Source as Visual) is { } native
                && native.FindAncestorOfType<MarkdownTableGrid>() is null ? native : null;
            _isPointerSelecting = extending;
            UpdateSelectionState();
            if (extending) { e.Pointer.Capture(this); e.Handled = true; }
        }
    }

    private void OnViewerPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_documentHost is null || _pointerSelectionState is not { } state)
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed)
        {
            _pointerSelectionState = null;
            _nativeSelectionTextBlock = null;
            _isPointerSelecting = false;
            e.Pointer.Capture(null);
            return;
        }

        if (!state.IsDragging(e.GetPosition(this)))
        {
            return;
        }

        if (!_isPointerSelecting
            && _nativeSelectionTextBlock is { } nativeTextBlock
            && IsPointerInside(nativeTextBlock, e))
        {
            return;
        }

        if (ResolveScrollHost() is { } scroll)
        {
            var pointer = e.GetPosition(scroll);
            var delta = pointer.Y < 0 ? Math.Max(-48, pointer.Y) : pointer.Y > scroll.Viewport.Height ? Math.Min(48, pointer.Y - scroll.Viewport.Height) : 0;
            if (delta != 0) { scroll.Offset = new Vector(scroll.Offset.X, Math.Clamp(scroll.Offset.Y + delta, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height))); scroll.UpdateLayout(); }
        }
        if (_selectionController.Extend(e.GetPosition(_documentHost)))
        {
            _isPointerSelecting = true;
            e.Pointer.Capture(this);
            UpdateSelectionState();
            e.Handled = true;
            return;
        }

        if (_isPointerSelecting)
        {
            e.Handled = true;
        }
    }

    private void OnViewerPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pointerSelectionState is null)
        {
            return;
        }

        if (_isPointerSelecting)
        {
            e.Handled = true;
        }
        else
        {
            _selectionController.CommitClickWithoutDrag();
            UpdateSelectionState();
        }

        _pointerSelectionState = null;
        _nativeSelectionTextBlock = null;
        _isPointerSelecting = false;
        e.Pointer.Capture(null);
    }

    private void RefreshSelectionBlocks(bool preserveSelection = false)
    {
        // 表格按单元格展开为子块（同块键 + SubIndex 区分）：跨格拖选命中到具体格，
        // 复制按行 Tab、跨行换行拼接——网页表格复制语义；其余块保持一表一项。
        var blocks = new List<MarkdownSelectionBlock>();
        foreach (var block in _renderedBlocks)
        {
            if (block.Kind == MarkdownBlockKind.Table && block.Control is MarkdownTableBlock tableBlock)
            {
                var subIndex = 0;
                foreach (var cell in EnumerateTableCells(tableBlock))
                {
                    var text = ExtractCellPlainText(cell);
                    if (text.Length == 0)
                    {
                        continue;
                    }

                    blocks.Add(new MarkdownSelectionBlock(
                        cell,
                        block.Start,
                        new MarkdownTextSpan(0, text.Length),
                        text,
                        subIndex++,
                        Grid.GetRow(cell),
                        Grid.GetColumn(cell)));
                }

                continue;
            }

            blocks.Add(new MarkdownSelectionBlock(
                block.Control,
                block.Start,
                new MarkdownTextSpan(block.PlainTextStart, block.PlainTextEnd),
                block.PlainText));
        }

        _selectionController.SetBlocks(blocks, _documentHost, preserveSelection);
        UpdateSelectionState();
    }

    /// <summary>表格块内的单元格 Border，按（行,列）文档顺序枚举。</summary>
    private static IEnumerable<Border> EnumerateTableCells(MarkdownTableBlock tableBlock)
    {
        var grid = tableBlock.GetVisualDescendants().OfType<MarkdownTableGrid>().FirstOrDefault();
        if (grid is null)
        {
            yield break;
        }

        foreach (var cell in grid.Children
            .OfType<Border>()
            .OrderBy(Grid.GetRow)
            .ThenBy(Grid.GetColumn))
        {
            yield return cell;
        }
    }

    /// <summary>单元格纯文本：段控件与可选文本块按视觉序拼接，块级子内容换行分隔。</summary>
    private static string ExtractCellPlainText(Border cell)
    {
        var pieces = new List<string>();
        foreach (var descendant in cell.GetVisualDescendants())
        {
            if (descendant is MarkdownTextSegmentView segment && segment.PlainText.Length > 0)
            {
                pieces.Add(segment.PlainText);
            }
            else if (descendant is SelectableTextBlock textBlock)
            {
                var text = textBlock.Text;
                if (string.IsNullOrEmpty(text) && textBlock.Inlines is { } inlines)
                {
                    text = string.Join("", inlines.OfType<global::Avalonia.Controls.Documents.Run>()
                        .Select(run => run.Text ?? ""));
                }

                if (!string.IsNullOrEmpty(text))
                {
                    pieces.Add(text);
                }
            }
        }

        return string.Join("\n", pieces);
    }

    private void ResetSelectionState()
    {
        _selectionController.Clear();
        _pointerSelectionState = null;
        _nativeSelectionTextBlock = null;
        _isPointerSelecting = false;
        UpdateSelectionState();
    }

    private void UpdateSelectionState()
    {
        var selectedText = _selectionController.SelectedText;
        var hasSelection = !string.IsNullOrEmpty(selectedText);
        var changed = selectedText != SelectedText || hasSelection != HasSelection;
        SelectedText = selectedText;
        HasSelection = hasSelection;
        if (changed)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool IsInteractiveSelectionSource(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button
                or MenuItem
                or CheckBox
                or ScrollBar
                or IMarkdownInteractiveVisual)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerInside(Control control, PointerEventArgs e)
    {
        var point = e.GetPosition(control);
        return new Rect(control.Bounds.Size).Contains(point);
    }

    private static bool IsLinkPointerSource(PointerEventArgs e)
    {
        if (FindSelectableTextBlock(e.Source as Visual) is not { } textBlock)
        {
            return false;
        }

        string? url;
        return MarkdownLinkInteraction.TryGetLinkAtPointer(textBlock, e, out url);
    }

}
