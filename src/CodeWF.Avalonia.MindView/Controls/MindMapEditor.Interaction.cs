using CodeWF.Avalonia.MindView;
using global::Avalonia;
using global::Avalonia.Animation;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CodeWF.Avalonia.MindView.Controls;

public partial class MindMapEditor
{
    private void HandleTitleKeyDown(MindMapNode node, TextBox? editor, KeyEventArgs e)
    {
        if (ReferenceEquals(_lastHandledEditorKeyEvent, e))
        {
            return;
        }

        if (TryHandleTitleNavigation(node, editor, e))
        {
            return;
        }

        var action = MindMapKeyboardGestureRouter.ResolveTitleAction(
            e.Key,
            e.KeyModifiers,
            string.IsNullOrWhiteSpace(editor?.Text),
            MindMapKeyboardTabBehavior.AddChild);

        switch (action)
        {
            case MindMapKeyboardAction.AddFromEnter:
                FocusNode(HandleMapEnter(node));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.AddChildFromTab:
                FocusNode(AddChild(node, string.Empty));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.Promote:
                PromoteNode(node);
                FocusNode(node);
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.Demote:
                FocusNode(HandleMapTab(node));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.MoveUp:
                if (MoveNodeUp(node))
                {
                    FocusNode(node);
                }
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.MoveDown:
                if (MoveNodeDown(node))
                {
                    FocusNode(node);
                }
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.DeleteEmptyTitle:
                if (!IsRootNode(node))
                {
                    FocusNode(DeleteNode(node));
                    MarkEditorKeyEventHandled(e);
                }
                return;
        }
    }

    private void HandleNoteKeyDown(MindMapNode node, TextBox? editor, KeyEventArgs e)
    {
        if (ReferenceEquals(_lastHandledEditorKeyEvent, e))
        {
            return;
        }

        var action = MindMapKeyboardGestureRouter.ResolveNoteAction(
            e.Key,
            string.IsNullOrWhiteSpace(editor?.Text));
        if (action != MindMapKeyboardAction.DeleteEmptyNote)
        {
            return;
        }

        node.Note = string.Empty;
        _editingNoteNodes.Remove(node);
        UpdateNoteEditorVisibility(node);
        FocusNode(node);
        MarkEditorKeyEventHandled(e);
    }

    private void HandleFrameKeyDown(MindMapNode node, KeyEventArgs e)
    {
        if (ReferenceEquals(_lastHandledEditorKeyEvent, e))
        {
            return;
        }

        if (TryHandleFrameNavigation(node, e))
        {
            return;
        }

        var action = MindMapKeyboardGestureRouter.ResolveFrameAction(
            e.Key,
            e.KeyModifiers,
            MindMapKeyboardTabBehavior.AddChild);
        switch (action)
        {
            case MindMapKeyboardAction.DeleteSelected:
                FocusFrame(DeleteNode(node));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.AddFromEnter:
                FocusNode(HandleMapEnter(node));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.AddChildFromTab:
                FocusNode(AddChild(node, string.Empty));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.Promote:
                if (PromoteNode(node))
                {
                    FocusNode(node);
                }
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.Demote:
                FocusNode(HandleMapTab(node));
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.MoveUp:
                if (MoveNodeUp(node))
                {
                    FocusNode(node);
                }
                MarkEditorKeyEventHandled(e);
                return;

            case MindMapKeyboardAction.MoveDown:
                if (MoveNodeDown(node))
                {
                    FocusNode(node);
                }
                MarkEditorKeyEventHandled(e);
                return;
        }
    }

    private void MarkEditorKeyEventHandled(KeyEventArgs e)
    {
        _lastHandledEditorKeyEvent = e;
        e.Handled = true;
    }

    private bool TryHandleTitleNavigation(MindMapNode node, TextBox? editor, KeyEventArgs e)
    {
        if (!CanNavigateWithArrowKey(e))
        {
            return false;
        }

        if (e.Key == Key.Left && !IsCaretAtStart(editor))
        {
            return false;
        }

        if (e.Key == Key.Right && !IsCaretAtEnd(editor))
        {
            return false;
        }

        return TryNavigateToDirectionalNode(node, e.Key, focusTitleEditor: true, e);
    }

