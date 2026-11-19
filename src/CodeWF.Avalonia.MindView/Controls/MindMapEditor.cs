using global::Avalonia;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using CodeWF.Avalonia.MindView.I18n;
using CodeWF.Toolkit.Lang;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CodeWF.Avalonia.MindView.Controls;

public partial class MindMapEditor : UserControl
{
    public static readonly StyledProperty<ObservableCollection<MindMapNode>?> RootsProperty =
        AvaloniaProperty.Register<MindMapEditor, ObservableCollection<MindMapNode>?>(nameof(Roots));

    public static readonly StyledProperty<MindMapNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<MindMapEditor, MindMapNode?>(
            nameof(SelectedNode),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<Color?> AccentColorProperty =
        AvaloniaProperty.Register<MindMapEditor, Color?>(nameof(AccentColor));

    public Color? AccentColor { get => GetValue(AccentColorProperty); set => SetValue(AccentColorProperty, value); }

    public static readonly StyledProperty<MindMapLayoutKind> LayoutKindProperty =
        AvaloniaProperty.Register<MindMapEditor, MindMapLayoutKind>(nameof(LayoutKind), MindMapLayoutKind.Right);

    public static readonly StyledProperty<MindMapStyleKind> StyleKindProperty =
        AvaloniaProperty.Register<MindMapEditor, MindMapStyleKind>(nameof(StyleKind), MindMapStyleKind.Quiet);

    public static readonly StyledProperty<bool> IsDarkThemeProperty =
        AvaloniaProperty.Register<MindMapEditor, bool>(nameof(IsDarkTheme));

    public static readonly StyledProperty<IMindMapEditorController?> ControllerProperty =
        AvaloniaProperty.Register<MindMapEditor, IMindMapEditorController?>(nameof(Controller));

    public static readonly StyledProperty<string> CenterTopicPlaceholderProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(CenterTopicPlaceholder),
            GetResource(MindViewL.CenterTopicPlaceholder, "Center topic"));

