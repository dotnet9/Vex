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
        var extending = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_selectionController.Begin(documentPoint, extending))
        {
            _pointerSelectionState = new MarkdownPointerSelectionState(e.GetPosition(this));
            _nativeSelectionTextBlock = FindSelectableTextBlock(e.Source as Visual);
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
        _selectionController.SetBlocks(_renderedBlocks.Select(block =>
            new MarkdownSelectionBlock(
                block.Control,
                block.Start,
                new MarkdownTextSpan(block.PlainTextStart, block.PlainTextEnd),
                block.PlainText)), _documentHost, preserveSelection);
        UpdateSelectionState();
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