    private bool TryHandleFrameNavigation(MindMapNode node, KeyEventArgs e)
    {
        return CanNavigateWithArrowKey(e)
            && TryNavigateToDirectionalNode(node, e.Key, focusTitleEditor: false, e);
    }

    private bool TryNavigateToDirectionalNode(
        MindMapNode current,
        Key key,
        bool focusTitleEditor,
        KeyEventArgs e)
    {
        var target = FindStructuralNavigationTarget(current, key);
        if (target is null)
        {
            return false;
        }

        if (focusTitleEditor)
        {
            FocusNode(target);
        }
        else
        {
            FocusFrame(target);
        }

        EnsureNodeVisible(target);
        MarkEditorKeyEventHandled(e);
        return true;
    }

    private MindMapNode? FindStructuralNavigationTarget(MindMapNode current, Key key)
    {
        return key switch
        {
            Key.Left => FindParent(current),
            Key.Right => current.Children.FirstOrDefault(),
            Key.Up => FindAdjacentSiblingNode(current, -1),
            Key.Down => FindAdjacentSiblingNode(current, 1),
            _ => null
        };
    }

    private MindMapNode? FindAdjacentSiblingNode(MindMapNode current, int offset)
    {
        var siblings = FindParent(current)?.Children ?? Roots;
        if (siblings is null)
        {
            return null;
        }

        var index = siblings.IndexOf(current);
        var targetIndex = index + offset;
        return index >= 0 && targetIndex >= 0 && targetIndex < siblings.Count
            ? siblings[targetIndex]
            : null;
    }

    private void EnsureNodeVisible(MindMapNode node)
    {
        if (_zoomScale <= 0 || _scrollViewer.Viewport.Width <= 0 || _scrollViewer.Viewport.Height <= 0)
        {
            return;
        }

        var size = GetRenderedNodeSize(node);
        var bounds = new Rect(node.X, node.Y, size.Width, size.Height).Inflate(40);
        var viewport = ViewportBounds;
        if (bounds.Left >= viewport.Left
            && bounds.Top >= viewport.Top
            && bounds.Right <= viewport.Right
            && bounds.Bottom <= viewport.Bottom)
        {
            return;
        }

        var offsetX = _scrollViewer.Offset.X;
        var offsetY = _scrollViewer.Offset.Y;
        if (bounds.Left < viewport.Left)
        {
            offsetX = bounds.Left * _zoomScale;
        }
        else if (bounds.Right > viewport.Right)
        {
            offsetX = bounds.Right * _zoomScale - _scrollViewer.Viewport.Width;
        }

        if (bounds.Top < viewport.Top)
        {
            offsetY = bounds.Top * _zoomScale;
        }
        else if (bounds.Bottom > viewport.Bottom)
        {
            offsetY = bounds.Bottom * _zoomScale - _scrollViewer.Viewport.Height;
        }

        _scrollViewer.Offset = ClampScrollOffset(new Vector(offsetX, offsetY));
        UpdateViewportBounds();
    }

    private static bool CanNavigateWithArrowKey(KeyEventArgs e)
    {
        return e.KeyModifiers == KeyModifiers.None
            && e.Key is Key.Left or Key.Right or Key.Up or Key.Down;
    }

    private static bool IsCaretAtStart(TextBox? editor)
    {
        return editor is null || editor.CaretIndex <= 0;
    }

    private static bool IsCaretAtEnd(TextBox? editor)
    {
        return editor is null || editor.CaretIndex >= (editor.Text?.Length ?? 0);
    }

