using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.VisualTree;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Styling;

using CodeWF.Avalonia.Markdown.Editor;
using CodeWF.Avalonia.Markdown.Controls;

using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using CodeWF.Avalonia.Markdown.Core;
using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>
/// 单栏实时编辑（所见即所得）视图：每个 Markdown 块渲染成富文本，
/// 点入该块就地按源码编辑，离开即回渲染；表格直接渲染为表格、任务列表可直接勾选。
/// <para>
/// 与 <see cref="MarkdownEditorView"/>（源码编辑器）保持一致的对外语义：
/// <see cref="SetText"/> 与 <see cref="ApplyExternalEdit"/> 不会触发 <see cref="MarkdownChanged"/>，
/// 只有用户编辑才回抛。
/// </para>
/// </summary>
public partial class MarkdownLiveEditorView : UserControl
{
    private const string DefaultEditorBackgroundKey = "CodeWFMarkdownEditorBackgroundBrush";
    private const string DefaultEditorForegroundKey = "CodeWFMarkdownEditorForegroundBrush";
    private const string DefaultSeparatorKey = "CodeWFMarkdownEditorSeparatorBrush";

    private readonly MarkdownEditorOptions _options;
    private readonly IMarkdownEditorLocalizer _localizer;
    private readonly MarkdownBlockViewOptions _blockOptions;
    private readonly WindowedStackPanel _blockHost = new() { EstimatedItemHeight = 55, OverscanViewports = 1 };
    private readonly List<int> _hostIndices = [];
    private readonly List<Control> _blockViews = [];
    private readonly List<MarkdownBlockModel> _models = [];
    private readonly MarkdownBlockEditorCore _core;
    private readonly Border _container;
    private readonly Border _paneHead;
    private readonly TextBlock _paneTitle;
    private readonly Border _chipHost;
    private readonly TextBlock _chipText = new();

    private bool _suppressChanged;
    private bool _autoPairEnabled = true;
    private int _activeIndex = -1;
    private readonly List<MarkdownLiveEditorSnapshot> _undo = [];
    private readonly List<MarkdownLiveEditorSnapshot> _redo = [];
    private MarkdownLiveEditorSnapshot _lastSnapshot = new(string.Empty, -1, 0, 0);
    private bool _restoringHistory;

    public static readonly StyledProperty<string?> ImageBasePathProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string?>(nameof(ImageBasePath));

    /// <summary>相对图片的文档目录。</summary>
    public string? ImageBasePath
    {
        get => GetValue(ImageBasePathProperty);
        set => SetValue(ImageBasePathProperty, value);
    }

    /// <summary>当前文档的预览语法选项；更改不修改正文或撤销历史。</summary>
    public static readonly StyledProperty<MarkdownSyntaxOptions?> SyntaxOptionsProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, MarkdownSyntaxOptions?>(nameof(SyntaxOptions));
    public MarkdownSyntaxOptions? SyntaxOptions { get => GetValue(SyntaxOptionsProperty); set => SetValue(SyntaxOptionsProperty, value); }

    /// <summary>代码块字号，独立于正文；默认保持原有 13px。</summary>
    public static readonly StyledProperty<double> CodeFontSizeProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, double>(nameof(CodeFontSize), 13);
    public double CodeFontSize { get => GetValue(CodeFontSizeProperty); set => SetValue(CodeFontSizeProperty, value); }
    public static readonly StyledProperty<bool> CodeWrapProperty = AvaloniaProperty.Register<MarkdownLiveEditorView, bool>(nameof(CodeWrap));
    public bool CodeWrap { get => GetValue(CodeWrapProperty); set => SetValue(CodeWrapProperty, value); }

    /// <summary>正文最大宽度（含留白），默认不限制；宿主可控制阅读行长。</summary>
    public static readonly StyledProperty<double> DocumentMaxWidthProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, double>(nameof(DocumentMaxWidth), double.PositiveInfinity);
    public double DocumentMaxWidth { get => GetValue(DocumentMaxWidthProperty); set => SetValue(DocumentMaxWidthProperty, value); }
    public static readonly StyledProperty<Thickness> DocumentPaddingProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, Thickness>(nameof(DocumentPadding), new Thickness(32, 20, 32, 48));
    public Thickness DocumentPadding { get => GetValue(DocumentPaddingProperty); set => SetValue(DocumentPaddingProperty, value); }

    /// <summary>最近一次上下文请求命中块的源码行；可绑定到宿主菜单 ViewModel。</summary>
    public static readonly DirectProperty<MarkdownLiveEditorView, int> ContextSourceLineProperty =
        AvaloniaProperty.RegisterDirect<MarkdownLiveEditorView, int>(nameof(ContextSourceLine),
            editor => editor.ContextSourceLine, (editor, value) => editor.ContextSourceLine = value,
            defaultBindingMode: global::Avalonia.Data.BindingMode.OneWayToSource);
    private int _contextSourceLine = 1;
    public int ContextSourceLine
    {
        get => _contextSourceLine;
        set => SetAndRaise(ContextSourceLineProperty, ref _contextSourceLine, Math.Max(1, value));
    }

    private void UpdateContextSourceLine(Point position)
    {
        ContextSourceLine = 1;
        for (var i = 0; i < _blockViews.Count; i++)
        {
            var block = _blockViews[i];
            if (!block.IsEffectivelyVisible || block.Bounds.Width <= 0 || block.Bounds.Height <= 0
                || block.TranslatePoint(default, this) is not { } origin) continue;
            if (new Rect(origin, block.Bounds.Size).Contains(position))
            {
                ContextSourceLine = Math.Max(1, _models[i].SourceLine);
                return;
            }
        }
    }

    /// <summary>按当前文档源码行定位块，不改写内容；可选择仅滚动或进入编辑。</summary>
    public void NavigateToSourceLine(int line, bool beginEdit = false)
    {
        if (_blockViews.Count == 0) return;
        var parsed = MarkdownBlockParser.Parse(Text);
        var index = parsed.FindLastIndex(model => model.SourceLine <= Math.Max(1, line));
        index = Math.Clamp(index, 0, _blockViews.Count - 1);
        if (index < 0) return;
        RevealBlock(index);
        if (beginEdit) FocusBlock(index);
    }

    public static readonly StyledProperty<double> DocumentBlockSpacingProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, double>(nameof(DocumentBlockSpacing));
    public double DocumentBlockSpacing { get => GetValue(DocumentBlockSpacingProperty); set => SetValue(DocumentBlockSpacingProperty, value); }

    public MarkdownLiveEditorView()
        : this(new MarkdownEditorOptions(), null)
    {
    }

