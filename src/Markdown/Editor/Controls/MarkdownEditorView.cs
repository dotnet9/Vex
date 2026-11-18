using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Input.Platform;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Styling;

using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;
using System.Xml;

using CodeWF.Avalonia.Markdown.Editor.Services;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>
/// Markdown 源码编辑器控件：AvaloniaEdit 宿主 + Markdown 语法着色 + 动作/查找服务 + 自动配对。
/// <para>
/// 与宿主解耦：文案走 <see cref="IMarkdownEditorLocalizer"/>，文本通过 <see cref="Text"/> 进出，
/// 编辑器事件以 CLR 事件对外（<see cref="MarkdownChanged"/>、<see cref="SelectionChanged"/>、
/// <see cref="ActionRequested"/> 等），宿主自行接到自己的消息总线。
/// </para>
/// <para>
/// 配色自带默认值（明/暗两套内置调色板），宿主把 *Key 属性指向自己的资源键即可整包换肤，
/// 例如 Vex 指向 <c>VexEditorBackgroundBrush</c>；不指向时控件自己就能看，不依赖任何宿主资源。
/// </para>
/// </summary>
public partial class MarkdownEditorView : UserControl
{
    private const string DefaultEditorBackgroundKey = "CodeWFMarkdownEditorBackgroundBrush";
    private const string DefaultEditorForegroundKey = "CodeWFMarkdownEditorForegroundBrush";
    private const string DefaultCurrentLineBackgroundKey = "CodeWFMarkdownEditorCurrentLineBackgroundBrush";
    private const string DefaultCurrentLineBorderKey = "CodeWFMarkdownEditorCurrentLineBorderBrush";
    private const string DefaultAccentKey = "CodeWFMarkdownEditorAccentBrush";
    private const string DefaultCodeKey = "CodeWFMarkdownEditorCodeBrush";
    private const string DefaultQuoteKey = "CodeWFMarkdownEditorQuoteBrush";
    private const string DefaultLinkKey = "CodeWFMarkdownEditorLinkBrush";
    private const string DefaultImageKey = "CodeWFMarkdownEditorImageBrush";
    private const string DefaultWarningKey = "CodeWFMarkdownEditorWarningBrush";
    private const string DefaultDangerKey = "CodeWFMarkdownEditorDangerBrush";
    private const string DefaultSeparatorKey = "CodeWFMarkdownEditorSeparatorBrush";
    private const string DefaultMarkerKey = "CodeWFMarkdownEditorMarkerBrush";
    private const string DefaultLineNumberKey = "CodeWFMarkdownEditorLineNumberBrush";
    private const string DefaultCurrentLineNumberKey = "CodeWFMarkdownEditorCurrentLineNumberBrush";

    private readonly MarkdownEditorOptions _options;
    private readonly IHighlightingDefinition _codeHighlighting;
    private readonly IMarkdownEditorActionService _actions;
    private readonly IMarkdownEditorSearchService _search;
    private readonly Border _container;
    private readonly TextEditor _editor;
    private MarkdownLineNumberMargin? _lineNumberMargin;
    private readonly double _defaultLineHeightFactor;
    private readonly Border _paneHead;
    private readonly Border _chipHost;
    private readonly TextBlock _paneTitle;
    private bool _suppressChanged;
    private string _lastText = string.Empty;
    private bool _autoPairEnabled = true;