    private void HandleNodeDragStarted(MindMapNode node, Control? control, PointerPressedEventArgs e)
    {
        if (control is null
            || IsRootNode(node)
            || !IsLeftPointerPressed(e.GetCurrentPoint(control).Properties))
        {
            return;
        }

        SelectNode(node);
        _dragNode = node;
        _dragStartPointer = e.GetPosition(_canvas);
        _isDraggingNode = false;
        _dropTarget = null;
        _dropPlacement = MindMapDropPlacement.Child;
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void HandleNodeDragged(object? sender, PointerEventArgs e)
    {
        if (_dragNode is null)
        {
            return;
        }

        var current = e.GetPosition(_canvas);
        var delta = current - _dragStartPointer;
        if (!_isDraggingNode && Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) < DragStartDistance)
        {
            return;
        }

        _isDraggingNode = true;
        UpdateDropTarget(_dragNode, current);

        ApplySelectionState();
        e.Handled = true;
    }

    private void HandleNodeDragCompleted(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragNode is null)
        {
            return;
        }

        var dragNode = _dragNode;
        var dropTarget = _dropTarget;
        var dropPlacement = _dropPlacement;
        var wasDragging = _isDraggingNode;
        var releasePoint = e.GetPosition(_canvas);

        _dragNode = null;
        _dropTarget = null;
        _dropPlacement = MindMapDropPlacement.Child;
        _isDraggingNode = false;
        HideDropPreview();
        e.Pointer.Capture(null);

        var moved = dropTarget is not null
            && MoveNode(dragNode, dropTarget, dropPlacement);
        if (moved)
        {
            FocusNode(dragNode);
        }
        else if (wasDragging)
        {
            FocusFrame(dragNode);
            ShowDropFeedback(DropUnavailableText, releasePoint, isError: true);
        }
        else if (!wasDragging)
        {
            FocusFrame(dragNode);
        }