    public MarkdownLiveEditorView(
        MarkdownEditorOptions? options = null,
        IMarkdownEditorLocalizer? localizer = null)
    {
        _options = options ?? new MarkdownEditorOptions();
        _autoPairEnabled = _options.EnableAutoPair;
        _localizer = localizer ?? new MarkdownEditorLocalizer();
        _blockOptions = new MarkdownBlockViewOptions();

        _blockHost.Spacing = DocumentBlockSpacing;
        _blockHost.Margin = DocumentPadding;
        Focusable = true;

        BuildPaneHead(out _paneHead, out _chipHost, out _paneTitle);
        UpdatePaneHead();

        _container = new Border { Child = _blockHost, MaxWidth = DocumentMaxWidth };
        // Typora 行为：点击块间/文档空白，光标落到最近的段落块（空白文档也能点击落光标）。
        // 点在块内时交给块自己的 OnPointerPressed（BeginEdit 保 caret），这里只接管空白区。
        // 挂自身根 Tunnel：点击落在块宿主边界外（文档下方空白）时事件不经过 _container，
        // Tunnel 从根向下必经此 handler；点在块内时交还给块自己的处理。
        this.AddHandler(PointerPressedEvent, (_, e) =>
        {
            try { CodeWF.Toolkit.Logging.Logger.Debug("live pressed src=" + e.Source?.GetType().Name); } catch { }
            if (e.Source is not Visual source) return;
            foreach (var view in _blockViews)
            {
                if (ReferenceEquals(source, view) || source.GetVisualAncestors().Contains(view)) return;
            }
            var nearest = FindNearestBlockIndex(e.GetCurrentPoint(_container).Position.Y);
            if (nearest >= 0) FocusBlock(nearest);
        }, RoutingStrategies.Tunnel);
        Content = BuildContent();

        _core = new MarkdownBlockEditorCore(_models);
        SetText(string.Empty);
        ActualThemeVariantChanged += (_, _) => ApplyThemedVisuals();
        AttachedToVisualTree += (_, _) => ApplyThemedVisuals();
        ApplyThemedVisuals();
        InitializeDocumentSelection();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.Z or Key.Y)
            {
                ApplyAction(e.Key == Key.Z ? MarkdownEditorAction.Undo : MarkdownEditorAction.Redo);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    /// <summary>文档内容变化（用户编辑产生）；参数为当前全文 Markdown。</summary>
    public event EventHandler<string>? MarkdownChanged;

    /// <summary>插入点变化：行、列、总块数。</summary>
    public event EventHandler<MarkdownCaretEventArgs>? SelectionChanged;

    /// <summary>动作已执行，宿主可同步自身状态。</summary>
    public event EventHandler<MarkdownEditorAction>? ActionRequested;

    /// <summary>窗格头标题；为空时不显示窗格头。</summary>
    public static readonly StyledProperty<string?> HeaderTextProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string?>(nameof(HeaderText));

    /// <summary>窗格头右侧芯片文案；为空时不显示。</summary>
    public static readonly StyledProperty<string?> ChipTextProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string?>(nameof(ChipText), "Markdown");

    /// <summary>设计令牌键：背景色。</summary>
    public static readonly StyledProperty<string> EditorBackgroundKeyProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string>(nameof(EditorBackgroundKey), DefaultEditorBackgroundKey);

    /// <summary>设计令牌键：前景色。</summary>
    public static readonly StyledProperty<string> EditorForegroundKeyProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string>(nameof(EditorForegroundKey), DefaultEditorForegroundKey);

    public string? HeaderText
    {
        get => GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string? ChipText
    {
        get => GetValue(ChipTextProperty);
        set => SetValue(ChipTextProperty, value);
    }

    public string EditorBackgroundKey
    {
        get => GetValue(EditorBackgroundKeyProperty);
        set => SetValue(EditorBackgroundKeyProperty, value);
    }

    public string EditorForegroundKey
    {
        get => GetValue(EditorForegroundKeyProperty);
        set => SetValue(EditorForegroundKeyProperty, value);
    }

    /// <summary>自动配对开关（编辑态生效）。</summary>
    public bool EnableAutoPair
    {
        get => _autoPairEnabled;
        set => _autoPairEnabled = value;
    }

    /// <summary>编辑器字号。</summary>
    public static readonly StyledProperty<double> EditorFontSizeProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, double>(nameof(EditorFontSize), 15);

    /// <summary>可编辑表格的表头底色；未设置时使用内置代码底色。</summary>
    public static readonly StyledProperty<IBrush?> TableHeaderBackgroundBrushProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, IBrush?>(nameof(TableHeaderBackgroundBrush));

    public IBrush? TableHeaderBackgroundBrush
    {
        get => GetValue(TableHeaderBackgroundBrushProperty);
        set => SetValue(TableHeaderBackgroundBrushProperty, value);
    }

    public double EditorFontSize
    {
        get => GetValue(EditorFontSizeProperty);
        set => SetValue(EditorFontSizeProperty, value);
    }

    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, FontFamily>(nameof(CodeFontFamily), MarkdownWysiwygPalette.CodeFontFamily);
    /// <summary>源码字体由宿主配置，预览、行内代码与原位编辑使用同一字体。</summary>
    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    public static readonly StyledProperty<string?> TypographyThemeProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string?>(nameof(TypographyTheme));
    public static readonly StyledProperty<string?> TypographySizeProperty =
        AvaloniaProperty.Register<MarkdownLiveEditorView, string?>(nameof(TypographySize));
    public string? TypographyTheme
    {
        get => GetValue(TypographyThemeProperty);
        set => SetValue(TypographyThemeProperty, value);
    }
    public string? TypographySize
    {
        get => GetValue(TypographySizeProperty);
        set => SetValue(TypographySizeProperty, value);
    }

    /// <summary>正文字色（预览渲染用，随 <see cref="EditorForegroundKey"/> 令牌解析）。</summary>
    public IBrush TextBrush => _blockOptions.TextBrush;

    /// <summary>次要字色（列表符号、弱化文案）。</summary>
    public IBrush MutedBrush => _blockOptions.MutedBrush;

    /// <summary>强调色（引用条、编辑框描边）。</summary>
    public IBrush AccentBrush => _blockOptions.AccentBrush;

    /// <summary>行内代码字色。</summary>
    public IBrush CodeBrush => _blockOptions.CodeBrush;

    /// <summary>链接字色。</summary>
    public IBrush LinkBrush => _blockOptions.LinkBrush;

    /// <summary>行内代码与代码块背景色。</summary>
    public IBrush CodeBackgroundBrush => _blockOptions.CodeBackgroundBrush;

    /// <summary>分隔线 / 表格边框色。</summary>
    public IBrush SeparatorBrush => _blockOptions.SeparatorBrush;

    /// <summary>当前 Markdown 全文。</summary>
    public string Text => MarkdownBlockParser.Write(_models);

    /// <summary>块数量。</summary>
    public int BlockCount => _models.Count;

    /// <summary>当前处于编辑态的块序号；无则 -1。</summary>
    public int ActiveBlockIndex => _activeIndex;

    public string SelectedText => _documentSelection is { } range ? Text.Substring(range.Start, range.Length) : ActiveTextBox?.SelectedText ?? string.Empty;
    private TextBox? ActiveTextBox => _activeIndex >= 0 && _activeIndex < _blockViews.Count ? FindActiveEditor(_blockViews[_activeIndex]) : null;
    private TextBox? SelectionTextBox => _activeIndex >= 0 && _activeIndex < _blockViews.Count ? FindBlockView(_blockViews[_activeIndex])?.EditingInput : null;

    /// <summary>当前活动块的选择映射到原始 Markdown；无选择时返回插入点。</summary>
    public (int Start, int Length) Selection
    {
        get
        {
            if (_documentSelection is { } range) return range;
            if (_activeIndex < 0 || _activeIndex >= _models.Count) return (0, 0);
            var found = GetContentStart(_activeIndex);
            var box = SelectionTextBox;
            var start = Math.Min(box?.SelectionStart ?? 0, box?.SelectionEnd ?? 0);
            return (Math.Min(_selectionMappedText.Length, found + start), Math.Abs((box?.SelectionEnd ?? 0) - (box?.SelectionStart ?? 0)));
        }
    }

    public MarkdownLiveEditorState CaptureState() => new(CaptureSnapshot(), _undo.ToArray(), _redo.ToArray());
    public void RestoreState(MarkdownLiveEditorState state)
    {
        RestoreSnapshot(state.Current);
        _undo.Clear(); _undo.AddRange(state.Undo);
        _redo.Clear(); _redo.AddRange(state.Redo);
    }
    private MarkdownLiveEditorSnapshot CaptureSnapshot() => new(Text, _activeIndex, SelectionTextBox?.SelectionStart ?? 0, SelectionTextBox?.SelectionEnd ?? 0);
    private void RestoreSnapshot(MarkdownLiveEditorSnapshot snapshot)
    {
        _restoringHistory = true;
        try
        {
            SetText(snapshot.Text);
            if (snapshot.ActiveBlock >= 0)
            {
                FocusBlock(Math.Min(snapshot.ActiveBlock, _models.Count - 1));
                if (ActiveTextBox is { } box) { box.SelectionStart = snapshot.SelectionStart; box.SelectionEnd = snapshot.SelectionEnd; }
            }
            _lastSnapshot = CaptureSnapshot();
        }
        finally { _restoringHistory = false; }
    }

    /// <summary>替换全文（载入文档）；不触发 <see cref="MarkdownChanged"/>。</summary>
    public void SetText(string? markdown) => SetText(markdown, ImageBasePath);

    /// <summary>同时载入正文和图片基址，只重建一次文档；不触发 <see cref="MarkdownChanged"/>。</summary>
    public void SetText(string? markdown, string? imageBasePath)
    {
        CodeWF.Toolkit.Logging.Logger.Debug("SetText len=" + (markdown?.Length ?? -1));
        ContextSourceLine = 1;
        _suppressChanged = true;
        try
        {
            ClearDocumentSelection();
            _selectionContentStarts = null;
            ImageBasePath = imageBasePath;
            _models.Clear();
            _activeIndex = -1;
            _models.AddRange(MarkdownBlockParser.Parse(markdown));
            // 空文档保持一个空段落并光标就位：点击即可输入（Typora 行为），
            // 否则零块视图点不进任何编辑器，空白文档等于不可编辑。
            if (_core.EnsureFirstBlock())
            {
                FocusBlock(0);
            }
            RebuildBlocks();
            if (!_restoringHistory) { _undo.Clear(); _redo.Clear(); }
            _lastSnapshot = CaptureSnapshot();
        }
        finally
        {
            _suppressChanged = false;
        }
    }

    /// <summary>按源码偏移应用一次外部改写（预览勾选任务回写）；语义与源码编辑器一致。</summary>
    public void ApplyExternalEdit(string markdown, int start, int length)
    {
        SetText(markdown);
    }

    /// <summary>在当前活动块插入文本；无活动块时追加一个段落块。</summary>
    public void InsertText(string text)
    {
        if (_documentSelection is not null) { ReplaceDocumentSelection(text); return; }
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (_activeIndex >= 0 && _activeIndex < _models.Count)
        {
            if (FindBlockView(_blockViews[_activeIndex]) is { } view)
            {
                view.InsertText(text);
            }
            return;
        }

        _models.Add(new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = text });
        RebuildBlocks();
        RaiseChanged();
    }

    /// <summary>复制为纯文本：有活动编辑块时复制其内容，否则复制全文。</summary>
    public async Task CopyPlainTextAsync()
    {
        var text = MarkdownTextRunParser.ToPlainText(
            SelectedText.Length > 0
                ? MarkdownTextRunParser.Parse(SelectedText)
                : _activeIndex >= 0 && _activeIndex < _models.Count ? MarkdownTextRunParser.Parse(_models[_activeIndex].Text)
                : []);

        if (text.Length == 0)
        {
            text = Text;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>执行一次格式化动作（作用于活动块的编辑文本框）。</summary>
    public Task ExecuteAsync(MarkdownEditorAction action)
    {
        ActionRequested?.Invoke(this, action);
        ApplyAction(action);
        return Task.CompletedTask;
    }

    /// <summary>按块序号聚焦（0 基）。</summary>
    /// <summary>按点击的纵坐标找最近的块（垂直范围内直接命中，否则取距离最小者）。</summary>
    private int FindNearestBlockIndex(double y)
    {
        if (_blockViews.Count == 0) return -1;
        int nearest = -1;
        double best = double.MaxValue;
        for (var i = 0; i < _blockViews.Count; i++)
        {
            var top = _blockViews[i].TranslatePoint(new Point(0, 0), _container);
            if (top is not { } origin) continue;
            var bottom = origin.Y + _blockViews[i].Bounds.Height;
            if (y >= origin.Y && y <= bottom) return i;
            var distance = y < origin.Y ? origin.Y - y : y - bottom;
            if (distance < best) { best = distance; nearest = i; }
        }
        return nearest;
    }

    /// <summary>点击文档空白（未命中任何块）：光标落到最后一个段落块末尾（Typora 行为）。</summary>
    public void FocusLastParagraph()
    {
        if (_blockViews.Count == 0) return;
        FocusBlock(_blockViews.Count - 1);
    }

    public void FocusBlock(int index)
    {
        CodeWF.Toolkit.Logging.Logger.Debug("FocusBlock index=" + index);
        if (index < 0 || index >= _blockViews.Count)
        {
            return;
        }

        _activeIndex = index;
        RevealBlock(index);
        FindBlockView(_blockViews[index])?.BeginEditAtEnd();

        RaiseSelectionChanged();
    }

    public void FocusEditor()
    {
        if (_models.Count == 0)
        {
            return;
        }

        FocusBlock(Math.Clamp(_activeIndex < 0 ? 0 : _activeIndex, 0, _models.Count - 1));
    }

    #region 视图构建
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_paneHead is null) return;
        if (change.Property == ContextMenuProperty)
        {
            _blockOptions.IsDefaultContextMenuEnabled = ContextMenu is null;
            RebuildPreservingEdit();
        }
        if (change.Property == SyntaxOptionsProperty)
        {
            _blockOptions.SyntaxOptions = SyntaxOptions;
            RebuildPreservingEdit();
        }
        if (change.Property == DocumentBlockSpacingProperty) _blockHost.Spacing = DocumentBlockSpacing;
        if (change.Property == DocumentPaddingProperty) _blockHost.Margin = DocumentPadding;
        if (change.Property == DocumentMaxWidthProperty && _container is not null) _container.MaxWidth = DocumentMaxWidth;
        if (change.Property == HeaderTextProperty || change.Property == ChipTextProperty) UpdatePaneHead();
        if (change.Property == ImageBasePathProperty)
        {
            _blockOptions.ImageBasePath = ImageBasePath;
            if (!_suppressChanged) RebuildPreservingEdit();
        }
        if (change.Property == EditorFontSizeProperty || change.Property == CodeFontSizeProperty || change.Property == CodeWrapProperty || change.Property == FontFamilyProperty) RebuildPreservingEdit();
        if (change.Property == CodeFontFamilyProperty)
        {
            _blockOptions.CodeFontFamily = CodeFontFamily;
            RebuildPreservingEdit();
        }
        if (change.Property == TableHeaderBackgroundBrushProperty)
        {
            _blockOptions.TableHeaderBackgroundBrush = TableHeaderBackgroundBrush;
            RebuildPreservingEdit();
        }
        if (change.Property == TypographyThemeProperty || change.Property == TypographySizeProperty)
        {
            _blockOptions.TypographyTheme = TypographyTheme;
            _blockOptions.TypographySize = TypographySize;
            RebuildPreservingEdit();
        }
        if (change.Property == EditorBackgroundKeyProperty || change.Property == EditorForegroundKeyProperty) ApplyThemedVisuals();
    }


    private Control BuildContent()
    {
        // 透明背景：让空白区域参与 hit test（点击空白→光标落最近段落，Typora 行为）。
        var host = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Background = Brushes.Transparent };
        host.Children.Add(_paneHead);

        var scroll = new ScrollViewer
        {
            Classes = { "MdEditorScroll" },
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            // 透明背景：滚动区空白参与 hit test（配合空白点击落光标）。
            Background = global::Avalonia.Media.Brushes.Transparent,
            Content = _container
        };
        _blockHost.ScrollHost = scroll;
        Grid.SetRow(scroll, 1);
        host.Children.Add(scroll);
        return host;
    }