    static MarkdownEditorView()
    {
        HeaderTextProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) => view.UpdatePaneHead());
        ChipTextProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) => view.UpdatePaneHead());
        EditorPaddingProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) =>
        {
            if (view._container is not null) view._container.Padding = view.EditorPadding;
        });
        EditorTextViewMarginProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) =>
            view._editor.TextArea.TextView.Margin = view.EditorTextViewMargin);
        EditorLineHeightProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) => view.UpdateEditorLineHeight());
        HighlightCurrentLineProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) => view.ApplyThemedVisuals());
        LineNumberBrushKeyProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) => view.ApplyThemedVisuals());
        CurrentLineNumberBrushKeyProperty.Changed.AddClassHandler<MarkdownEditorView>((view, _) => view.ApplyThemedVisuals());
    }

    public MarkdownEditorView()
        : this(new MarkdownEditorOptions(), null, null, null)
    {
    }

    public MarkdownEditorView(
        MarkdownEditorOptions? options = null,
        IMarkdownEditorLocalizer? localizer = null,
        IMarkdownEditorActionService? actionService = null,
        IMarkdownEditorSearchService? searchService = null)
    {
        _options = options ?? new MarkdownEditorOptions();
        _autoPairEnabled = _options.EnableAutoPair;

        var resolvedLocalizer = localizer ?? new MarkdownEditorLocalizer();
        var templates = new MarkdownEditorTemplateService(resolvedLocalizer, _options);
        Localizer = resolvedLocalizer;
        _actions = actionService ?? new MarkdownEditorActionService(templates, new MarkdownEditorMutationService(), _options);
        _search = searchService ?? new MarkdownEditorSearchService(resolvedLocalizer);

        _editor = new TextEditor
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas, Microsoft YaHei UI"),
            FontSize = _options.FontSize,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            WordWrap = true
        };
        _defaultLineHeightFactor = _editor.Options.LineHeightFactor;

        // 每个编辑器拥有独立配色，避免不同主题的并存编辑器改写全局语法定义。
        _codeHighlighting = LoadHighlighting("CSharp-Mode", HighlightingManager.Instance);
        _editor.SyntaxHighlighting = LoadEditorHighlighting();
        _showLineNumbers = _options.ShowLineNumbers;
        UpdateLineNumberMargin();
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextChanged += (_, _) => RaiseMarkdownChanged();
        _editor.TextArea.Caret.PositionChanged += (_, _) => RaiseSelectionChanged();
        _editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        InitializeSelectionToolbar();

        _container = new Border
        {
            Padding = new Thickness(6, 28, 34, 28),
            Child = _editor
        };

        BuildPaneHead(out _paneHead, out _chipHost, out _paneTitle);
        UpdatePaneHead();

        Content = BuildContent();
        ActualThemeVariantChanged += (_, _) => ApplyThemedVisuals();
        AttachedToVisualTree += (_, _) =>
        {
            ApplyThemedVisuals();
            UpdateEditorLineHeight();
        };
        ApplyThemedVisuals();
    }

    /// <summary>编辑器文本变更（含自动配对与动作产生的变更）；参数为当前全文。</summary>
    public event EventHandler<string>? MarkdownChanged;

    /// <summary>插入点变化：行、列、总行数。</summary>
    public event EventHandler<MarkdownCaretEventArgs>? SelectionChanged;

    /// <summary>动作被执行（宿主右键菜单、工具栏、快捷键都会走到这里），宿主可据此同步状态。</summary>
    public event EventHandler<MarkdownEditorAction>? ActionRequested;

    /// <summary>查找/替换结果文案，宿主可显示在查找栏。</summary>
    public event EventHandler<MarkdownEditorSearchResultEventArgs>? SearchResultProduced;

    /// <summary>插入点被移到某一行（例如点击大纲），宿主可据此滚动预览。</summary>
    public event EventHandler<MarkdownCaretEventArgs>? Navigated;

    /// <summary>当前使用的本地化实现（宿主可读取以复用文案）。</summary>
    public IMarkdownEditorLocalizer Localizer { get; }

    /// <summary>窗格头标题（例如「源码」）；为 null/空时不显示窗格头。</summary>
    public static readonly StyledProperty<string?> HeaderTextProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string?>(nameof(HeaderText));

    /// <summary>窗格头右侧芯片文案（例如 Markdown）；为 null/空时不显示芯片。</summary>
    public static readonly StyledProperty<string?> ChipTextProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string?>(nameof(ChipText), "Markdown");

    public static readonly StyledProperty<Thickness> EditorPaddingProperty =
        AvaloniaProperty.Register<MarkdownEditorView, Thickness>(nameof(EditorPadding), new Thickness(6, 28, 34, 28));

    /// <summary>
    /// 代码自动换行开关。关闭（false）时源码区横向滚动：
    /// TextEditor 以 Disabled 为横滚基准值，直接改 WordWrap 会被 AvaloniaEdit 还原成 Disabled，
    /// 因此这里成对改写滚动条可见性，保证「关=横向滚动」真实生效。
    /// </summary>
    public bool WordWrap
    {
        get => _editor.WordWrap;
        set
        {
            _editor.WordWrap = value;
            _editor.HorizontalScrollBarVisibility = value
                ? global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                : global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        }
    }

    public Thickness EditorPadding
    {
        get => GetValue(EditorPaddingProperty);
        set => SetValue(EditorPaddingProperty, value);
    }

    /// <summary>源码正文相对行号槽的边距。</summary>
    public static readonly StyledProperty<Thickness> EditorTextViewMarginProperty =
        AvaloniaProperty.Register<MarkdownEditorView, Thickness>(nameof(EditorTextViewMargin), new Thickness(6, 0, 0, 0));

    public Thickness EditorTextViewMargin
    {
        get => GetValue(EditorTextViewMarginProperty);
        set => SetValue(EditorTextViewMarginProperty, value);
    }

    /// <summary>源码行高（设备无关像素）；NaN 使用 AvaloniaEdit 默认行距，不改变字号。</summary>
    public static readonly StyledProperty<double> EditorLineHeightProperty =
        AvaloniaProperty.Register<MarkdownEditorView, double>(nameof(EditorLineHeight), double.NaN,
            validate: value => double.IsNaN(value) || double.IsFinite(value) && value > 0);

    public double EditorLineHeight
    {
        get => GetValue(EditorLineHeightProperty);
        set => SetValue(EditorLineHeightProperty, value);
    }

    /// <summary>设计令牌键：编辑器背景色。</summary>
    public static readonly StyledProperty<string> EditorBackgroundKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(EditorBackgroundKey), DefaultEditorBackgroundKey);

    /// <summary>设计令牌键：编辑器前景色。</summary>
    public static readonly StyledProperty<string> EditorForegroundKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(EditorForegroundKey), DefaultEditorForegroundKey);

    /// <summary>设计令牌键：当前行背景色。</summary>
    public static readonly StyledProperty<string> CurrentLineBackgroundKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(CurrentLineBackgroundKey), DefaultCurrentLineBackgroundKey);

    /// <summary>设计令牌键：当前行边框色。</summary>
    public static readonly StyledProperty<string> CurrentLineBorderKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(CurrentLineBorderKey), DefaultCurrentLineBorderKey);

    public static readonly StyledProperty<string> LineNumberBrushKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(LineNumberBrushKey), DefaultLineNumberKey);

    public static readonly StyledProperty<string> CurrentLineNumberBrushKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(CurrentLineNumberBrushKey), DefaultCurrentLineNumberKey);

    public static readonly StyledProperty<string> MarkerKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(MarkerKey), DefaultMarkerKey);

    /// <summary>设计令牌键：标题等强调色。</summary>
    public static readonly StyledProperty<string> AccentKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(AccentKey), DefaultAccentKey);

    /// <summary>设计令牌键：行内/围栏代码色。</summary>
    public static readonly StyledProperty<string> CodeKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(CodeKey), DefaultCodeKey);

    /// <summary>设计令牌键：引用色（同时用于代码注释）。</summary>
    public static readonly StyledProperty<string> QuoteKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(QuoteKey), DefaultQuoteKey);

    /// <summary>设计令牌键：链接色。</summary>
    public static readonly StyledProperty<string> LinkKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(LinkKey), DefaultLinkKey);

    /// <summary>设计令牌键：图片语法色。</summary>
    public static readonly StyledProperty<string> ImageKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(ImageKey), DefaultImageKey);

    /// <summary>设计令牌键：字符串/数字等字面量色。</summary>
    public static readonly StyledProperty<string> WarningKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(WarningKey), DefaultWarningKey);

    /// <summary>设计令牌键：类型关键字色。</summary>
    public static readonly StyledProperty<string> DangerKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(DangerKey), DefaultDangerKey);

    /// <summary>设计令牌键：分隔线/换行标记色。</summary>
    public static readonly StyledProperty<string> SeparatorKeyProperty =
        AvaloniaProperty.Register<MarkdownEditorView, string>(nameof(SeparatorKey), DefaultSeparatorKey);

    /// <summary>是否高亮当前行。</summary>
    public static readonly StyledProperty<bool> HighlightCurrentLineProperty =
        AvaloniaProperty.Register<MarkdownEditorView, bool>(nameof(HighlightCurrentLine), true);

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

    public string CurrentLineBackgroundKey
    {
        get => GetValue(CurrentLineBackgroundKeyProperty);
        set => SetValue(CurrentLineBackgroundKeyProperty, value);
    }

    public string CurrentLineBorderKey
    {
        get => GetValue(CurrentLineBorderKeyProperty);
        set => SetValue(CurrentLineBorderKeyProperty, value);
    }

    public string LineNumberBrushKey
    {
        get => GetValue(LineNumberBrushKeyProperty);
        set => SetValue(LineNumberBrushKeyProperty, value);
    }

    public string CurrentLineNumberBrushKey
    {
        get => GetValue(CurrentLineNumberBrushKeyProperty);
        set => SetValue(CurrentLineNumberBrushKeyProperty, value);
    }

    public string MarkerKey
    {
        get => GetValue(MarkerKeyProperty);
        set => SetValue(MarkerKeyProperty, value);
    }

    public string AccentKey
    {
        get => GetValue(AccentKeyProperty);
        set => SetValue(AccentKeyProperty, value);
    }

    public string CodeKey
    {
        get => GetValue(CodeKeyProperty);
        set => SetValue(CodeKeyProperty, value);
    }

    public string QuoteKey
    {
        get => GetValue(QuoteKeyProperty);
        set => SetValue(QuoteKeyProperty, value);
    }

    public string LinkKey
    {
        get => GetValue(LinkKeyProperty);
        set => SetValue(LinkKeyProperty, value);
    }

    public string ImageKey
    {
        get => GetValue(ImageKeyProperty);
        set => SetValue(ImageKeyProperty, value);
    }

    public string WarningKey
    {
        get => GetValue(WarningKeyProperty);
        set => SetValue(WarningKeyProperty, value);
    }

    public string DangerKey
    {
        get => GetValue(DangerKeyProperty);
        set => SetValue(DangerKeyProperty, value);
    }

    public string SeparatorKey
    {
        get => GetValue(SeparatorKeyProperty);
        set => SetValue(SeparatorKeyProperty, value);
    }

    public bool HighlightCurrentLine
    {
        get => GetValue(HighlightCurrentLineProperty);
        set => SetValue(HighlightCurrentLineProperty, value);
    }

    /// <summary>底层 AvaloniaEdit 控件，供宿主做深度定制（缩进、Tab 行为、右键菜单等）。</summary>
    public TextEditor Editor => _editor;

    public static readonly DirectProperty<MarkdownEditorView, string> TextProperty =
        AvaloniaProperty.RegisterDirect<MarkdownEditorView, string>(
            nameof(Text), view => view.Text, (view, value) => view.SetText(value),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public string Text
    {
        get => _editor.Text ?? string.Empty;
        set => SetText(value);
    }

    private bool _showLineNumbers;

    /// <summary>是否显示行号：使用库内行号边距（当前行强调色加粗），不用 AvaloniaEdit 内置边距。</summary>
    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            if (_showLineNumbers == value)
            {
                return;
            }

            _showLineNumbers = value;
            UpdateLineNumberMargin();
        }
    }

    /// <summary>编辑器字号（不占用 UserControl 的 FontSize，避免与控件自身字体语义混淆）。</summary>
    public double EditorFontSize
    {
        get => _editor.FontSize;
        set
        {
            _editor.FontSize = value;
            UpdateEditorLineHeight();
        }
    }

    /// <summary>自动配对开关（成对符号插入、选区包裹、空配对退格删除）。</summary>
    public bool EnableAutoPair
    {
        get => _autoPairEnabled;
        set => _autoPairEnabled = value;
    }

    /// <summary>替换文本（宿主加载文件时使用）；不会触发 <see cref="MarkdownChanged"/>。</summary>
    public void SetText(string? markdown)
    {
        var normalized = markdown ?? string.Empty;
        if (_editor.Text == normalized)
        {
            return;
        }

        _suppressChanged = true;
        try
        {
            _editor.Text = normalized;
            _editor.CaretOffset = 0;
        }
        finally
        {
            _suppressChanged = false;
        }
        NotifyTextChanged();
    }

    /// <summary>执行一次编辑器动作；完成后通过 <see cref="ActionRequested"/> 通知宿主。</summary>
    public Task ExecuteAsync(MarkdownEditorAction action)
    {
        ActionRequested?.Invoke(this, action);
        return _actions.ExecuteAsync(_editor, action, RunTextMutation);
    }

    /// <summary>粘贴 CSV/TSV/表格文本后插入 Markdown 表格骨架（未命中时插入 <paramref name="fallbackInsertion"/>）。</summary>
    public void InsertTable(string fallbackInsertion) =>
        RunTextMutation(() => new MarkdownEditorMutationService().InsertTable(_editor, fallbackInsertion));

    /// <summary>用前后缀包裹当前选区（无选区时插入占位符），供宿主自定义标记（如删除线）。</summary>
    public void WrapSelection(string prefix, string suffix, string? placeholder = null) =>
        RunTextMutation(() =>
            new MarkdownEditorMutationService().WrapSelection(_editor, prefix, suffix, placeholder ?? string.Empty));

    /// <summary>给选区包上行内 <c>&lt;span style&gt;</c>（color / background-color / font-size）；同款再执行一次解包。</summary>
    public void ApplySpanStyle(string property, string value) =>
        _actions.ExecuteSpan(_editor, property, value, RunTextMutation);

    /// <summary>读当前选区所在的行内样式值（紧贴同款 span 时返回，供宿主面板预填），否则 null。</summary>
    public string? GetSpanStyle(string property)
    {
        var text = _editor.Text ?? string.Empty;
        var start = Math.Clamp(_editor.SelectionStart, 0, text.Length);
        var end = Math.Clamp(_editor.SelectionStart + _editor.SelectionLength, start, text.Length);
        return CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownSpanOperations.CurrentValue(text, start, end, property);
    }

    /// <summary>按源码偏移应用一次外部改写（如预览勾选任务回写）；长度一致时只替换目标区间并保持光标。</summary>
    public void ApplyExternalEdit(string markdown, int start, int length)
    {
        if (_editor.Document is not { } document)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        var canReplaceInPlace = document.TextLength == markdown.Length
                                && start >= 0
                                && length > 0
                                && start + length <= markdown.Length;

        if (!canReplaceInPlace)
        {
            SetText(markdown);
            RaiseMarkdownChanged();
            return;
        }

        _suppressChanged = true;
        try
        {
            document.Replace(start, length, markdown.Substring(start, length));
            _editor.CaretOffset = Math.Min(caret, document.TextLength);
        }
        finally
        {
            _suppressChanged = false;
        }

        RaiseMarkdownChanged();
    }

    /// <summary>在当前插入点插入文本（图片拖入等场景）。</summary>
    public void InsertText(string text)
    {
        if (_editor.Document is not { } document || string.IsNullOrEmpty(text))
        {
            return;
        }

        var offset = Math.Clamp(_editor.CaretOffset, 0, document.TextLength);
        document.Insert(offset, text);
        _editor.CaretOffset = offset + text.Length;
        RaiseMarkdownChanged();
    }

    /// <summary>复制为纯文本：有选区复制选区，否则复制整篇。</summary>
    public async Task CopyPlainTextAsync()
    {
        var text = _editor.SelectedText;
        if (string.IsNullOrEmpty(text))
        {
            text = _editor.Text ?? string.Empty;
        }

        if (TopLevel.GetTopLevel(_editor)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>查找/替换；结果通过 <see cref="SearchResultProduced"/> 回调。</summary>
    public void Search(MarkdownEditorSearchCommand command) =>
        _search.Search(
            _editor,
            command,
            RunTextMutation,
            (message, current, total) => SearchResultProduced?.Invoke(
                this,
                new MarkdownEditorSearchResultEventArgs(message, current, total)));

    /// <summary>当前选中的文本。</summary>
    public string SelectedText => _editor.SelectedText ?? string.Empty;

    /// <summary>选中区间的起点与长度。</summary>
    public (int Start, int Length) Selection => (_editor.SelectionStart, _editor.SelectionLength);

    /// <summary>当前插入点（行、列）与文档总行数；未挂载文档时为 (1, 1, 1)。</summary>
    public (int Line, int Column, int LineCount) CaretPosition
    {
        get
        {
            var caret = _editor.TextArea.Caret;
            return (caret.Line, caret.Column, _editor.Document?.LineCount ?? 1);
        }
    }

    /// <summary>文档总行数；未挂载文档时为 1。</summary>
    public int LineCount => _editor.Document?.LineCount ?? 1;

    public void FocusEditor() => _editor.Focus();

    /// <summary>滚动到指定行并把光标放在行首。</summary>
    public void NavigateToLine(int line)
    {
        if (_editor.Document is null)
        {
            return;
        }

        var target = Math.Clamp(line, 1, _editor.Document.LineCount);
        _editor.CaretOffset = _editor.Document.GetLineByNumber(target).Offset;
        _editor.TextArea.Caret.BringCaretToView();
        _editor.Focus();
        RaiseSelectionChanged();
        Navigated?.Invoke(this, new MarkdownCaretEventArgs(target, 1, _editor.Document.LineCount));
    }

    /// <summary>空配对退格删除（自动配对的一部分）；返回是否已处理。</summary>
    public bool TryHandleAutoPairBackspace()
    {
        if (_editor.Document is not { } document || !_autoPairEnabled)
        {
            return false;
        }

        var result = TextAutoPair.HandleBackspace(
            document.Text,
            _editor.SelectionStart,
            _editor.SelectionLength);
        if (result is not { } change)
        {
            return false;
        }

        document.Replace(change.Start, change.Length, change.Text);
        _editor.CaretOffset = change.CaretOffset;
        return true;
    }

    #region 视觉

    private Control BuildContent()
    {
        var host = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };

        host.Children.Add(_paneHead);

        Grid.SetRow(_container, 1);
        host.Children.Add(_container);
        return host;
    }

    // 窗格头只提供结构与语义类名（pane-head / pane-title / kbd-chip / stroke-ico），
    // 皮肤交给宿主样式表，避免控件把宿主的配色写死。
    private void BuildPaneHead(out Border head, out Border chipHost, out TextBlock title)
    {
        title = new TextBlock
        {
            Classes = { "pane-title" },
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };

        var icon = new global::Avalonia.Controls.Shapes.Path
        {
            Classes = { "stroke-ico" },
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
        };
        icon.SetValue(WidthProperty, 12, global::Avalonia.Data.BindingPriority.Style);
        icon.SetValue(HeightProperty, 12, global::Avalonia.Data.BindingPriority.Style);
        icon.SetValue(global::Avalonia.Controls.Shapes.Path.DataProperty,
            Geometry.Parse("M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7 M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4z"),
            global::Avalonia.Data.BindingPriority.Style);

        var left = new StackPanel
        {
            Orientation = global::Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8
        };
        left.Children.Add(icon);
        left.Children.Add(title);

        chipHost = new Border { Classes = { "kbd-chip" }, Child = new TextBlock() };

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

        var chip = ChipText ?? string.Empty;
        if (_chipHost.Child is TextBlock chipText)
        {
            chipText.Text = chip;
        }

        _chipHost.IsVisible = !string.IsNullOrEmpty(chip);
    }

    private void UpdateEditorLineHeight()
    {
        if (double.IsNaN(EditorLineHeight))
        {
            _editor.Options.LineHeightFactor = _defaultLineHeightFactor;
            return;
        }

        // LineHeightFactor 相对字体实测高度，而 CSS 行距相对字号，需先去掉当前倍率。
        var textHeight = _editor.TextArea.TextView.DefaultLineHeight / _editor.Options.LineHeightFactor;
        _editor.Options.LineHeightFactor = EditorLineHeight / textHeight;
    }

    private void ApplyThemedVisuals()
    {
        _editor.Options.HighlightCurrentLine = HighlightCurrentLine;
        _editor.TextArea.TextView.Margin = EditorTextViewMargin;

        var background = ResolveBrush(EditorBackgroundKey);
        _container.Background = background;
        _editor.Background = background;
        _editor.Foreground = ResolveBrush(EditorForegroundKey);
        _editor.TextArea.TextView.LinkTextForegroundBrush = ResolveBrush(LinkKey);

        _editor.TextArea.TextView.CurrentLineBackground = ResolveBrush(CurrentLineBackgroundKey) ?? Brushes.Transparent;
        _editor.TextArea.TextView.CurrentLineBorder = new Pen(ResolveBrush(CurrentLineBorderKey) ?? Brushes.Transparent, 1);
        UpdateLineNumberMargin();

        ApplyThemedSyntaxHighlighting();
    }

    // 行号边距：AvaloniaEdit 内置边距无逐行着色能力，改用库内边距实现当前行强调。
    private void UpdateLineNumberMargin()
    {
        if (_showLineNumbers && _lineNumberMargin is null)
        {
            _lineNumberMargin = new MarkdownLineNumberMargin();
            _editor.TextArea.LeftMargins.Insert(0, _lineNumberMargin);
        }
        else if (!_showLineNumbers && _lineNumberMargin is not null)
        {
            _editor.TextArea.LeftMargins.Remove(_lineNumberMargin);
            _lineNumberMargin = null;
        }

        if (_lineNumberMargin is null)
        {
            return;
        }

        _lineNumberMargin.LineNumbersBrush = ResolveBrush(LineNumberBrushKey) ?? ResolveBrush(EditorForegroundKey);
        _lineNumberMargin.CurrentLineBrush = ResolveBrush(CurrentLineNumberBrushKey)
                                             ?? ResolveBrush(AccentKey)
                                             ?? ResolveBrush(EditorForegroundKey);
        _lineNumberMargin.InvalidateMeasure();
        _lineNumberMargin.InvalidateVisual();
    }

    // Markdown 语法着色：标题用强调色，代码/引用/链接/图片各自语义色，颜色全部可被宿主资源键覆盖。
    private void ApplyThemedSyntaxHighlighting()
    {
        if (_editor.SyntaxHighlighting is not { } definition)
        {
            return;
        }

        SetHighlightingForeground(definition, "Heading", ResolveBrush(AccentKey));
        SetHighlightingForeground(definition, "Marker", ResolveBrush(MarkerKey) ?? ResolveBrush(EditorForegroundKey));
        SetHighlightingForeground(definition, "Strike", ResolveBrush(QuoteKey));
        SetHighlightingForeground(definition, "Url", ResolveBrush(MarkerKey) ?? ResolveBrush(EditorForegroundKey));
        SetHighlightingForeground(definition, "Code", ResolveBrush(CodeKey));
        SetHighlightingForeground(definition, "BlockQuote", ResolveBrush(QuoteKey));
        SetHighlightingForeground(definition, "Link", ResolveBrush(LinkKey));
        SetHighlightingForeground(definition, "Image", ResolveBrush(ImageKey));
        // LineBreak 内置浅灰背景在暗色主题刺眼，换成主题分隔色。
        SetHighlightingBackground(definition, "LineBreak", ResolveBrush(SeparatorKey));
        ApplyCodeHighlightingColors();
        _editor.TextArea.TextView.Redraw();
    }

    // Markdown 缩进代码块通过 ruleSet 导入复用 C# 着色，内置配色（Green 注释、Blue 关键字等）
    // 在暗色编辑器背景上不可读，映射到当前实例的编辑器语义色。
    private void ApplyCodeHighlightingColors()
    {
        var codeDefinition = _codeHighlighting;

        SetHighlightingForeground(codeDefinition, "Comment", ResolveBrush(QuoteKey));
        SetHighlightingForeground(codeDefinition, "Preprocessor", ResolveBrush(QuoteKey));
        SetHighlightingForeground(codeDefinition, "String", ResolveBrush(WarningKey));
        SetHighlightingForeground(codeDefinition, "Char", ResolveBrush(WarningKey));
        SetHighlightingForeground(codeDefinition, "StringInterpolation", ResolveBrush(CodeKey));
        SetHighlightingForeground(codeDefinition, "NumberLiteral", ResolveBrush(WarningKey));
        SetHighlightingForeground(codeDefinition, "Keywords", ResolveBrush(LinkKey));
        SetHighlightingForeground(codeDefinition, "GotoKeywords", ResolveBrush(LinkKey));
        SetHighlightingForeground(codeDefinition, "ValueTypeKeywords", ResolveBrush(DangerKey));
        SetHighlightingForeground(codeDefinition, "ReferenceTypeKeywords", ResolveBrush(DangerKey));
        SetHighlightingForeground(codeDefinition, "NullOrValueKeywords", ResolveBrush(DangerKey));
        SetHighlightingForeground(codeDefinition, "MethodCall", ResolveBrush(CodeKey));
        foreach (var name in new[] { "Visibility", "Modifiers", "ContextKeywords", "TypeKeywords",
            "TrueFalse", "NamespaceKeywords", "GetSetAddRemove", "SemanticKeywords" })
        {
            SetHighlightingForeground(codeDefinition, name, ResolveBrush(LinkKey));
        }
        foreach (var name in new[] { "ExceptionKeywords", "CheckedKeyword", "UnsafeKeywords",
            "OperatorKeywords", "ParameterModifiers" })
        {
            SetHighlightingForeground(codeDefinition, name, ResolveBrush(CodeKey));
        }
    }

    private static void SetHighlightingForeground(IHighlightingDefinition definition, string colorName, IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return;
        }

        if (FindColor(definition, colorName) is { } color)
        {
            color.Foreground = new SimpleHighlightingBrush(solid.Color);
        }
    }

    private static void SetHighlightingBackground(IHighlightingDefinition definition, string colorName, IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return;
        }

        if (FindColor(definition, colorName) is { } color)
        {
            color.Background = new SimpleHighlightingBrush(solid.Color);
        }
    }

    // 源码着色用库内自定义 xshd（符号降调、围栏/表格/任务列表齐备），镜像原型 source-highlight.js。
    private IHighlightingDefinition LoadEditorHighlighting()
    {
        using var stream = typeof(MarkdownEditorView).Assembly.GetManifestResourceStream(
            "CodeWF.Avalonia.Markdown.Editor.Highlighting.VexMarkDown-Mode.xshd")
            ?? throw new InvalidOperationException("Missing editor highlighting resource: VexMarkDown-Mode.xshd");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, new EditorHighlightingResolver(_codeHighlighting));
    }

    private static HighlightingColor? FindColor(IHighlightingDefinition definition, string colorName) =>
        definition.NamedHighlightingColors.FirstOrDefault(
            candidate => string.Equals(candidate.Name, colorName, StringComparison.OrdinalIgnoreCase));

    private static IHighlightingDefinition LoadHighlighting(string resource, IHighlightingDefinitionReferenceResolver resolver)
    {
        using var stream = typeof(HighlightingManager).Assembly.GetManifestResourceStream(
            $"AvaloniaEdit.Highlighting.Resources.{resource}.xshd")
            ?? throw new InvalidOperationException($"Missing editor highlighting resource: {resource}");
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, resolver);
    }

    private sealed class EditorHighlightingResolver(IHighlightingDefinition code) : IHighlightingDefinitionReferenceResolver
    {
        public IHighlightingDefinition GetDefinition(string name) => name == "C#"
            ? code : HighlightingManager.Instance.GetDefinition(name);
    }

    /// <summary>
    /// 先查宿主资源（<paramref name="key"/>），再查内置调色板（同名键）。
    /// 沿控件、容器、窗口和应用依次查找，保留宿主的局部主题覆盖。
    /// </summary>
    private IBrush? ResolveBrush(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return MarkdownEditorPalette.Resolve(key, ActualThemeVariant);
    }

    #endregion

    #region 输入

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && IsPlainPasteGesture(e.KeyModifiers))
        {
            e.Handled = true;
            _ = ExecuteAsync(MarkdownEditorAction.Paste);
            return;
        }

        if (e.Key == Key.C && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = CopyPlainTextAsync();
            return;
        }

        if (e.Key == Key.Back && e.KeyModifiers == KeyModifiers.None && _autoPairEnabled)
        {
            e.Handled = TryHandleAutoPairBackspace();
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            _ = ExecuteAsync(MarkdownEditorAction.SmartNewLine);
            return;
        }

        if (e.Key != Key.Tab)
        {
            return;
        }

        e.Handled = true;
        _ = ExecuteAsync(e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? MarkdownEditorAction.Outdent
            : MarkdownEditorAction.Indent);
    }

    private static bool IsPlainPasteGesture(KeyModifiers modifiers) =>
        OperatingSystem.IsMacOS()
            ? modifiers is KeyModifiers.Meta
            : modifiers is KeyModifiers.Control;

    // 自动配对：Unicode 字符输入（含 IME 组字）不经此事件，故组字态天然不受影响。
    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (!_autoPairEnabled || string.IsNullOrEmpty(e.Text) || e.Text.Length != 1 || _editor.Document is not { } document)
        {
            return;
        }

        var result = TextAutoPair.HandleTextInput(
            document.Text,
            _editor.SelectionStart,
            _editor.SelectionLength,
            e.Text);
        if (result is not { } change)
        {
            return;
        }

        e.Handled = true;
        if (change.Length > 0 || change.Text.Length > 0)
        {
            document.Replace(change.Start, change.Length, change.Text);
        }

        _editor.CaretOffset = change.CaretOffset;
    }

    #endregion

    #region 变更通知

    private void RunTextMutation(Action mutation)
    {
        _suppressChanged = true;
        try
        {
            mutation();
        }
        finally
        {
            _suppressChanged = false;
        }

        RaiseMarkdownChanged();
    }

    private void RaiseMarkdownChanged()
    {
        if (!_suppressChanged)
        {
            NotifyTextChanged();
            MarkdownChanged?.Invoke(this, _editor.Text ?? string.Empty);
        }
    }

    private void NotifyTextChanged()
    {
        var text = Text;
        var previous = _lastText;
        _lastText = text;
        RaisePropertyChanged(TextProperty, previous, text);
    }

    private void RaiseSelectionChanged()
    {
        var caret = _editor.TextArea.Caret;
        SelectionChanged?.Invoke(
            this,
            new MarkdownCaretEventArgs(caret.Line, caret.Column, _editor.Document?.LineCount ?? 1));
    }

    #endregion
}