        ApplySelectionState();
        e.Handled = true;
    }

    private void HandleCanvasPanStarted(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(_scrollViewer);
        var isLeftSpaceDrag = _isSpacePressed && IsLeftPointerPressed(point.Properties);
        var isRootLeftDrag = IsLeftPointerPressed(point.Properties) && IsRootPanSource(e.Source);
        var isMiddleDrag = IsMiddlePointerPressed(point.Properties);
        var isRightDrag = IsRightPointerPressed(point.Properties);

        if (_isPanningCanvas
            || _dragNode is not null
            || (!isRootLeftDrag && !IsCanvasPanSource(e.Source))
            || (!isLeftSpaceDrag && !isRootLeftDrag && !isMiddleDrag && !isRightDrag))
        {
            return;
        }

        _isPanningCanvas = true;
        _panStartPointer = e.GetPosition(_scrollViewer);
        _panStartOffset = _scrollViewer.Offset;
        // 原型 workbench.css:140：拖动中 grabbing（握拳），Windows 走内嵌 .cur，其他平台回退四向箭头。
        _canvas.Cursor = MindMapCursors.Grabbing ?? new Cursor(StandardCursorType.SizeAll);
        _scrollViewer.Cursor = MindMapCursors.Grabbing ?? new Cursor(StandardCursorType.SizeAll);
        e.Pointer.Capture(_scrollViewer);
        e.Handled = true;
    }

    private void HandleCanvasPanned(object? sender, PointerEventArgs e)
    {
        if (!_isPanningCanvas)
        {
            return;
        }

        var current = e.GetPosition(_scrollViewer);
        var delta = current - _panStartPointer;
        _scrollViewer.Offset = ClampScrollOffset(new Vector(
            _panStartOffset.X - delta.X,
            _panStartOffset.Y - delta.Y));
        UpdateViewportBounds();
        e.Handled = true;
    }

    private void HandleCanvasPanCompleted(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isPanningCanvas)
        {
            return;
        }

        StopCanvasPan();
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void StopCanvasPan()
    {
        _isPanningCanvas = false;
        // 原型 workbench.css:139：画布常态 grab（张手），Windows 走内嵌 .cur，其他平台回退手掌。
        _canvas.Cursor = MindMapCursors.Grab ?? new Cursor(StandardCursorType.Hand);
        _scrollViewer.Cursor = Cursor.Default;
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || HasVisualAncestor<TextBox>(e.Source))
        {
            return;
        }

        _isSpacePressed = true;
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _isSpacePressed = false;
        }
    }

    private void HandlePointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (!IsCanvasWheelSource(e.Source))
        {
            return;
        }

        if (HasZoomModifier(e.KeyModifiers))
        {
            ZoomAtPointer(e.Delta.Y, e.GetPosition(_scrollViewer));
            e.Handled = true;
            return;
        }

        if (TryPanWithWheel(e))
        {
            e.Handled = true;
        }
    }

    private void HandleTouchPadMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (ReferenceEquals(_lastHandledTouchPadMagnifyEvent, e))
        {
            return;
        }

        if (!IsCanvasGestureSource(e.Source))
        {
            return;
        }

        var zoomFactor = ToTouchPadZoomFactor(e.Delta);
        if (Math.Abs(zoomFactor - 1) < double.Epsilon)
        {
            return;
        }

        SetZoom(_zoomScale * zoomFactor, e.GetPosition(_scrollViewer));
        _lastHandledTouchPadMagnifyEvent = e;
        e.Handled = true;
    }

    private void HandlePinch(object? sender, PinchEventArgs e)
    {
        if (ReferenceEquals(_lastHandledPinchEvent, e))
        {
            return;
        }

        if (!IsCanvasGestureSource(e.Source))
        {
            return;
        }

        if (!_isPinching)
        {
            _isPinching = true;
            _pinchStartZoom = _zoomScale;
        }

        var zoomFactor = NormalizePinchScale(e.Scale);
        if (Math.Abs(zoomFactor - 1) < double.Epsilon)
        {
            return;
        }

        SetZoom(_pinchStartZoom * zoomFactor, GetPinchAnchor(e));
        _lastHandledPinchEvent = e;
        e.Handled = true;
    }

    private void HandlePinchEnded(object? sender, RoutedEventArgs e)
    {
        _isPinching = false;
        _pinchStartZoom = _zoomScale;
        _lastHandledPinchEvent = null;
        e.Handled = true;
    }

    private static bool HasZoomModifier(KeyModifiers modifiers)
    {
        var commandModifier = OperatingSystem.IsMacOS()
            ? KeyModifiers.Meta
            : KeyModifiers.Control;
        return modifiers.HasFlag(commandModifier);
    }

    public void ZoomOut()
    {
        SetZoom(_zoomScale / ZoomFactor, GetViewportCenter());
    }

    public void ZoomIn()
    {
        SetZoom(_zoomScale * ZoomFactor, GetViewportCenter());
    }

    public void ResetZoom()
    {
        SetZoom(1, GetViewportCenter());
    }

    public void CenterRoot()
    {
        var root = Roots?.FirstOrDefault();
        if (root is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() => CenterNode(root), DispatcherPriority.Loaded);
    }

    public void PlaceRootNearLeftCenter()
    {
        _shouldPlaceRootNearLeftCenter = true;
        Dispatcher.UIThread.Post(TryPlaceRootNearLeftCenter, DispatcherPriority.Loaded);
    }

    public void CenterViewportAt(Point canvasPoint)
    {
        var offset = new Vector(
            canvasPoint.X * _zoomScale - _scrollViewer.Viewport.Width / 2,
            canvasPoint.Y * _zoomScale - _scrollViewer.Viewport.Height / 2);
        _scrollViewer.Offset = ClampScrollOffset(offset);
        UpdateViewportBounds();
    }

    private bool TryPanWithWheel(PointerWheelEventArgs e)
    {
        if (!IsCanvasWheelSource(e.Source))
        {
            return false;
        }

        var panX = e.Delta.X;
        var panY = e.Delta.Y;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && Math.Abs(panX) < double.Epsilon)
        {
            panX = panY;
            panY = 0;
        }

        if (Math.Abs(panX) < double.Epsilon && Math.Abs(panY) < double.Epsilon)
        {
            return false;
        }

        _scrollViewer.Offset = ClampScrollOffset(new Vector(
            _scrollViewer.Offset.X - ToWheelPanDistance(panX),
            _scrollViewer.Offset.Y - ToWheelPanDistance(panY)));
        UpdateViewportBounds();
        return true;
    }

    private double ToWheelPanDistance(double delta)
    {
        if (Math.Abs(delta) <= 3)
        {
            return delta * WheelPanStep;
        }

        return delta;
    }

    private double ToTouchPadZoomFactor(Vector delta)
    {
        var value = Math.Abs(delta.Y) >= Math.Abs(delta.X)
            ? delta.Y
            : delta.X;
        if (Math.Abs(value) < double.Epsilon)
        {
            return 1;
        }

        var steps = Math.Abs(value) <= 0.2
            ? value * TouchPadMagnifySensitivity
            : value;
        steps = Math.Clamp(steps, -TouchPadMagnifyMaxSteps, TouchPadMagnifyMaxSteps);
        return Math.Pow(ZoomFactor, steps);
    }

    private static double NormalizePinchScale(double scale)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            return 1;
        }

        return Math.Clamp(scale, 0.25, 4);
    }

    private Point GetPinchAnchor(PinchEventArgs e)
    {
        if (e.Source is Visual source
            && !ReferenceEquals(source, _scrollViewer)
            && source.TranslatePoint(e.ScaleOrigin, _scrollViewer) is { } point)
        {
            return point;
        }

        return e.ScaleOrigin;
    }

    private void ZoomAtPointer(double wheelDelta, Point viewportAnchor)
    {
        if (Math.Abs(wheelDelta) < double.Epsilon)
        {
            return;
        }

        SetZoom(_zoomScale * Math.Pow(ZoomFactor, wheelDelta), viewportAnchor);
    }

    private Point GetViewportCenter()
    {
        return new Point(_scrollViewer.Viewport.Width / 2, _scrollViewer.Viewport.Height / 2);
    }

    private void SetZoom(double zoom, Point viewportAnchor)
    {
        if (_scrollViewer.Viewport.Width <= 0 || _scrollViewer.Viewport.Height <= 0)
        {
            viewportAnchor = GetViewportCenter();
        }

        var anchor = new Point(
            (_scrollViewer.Offset.X + viewportAnchor.X) / _zoomScale,
            (_scrollViewer.Offset.Y + viewportAnchor.Y) / _zoomScale);

        _zoomScale = Math.Clamp(zoom, MinZoom, MaxZoom);
        _zoomHost.LayoutTransform = CreateZoomTransform(_zoomScale);
        EnsureCanvasSize();
        _zoomHost.InvalidateMeasure();
        UpdateZoomText();
        _scrollViewer.Offset = ClampScrollOffset(new Vector(
            anchor.X * _zoomScale - viewportAnchor.X,
            anchor.Y * _zoomScale - viewportAnchor.Y));
        UpdateViewportBounds();
    }

    private static ScaleTransform CreateZoomTransform(double zoom)
    {
        return new ScaleTransform(zoom, zoom);
    }

    private Vector ClampScrollOffset(Vector offset)
    {
        var maxX = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
        var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        return new Vector(
            Math.Clamp(offset.X, 0, maxX),
            Math.Clamp(offset.Y, 0, maxY));
    }

    private void CenterNode(MindMapNode node)
    {
        var nodeSize = GetRenderedNodeSize(node);
        var center = new Point(node.X + nodeSize.Width / 2, node.Y + nodeSize.Height / 2);
        CenterViewportAt(center);
    }

    private void TryPlaceRootNearLeftCenter()
    {
        if (!_shouldPlaceRootNearLeftCenter
            || _isRebuildingVisuals
            || _zoomScale <= 0
            || _scrollViewer.Viewport.Width <= 0
            || _scrollViewer.Viewport.Height <= 0)
        {
            return;
        }

        var root = Roots?.FirstOrDefault();
        if (root is null)
        {
            _shouldPlaceRootNearLeftCenter = false;
            return;
        }

        EnsureCanvasSize();
        var rootSize = GetRenderedNodeSize(root);
        var rootCenterY = root.Y + rootSize.Height / 2;
        _shouldPlaceRootNearLeftCenter = false;
        _scrollViewer.Offset = ClampScrollOffset(new Vector(
            root.X * _zoomScale - RootViewportLeftInset,
            rootCenterY * _zoomScale - _scrollViewer.Viewport.Height / 2));
        UpdateViewportBounds();
    }

    private void HandleScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty)
        {
            EnsureCanvasSize();
        }

        if (e.Property == ScrollViewer.OffsetProperty
            || e.Property == ScrollViewer.ViewportProperty
            || e.Property == ScrollViewer.ExtentProperty)
        {
            UpdateViewportBounds();
            TryPlaceRootNearLeftCenter();
        }
    }

    private void UpdateViewportBounds()
    {
        if (_zoomScale <= 0 || _scrollViewer.Viewport.Width <= 0 || _scrollViewer.Viewport.Height <= 0)
        {
            ViewportBounds = default;
            return;
        }

        ViewportBounds = new Rect(
            _scrollViewer.Offset.X / _zoomScale,
            _scrollViewer.Offset.Y / _zoomScale,
            _scrollViewer.Viewport.Width / _zoomScale,
            _scrollViewer.Viewport.Height / _zoomScale);
    }

    private bool IsCanvasPanSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is TextBox or Button or ScrollBar or Thumb)
            {
                return false;
            }

            if (_nodeFrames.Values.Contains(current))
            {
                return false;
            }

            if (ReferenceEquals(current, _nodeToolbar))
            {
                return false;
            }

            if (ReferenceEquals(current, _scrollViewer))
            {
                return true;
            }
        }

        return true;
    }

    private bool IsRootPanSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is Button or ScrollBar or Thumb)
            {
                return false;
            }

            if (ReferenceEquals(current, _nodeToolbar)
                || ReferenceEquals(current, _nodeMenu))
            {
                return false;
            }

            foreach (var (node, frame) in _nodeFrames)
            {
                if (ReferenceEquals(current, frame))
                {
                    return IsRootNode(node);
                }
            }
        }

        return false;
    }

    private bool IsCanvasWheelSource(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is TextBox or Button or ScrollBar or Thumb)
            {
                return false;
            }

            if (ReferenceEquals(current, _nodeToolbar)
                || ReferenceEquals(current, _nodeMenu))
            {
                return false;
            }

            if (ReferenceEquals(current, _scrollViewer))
            {
                return true;
            }
        }

        return true;
    }

    private bool IsCanvasGestureSource(object? source)
    {
        return source is not Visual || IsCanvasWheelSource(source);
    }

    private static bool HasVisualAncestor<T>(object? source)
        where T : Visual
    {
        return FindVisualAncestor<T>(source) is not null;
    }

    private static T? FindVisualAncestor<T>(object? source)
        where T : Visual
    {
        if (source is not Visual visual)
        {
            return null;
        }

        for (var current = visual; current is not null; current = current.GetVisualParent())
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }

    private void UpdateZoomText()
    {
        ZoomText = $"{_zoomScale:P0}";
        UpdateViewportBounds();
    }

    private void UpdateDropTarget(MindMapNode dragNode, Point canvasPoint)
    {
        MindMapNode? nextTarget = null;
        var nextPlacement = MindMapDropPlacement.Child;

        foreach (var (node, frame) in _nodeFrames)
        {
            if (!CanMoveNode(dragNode, node))
            {
                continue;
            }

            var bounds = GetNodeBounds(node, frame);
            if (!bounds.Contains(canvasPoint))
            {
                continue;
            }

            nextTarget = node;
            nextPlacement = GetDropPlacement(bounds, canvasPoint);
            if (IsRootNode(node) && nextPlacement is MindMapDropPlacement.Before or MindMapDropPlacement.After)
            {
                nextPlacement = MindMapDropPlacement.Child;
            }

            break;
        }

        if (nextTarget is null)
        {
            ClearDropTarget();
            return;
        }

        _dropTarget = nextTarget;
        _dropPlacement = nextPlacement;
        ShowDropPreview(nextTarget, nextPlacement);
    }

    private void ClearDropTarget()
    {
        _dropTarget = null;
        _dropPlacement = MindMapDropPlacement.Child;
        HideDropPreview();
    }

    private MindMapDropPlacement GetDropPlacement(Rect targetBounds, Point pointer)
    {
        var offsetY = pointer.Y - targetBounds.Top;
        if (offsetY < targetBounds.Height * DropEdgeRatio)
        {
            return MindMapDropPlacement.Before;
        }

        if (offsetY > targetBounds.Height * (1 - DropEdgeRatio))
        {
            return MindMapDropPlacement.After;
        }

        return MindMapDropPlacement.Child;
    }

    private static bool IsRightPointerPressed(PointerPointProperties properties)
    {
        return properties.IsRightButtonPressed
            || properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed;
    }

    private static bool IsLeftPointerPressed(PointerPointProperties properties)
    {
        return properties.IsLeftButtonPressed
            || properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed;
    }

    private static bool IsMiddlePointerPressed(PointerPointProperties properties)
    {
        return properties.IsMiddleButtonPressed
            || properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed;
    }

    private Rect GetNodeBounds(MindMapNode node, Control frame)
    {
        var size = GetRenderedNodeSize(node);
        return new Rect(node.X, node.Y, size.Width, size.Height);
    }

    private Border CreateDropPreviewLabel()
    {
        return new Border
        {
            MinWidth = 72,
            MinHeight = 26,
            Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            BoxShadow = GetResourceBoxShadows(MindViewStyleKeys.ToolbarBoxShadowResource, "0 6 18 0 #22000000"),
            Child = _dropPreviewText,
            IsHitTestVisible = false,
            IsVisible = false
        };
    }

    private void ShowDropPreview(MindMapNode target, MindMapDropPlacement placement)
    {
        if (!_nodeFrames.TryGetValue(target, out var frame))
        {
            HideDropPreview();
            return;
        }

        var bounds = GetNodeBounds(target, frame);
        _dropPreviewPath.Stroke = placement == MindMapDropPlacement.Child
            ? GetResourceBrush(MindViewStyleKeys.DropChildBrushResource, "#22C55E", "#22C55E")
            : GetSelectionBrush();
        _dropPreviewPath.Data = CreateDropPreviewGeometry(bounds, placement);
        _dropPreviewPath.IsVisible = true;
        ShowDropPreviewLabel(GetDropPlacementText(placement), bounds, placement, isError: false);
    }

    private void HideDropPreview()
    {
        _dropFeedbackVersion++;
        _dropPreviewPath.IsVisible = false;
        _dropPreviewPath.Data = null;
        _dropPreviewLabel.IsVisible = false;
        _dropPreviewText.Text = string.Empty;
    }

    private void ShowDropPreviewLabel(string text, Rect bounds, MindMapDropPlacement placement, bool isError)
    {
        _dropFeedbackVersion++;
        ApplyDropPreviewLabelStyle(isError);
        _dropPreviewText.Text = text;
        _dropPreviewLabel.IsVisible = true;
        PositionDropPreviewLabel(bounds, placement);
    }

    private void ShowDropFeedback(string text, Point canvasPoint, bool isError)
    {
        var version = ++_dropFeedbackVersion;
        _dropPreviewPath.IsVisible = false;
        _dropPreviewPath.Data = null;
        ApplyDropPreviewLabelStyle(isError);
        _dropPreviewText.Text = text;
        _dropPreviewLabel.IsVisible = true;
        PositionDropPreviewLabel(canvasPoint);
        DispatcherTimer.RunOnce(
            () =>
            {
                if (version == _dropFeedbackVersion)
                {
                    HideDropPreview();
                }
            },
            TimeSpan.FromMilliseconds(900));
    }

    private void ApplyDropPreviewLabelStyle(bool isError)
    {
        if (isError)
        {
            _dropPreviewLabel.Background = Brush.Parse(IsDarkTheme ? "#7F1D1D" : "#FEF2F2");
            _dropPreviewLabel.BorderBrush = Brush.Parse(IsDarkTheme ? "#FCA5A5" : "#FCA5A5");
            _dropPreviewText.Foreground = Brush.Parse(IsDarkTheme ? "#FEE2E2" : "#991B1B");
            return;
        }

        _dropPreviewLabel.Background = GetResourceBrush(MindViewStyleKeys.ToolbarBackgroundBrushResource, "#FFFFFF", "#111827");
        _dropPreviewLabel.BorderBrush = GetSelectionBrush();
        _dropPreviewText.Foreground = GetPrimaryTextBrush();
    }

    private string GetDropPlacementText(MindMapDropPlacement placement)
    {
        return placement switch
        {
            MindMapDropPlacement.Before => DropBeforeText,
            MindMapDropPlacement.After => DropAfterText,
            _ => DropAsChildText
        };
    }

    private void PositionDropPreviewLabel(Rect bounds, MindMapDropPlacement placement)
    {
        var labelSize = MeasureDropPreviewLabel();
        var x = placement == MindMapDropPlacement.Child
            ? bounds.Right + 10
            : bounds.Left + bounds.Width / 2 - labelSize.Width / 2;
        var y = placement switch
        {
            MindMapDropPlacement.Before => bounds.Top - labelSize.Height - 12,
            MindMapDropPlacement.After => bounds.Bottom + 12,
            _ => bounds.Top + bounds.Height / 2 - labelSize.Height / 2
        };
        PositionDropPreviewLabel(new Point(x, y), labelSize);
    }

    private void PositionDropPreviewLabel(Point canvasPoint)
    {
        PositionDropPreviewLabel(canvasPoint, MeasureDropPreviewLabel());
    }

    private void PositionDropPreviewLabel(Point canvasPoint, Size labelSize)
    {
        var canvasWidth = GetFiniteSize(_canvas.Width, _canvas.Bounds.Width);
        var canvasHeight = GetFiniteSize(_canvas.Height, _canvas.Bounds.Height);
        var x = Math.Clamp(canvasPoint.X, 8, Math.Max(8, canvasWidth - labelSize.Width - 8));
        var y = Math.Clamp(canvasPoint.Y, 8, Math.Max(8, canvasHeight - labelSize.Height - 8));
        Canvas.SetLeft(_dropPreviewLabel, x);
        Canvas.SetTop(_dropPreviewLabel, y);
    }

    private Size MeasureDropPreviewLabel()
    {
        _dropPreviewLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _dropPreviewLabel.DesiredSize;
        return new Size(
            size.Width > 0 ? size.Width : 96,
            size.Height > 0 ? size.Height : 26);
    }

    private static double GetFiniteSize(double preferred, double fallback)
    {
        if (!double.IsNaN(preferred) && !double.IsInfinity(preferred) && preferred > 0)
        {
            return preferred;
        }

        return !double.IsNaN(fallback) && !double.IsInfinity(fallback) && fallback > 0 ? fallback : 1000;
    }

    private static Geometry CreateDropPreviewGeometry(Rect bounds, MindMapDropPlacement placement)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();

        if (placement == MindMapDropPlacement.Child)
        {
            // 拖到节点中部时用虚线框提示“成为子节点”的最终结果。
            var rect = bounds.Inflate(7);
            context.BeginFigure(rect.TopLeft, isFilled: false);
            context.LineTo(rect.TopRight);
            context.LineTo(rect.BottomRight);
            context.LineTo(rect.BottomLeft);
            context.LineTo(rect.TopLeft);
            context.EndFigure(isClosed: true);
            return geometry;
        }

        var y = placement == MindMapDropPlacement.Before
            ? bounds.Top - 8
            : bounds.Bottom + 8;
        // 拖到上下边缘时只画一条插入线，表示调整同级顺序。
        var start = new Point(bounds.Left - 16, y);
        var end = new Point(bounds.Right + 16, y);
        context.BeginFigure(start, isFilled: false);
        context.LineTo(end);
        return geometry;
    }

}