    private void BuildPaneHead(out Border head, out Border chipHost, out TextBlock title)
    {
        title = new TextBlock
        {
            Classes = { "pane-title" },
            VerticalAlignment = VerticalAlignment.Center
        };

        var icon = new global::Avalonia.Controls.Shapes.Path
        {
            Classes = { "stroke-ico" },
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetValue(WidthProperty, 12, global::Avalonia.Data.BindingPriority.Style);
        icon.SetValue(HeightProperty, 12, global::Avalonia.Data.BindingPriority.Style);
        icon.SetValue(global::Avalonia.Controls.Shapes.Path.DataProperty,
            Geometry.Parse("M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7 M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"),
            global::Avalonia.Data.BindingPriority.Style);

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        left.Children.Add(icon);
        left.Children.Add(title);

        chipHost = new Border { Classes = { "kbd-chip" }, Child = _chipText };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(left);
        Grid.SetColumn(chipHost, 1);
        grid.Children.Add(chipHost);

        head = new Border { Classes = { "pane-head" }, Child = grid };
    }

    private void UpdatePaneHead()
    {
        _paneTitle.Text = HeaderText ?? string.Empty;
        _paneHead.IsVisible = !string.IsNullOrEmpty(HeaderText);
        _chipText.Text = ChipText ?? string.Empty;
        _chipHost.IsVisible = !string.IsNullOrEmpty(ChipText);
    }

    private void RebuildPreservingEdit()
    {
        var box = ActiveTextBox;
        var index = _activeIndex;
        var start = box?.SelectionStart ?? 0;
        var end = box?.SelectionEnd ?? 0;
        RebuildBlocks();
        if (box is null || index < 0 || index >= _blockViews.Count) return;
        FocusBlock(index);
        if (ActiveTextBox is { } current) { current.SelectionStart = start; current.SelectionEnd = end; }
    }

    private void RebuildBlocks()
    {
        _blockHost.ClearItems();
        _blockViews.Clear();
        _hostIndices.Clear();

        for (var index = 0; index < _models.Count; index++)
        {
            var model = _models[index];
            var end = index + 1;
            if (model.Kind == MarkdownBlockKind.Quote && model.QuoteDepth == 1 && model.Source is not null)
            {
                while (end < _models.Count && _models[end].Kind == MarkdownBlockKind.Quote
                    && _models[end].QuoteDepth == 1 && ReferenceEquals(_models[end].Source, model.Source)) end++;
            }

            if (end - index > 1)
            {
                var paragraphs = new StackPanel();
                for (; index < end; index++)
                {
                    var paragraph = CreateLazyBlock(_models[index], quote: true);
                    _blockViews.Add(paragraph);
                    paragraphs.Children.Add(paragraph);
                }
                _hostIndices.AddRange(Enumerable.Repeat(_blockHost.ItemCount, end - _hostIndices.Count));
                var quote = new Border { Classes = { "MdQuote", "MdLiveQuote" }, Child = paragraphs };
                quote.SetValue(Border.BorderBrushProperty, _blockOptions.AccentBrush, global::Avalonia.Data.BindingPriority.Style);
                quote.SetValue(Border.BackgroundProperty, _blockOptions.CodeBackgroundBrush, global::Avalonia.Data.BindingPriority.Style);
                _blockHost.AddItem(quote);
                index--;
            }
            else
            {
                var lazy = CreateLazyBlock(model);
                _blockViews.Add(lazy);
                _hostIndices.Add(_blockHost.ItemCount);
                _blockHost.AddItem(lazy);
            }
        }
    }

    /// <summary>惰性块工厂：按模型（非下标）建块——结构增量更新后下标漂移不影响工厂产物。</summary>
    private Control CreateLazyBlock(MarkdownBlockModel model, bool quote = false)
    {
        var lazy = new LazyLiveBlock(() =>
        {
            var content = BuildBlockView(model);
            if (quote && FindBlockView(content) is { } block) block.RenderQuoteAsParagraph = true;
            return content;
        });
        return lazy;
    }

    /// <summary>事件期解析模型当前下标（结构变化后构建期捕获的下标已漂移）。</summary>
    private int CurrentIndexOf(MarkdownBlockModel model) => Math.Max(0, _models.IndexOf(model));

    /// <summary>激活模型所在块为编辑态：写下标、防虚拟化解体、钉扎宿主条目。</summary>
    private void ActivateModel(MarkdownBlockModel model)
    {
        var index = _models.IndexOf(model);
        if (index < 0) return;
        _activeIndex = index;
        if (index < _hostIndices.Count)
        {
            _blockHost.DetachAllPinned();
            _blockHost.PinItem(_hostIndices[index]);
        }
    }

    // ---- 增量结构更新：热路径（纯文本块）不再整表重建 ----
    // 分组规则与 RebuildBlocks 一致（连续同源 depth-1 引用合并一个宿主条目）；
    // 触及分组的变更仍回退全量重建（引用块编辑频率低，正确性优先），后续再收敛组级重建。

    /// <summary>模型 i 是否位于多成员 Quote 分组（含自身在组边界上的情形）。</summary>
    private bool IsQuoteGroupedMember(int index)
    {
        if (index < 0 || index >= _models.Count) return false;
        var model = _models[index];
        if (model.Kind != MarkdownBlockKind.Quote || model.QuoteDepth != 1 || model.Source is null) return false;
        if (index > 0 && _models[index - 1] is { Kind: MarkdownBlockKind.Quote, QuoteDepth: 1, Source: { } } prev
            && ReferenceEquals(prev.Source, model.Source)) return true;
        if (index + 1 < _models.Count && _models[index + 1] is { Kind: MarkdownBlockKind.Quote, QuoteDepth: 1, Source: { } } next
            && ReferenceEquals(next.Source, model.Source)) return true;
        return false;
    }

    /// <summary>结构变化能否走增量路径：触及块及其相邻块都不在 Quote 分组内。</summary>
    private bool CanUpdateIncrementally(int index)
    {
        return !IsQuoteGroupedMember(index - 1) && !IsQuoteGroupedMember(index) && !IsQuoteGroupedMember(index + 1);
    }

    /// <summary>模型层已在 newIndex 插入新块：增量同步映射与宿主；触及分组回退全量。</summary>
    private void HostInsertModel(int newIndex)
    {
        if (!CanUpdateIncrementally(newIndex))
        {
            RebuildBlocks();
            return;
        }
        var lazy = CreateLazyBlock(_models[newIndex]);
        var hostPos = newIndex == 0 ? 0 : _hostIndices[newIndex - 1] + 1;
        _blockHost.InsertItem(hostPos, lazy);
        _blockViews.Insert(newIndex, lazy);
        _hostIndices.Insert(newIndex, hostPos);
        for (var i = newIndex + 1; i < _hostIndices.Count; i++) _hostIndices[i]++;
    }

    /// <summary>模型层已移除 removedIndex 处的旧块（映射仍持旧条目）：增量同步；触及分组回退全量。</summary>
    private void HostRemoveModel(int removedIndex)
    {
        if (removedIndex < 0 || removedIndex >= _blockViews.Count
            || !CanUpdateIncrementally(Math.Min(removedIndex, _models.Count - 1)))
        {
            RebuildBlocks();
            return;
        }
        var hostPos = _hostIndices[removedIndex];
        _blockHost.RemoveAt(hostPos);
        _blockViews.RemoveAt(removedIndex);
        _hostIndices.RemoveAt(removedIndex);
        for (var i = removedIndex; i < _hostIndices.Count; i++) _hostIndices[i]--;
    }

    /// <summary>单块的 Kind/缩进等形态变化：原位替换宿主条目（保留下游映射不动）；触及分组回退全量。
    /// 调用方须在模型变更【前】确认未处于分组（分组成员形态转换改变分组拓扑）。</summary>
    private void RefreshBlockView(int index)
    {
        if (index < 0 || index >= _blockViews.Count || index >= _hostIndices.Count
            || !CanUpdateIncrementally(index))
        {
            RebuildBlocks();
            return;
        }
        var hostPos = _hostIndices[index];
        var lazy = CreateLazyBlock(_models[index]);
        _blockHost.RemoveAt(hostPos);
        _blockHost.InsertItem(hostPos, lazy);
        _blockViews[index] = lazy;
    }
    private void RevealBlock(int index)
    {
        if (index < 0 || index >= _hostIndices.Count) return;
        var hostIndex = _hostIndices[index];
        _blockHost.DetachAllPinned(); _blockHost.PinItem(hostIndex); _blockHost.RealizeItem(hostIndex);
        if (_blockHost.ScrollHost is not { } scroll) return;
        // 新物化的块尚无有效 Bounds；先推进布局，再按实际坐标定位。
        // 新视口量测会修正前面块的估高，因此按同一目标做有限次校正。
        for (var pass = 0; pass < 4; pass++)
        {
            scroll.UpdateLayout();
            if (scroll.Viewport.Height <= 0 || _blockViews[index].TranslatePoint(default, scroll) is not { } origin) return;
            var height = _blockViews[index].Bounds.Height;
            var shift = origin.Y < 0 || height >= scroll.Viewport.Height ? origin.Y
                : Math.Max(0, origin.Y + height - scroll.Viewport.Height);
            var target = Math.Clamp(scroll.Offset.Y + shift, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
            if (Math.Abs(target - scroll.Offset.Y) < .5) return;
            scroll.Offset = new Vector(scroll.Offset.X, target);
        }
        scroll.UpdateLayout();
    }

    /// <summary>按模型建块视图：事件回调在下标漂移后仍正确——触发时重新解析模型当前下标。</summary>
    private Control BuildBlockView(MarkdownBlockModel model)
    {
        _blockOptions.FontSize = EditorFontSize;
        _blockOptions.CodeFontSize = CodeFontSize;
        _blockOptions.CodeWrap = CodeWrap;
        _blockOptions.ContentFontFamily = FontFamily;
        var index = _models.IndexOf(model);
        var beforeHeading = index + 1 < _models.Count
            && _models[index + 1] is { Kind: MarkdownBlockKind.Heading, HeadingLevel: <= 4 };

        if (model.Kind == MarkdownBlockKind.Table)
        {
            var table = new MarkdownTableView(model, _blockOptions)
            {
                CellsChanged = _ => RaiseChanged(),
                CellEditingStarted = _ => { ActivateModel(model); RaiseSelectionChanged(); },
                CellSelectionChanged = RaiseSelectionChanged,
                StructureChangeRequested = (tableView, action) =>
                {
                    if (MarkdownTableEditor.ChangeStructure(model, action))
                    {
                        tableView.RefreshFromModel();
                        RaiseChanged();
                    }
                }
            };
            table.Classes.Set("MdBeforeHeading", beforeHeading);
            return table;
        }

        if (model.Kind == MarkdownBlockKind.ThematicBreak)
        {
            var rule = new Border
            {
                Height = 1,
                Margin = new Thickness(0, 8, 0, 8),
                Background = _blockOptions.SeparatorBrush
            };
            return rule;
        }

        var view = new MarkdownBlockView(model, isCode: model.Kind == MarkdownBlockKind.Code, _blockOptions)
        {
            TextCommitted = OnBlockTextCommitted,
            NavigateRequested = direction => FocusBlock(Math.Clamp(CurrentIndexOf(model) + direction, 0, _models.Count - 1)),
            BoundaryNavigateRequested = (direction, x) => FocusAdjacentBlock(CurrentIndexOf(model), direction, x),
            MergeRequested = direction => MergeBlock(CurrentIndexOf(model), direction),
            IndentRequested = delta => ChangeIndent(delta),
            EditingStarted = _ => ActivateModel(model),
            SplitRequested = SplitBlock
        };
        AttachAutoPair(view);
        view.Classes.Set("MdBeforeHeading", beforeHeading);
        view.EditorSelectionChanged = RaiseSelectionChanged;
        view.RunFontSize = model.Kind switch
        {
            MarkdownBlockKind.Heading => model.HeadingLevel switch
            {
                1 => EditorFontSize * 2,
                2 => EditorFontSize * 1.5,
                3 => EditorFontSize * 1.2142857,
                4 => EditorFontSize * 1.15,
                _ => EditorFontSize
            },
            MarkdownBlockKind.Code => CodeFontSize,
            _ => EditorFontSize
        };

        var root = DecorateBlock(model, view);
        root.Classes.Set("MdBeforeHeading", beforeHeading);
        return root;
    }

    private Control DecorateBlock(MarkdownBlockModel model, MarkdownBlockView view)
    {
        switch (model.Kind)
        {
            case MarkdownBlockKind.ListItem:
                var row = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    Margin = new Thickness(model.IndentLevel * 18, 0, 0, 0)
                };

                if (model.IsTask)
                {
                    // 任务复选框本身就是列表标记，与阅读视图保持一致。
                    var box = new CheckBox
                    {
                        IsChecked = model.IsChecked,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    box.SetValue(Control.MarginProperty, new Thickness(0, 7, 6, 0), global::Avalonia.Data.BindingPriority.Style);
                    box.Classes.Add("MdTaskMarkerBox");
                    box.IsCheckedChanged += (_, _) =>
                    {
                        model.IsChecked = box.IsChecked == true;
                        view.NotifyModelChanged();
                    };
                    row.Children.Add(box);
                }
                else
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = model.IsOrdered || model.OrderedNumber > 0 ? $"{model.OrderedNumber}. " : "• ",
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = _blockOptions.MutedBrush,
                        MinWidth = 20
                    });
                }

                Grid.SetColumn(view, 1);
                row.Children.Add(view);
                return new Border { Classes = { "MdLiveListItem" }, Child = row };

            case MarkdownBlockKind.Quote:
            case MarkdownBlockKind.Heading:
            case MarkdownBlockKind.Code:
                return view;

            default:
                return view;
        }
    }


    #endregion

    #region 编辑事件

    private void OnBlockTextCommitted(MarkdownBlockView view, string text)
    {
        RaiseChanged();
    }

    private bool FocusAdjacentBlock(int index, int direction, double? x)
    {
        var next = index + direction;
        if (next < 0 || next >= _blockViews.Count || _models[next].Kind is MarkdownBlockKind.Table or MarkdownBlockKind.ThematicBreak) return false;
        var leaving = FindBlockView(_blockViews[index]);
        // 纯文本块编辑态常驻：跨块导航不翻回渲染态（模型已逐键同步，无内容可丢）。
        if (leaving is { IsPureText: false }) leaving.CommitAndRender();
        FocusBlock(next);
        if (ActiveTextBox is { } box)
        {
            var caret = direction < 0 ? box.Text?.Length ?? 0 : 0;
            if (x is { } horizontal && box.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is { } presenter)
            {
                box.UpdateLayout();
                var y = direction < 0 ? Math.Max(0, presenter.TextLayout.Height - 1) : 0;
                caret = presenter.TextLayout.HitTestPoint(new Point(horizontal, y)).TextPosition;
            }
            box.CaretIndex = box.SelectionStart = box.SelectionEnd = caret;
        }
        return true;
    }

    private bool MergeBlock(int index, int direction)
    {
        // 合并/降级语义收回 Core（muya 对齐，单测护航）：退格=Backspace（降级或并入前块），
        // Delete=MergeForward（吞并后块）；null=不可合并（UI 保留默认行为）。
        var before = _models.Count;
        var result = direction < 0 ? _core.Backspace(index) : _core.MergeForward(index);
        if (result is null) return false;
        if (_models.Count < before)
        {
            // 合并吞块：增量移除被吞条目；存活块编辑器强制同步合并文本（编辑态 BeginEdit 不重读模型）。
            HostRemoveModel(direction < 0 ? index : index + 1);
            if (FindBlockView(_blockViews[result.FocusIndex]) is { } surviving)
                surviving.SyncEditorFromModel(result.Caret);
        }
        else
        {
            // 块首退格降级：Kind 变了但条目数不变，原位替换单块视图。
            RefreshBlockView(index);
        }
        RaiseChanged();
        FocusBlock(result.FocusIndex);
        if (ActiveTextBox is { } box) box.CaretIndex = box.SelectionStart = box.SelectionEnd = result.Caret;
        return true;
    }

    private void SplitBlock(MarkdownBlockView view, int caret)
    {
        var index = _models.IndexOf(view.Model);
        if (index < 0) return;
        // 拆块语义收回 Core；视图只负责增量结构同步/事件/焦点执行。
        var before = _models.Count;
        var result = _core.Enter(index, caret);
        if (!result.Changed) return;
        if (_models.Count > before)
        {
            HostInsertModel(index + 1);
            // 拆剩的头部块仍处编辑态且编辑器还持有拆分前全文：同步截断文本，防失焦提交回滚。
            if (FindBlockView(_blockViews[index]) is { } head) head.SyncEditorFromModel(caret);
        }
        else
        {
            // 空列表项回车降级：条目数不变，原位替换单块视图。
            RefreshBlockView(index);
        }
        RaiseChanged();
        FocusBlock(result.FocusIndex);
        // 光标落拆分点（Core 契约 Caret=0=新块首）；修正旧实现 BeginEditAtEnd 落新块末尾。
        if (ActiveTextBox is { } box) box.CaretIndex = box.SelectionStart = box.SelectionEnd = result.Caret;
    }

    private void AttachAutoPair(MarkdownBlockView view)
    {
        view.AddHandler(InputElement.KeyDownEvent, OnBlockKeyDown, RoutingStrategies.Tunnel);
        view.TextEntering = e => OnBlockTextInput(view, e);
    }

    private void OnBlockTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text is not { Length: > 0 } input || sender is not MarkdownBlockView { ActiveEditor: { } box } view) return;
        var start = Math.Min(box.SelectionStart, box.SelectionEnd);
        var end = Math.Max(box.SelectionStart, box.SelectionEnd);
        var text = box.Text ?? string.Empty;
        if (start <= 4 && (input.Contains('>') || text.AsSpan(0, Math.Min(text.Length, 5)).Contains('>')))
        {
            var index = _models.IndexOf(view.Model);
            var wasGrouped = IsQuoteGroupedMember(index);
            if (_core.EnterQuoteMarker(index, text, start, end, input) is { } result)
            {
                e.Handled = true;
                // 旧输入框保留原始文本；先同步再换视图，避免失焦时回写旧正文。
                _selectionContentStarts = null;
                view.SyncEditorFromModel(result.Caret);
                if (wasGrouped) RebuildBlocks(); else RefreshBlockView(index);
                RevealBlock(index);
                _activeIndex = index;
                FindBlockView(_blockViews[index])?.BeginEdit(result.Caret);
                RaiseChanged();
                return;
            }
        }
        if (!_autoPairEnabled || input.Length != 1
            || TextAutoPair.HandleTextInput(text, start, end - start, input) is not { } change) return;
        e.Handled = true;
        if (change.Length > 0 || change.Text.Length > 0)
            box.Text = (box.Text ?? string.Empty).Remove(change.Start, change.Length).Insert(change.Start, change.Text);
        box.CaretIndex = change.CaretOffset;
        box.SelectionStart = box.SelectionEnd = change.CaretOffset;
    }

    private void OnBlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not MarkdownBlockView view || !_autoPairEnabled)
        {
            return;
        }

        if (e.Key == Key.Back && e.KeyModifiers == KeyModifiers.None && view.ActiveEditor is { } backBox)
        {
            var result = TextAutoPair.HandleBackspace(backBox.Text ?? string.Empty, Math.Min(backBox.SelectionStart, backBox.SelectionEnd), Math.Abs(backBox.SelectionEnd - backBox.SelectionStart));
            if (result is { } change)
            {
                backBox.Text = (backBox.Text ?? string.Empty).Remove(change.Start, change.Length).Insert(change.Start, change.Text);
                backBox.CaretIndex = change.CaretOffset;
                e.Handled = true;
            }
        }
    }

    private void RaiseChanged()
    {
        _selectionContentStarts = null;
        if (_suppressChanged)
        {
            return;
        }

        var current = CaptureSnapshot();
        if (current.Text != _lastSnapshot.Text)
        {
            if (!_restoringHistory)
            {
                _undo.Add(_lastSnapshot);
                if (_undo.Count > 200) _undo.RemoveAt(0);
                _redo.Clear();
            }
            _lastSnapshot = current;
        }
        MarkdownChanged?.Invoke(this, current.Text);
        RaiseSelectionChanged();
    }

    private void RaiseSelectionChanged()
    {
        if (_suppressChanged) return;
        EnsureSelectionMap();
        var text = _selectionMappedText;
        var offset = Math.Clamp(Selection.Start, 0, text.Length);
        var line = 1 + text.AsSpan(0, offset).Count('\n');
        var previous = offset == 0 ? -1 : text.LastIndexOf('\n', offset - 1);
        SelectionChanged?.Invoke(this, new MarkdownCaretEventArgs(line, offset - previous, 1 + text.Count(c => c == '\n')));
    }

    #endregion

    #region 动作

    private void ApplyAction(MarkdownEditorAction action)
    {
        if (ApplyDocumentSelectionAction(action)) return;
        if (action is not MarkdownEditorAction.Undo and not MarkdownEditorAction.Redo and not MarkdownEditorAction.FocusEditor)
        {
            EnsureActiveBlockEditing();
        }
        var activeEditor = (_activeIndex >= 0 && _activeIndex < _blockViews.Count)
            ? FindActiveEditor(_blockViews[_activeIndex])
            : null;

        switch (action)
        {
            case MarkdownEditorAction.Undo:
                ChangeHistory(_undo, _redo);
                break;
            case MarkdownEditorAction.Redo:
                ChangeHistory(_redo, _undo);
                break;
            case MarkdownEditorAction.Cut:
                activeEditor?.Cut();
                break;
            case MarkdownEditorAction.Copy:
                activeEditor?.Copy();
                break;
            case MarkdownEditorAction.Paste:
                activeEditor?.Paste();
                break;
            case MarkdownEditorAction.SelectAll:
                activeEditor?.SelectAll();
                break;
            case MarkdownEditorAction.CopyPlainText:
                _ = CopyPlainTextAsync();
                break;
            case MarkdownEditorAction.ClearFormatting:
                if (activeEditor is { } clear)
                {
                    var text = clear.Text ?? string.Empty;
                    var start = Math.Min(clear.SelectionStart, clear.SelectionEnd);
                    var length = Math.Abs(clear.SelectionStart - clear.SelectionEnd);
                    if (length == 0) { start = 0; length = text.Length; }
                    var plain = MarkdownTextRunParser.ToPlainText(MarkdownTextRunParser.Parse(text.Substring(start, length)));
                    clear.Text = text.Remove(start, length).Insert(start, plain);
                    clear.CaretIndex = start + plain.Length;
                }
                break;
            case MarkdownEditorAction.Bold:
                WrapActive("**", "**");
                break;
            case MarkdownEditorAction.Italic:
                WrapActive("*", "*");
                break;
            case MarkdownEditorAction.Strikethrough:
                WrapActive("~~", "~~"); break;
            case MarkdownEditorAction.Highlight:
                WrapActive("==", "=="); break;
            case MarkdownEditorAction.Superscript:
                WrapActive("^", "^"); break;
            case MarkdownEditorAction.Subscript:
                WrapActive("~", "~"); break;
            case MarkdownEditorAction.InlineCode:
                WrapActive("`", "`");
                break;
            case MarkdownEditorAction.ClearSpanStyles:
                ClearSpanStyles();
                break;
            case MarkdownEditorAction.Link:
                WrapActive("[", $"]({_options.LinkUrlPlaceholder})");
                break;
            case MarkdownEditorAction.Image:
                WrapActive("![", $"]({_options.ImageTargetPlaceholder})");
                break;
            case MarkdownEditorAction.Paragraph:
                ConvertActiveBlock(MarkdownBlockKind.Paragraph, 0);
                break;
            case MarkdownEditorAction.Heading1:
            case MarkdownEditorAction.Heading2:
            case MarkdownEditorAction.Heading3:
            case MarkdownEditorAction.Heading4:
            case MarkdownEditorAction.Heading5:
            case MarkdownEditorAction.Heading6:
                ConvertActiveBlock(MarkdownBlockKind.Heading, (int)action - (int)MarkdownEditorAction.Heading1 + 1);
                break;
            case MarkdownEditorAction.Quote:
                ConvertActiveBlock(MarkdownBlockKind.Quote, 0);
                break;
            case MarkdownEditorAction.UnorderedList:
                ConvertActiveBlock(MarkdownBlockKind.ListItem, 0);
                break;
            case MarkdownEditorAction.OrderedList:
                ConvertActiveBlock(MarkdownBlockKind.ListItem, 0, isOrdered: true);
                break;
            case MarkdownEditorAction.TaskList:
                ConvertActiveBlock(MarkdownBlockKind.ListItem, 0, isTask: true);
                break;
            case MarkdownEditorAction.CodeFence:
                InsertBlock(new MarkdownBlockModel { Kind = MarkdownBlockKind.Code, IsFenced = true, Language = _options.CodeFenceLanguage });
                break;
            case MarkdownEditorAction.Table:
                InsertBlock(new MarkdownBlockModel
                {
                    Kind = MarkdownBlockKind.Table,
                    HasHeader = true,
                    Alignments = [TableCellAlignment.None, TableCellAlignment.None],
                    Cells = [["", ""], ["", ""]]
                });
                break;
            case MarkdownEditorAction.MathBlock:
                InsertBlock(new MarkdownBlockModel { Kind = MarkdownBlockKind.Raw, Raw = "$$\n\n$$" });
                break;
            case MarkdownEditorAction.HorizontalRule:
                InsertBlock(new MarkdownBlockModel { Kind = MarkdownBlockKind.ThematicBreak });
                break;
            case MarkdownEditorAction.Indent:
                ChangeIndent(1);
                break;
            case MarkdownEditorAction.Outdent:
                ChangeIndent(-1);
                break;
            case MarkdownEditorAction.FocusEditor:
                FocusEditor();
                break;
            case MarkdownEditorAction.SmartNewLine:
                if (_activeIndex >= 0 && _activeIndex < _blockViews.Count && FindBlockView(_blockViews[_activeIndex]) is { } current)
                    SplitBlock(current, activeEditor?.CaretIndex ?? current.Model.Text.Length);
                break;
        }
    }

    private void ChangeHistory(List<MarkdownLiveEditorSnapshot> from, List<MarkdownLiveEditorSnapshot> to)
    {
        if (from.Count == 0) return;
        var snapshot = from[^1]; from.RemoveAt(from.Count - 1);
        to.Add(CaptureSnapshot());
        RestoreSnapshot(snapshot);
        _restoringHistory = true;
        try { RaiseChanged(); }
        finally { _restoringHistory = false; }
    }

    /// <summary>剥掉当前选区（跨块或块内）的所有行内样式 span 标签。</summary>
    public void ClearSpanStyles()
    {
        if (_documentSelection is null) EnsureActiveBlockEditing();
        if (_documentSelection is { } range && range.Length > 0)
        {
            var (updated, newStart, newEnd) = Shared.Rendering.MarkdownSpanOperations.Clear(Text, range.Start, range.Start + range.Length);
            if (updated == Text)
            {
                return;
            }

            var previous = CaptureSnapshot();
            _restoringHistory = true;
            try { SetText(updated); }
            finally { _restoringHistory = false; }
            _lastSnapshot = previous;
            FocusSourceOffset(newEnd);
            RaiseChanged();
            return;
        }

        if (_activeIndex >= 0 && _activeIndex < _blockViews.Count && FindActiveEditor(_blockViews[_activeIndex]) is { } box)
        {
            var text = box.Text ?? string.Empty;
            var start = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, text.Length);
            var end = Math.Clamp(Math.Max(box.SelectionStart, box.SelectionEnd), start, text.Length);
            if (start == end)
            {
                return;
            }

            var (updated, newStart, newEnd) = Shared.Rendering.MarkdownSpanOperations.Clear(text, start, end);
            if (updated == text)
            {
                return;
            }

            box.Text = updated;
            box.CaretIndex = newEnd;
            box.SelectionStart = newStart;
            box.SelectionEnd = newEnd;
            if (_models[_activeIndex].Kind is MarkdownBlockKind.Paragraph or MarkdownBlockKind.Heading or MarkdownBlockKind.ListItem or MarkdownBlockKind.Quote)
            {
                _models[_activeIndex].Text = box.Text ?? string.Empty;
            }

            RaiseChanged();
        }
    }

    /// <summary>读当前选区所在的行内样式值（紧贴同款 span 时返回，供宿主面板预填），否则 null。</summary>
    public string? GetSpanStyle(string property)
    {
        if (_documentSelection is { } range && range.Length > 0)
        {
            return Shared.Rendering.MarkdownSpanOperations.CurrentValue(Text, range.Start, range.Start + range.Length, property);
        }

        if (SelectionTextBox is { } box)
        {
            var text = box.Text ?? string.Empty;
            var start = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, text.Length);
            var end = Math.Clamp(Math.Max(box.SelectionStart, box.SelectionEnd), start, text.Length);
            return Shared.Rendering.MarkdownSpanOperations.CurrentValue(text, start, end, property);
        }

        return null;
    }

    /// <summary>
    /// 给当前选区包上行内 <c>&lt;span style&gt;</c>（color / background-color / font-size）；
    /// 同款样式再执行一次即解包。跨块选区走文档级替换，块内选区就地改写。
    /// </summary>
    public void ApplySpanStyle(string property, string value)
    {
        if (_documentSelection is null) EnsureActiveBlockEditing();
        if (_documentSelection is { } range && range.Length > 0)
        {
            var text = Text;
            var (updated, newStart, newEnd) = Shared.Rendering.MarkdownSpanOperations.Wrap(text, range.Start, range.Start + range.Length, property, value);
            if (updated == text)
            {
                return;
            }

            var previous = CaptureSnapshot();
            _restoringHistory = true;
            try { SetText(updated); }
            finally { _restoringHistory = false; }
            _lastSnapshot = previous;
            FocusSourceOffset(newEnd);
            RaiseChanged();
            return;
        }

        WrapActiveSpan(property, value);
    }

    private void WrapActiveSpan(string property, string value)
    {
        if (_activeIndex < 0 || _activeIndex >= _blockViews.Count)
        {
            return;
        }

        if (FindActiveEditor(_blockViews[_activeIndex]) is not { } box)
        {
            return;
        }

        var text = box.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(box.SelectionStart, box.SelectionEnd), start, text.Length);
        if (start == end)
        {
            return;
        }

        var (updated, newStart, newEnd) = Shared.Rendering.MarkdownSpanOperations.Wrap(text, start, end, property, value);
        if (updated == text)
        {
            return;
        }

        box.Text = updated;
        box.CaretIndex = newEnd;
        box.SelectionStart = newStart;
        box.SelectionEnd = newEnd;
        if (_models[_activeIndex].Kind is MarkdownBlockKind.Paragraph or MarkdownBlockKind.Heading or MarkdownBlockKind.ListItem or MarkdownBlockKind.Quote)
        {
            _models[_activeIndex].Text = box.Text ?? string.Empty;
        }

        RaiseChanged();
    }

    private void EnsureActiveBlockEditing()
    {
        if (_activeIndex < 0) FocusEditor();
        else if (_activeIndex < _blockViews.Count && FindBlockView(_blockViews[_activeIndex]) is { IsEditing: false } block)
        {
            var input = block.EditingInput;
            var start = input.SelectionStart; var end = input.SelectionEnd;
            block.BeginEdit(input.CaretIndex);
            input.SelectionStart = start; input.SelectionEnd = end;
        }
    }

    private void WrapActive(string prefix, string suffix)
    {
        if (_activeIndex < 0 || _activeIndex >= _blockViews.Count)
        {
            return;
        }

        if (FindActiveEditor(_blockViews[_activeIndex]) is not { } box)
        {
            return;
        }

        var text = box.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(box.SelectionStart, box.SelectionEnd), 0, text.Length);
        var selectionEnd = Math.Clamp(Math.Max(box.SelectionStart, box.SelectionEnd), start, text.Length);
        var length = selectionEnd - start;
        var selected = length > 0
            ? text.Substring(start, length)
            : prefix == "**" ? _localizer.Get(MarkdownEditorText.BoldPlaceholder) : string.Empty;
        var replacement = prefix + selected + suffix;
        box.Text = text[..start] + replacement + text[(selectionEnd)..];
        box.CaretIndex = start + replacement.Length;
        box.SelectionStart = start + prefix.Length;
        box.SelectionEnd = box.SelectionStart + selected.Length;
        if (_models[_activeIndex].Kind is MarkdownBlockKind.Paragraph or MarkdownBlockKind.Heading or MarkdownBlockKind.ListItem or MarkdownBlockKind.Quote)
        {
            _models[_activeIndex].Text = box.Text ?? string.Empty;
        }

        RaiseChanged();
    }

    private void ConvertActiveBlock(MarkdownBlockKind kind, int headingLevel, bool isTask = false, bool isOrdered = false)
    {
        if (_activeIndex < 0 || _activeIndex >= _models.Count)
        {
            return;
        }

        // 分组成员的形态转换会改变分组拓扑（Quote↔非 Quote），先验后走增量或全量。
        var wasGrouped = IsQuoteGroupedMember(_activeIndex);
        // 转换语义收回 Core（含标记清理不变式，单测护航）。
        if (!_core.ConvertBlock(_activeIndex, kind, headingLevel, isTask, isOrdered))
        {
            return;
        }

        if (wasGrouped) RebuildBlocks(); else RefreshBlockView(_activeIndex);
        RaiseChanged();
    }

    private void ChangeIndent(int delta)
    {
        if (_activeIndex < 0 || _activeIndex >= _models.Count)
        {
            return;
        }

        if (!_core.IndentListItem(_activeIndex, delta))
        {
            return;
        }

        // 列表项缩进只改边距形态，不涉分组拓扑：单块原位替换。
        RefreshBlockView(_activeIndex);
        RaiseChanged();
    }

    private void InsertBlock(MarkdownBlockModel model)
    {
        var index = _activeIndex < 0 ? _models.Count : Math.Min(_activeIndex + 1, _models.Count);
        _models.Insert(index, model);
        HostInsertModel(index);
        RaiseChanged();
        FocusBlock(index);
    }

    private static MarkdownBlockView? FindBlockView(Control control) => control switch
    {
        LazyLiveBlock lazy => FindBlockView(lazy.Realize()),
        MarkdownTableView table => table.ActiveCell ?? table.FirstCell,
        MarkdownBlockView view => view,
        Panel panel => panel.Children.Select(FindBlockView).FirstOrDefault(view => view is not null),
        Border { Child: Control child } => FindBlockView(child),
        _ => null
    };

    private static TextBox? FindActiveEditor(Control control) => FindBlockView(control)?.ActiveEditor;

    /// <summary>只在面板物化并量测此块时创建表格、渲染器和输入控件。</summary>
    private sealed class LazyLiveBlock(Func<Control> factory) : Decorator
    {
        public Control Realize() => Child ??= factory();
        protected override Size MeasureOverride(Size availableSize) { Realize(); return base.MeasureOverride(availableSize); }
    }

    #endregion

    #region 视觉

    private void ApplyThemedVisuals()
    {
        var background = ResolveBrush(EditorBackgroundKey, DefaultEditorBackgroundKey);
        var foreground = ResolveBrush(EditorForegroundKey, DefaultEditorForegroundKey) ?? Brushes.Black;
        var muted = ResolveBrush(MarkdownEditorPalette.QuoteKey, MarkdownEditorPalette.QuoteKey);
        var accent = ResolveBrush(MarkdownEditorPalette.AccentKey, MarkdownEditorPalette.AccentKey);
        var code = ResolveBrush(MarkdownEditorPalette.CodeKey, MarkdownEditorPalette.CodeKey);
        var link = ResolveBrush(MarkdownEditorPalette.LinkKey, MarkdownEditorPalette.LinkKey);
        var codeBackground = ResolveBrush(
            MarkdownEditorPalette.CurrentLineBackgroundKey,
            MarkdownEditorPalette.CurrentLineBackgroundKey);
        var separator = ResolveBrush(MarkdownEditorPalette.SeparatorKey, MarkdownEditorPalette.SeparatorKey);

        _container.Background = background;
        Foreground = foreground;

        // 宿主令牌变化后，已建好的块视图要按新色重建，否则只有容器换色。
        if (_blockOptions.Apply(foreground, muted, accent, code, link, codeBackground, separator)
            && _models.Count > 0)
        {
            RebuildBlocks();
        }
    }

    private IBrush? ResolveBrush(string key, string fallbackKey)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return MarkdownEditorPalette.Resolve(key, ActualThemeVariant)
               ?? MarkdownEditorPalette.Resolve(fallbackKey, ActualThemeVariant);
    }

    #endregion
}
