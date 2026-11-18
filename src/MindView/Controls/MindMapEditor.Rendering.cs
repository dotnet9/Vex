using global::Avalonia;
using global::Avalonia.Animation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CodeWF.Avalonia.MindView.Controls;

public partial class MindMapEditor
{
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RootsProperty)
        {
            Rebuild();
        }
        else if (change.Property == SelectedNodeProperty)
        {
            CollapseEmptyNoteEditorsExcept(SelectedNode);
            HideNodeMenu();
            if (!ReferenceEquals(_toolbarNode, SelectedNode))
            {
                _toolbarNode = null;
            }

            ApplySelectionState();
            UpdateToolbarVisibility();
        }
        else if (change.Property == IsDarkThemeProperty || change.Property == AccentColorProperty)
        {
            ApplyTheme();
            RecreateNodeChrome();
        }
        else if (change.Property == LayoutKindProperty)
        {
            ArrangeNodes();
        }
        else if (change.Property == StyleKindProperty)
        {
            RecreateNodeChrome();
            UpdateConnectors();
        }
        else if (change.Property == ControllerProperty)
        {
            Rebuild();
        }
        else if (IsTextResourceProperty(change.Property))
        {
            if (!_isApplyingLocalizedText)
            {
                _hostTextProperties.Add(change.Property);
                RecreateNodeChrome();
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeToI18n();
        RefreshLocalizedText();
        AttachTopLevelKeyTracking();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachTopLevelKeyTracking();
        UnsubscribeFromI18n();
        base.OnDetachedFromVisualTree(e);
    }

    private void AttachTopLevelKeyTracking()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (ReferenceEquals(_topLevel, topLevel))
        {
            return;
        }

        DetachTopLevelKeyTracking();
        _topLevel = topLevel;
        _topLevel?.AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        _topLevel?.AddHandler(KeyUpEvent, HandleKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachTopLevelKeyTracking()
    {
        if (_topLevel is null)
        {
            return;
        }

        _topLevel.RemoveHandler(KeyDownEvent, HandleKeyDown);
        _topLevel.RemoveHandler(KeyUpEvent, HandleKeyUp);
        _topLevel = null;
        _isSpacePressed = false;
        StopCanvasPan();
    }

    private void Rebuild()
    {
        var version = ++_rebuildVersion;
        _isRebuildingVisuals = false;
        DetachTreeSubscriptions();

        _canvas.Children.Clear();
        _nodeFrames.Clear();
        _titleEditors.Clear();
        _noteEditors.Clear();
        _noteFrames.Clear();
        _connectors.Clear();
        _hoverNode = null;

        if (Roots is null)
        {
            return;
        }

        Roots.CollectionChanged += HandleTreeStructureChanged;
        _observedCollections.Add(Roots);

        var connectorWork = new List<ConnectorWorkItem>();
        var nodeWork = new List<MindMapNode>();
        foreach (var root in Roots)
        {
            AssignMissingColors(root);
            WatchNode(root);
            CollectVisualWork(root, nodeWork, connectorWork);
        }

        _isRebuildingVisuals = true;
        RenderRebuildBatch(version, connectorWork, nodeWork, connectorIndex: 0, nodeIndex: 0);
    }

    private void CollectVisualWork(
        MindMapNode node,
        ICollection<MindMapNode> nodeWork,
        ICollection<ConnectorWorkItem> connectorWork)
    {
        nodeWork.Add(node);
        foreach (var child in node.Children)
        {
            connectorWork.Add(new ConnectorWorkItem(node, child));
            CollectVisualWork(child, nodeWork, connectorWork);
        }
    }

    private void RenderRebuildBatch(
        int version,
        IReadOnlyList<ConnectorWorkItem> connectorWork,
        IReadOnlyList<MindMapNode> nodeWork,
        int connectorIndex,
        int nodeIndex)
    {
        if (version != _rebuildVersion)
        {
            return;
        }

        var connectorLimit = Math.Min(connectorWork.Count, connectorIndex + RebuildConnectorBatchSize);
        for (var i = connectorIndex; i < connectorLimit; i++)
        {
            AddConnector(connectorWork[i].Parent, connectorWork[i].Child);
        }

        connectorIndex = connectorLimit;
        if (connectorIndex >= connectorWork.Count)
        {
            var nodeLimit = Math.Min(nodeWork.Count, nodeIndex + RebuildNodeBatchSize);
            for (var i = nodeIndex; i < nodeLimit; i++)
            {
                AddNodeVisual(nodeWork[i]);
            }

            nodeIndex = nodeLimit;
        }

        if (connectorIndex < connectorWork.Count || nodeIndex < nodeWork.Count)
        {
            Dispatcher.UIThread.Post(
                () => RenderRebuildBatch(version, connectorWork, nodeWork, connectorIndex, nodeIndex),
                DispatcherPriority.Background);
            return;
        }

        _canvas.Children.Add(_dropPreviewPath);
        _canvas.Children.Add(_dropPreviewLabel);
        _canvas.Children.Add(_nodeToolbar);
        _canvas.Children.Add(_nodeAddChildButton);
        _canvas.Children.Add(_nodeMenu);
        _isRebuildingVisuals = false;
        HideDropPreview();
        HideNodeMenu();
        UpdateToolbarVisibility();
        UpdateConnectors();
        ApplySelectionState();
        EnsureCanvasSize();
        UpdateViewportBounds();
        Dispatcher.UIThread.Post(TryPlaceRootNearLeftCenter, DispatcherPriority.Loaded);
    }

    private void DetachTreeSubscriptions()
    {
        foreach (var node in _observedNodes)
        {
            node.PropertyChanged -= HandleNodePropertyChanged;
        }

        foreach (var collection in _observedCollections)
        {
            collection.CollectionChanged -= HandleTreeStructureChanged;
        }

        _observedNodes.Clear();
        _observedCollections.Clear();
    }

    private void WatchNode(MindMapNode node)
    {
        _observedNodes.Add(node);
        node.PropertyChanged += HandleNodePropertyChanged;
        node.Children.CollectionChanged += HandleTreeStructureChanged;
        _observedCollections.Add(node.Children);

        foreach (var child in node.Children)
        {
            WatchNode(child);
        }
    }

    private void AddConnectors(MindMapNode parent)
    {
        foreach (var child in parent.Children)
        {
            AddConnector(parent, child);
            AddConnectors(child);
        }
    }

    private void AddConnector(MindMapNode parent, MindMapNode child)
    {
        var path = new global::Avalonia.Controls.Shapes.Path
        {
            Stroke = GetConnectorBrush(),
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        _canvas.Children.Add(path);
        _connectors.Add(new Connector(parent, child, path));
    }

    private void AddNodeVisuals(MindMapNode node)
    {
        AddNodeVisual(node);

        foreach (var child in node.Children)
        {
            AddNodeVisuals(child);
        }
    }

    private void AddNodeVisual(MindMapNode node)
    {
        var nodeVisual = CreateNodeVisual(node);
        _nodeFrames[node] = nodeVisual;
        _canvas.Children.Add(nodeVisual);
        UpdateNodePosition(node);
    }

    private Border CreateNodeVisual(MindMapNode node)
    {
        var metrics = GetNodeMetrics(node);
        var root = new Border
        {
            MinWidth = metrics.MinWidth,
            MaxWidth = metrics.MaxWidth,
            MinHeight = metrics.MinHeight,
            CornerRadius = metrics.CornerRadius,
            Background = metrics.Background,
            BorderBrush = metrics.BorderBrush,
            BorderThickness = metrics.BorderThickness,
            Padding = metrics.Padding,
            BoxShadow = metrics.BoxShadow,
            DataContext = node,
            Focusable = true,
            Transitions = new Transitions
            {
                new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(160) },
                new BrushTransition { Property = Border.BorderBrushProperty, Duration = TimeSpan.FromMilliseconds(160) },
                new ThicknessTransition { Property = Border.BorderThicknessProperty, Duration = TimeSpan.FromMilliseconds(120) },
                new BoxShadowsTransition { Property = Border.BoxShadowProperty, Duration = TimeSpan.FromMilliseconds(180) }
            }
        };

        var isRoot = IsRootNode(node);
        var titleBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = metrics.Foreground,
            CaretBrush = metrics.Foreground,
            FontSize = metrics.FontSize,
            FontWeight = metrics.FontWeight,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            PlaceholderText = metrics.Placeholder,
            PlaceholderForeground = GetTitlePlaceholderBrush(isRoot),
            FocusAdorner = null,
            TextAlignment = ToTextAlignment(metrics.ContentAlignment),
            HorizontalContentAlignment = metrics.ContentAlignment,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinWidth = Math.Max(12, metrics.MinWidth - metrics.Padding.Left - metrics.Padding.Right),
            MaxWidth = Math.Max(12, metrics.MaxWidth - metrics.Padding.Left - metrics.Padding.Right),
            MinHeight = metrics.MinHeight - metrics.Padding.Top - metrics.Padding.Bottom,
            Padding = new Thickness(0)
        };
        titleBox.Classes.Add("codewfMindMapTitleEditor");
        titleBox.Text = node.Title;
        titleBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !string.Equals(node.Title, titleBox.Text, StringComparison.Ordinal))
            {
                node.Title = titleBox.Text ?? string.Empty;
            }
        };
        titleBox.GotFocus += (_, _) =>
        {
            SelectNode(node);
            ShowNodeToolbar(node);
        };
        titleBox.AddHandler(
            KeyDownEvent,
            (sender, e) => HandleTitleKeyDown(node, sender as TextBox, e),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        _titleEditors[node] = titleBox;

        var titleHost = CreateTitleHost(node, titleBox, isRoot);
        var noteBox = CreateNoteEditor(node, metrics);
        var noteFrame = new Border
        {
            Margin = new Thickness(0, MindMapLayoutMetrics.NoteVerticalSpacing, 0, 0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Child = noteBox
        };

        var content = new StackPanel
        {
            Spacing = 0
        };
        content.Children.Add(titleHost);
        content.Children.Add(noteFrame);
        root.Child = content;
        _noteEditors[node] = noteBox;
        _noteFrames[node] = noteFrame;
        UpdateNoteEditorVisibility(node);

        root.SizeChanged += (_, _) =>
        {
            if (_isRebuildingVisuals)
            {
                return;
            }

            UpdateConnectors();
            EnsureCanvasSize();
        };

        root.AddHandler(
            PointerPressedEvent,
            (_, e) =>
            {
                var point = e.GetCurrentPoint(root);
                if (IsRightPointerPressed(point.Properties))
                {
                    SelectNode(node);
                    ShowNodeToolbar(node);
                    ShowNodeMenu(node, e.GetPosition(_canvas));
                    e.Handled = true;
                    return;
                }

                var textEditor = FindVisualAncestor<TextBox>(e.Source);
                if (textEditor is not null)
                {
                    HideNodeMenu();
                    SelectNode(node);
                    ShowNodeToolbar(node);
                    if (IsLeftPointerPressed(point.Properties) && e.ClickCount >= 2)
                    {
                        FocusTextEditor(textEditor);
                        e.Handled = true;
                        return;
                    }

                    if (IsLeftPointerPressed(point.Properties))
                    {
                        root.Focus();
                        if (!isRoot)
                        {
                            HandleNodeDragStarted(node, root, e);
                        }

                        e.Handled = true;
                    }

                    return;
                }

                HideNodeMenu();
                SelectNode(node);
                ShowNodeToolbar(node);
                if (isRoot)
                {
                    root.Focus();
                }
                else
                {
                    root.Focus();
                    HandleNodeDragStarted(node, root, e);
                }

                e.Handled = true;
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        root.PointerMoved += HandleNodeDragged;
        root.PointerReleased += HandleNodeDragCompleted;
        root.PointerEntered += (_, _) => SetHoveredNode(node);
        root.PointerExited += (_, _) =>
        {
            if (ReferenceEquals(_hoverNode, node))
            {
                SetHoveredNode(null);
            }
        };
        root.KeyDown += (_, e) => HandleFrameKeyDown(node, e);

        return root;
    }

    private Control CreateTitleHost(MindMapNode node, TextBox titleBox, bool isRoot)
    {
        return titleBox;
    }

    private TextBox CreateNoteEditor(MindMapNode node, NodeMetrics metrics)
    {
        var noteForeground = metrics.IsTextOnly
            ? GetSecondaryTextBrush()
            : IsRootNode(node)
                ? GetResourceBrush(MindViewStyleKeys.RootNoteForegroundBrushResource, "#F8FAFC", "#F8FAFC")
                : GetResourceBrush(MindViewStyleKeys.NoteForegroundBrushResource, "#6B7280", "#CBD5E1");
        var noteBox = new TextBox
        {
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = noteForeground,
            CaretBrush = noteForeground,
            FontSize = MindMapLayoutMetrics.NoteFontSize,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            PlaceholderText = NotePlaceholder,
            PlaceholderForeground = GetNotePlaceholderBrush(IsRootNode(node)),
            FocusAdorner = null,
            TextAlignment = ToTextAlignment(metrics.ContentAlignment),
            MinWidth = Math.Max(12, metrics.MinWidth - metrics.Padding.Left - metrics.Padding.Right),
            MaxWidth = Math.Max(12, metrics.MaxWidth - metrics.Padding.Left - metrics.Padding.Right),
            MinHeight = MindMapLayoutMetrics.NoteMinHeight,
            MaxHeight = MindMapLayoutMetrics.NoteMaxHeight,
            Padding = new Thickness(0, 2, 0, 0),
            HorizontalContentAlignment = metrics.ContentAlignment,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        noteBox.Classes.Add("codewfMindMapTitleEditor");
        noteBox.Text = node.Note;
        noteBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty && !string.Equals(node.Note, noteBox.Text, StringComparison.Ordinal))
            {
                node.Note = noteBox.Text ?? string.Empty;
            }
        };
        noteBox.GotFocus += (_, _) =>
        {
            SelectNode(node);
            ShowNodeToolbar(node);
        };
        noteBox.LostFocus += (_, _) => CollapseEmptyNoteEditor(node);
        noteBox.AddHandler(
            KeyDownEvent,
            (sender, e) => HandleNoteKeyDown(node, sender as TextBox, e),
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
            handledEventsToo: true);
        return noteBox;
    }

    private void HandleNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MindMapNode node)
        {
            return;
        }

        if (e.PropertyName is nameof(MindMapNode.X) or nameof(MindMapNode.Y))
        {
            UpdateNodePosition(node);
            UpdateConnectors();
            EnsureCanvasSize();
        }
        else if (e.PropertyName == nameof(MindMapNode.AccentColor))
        {
            Rebuild();
        }
        else if (e.PropertyName == nameof(MindMapNode.Title))
        {
            UpdateEditorText(_titleEditors, node, node.Title);
            UpdateConnectors();
            EnsureCanvasSize();
            PositionNodeToolbar();
        }
        else if (e.PropertyName == nameof(MindMapNode.Note))
        {
            UpdateEditorText(_noteEditors, node, node.Note);
            UpdateNoteEditorVisibility(node);
            UpdateConnectors();
            EnsureCanvasSize();
            PositionNodeToolbar();
        }
    }

    private static void UpdateEditorText(Dictionary<MindMapNode, TextBox> editors, MindMapNode node, string text)
    {
        if (editors.TryGetValue(node, out var editor)
            && !string.Equals(editor.Text, text, StringComparison.Ordinal))
        {
            editor.Text = text;
        }
    }

    private void HandleTreeStructureChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Rebuild();
    }

    private void SelectNode(MindMapNode node)
    {
        SetCurrentValue(SelectedNodeProperty, node);
        ApplySelectionState();
    }

    private void SetHoveredNode(MindMapNode? node)
    {
        if (ReferenceEquals(_hoverNode, node))
        {
            return;
        }

        _hoverNode = node;
        ApplySelectionState();
    }

    private void ShowNoteEditor(MindMapNode node)
    {
        // 备注输入框只有在用户显式添加或已有内容时显示，空备注失焦后会回收。
        _editingNoteNodes.Add(node);
        SelectNode(node);
        UpdateNoteEditorVisibility(node);
        PositionNodeToolbar();
        Dispatcher.UIThread.Post(() =>
        {
            if (_noteEditors.TryGetValue(node, out var editor))
            {
                editor.Focus();
                editor.CaretIndex = editor.Text?.Length ?? 0;
            }
        });
    }

    private void CollapseEmptyNoteEditor(MindMapNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Note))
        {
            return;
        }

        _editingNoteNodes.Remove(node);
        UpdateNoteEditorVisibility(node);
        UpdateConnectors();
        EnsureCanvasSize();
    }

    private void CollapseEmptyNoteEditorsExcept(MindMapNode? nodeToKeep)
    {
        foreach (var node in _editingNoteNodes.ToList())
        {
            if (!ReferenceEquals(node, nodeToKeep) && string.IsNullOrWhiteSpace(node.Note))
            {
                _editingNoteNodes.Remove(node);
                UpdateNoteEditorVisibility(node);
            }
        }
    }

    private void UpdateNoteEditorVisibility(MindMapNode node)
    {
        if (!_noteFrames.TryGetValue(node, out var frame))
        {
            return;
        }

        var visible = _editingNoteNodes.Contains(node) || !string.IsNullOrWhiteSpace(node.Note);
        frame.IsVisible = visible;
    }

    private void ShowNodeToolbar(MindMapNode node)
    {
        _toolbarNode = node;
        UpdateToolbarVisibility();
    }

    private void UpdateToolbarVisibility()
    {
        if (_toolbarNode is null || !_nodeFrames.ContainsKey(_toolbarNode))
        {
            _nodeToolbar.IsVisible = false;
            _nodeAddChildButton.IsVisible = false;
            return;
        }

        _nodeToolbar.Background = GetResourceBrush(MindViewStyleKeys.ToolbarBackgroundBrushResource, "#FFFFFF", "#111827");
        _nodeToolbar.BorderBrush = GetResourceBrush(MindViewStyleKeys.ToolbarBorderBrushResource, "#D8E0EA", "#334155");
        _nodeToolbar.IsVisible = true;
        _nodeAddChildButton.IsVisible = true;
        PositionNodeToolbar();
        PositionNodeAddChildButton();
    }

    private void PositionNodeToolbar()
    {
        if (_toolbarNode is null || !_nodeFrames.TryGetValue(_toolbarNode, out _))
        {
            return;
        }

        var size = GetRenderedNodeSize(_toolbarNode);
        var x = _toolbarNode.X + size.Width / 2 - _nodeToolbar.Width / 2;
        var y = _toolbarNode.Y - _nodeToolbar.Height - 8;
        if (y < 8)
        {
            y = _toolbarNode.Y + size.Height + 8;
        }

        Canvas.SetLeft(_nodeToolbar, Math.Max(8, x));
        Canvas.SetTop(_nodeToolbar, Math.Max(8, y));
    }

    private void PositionNodeAddChildButton()
    {
        if (_toolbarNode is null || !_nodeFrames.TryGetValue(_toolbarNode, out _))
        {
            return;
        }

        var size = GetRenderedNodeSize(_toolbarNode);
        var buttonWidth = _nodeAddChildButton.Width > 0 ? _nodeAddChildButton.Width : 28;
        var buttonHeight = _nodeAddChildButton.Height > 0 ? _nodeAddChildButton.Height : 28;
        var x = _toolbarNode.X + size.Width + 10;
        var y = _toolbarNode.Y + size.Height / 2 - buttonHeight / 2;
        Canvas.SetLeft(_nodeAddChildButton, Math.Max(8, x));
        Canvas.SetTop(_nodeAddChildButton, Math.Max(8, y));
    }

    private void FocusNode(MindMapNode? node)
    {
        if (node is null)
        {
            return;
        }

        SetCurrentValue(SelectedNodeProperty, node);
        Dispatcher.UIThread.Post(() =>
        {
            if (_titleEditors.TryGetValue(node, out var editor))
            {
                editor.Focus();
                editor.CaretIndex = editor.Text?.Length ?? 0;
            }
        });
    }

    private static void FocusTextEditor(TextBox editor)
    {
        editor.Focus();
        editor.CaretIndex = editor.Text?.Length ?? 0;
    }

    private void FocusFrame(MindMapNode? node)
    {
        if (node is null)
        {
            return;
        }

        SetCurrentValue(SelectedNodeProperty, node);
        Dispatcher.UIThread.Post(() =>
        {
            if (_nodeFrames.TryGetValue(node, out var frame))
            {
                frame.Focus();
            }
        });
    }

    private void ApplySelectionState()
    {
        foreach (var (node, frame) in _nodeFrames)
        {
            var metrics = GetNodeMetrics(node);
            var selected = ReferenceEquals(node, SelectedNode);
            var hovered = ReferenceEquals(node, _hoverNode);
            var dragging = _isDraggingNode && ReferenceEquals(node, _dragNode);

            frame.Background = selected
                ? metrics.SelectedBackground ?? metrics.HoverBackground ?? metrics.Background
                : hovered
                    ? metrics.HoverBackground ?? metrics.Background
                    : metrics.Background;
            frame.BorderBrush = selected
                ? metrics.SelectedBorderBrush ?? GetSelectionBrush()
                : hovered
                    ? metrics.HoverBorderBrush ?? metrics.BorderBrush
                    : metrics.BorderBrush;
            frame.BorderThickness = selected
                ? metrics.SelectedBorderThickness ?? metrics.BorderThickness
                : metrics.BorderThickness;
            frame.BoxShadow = dragging
                ? metrics.DragBoxShadow ?? metrics.SelectedBoxShadow ?? metrics.HoverBoxShadow ?? metrics.BoxShadow
                : selected
                    ? metrics.SelectedBoxShadow ?? metrics.HoverBoxShadow ?? metrics.BoxShadow
                    : hovered
                        ? metrics.HoverBoxShadow ?? metrics.BoxShadow
                        : metrics.BoxShadow;
            frame.Opacity = dragging ? 0.72 : 1;
            UpdateNoteEditorVisibility(node);

        }

        PositionNodeToolbar();
        PositionNodeAddChildButton();
    }

}