    public static readonly StyledProperty<string> TopicPlaceholderProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(TopicPlaceholder),
            GetResource(MindViewL.TopicPlaceholder, "Topic"));

    public static readonly StyledProperty<string> NotePlaceholderProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(NotePlaceholder),
            GetResource(MindViewL.NotePlaceholder, "Note"));

    public static readonly StyledProperty<string> AddSiblingTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(AddSiblingText),
            GetResource(MindViewL.AddSiblingText, "Add sibling topic"));

    public static readonly StyledProperty<string> AddChildTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(AddChildText),
            GetResource(MindViewL.AddChildText, "Add child topic"));

    public static readonly StyledProperty<string> PromoteTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(PromoteText),
            GetResource(MindViewL.PromoteText, "Promote"));

    public static readonly StyledProperty<string> DemoteTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DemoteText),
            GetResource(MindViewL.DemoteText, "Demote"));

    public static readonly StyledProperty<string> MoveUpTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(MoveUpText),
            GetResource(MindViewL.MoveUpText, "Move up"));

    public static readonly StyledProperty<string> MoveDownTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(MoveDownText),
            GetResource(MindViewL.MoveDownText, "Move down"));

    public static readonly StyledProperty<string> AddNoteTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(AddNoteText),
            GetResource(MindViewL.AddNoteText, "Add note"));

    public static readonly StyledProperty<string> EditNoteTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(EditNoteText),
            GetResource(MindViewL.EditNoteText, "Edit note"));

    public static readonly StyledProperty<string> DeleteNodeTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DeleteNodeText),
            GetResource(MindViewL.DeleteNodeText, "Delete node"));

    public static readonly StyledProperty<string> DragNodeTipProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DragNodeTip),
            GetResource(
                MindViewL.DragNodeTip,
                "Drop on the middle to make it a child; drop on the top or bottom edge to reorder siblings"));

    public static readonly StyledProperty<string> DropAsChildTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DropAsChildText),
            GetResource(DropAsChildTextResourceKey, "Set as child"));

    public static readonly StyledProperty<string> DropBeforeTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DropBeforeText),
            GetResource(DropBeforeTextResourceKey, "Insert before"));

    public static readonly StyledProperty<string> DropAfterTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DropAfterText),
            GetResource(DropAfterTextResourceKey, "Insert after"));

    public static readonly StyledProperty<string> DropUnavailableTextProperty =
        AvaloniaProperty.Register<MindMapEditor, string>(
            nameof(DropUnavailableText),
            GetResource(DropUnavailableTextResourceKey, "Cannot drop here"));

    public static readonly DirectProperty<MindMapEditor, string> ZoomTextProperty =
        AvaloniaProperty.RegisterDirect<MindMapEditor, string>(
            nameof(ZoomText),
            editor => editor.ZoomText);

    public static readonly DirectProperty<MindMapEditor, Rect> ViewportBoundsProperty =
        AvaloniaProperty.RegisterDirect<MindMapEditor, Rect>(
            nameof(ViewportBounds),
            editor => editor.ViewportBounds);

    private double MinCanvasWidth => GetResourceDouble(MindViewStyleKeys.MinCanvasWidthResource, 920);
    private double MinCanvasHeight => GetResourceDouble(MindViewStyleKeys.MinCanvasHeightResource, 620);
    private double MinCanvasHorizontalPadding => GetResourceDouble(MindViewStyleKeys.MinCanvasHorizontalPaddingResource, 560);
    private double MinCanvasVerticalPadding => GetResourceDouble(MindViewStyleKeys.MinCanvasVerticalPaddingResource, 180);
    private double MinZoom => GetResourceDouble(MindViewStyleKeys.MinZoomResource, 0.1);
    private double MaxZoom => GetResourceDouble(MindViewStyleKeys.MaxZoomResource, 2.0);
    private double ZoomFactor => GetResourceDouble(MindViewStyleKeys.ZoomFactorResource, 1.1);
    private double TouchPadMagnifySensitivity => GetResourceDouble(MindViewStyleKeys.TouchPadMagnifySensitivityResource, 16);
    private double TouchPadMagnifyMaxSteps => GetResourceDouble(MindViewStyleKeys.TouchPadMagnifyMaxStepsResource, 4);
    private double WheelPanStep => GetResourceDouble(MindViewStyleKeys.WheelPanStepResource, 72);
    private int RebuildConnectorBatchSize => GetResourceInt32(MindViewStyleKeys.RebuildConnectorBatchSizeResource, 160);
    private int RebuildNodeBatchSize => GetResourceInt32(MindViewStyleKeys.RebuildNodeBatchSizeResource, 48);
    private double DragStartDistance => GetResourceDouble(MindViewStyleKeys.DragStartDistanceResource, 6);
    private double DropEdgeRatio => GetResourceDouble(MindViewStyleKeys.DropEdgeRatioResource, 0.28);
    private double NodeMenuWidth => GetResourceDouble(MindViewStyleKeys.NodeMenuWidthResource, 224);
    private const double RootViewportLeftInset = 72;
    private const string DropAsChildTextResourceKey = "CodeWF.Avalonia.MindView.MindViewL.DropAsChildText";
    private const string DropBeforeTextResourceKey = "CodeWF.Avalonia.MindView.MindViewL.DropBeforeText";
    private const string DropAfterTextResourceKey = "CodeWF.Avalonia.MindView.MindViewL.DropAfterText";
    private const string DropUnavailableTextResourceKey = "CodeWF.Avalonia.MindView.MindViewL.DropUnavailableText";
    private static readonly string[] FallbackPalette =
    [
        "#2563EB",
        "#16A34A",
        "#F97316",
        "#DB2777",
        "#7C3AED",
        "#0891B2",
        "#DC2626",
        "#CA8A04",
        "#0D9488",
        "#4F46E5"
    ];

    private readonly Canvas _canvas = new()
    {
        Background = Brushes.Transparent,
        Cursor = MindMapCursors.Grab ?? new Cursor(StandardCursorType.Hand),
    };
    private readonly LayoutTransformControl _zoomHost;
    private readonly ScrollViewer _scrollViewer;
    private readonly global::Avalonia.Controls.Shapes.Path _dropPreviewPath = new()
    {
        StrokeThickness = 2,
        StrokeDashArray = new AvaloniaList<double> { 5, 4 },
        IsHitTestVisible = false,
        IsVisible = false
    };
    private readonly TextBlock _dropPreviewText = new()
    {
        FontSize = 12,
        FontWeight = FontWeight.SemiBold,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
    };
    private readonly Border _dropPreviewLabel;

    private readonly Dictionary<MindMapNode, Border> _nodeFrames = [];
    private readonly Dictionary<MindMapNode, TextBox> _titleEditors = [];
    private readonly Dictionary<MindMapNode, TextBox> _noteEditors = [];
    private readonly Dictionary<MindMapNode, Border> _noteFrames = [];
    private readonly HashSet<MindMapNode> _editingNoteNodes = [];
    private readonly List<Connector> _connectors = [];
    private readonly List<MindMapNode> _observedNodes = [];
    private readonly List<INotifyCollectionChanged> _observedCollections = [];
    private readonly HashSet<AvaloniaProperty> _hostTextProperties = [];

    private int _rebuildVersion;
    private bool _isRebuildingVisuals;
    private bool _isApplyingLocalizedText;
    private bool _isI18nSubscribed;
    private MindMapNode? _dragNode;
    private Point _dragStartPointer;
    private bool _isDraggingNode;
    private MindMapNode? _dropTarget;
    private MindMapDropPlacement _dropPlacement = MindMapDropPlacement.Child;
    private int _dropFeedbackVersion;
    private bool _isPanningCanvas;
    private bool _isSpacePressed;
    private bool _isPinching;
    private bool _shouldPlaceRootNearLeftCenter;
    private double _pinchStartZoom = 1;
    private RoutedEventArgs? _lastHandledTouchPadMagnifyEvent;
    private RoutedEventArgs? _lastHandledPinchEvent;
    private KeyEventArgs? _lastHandledEditorKeyEvent;
    private MindMapNode? _hoverNode;
    private MindMapNode? _toolbarNode;
    private int _nextPaletteIndex = Random.Shared.Next(FallbackPalette.Length);
    private Point _panStartPointer;
    private Vector _panStartOffset;
    private double _zoomScale = 1;
    private string _zoomText = "100%";
    private Rect _viewportBounds;
    private TopLevel? _topLevel;
    private Border _nodeToolbar;
    private Button _nodeAddChildButton;
    private readonly StackPanel _nodeMenuPanel = new()
    {
        Spacing = 2
    };
    private Border _nodeMenu;

    public MindMapEditor()
    {
        Focusable = true;
        ActualThemeVariantChanged += (_, _) =>
        {
            SetCurrentValue(IsDarkThemeProperty, MindViewThemeResources.IsDarkVariant(ActualThemeVariant));
            ApplyTheme();
            RecreateNodeChrome();
        };
        _zoomHost = new LayoutTransformControl
        {
            Child = _canvas,
            LayoutTransform = CreateZoomTransform(_zoomScale)
        };

        _scrollViewer = new ScrollViewer
        {
            Content = _zoomHost,
            Background = _canvas.Background,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        RefreshLocalizedText(recreateChrome: false);
        _nodeToolbar = CreateNodeToolbar();
        _nodeAddChildButton = CreateNodeAddChildButton();
        _nodeMenu = CreateNodeMenu();
        _dropPreviewLabel = CreateDropPreviewLabel();
        LostFocus += (_, _) => HideNodeMenu();

        var viewport = new Grid();
        viewport.Children.Add(_scrollViewer);

        Content = viewport;
        ApplyTheme();
        UpdateZoomText();
        _scrollViewer.PropertyChanged += HandleScrollViewerPropertyChanged;
        AddHandler(PointerPressedEvent, HandleCanvasPanStarted, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, HandleCanvasPanned, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, HandleCanvasPanCompleted, RoutingStrategies.Tunnel, handledEventsToo: true);
        _scrollViewer.AddHandler(PointerPressedEvent, HandleCanvasPanStarted, RoutingStrategies.Tunnel, handledEventsToo: true);
        _scrollViewer.AddHandler(PointerMovedEvent, HandleCanvasPanned, RoutingStrategies.Tunnel, handledEventsToo: true);
        _scrollViewer.AddHandler(PointerReleasedEvent, HandleCanvasPanCompleted, RoutingStrategies.Tunnel, handledEventsToo: true);
        _scrollViewer.PointerCaptureLost += (_, _) => StopCanvasPan();
        _scrollViewer.GestureRecognizers.Add(new PinchGestureRecognizer());
        AddHandler(KeyDownEvent, HandleKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, HandleKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerWheelChangedEvent, HandlePointerWheelChanged, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerTouchPadGestureMagnifyEvent, HandleTouchPadMagnify, RoutingStrategies.Tunnel, handledEventsToo: true);
        _scrollViewer.AddHandler(PointerTouchPadGestureMagnifyEvent, HandleTouchPadMagnify, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _canvas.AddHandler(PointerTouchPadGestureMagnifyEvent, HandleTouchPadMagnify, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _scrollViewer.AddHandler(PinchEvent, HandlePinch, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        _scrollViewer.AddHandler(PinchEndedEvent, HandlePinchEnded, RoutingStrategies.Direct | RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    public ObservableCollection<MindMapNode>? Roots
    {
        get => GetValue(RootsProperty);
        set => SetValue(RootsProperty, value);
    }

    public MindMapNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    public bool IsDarkTheme
    {
        get => GetValue(IsDarkThemeProperty);
        set => SetValue(IsDarkThemeProperty, value);
    }

    public IMindMapEditorController? Controller
    {
        get => GetValue(ControllerProperty);
        set => SetValue(ControllerProperty, value);
    }

    public string CenterTopicPlaceholder
    {
        get => GetValue(CenterTopicPlaceholderProperty);
        set => SetValue(CenterTopicPlaceholderProperty, value);
    }

    public string TopicPlaceholder
    {
        get => GetValue(TopicPlaceholderProperty);
        set => SetValue(TopicPlaceholderProperty, value);
    }

    public string NotePlaceholder
    {
        get => GetValue(NotePlaceholderProperty);
        set => SetValue(NotePlaceholderProperty, value);
    }

    public string AddSiblingText
    {
        get => GetValue(AddSiblingTextProperty);
        set => SetValue(AddSiblingTextProperty, value);
    }

    public string AddChildText
    {
        get => GetValue(AddChildTextProperty);
        set => SetValue(AddChildTextProperty, value);
    }

    public string PromoteText
    {
        get => GetValue(PromoteTextProperty);
        set => SetValue(PromoteTextProperty, value);
    }

    public string DemoteText
    {
        get => GetValue(DemoteTextProperty);
        set => SetValue(DemoteTextProperty, value);
    }

    public string MoveUpText
    {
        get => GetValue(MoveUpTextProperty);
        set => SetValue(MoveUpTextProperty, value);
    }

    public string MoveDownText
    {
        get => GetValue(MoveDownTextProperty);
        set => SetValue(MoveDownTextProperty, value);
    }

    public string AddNoteText
    {
        get => GetValue(AddNoteTextProperty);
        set => SetValue(AddNoteTextProperty, value);
    }

    public string EditNoteText
    {
        get => GetValue(EditNoteTextProperty);
        set => SetValue(EditNoteTextProperty, value);
    }

    public string DeleteNodeText
    {
        get => GetValue(DeleteNodeTextProperty);
        set => SetValue(DeleteNodeTextProperty, value);
    }

    public string DragNodeTip
    {
        get => GetValue(DragNodeTipProperty);
        set => SetValue(DragNodeTipProperty, value);
    }

    public string DropAsChildText
    {
        get => GetValue(DropAsChildTextProperty);
        set => SetValue(DropAsChildTextProperty, value);
    }

    public string DropBeforeText
    {
        get => GetValue(DropBeforeTextProperty);
        set => SetValue(DropBeforeTextProperty, value);
    }

    public string DropAfterText
    {
        get => GetValue(DropAfterTextProperty);
        set => SetValue(DropAfterTextProperty, value);
    }

    public string DropUnavailableText
    {
        get => GetValue(DropUnavailableTextProperty);
        set => SetValue(DropUnavailableTextProperty, value);
    }

    public string ZoomText
    {
        get => _zoomText;
        private set => SetAndRaise(ZoomTextProperty, ref _zoomText, value);
    }

    public Rect ViewportBounds
    {
        get => _viewportBounds;
        private set => SetAndRaise(ViewportBoundsProperty, ref _viewportBounds, value);
    }

    public MindMapLayoutKind LayoutKind
    {
        get => GetValue(LayoutKindProperty);
        set => SetValue(LayoutKindProperty, value);
    }

    public MindMapStyleKind StyleKind
    {
        get => GetValue(StyleKindProperty);
        set => SetValue(StyleKindProperty, value);
    }

    /// <summary>
    /// 按当前树结构重新计算节点坐标。未提供 Controller 时，控件的内置结构操作会自动调用它。
    /// </summary>
    public void ArrangeNodes()
    {
        if (Roots is null || Roots.Count == 0)
        {
            return;
        }

        foreach (var root in Roots)
        {
            AssignMissingColors(root);
        }

        MindMapLayout.Arrange(Roots, LayoutKind);
        UpdateConnectors();
    }

    /// <summary>
    /// 添加子主题。宿主提供 Controller 时委托给宿主，否则使用控件内置树操作。
    /// </summary>
    public MindMapNode AddChild(MindMapNode? parent = null, string title = "")
    {
        if (ControllerContext is { } controller)
        {
            return controller.AddChild(parent, title);
        }

        parent = ResolveEditableNode(parent);
        var child = CreateNode(title);
        parent.Children.Add(child);
        SelectedNode = child;
        ArrangeNodes();
        return child;
    }

    /// <summary>
    /// 添加同级主题；根节点会转为添加子主题，避免产生多个中心主题。
    /// </summary>
    public MindMapNode AddSibling(MindMapNode? node = null, string title = "")
    {
        if (ControllerContext is { } controller)
        {
            return controller.AddSibling(node, title);
        }

        node = ResolveEditableNode(node);
        if (IsRootNode(node))
        {
            return AddChild(node, title);
        }

        var parent = FindParent(node) ?? EnsureRoot();
        var sibling = CreateNode(title);
        var index = parent.Children.IndexOf(node);
        parent.Children.Insert(index + 1, sibling);
        SelectedNode = sibling;
        ArrangeNodes();
        return sibling;
    }

    public bool CanPromoteNode(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.CanPromoteNode(node);
        }

        if (node is null || IsRootNode(node))
        {
            return false;
        }

        var parent = FindParent(node);
        return parent is not null && !IsRootNode(parent) && FindParent(parent) is not null;
    }

    public bool PromoteNode(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.PromoteNode(node);
        }

        if (!CanPromoteNode(node) || node is null)
        {
            return false;
        }

        var parent = FindParent(node)!;
        var grandParent = FindParent(parent)!;
        parent.Children.Remove(node);
        var parentIndex = grandParent.Children.IndexOf(parent);
        grandParent.Children.Insert(parentIndex + 1, node);
        SelectedNode = node;
        ArrangeNodes();
        return true;
    }

    public bool CanDemoteNode(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.CanDemoteNode(node);
        }

        if (node is null || IsRootNode(node))
        {
            return false;
        }

        var parent = FindParent(node);
        return parent is not null && parent.Children.IndexOf(node) > 0;
    }

    public bool DemoteNode(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.DemoteNode(node);
        }

        if (!CanDemoteNode(node) || node is null)
        {
            return false;
        }

        var parent = FindParent(node)!;
        var index = parent.Children.IndexOf(node);
        var newParent = parent.Children[index - 1];
        parent.Children.RemoveAt(index);
        newParent.Children.Add(node);
        SelectedNode = node;
        ArrangeNodes();
        return true;
    }

    public bool CanMoveNodeUp(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.CanMoveNodeUp(node);
        }

        if (node is null || IsRootNode(node))
        {
            return false;
        }

        var parent = FindParent(node);
        return parent is not null && parent.Children.IndexOf(node) > 0;
    }

    public bool MoveNodeUp(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.MoveNodeUp(node);
        }

        return MoveNodeWithinSiblings(node, -1);
    }

    public bool CanMoveNodeDown(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.CanMoveNodeDown(node);
        }

        if (node is null || IsRootNode(node))
        {
            return false;
        }

        var parent = FindParent(node);
        if (parent is null)
        {
            return false;
        }

        var index = parent.Children.IndexOf(node);
        return index >= 0 && index < parent.Children.Count - 1;
    }

    public bool MoveNodeDown(MindMapNode? node = null)
    {
        node ??= SelectedNode;
        if (ControllerContext is { } controller)
        {
            return controller.MoveNodeDown(node);
        }

        return MoveNodeWithinSiblings(node, 1);
    }

    public MindMapNode DeleteNode(MindMapNode? node = null)
    {
        if (ControllerContext is { } controller)
        {
            return controller.DeleteNode(node);
        }

        node = ResolveEditableNode(node);
        if (IsRootNode(node))
        {
            SelectedNode = node;
            return node;
        }

        var parent = FindParent(node) ?? EnsureRoot();
        var index = parent.Children.IndexOf(node);
        var focusTarget = index > 0 ? parent.Children[index - 1] : parent;
        parent.Children.Remove(node);
        SelectedNode = focusTarget;
        ArrangeNodes();
        return focusTarget;
    }

    public bool CanMoveNode(MindMapNode? node, MindMapNode? target)
    {
        if (ControllerContext is { } controller)
        {
            return controller.CanMoveNode(node, target);
        }

        return node is not null
            && target is not null
            && !IsRootNode(node)
            && !ReferenceEquals(node, target)
            && !IsDescendant(node, target);
    }

    public bool MoveNode(MindMapNode? node, MindMapNode? target, MindMapDropPlacement placement)
    {
        if (ControllerContext is { } controller)
        {
            return controller.MoveNode(node, target, placement);
        }

        if (!CanMoveNode(node, target) || node is null || target is null)
        {
            return false;
        }

        var oldParent = FindParent(node);
        if (oldParent is null)
        {
            return false;
        }

        if (IsRootNode(target) && placement is MindMapDropPlacement.Before or MindMapDropPlacement.After)
        {
            placement = MindMapDropPlacement.Child;
        }

        var newParent = placement == MindMapDropPlacement.Child
            ? target
            : FindParent(target) ?? EnsureRoot();
        var insertionIndex = placement switch
        {
            MindMapDropPlacement.Before => newParent.Children.IndexOf(target),
            MindMapDropPlacement.After => newParent.Children.IndexOf(target) + 1,
            _ => newParent.Children.Count
        };

        var oldIndex = oldParent.Children.IndexOf(node);
        if (oldIndex < 0)
        {
            return false;
        }

        oldParent.Children.RemoveAt(oldIndex);
        if (ReferenceEquals(oldParent, newParent) && oldIndex < insertionIndex)
        {
            insertionIndex--;
        }

        insertionIndex = Math.Clamp(insertionIndex, 0, newParent.Children.Count);
        newParent.Children.Insert(insertionIndex, node);
        SelectedNode = node;
        ArrangeNodes();
        return true;
    }

    private IMindMapEditorController? ControllerContext => Controller ?? DataContext as IMindMapEditorController;

    private MindMapNode ResolveEditableNode(MindMapNode? node)
    {
        if (node is not null && ContainsNode(node))
        {
            return node;
        }

        if (SelectedNode is not null && ContainsNode(SelectedNode))
        {
            return SelectedNode;
        }

        return EnsureRoot();
    }

    private MindMapNode EnsureRoot()
    {
        var roots = Roots;
        if (roots is null)
        {
            roots = [];
            Roots = roots;
        }

        if (roots.Count == 0)
        {
            roots.Add(CreateNode(string.Empty));
        }

        return roots[0];
    }

    private MindMapNode CreateNode(string title)
    {
        return new MindMapNode(title)
        {
            AccentColor = NextColor()
        };
    }

    private void AssignMissingColors(MindMapNode node)
    {
        if (string.IsNullOrWhiteSpace(node.AccentColor))
        {
            node.AccentColor = NextColor();
        }

        foreach (var child in node.Children)
        {
            AssignMissingColors(child);
        }
    }

    private string NextColor()
    {
        var palette = GetPalette();
        return palette[_nextPaletteIndex++ % palette.Length];
    }

    private string[] GetPalette()
    {
        var paletteText = GetResourceString(
            MindViewStyleKeys.DefaultPaletteResource,
            string.Join(";", FallbackPalette));
        var palette = paletteText
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(value => value.Length > 0)
            .ToArray();
        return palette.Length == 0 ? FallbackPalette : palette;
    }

    private bool IsRootNode(MindMapNode? node)
    {
        if (ControllerContext is { } controller)
        {
            return controller.IsRoot(node);
        }

        return node is not null && Roots?.Contains(node) == true;
    }

    private int GetNodeLevel(MindMapNode node)
    {
        if (ControllerContext is { } controller)
        {
            return controller.GetLevel(node);
        }

        return FindLevel(node) ?? 1;
    }

    private int? FindLevel(MindMapNode node)
    {
        if (Roots is null)
        {
            return null;
        }

        foreach (var root in Roots)
        {
            var level = FindLevel(root, node, 1);
            if (level is not null)
            {
                return level;
            }
        }

        return null;
    }

    private static int? FindLevel(MindMapNode current, MindMapNode node, int level)
    {
        if (ReferenceEquals(current, node))
        {
            return level;
        }

        foreach (var child in current.Children)
        {
            var childLevel = FindLevel(child, node, level + 1);
            if (childLevel is not null)
            {
                return childLevel;
            }
        }

        return null;
    }

    private bool ContainsNode(MindMapNode node)
    {
        return FindLevel(node) is not null;
    }

    private MindMapNode? FindParent(MindMapNode node)
    {
        if (Roots is null)
        {
            return null;
        }

        foreach (var root in Roots)
        {
            var parent = FindParent(root, node);
            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    private static MindMapNode? FindParent(MindMapNode candidateParent, MindMapNode node)
    {
        if (candidateParent.Children.Contains(node))
        {
            return candidateParent;
        }

        foreach (var child in candidateParent.Children)
        {
            var parent = FindParent(child, node);
            if (parent is not null)
            {
                return parent;
            }
        }

        return null;
    }

    private static bool IsDescendant(MindMapNode candidateAncestor, MindMapNode candidateDescendant)
    {
        foreach (var child in candidateAncestor.Children)
        {
            if (ReferenceEquals(child, candidateDescendant) || IsDescendant(child, candidateDescendant))
            {
                return true;
            }
        }

        return false;
    }

    private MindMapNode HandleMapEnter(MindMapNode node)
    {
        if (ControllerContext is { } controller)
        {
            return controller.HandleMapEnter(node);
        }

        return IsRootNode(node) ? AddChild(node, string.Empty) : AddSibling(node, string.Empty);
    }

    private MindMapNode HandleMapTab(MindMapNode node)
    {
        if (ControllerContext is { } controller)
        {
            return controller.HandleMapTab(node);
        }

        DemoteNode(node);
        return node;
    }

    private bool MoveNodeWithinSiblings(MindMapNode? node, int offset)
    {
        if (node is null || IsRootNode(node))
        {
            return false;
        }

        var parent = FindParent(node);
        if (parent is null)
        {
            return false;
        }

        var oldIndex = parent.Children.IndexOf(node);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= parent.Children.Count)
        {
            return false;
        }

        parent.Children.Move(oldIndex, newIndex);
        SelectedNode = node;
        ArrangeNodes();
        return true;
    }

    private void SubscribeToI18n()
    {
        if (_isI18nSubscribed)
        {
            return;
        }

        I18nManager.Instance.CultureChanged += HandleCultureChanged;
        I18nManager.Instance.ResourcesChanged += HandleCultureChanged;
        _isI18nSubscribed = true;
    }

    private void UnsubscribeFromI18n()
    {
        if (!_isI18nSubscribed)
        {
            return;
        }

        I18nManager.Instance.CultureChanged -= HandleCultureChanged;
        I18nManager.Instance.ResourcesChanged -= HandleCultureChanged;
        _isI18nSubscribed = false;
    }

    private void HandleCultureChanged(object? sender, EventArgs e)
    {
        if (!global::Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleCultureChanged(sender, e));
            return;
        }
        if (!_isI18nSubscribed) return;
        RefreshLocalizedText();
    }

    private void RefreshLocalizedText(bool recreateChrome = true)
    {
        _isApplyingLocalizedText = true;
        try
        {
            SetLocalizedText(CenterTopicPlaceholderProperty, MindViewL.CenterTopicPlaceholder, "Center topic");
            SetLocalizedText(TopicPlaceholderProperty, MindViewL.TopicPlaceholder, "Topic");
            SetLocalizedText(NotePlaceholderProperty, MindViewL.NotePlaceholder, "Note");
            SetLocalizedText(AddSiblingTextProperty, MindViewL.AddSiblingText, "Add sibling topic");
            SetLocalizedText(AddChildTextProperty, MindViewL.AddChildText, "Add child topic");
            SetLocalizedText(PromoteTextProperty, MindViewL.PromoteText, "Promote");
            SetLocalizedText(DemoteTextProperty, MindViewL.DemoteText, "Demote");
            SetLocalizedText(MoveUpTextProperty, MindViewL.MoveUpText, "Move up");
            SetLocalizedText(MoveDownTextProperty, MindViewL.MoveDownText, "Move down");
            SetLocalizedText(AddNoteTextProperty, MindViewL.AddNoteText, "Add note");
            SetLocalizedText(EditNoteTextProperty, MindViewL.EditNoteText, "Edit note");
            SetLocalizedText(DeleteNodeTextProperty, MindViewL.DeleteNodeText, "Delete node");
            SetLocalizedText(
                DragNodeTipProperty,
                MindViewL.DragNodeTip,
                "Drop on the middle to make it a child; drop on the top or bottom edge to reorder siblings");
            SetLocalizedText(DropAsChildTextProperty, DropAsChildTextResourceKey, "Set as child");
            SetLocalizedText(DropBeforeTextProperty, DropBeforeTextResourceKey, "Insert before");
            SetLocalizedText(DropAfterTextProperty, DropAfterTextResourceKey, "Insert after");
            SetLocalizedText(DropUnavailableTextProperty, DropUnavailableTextResourceKey, "Cannot drop here");
        }
        finally
        {
            _isApplyingLocalizedText = false;
        }

        if (recreateChrome)
        {
            RecreateNodeChrome();
        }
    }

    private void SetLocalizedText(StyledProperty<string> property, string key, string fallback)
    {
        if (!_hostTextProperties.Contains(property))
        {
            SetValue(property, GetResource(key, fallback));
        }
    }

    private static string GetResource(string key, string fallback)
    {
        var value = MindViewLocalization.GetResource(key);
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static bool IsTextResourceProperty(AvaloniaProperty property)
    {
        return property == CenterTopicPlaceholderProperty
               || property == TopicPlaceholderProperty
               || property == NotePlaceholderProperty
               || property == AddSiblingTextProperty
               || property == AddChildTextProperty
               || property == PromoteTextProperty
               || property == DemoteTextProperty
               || property == MoveUpTextProperty
               || property == MoveDownTextProperty
               || property == AddNoteTextProperty
               || property == EditNoteTextProperty
               || property == DeleteNodeTextProperty
               || property == DragNodeTipProperty
               || property == DropAsChildTextProperty
               || property == DropBeforeTextProperty
               || property == DropAfterTextProperty
               || property == DropUnavailableTextProperty;
    }

    private void RecreateNodeChrome()
    {
        _nodeMenu.Child = null;
        _nodeMenuPanel.Children.Clear();
        _nodeToolbar = CreateNodeToolbar();
        _nodeAddChildButton = CreateNodeAddChildButton();
        _nodeMenu = CreateNodeMenu();
        Rebuild();
    }

}
