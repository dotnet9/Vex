using Avalonia;
using CodeWF.Avalonia.Markdown.Core.Blocks;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.Lang;
using global::Avalonia.Threading;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Controls.Presenters;
using global::Avalonia.Data;
using CodeWF.Avalonia.Markdown.Mermaid;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>在 TextBox 默认插入之前处理已提交文本，保留输入法预编辑流程。</summary>
internal class MarkdownInputTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);
    public Action<TextInputEventArgs>? BeforeTextInput { get; set; }
    protected override void OnTextInput(TextInputEventArgs e)
    {
        BeforeTextInput?.Invoke(e);
        if (!e.Handled) base.OnTextInput(e);
    }

    /// <summary>行内图片叠绘：presenter.Render 密封，挂到 TextBox 渲染层（其层级在 presenter 文字之下，
    /// 占位字符零宽且透明，视觉互不遮挡）。</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        foreach (var presenter in this.GetVisualDescendants().OfType<MarkdownInlinePresenter>())
            presenter.DrawImageOverlays(context, this);
    }
}

/// <summary>
/// 一个块的就地编辑视图：默认渲染为富文本（只读显示，可选中），
/// 点入后使用原位富文本输入（<see cref="TextBox"/>），失焦或按 Esc 结束编辑，
/// 按 Tab 进入下一个块。文本变化通过 <see cref="TextCommitted"/> 回抛给宿主视图。
/// </summary>
internal sealed class MarkdownBlockView : UserControl
{
    private readonly MarkdownRichTextView _richText = new();
    private MarkdownInputTextBox _editor;
    private readonly TextBox? _codeLanguage;
    private readonly Border _root;
    private readonly Grid _layer = new();
    private readonly Canvas _editingLayer = new() { IsVisible = false };
    private SelectableTextBlock? _editingText;
    private bool _inlineEditing;
    private MarkdownViewer? _preview;
    private readonly bool _isCode;
    private readonly bool _isTableCell;
    private readonly MarkdownBlockViewOptions _options;
    private TopLevel? _diagramOwner;
    private bool IsDiagram => _isCode && string.Equals(Model.Language, "mermaid", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 块正文包含图片语法（不含表格单元格/代码）。渲染态（未首击）仍由 Viewer 出图；
    /// 进入单表面后由 InlinePresenter 内嵌绘制（阶段 3），不再走源码覆盖模式。
    /// </summary>
    private bool HasImageSyntax => !_isCode && !_isTableCell && Model.Kind != MarkdownBlockKind.Raw
        && ModelText.Contains("![", StringComparison.Ordinal);
    private bool IsSourceOverlay => IsDiagram;

    private double _runFontSize = 15;

    private bool _editing;
    private bool _syncingText;
    private IPointer? _selectionPointer;
    private int _pointerSelectionAnchor;
    private string _editOriginalText = string.Empty;
    private string _editOriginalLanguage = string.Empty;

    public MarkdownBlockView(MarkdownBlockModel model, bool isCode, MarkdownBlockViewOptions options, bool isTableCell = false)
    {
        Model = model;
        _isCode = isCode;
        _isTableCell = isTableCell;
        _options = options;
        _editor = IsSourceOverlay ? new MermaidSourceInput() : new MarkdownInputTextBox();

        if (isCode && !IsDiagram)
        {
            // 语言选择占用代码块原有的下方留白，出现光标时不推动正文。
            _layer.RowDefinitions = new RowDefinitions("Auto,20");
            Grid.SetRowSpan(_editingLayer, 2);
            _codeLanguage = new TextBox
            {
                Classes = { "MdCodeLanguageInput" }, Width = 180, Height = 24, MinHeight = 0,
                Padding = new Thickness(8, 2), FontSize = 12, TextAlignment = TextAlignment.Center, PlaceholderText = "text",
                Background = options.CodeBackgroundBrush, Foreground = options.TextBrush,
                BorderBrush = options.SeparatorBrush, BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };
            _codeLanguage.TextChanged += (_, _) =>
            {
                if (!_editing || _syncingText) return;
                var language = (_codeLanguage.Text ?? string.Empty).Trim().Replace("`", string.Empty);
                if (Model.Language == language) return;
                Model.Language = language; UpdateRichText(); TextCommitted?.Invoke(this, ModelText);
            };
            _codeLanguage.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { _editor.Focus(); e.Handled = true; }
                else if (e.Key == Key.Escape) { CancelEdit(); e.Handled = true; }
            };
            _codeLanguage.LostFocus += (_, _) => CommitIfFocusLeft();
            _editingLayer.Children.Add(_codeLanguage);
        }

        ConfigureSourceInput();

        _editingLayer.Children.Add(_editor);
        _layer.Children.Add(_richText); _layer.Children.Add(_editingLayer);
        _root = new Border { Child = _layer };
        Content = _root;

        // 先接管渲染块的点击，避免 SelectableTextBlock 在冒泡前后抢回焦点，
        // 导致刚打开的编辑框立即触发 LostFocus 并消失。
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        PointerCaptureLost += (_, _) => _selectionPointer = null;
        UpdateRichText();
        LayoutUpdated += (_, _) => { if (_inlineEditing) PositionInlineEditor(); };
        DetachedFromVisualTree += (_, _) => StopWatchingDiagramPointer();
    }

    private void ConfigureSourceInput()
    {
        _editor.AcceptsReturn = true;
        _editor.ClearSelectionOnLostFocus = false;
        _editor.TextWrapping = TextWrapping.Wrap;
        _editor.FontFamily = _isCode || IsSourceOverlay ? _options.CodeFontFamily : _options.ContentFontFamily;
        if (_isCode) _editor.TextWrapping = _options.CodeWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _editor.FontSize = _runFontSize;
        _editor.Foreground = _options.TextBrush;
        _editor.CaretBrush = _options.AccentBrush;
        _editor.HorizontalAlignment = HorizontalAlignment.Stretch;
        _editor.Text = ModelText;
        if (IsSourceOverlay)
        {
            _layer.RowDefinitions = new RowDefinitions("Auto,Auto");
            _editor.Margin = new Thickness(0, 0, 0, 10);
            _editor.Background = _options.CodeBackgroundBrush;
            _editor.BorderBrush = _options.SeparatorBrush;
            if (!IsDiagram)
            {
                // 图片源通常一行写完，不留图表源码卡的最小高度，编辑时更贴近 Typora 的紧凑源码框。
                _editor.MinHeight = 0;
            }
        }
        else
        {
            _editor.Padding = new Thickness(0);
            _editor.BorderThickness = new Thickness(0);
            _editor.Background = Brushes.Transparent;
            // 空文档/空段落：一行高度的空白块也要可点击可显光标（Typora 空文档行为）。
            // MinWidth 兜底：Canvas 内空文本期望宽为 0，无它则编辑框 0 宽不可见——
            // 真机实测内容照进（字数/模型更新）但光标与文字全部不可见。
            _editor.MinWidth = 160;
            _editor.MinHeight = _runFontSize * 1.6;
            // 空预览（Viewer 零段落）时 Grid 高度塌陷为 0：Canvas 不贡献量测尺寸，
            // 块没有可视存在。层高兜底一行，光标/编辑框有绘制空间。
            _layer.MinHeight = _editor.MinHeight;
            _editor.Template = new FuncControlTemplate<TextBox>((box, scope) =>
            {
                var presenter = new MarkdownInlinePresenter(_options, _isCode || Model.Kind == MarkdownBlockKind.Raw) { Name = "PART_TextPresenter" };
                scope.Register("PART_TextPresenter", presenter);
                // 与标准 TextBox 模板一致，由输入框单向驱动 presenter；反向光标绑定会在 Shift 选择时清空选区。
                foreach (var (boxProperty, presenterProperty) in new (AvaloniaProperty, AvaloniaProperty)[]
                         {
                             (TextBox.TextProperty, TextPresenter.TextProperty),
                             (TextBox.CaretIndexProperty, TextPresenter.CaretIndexProperty),
                             (TextBox.SelectionStartProperty, TextPresenter.SelectionStartProperty),
                             (TextBox.SelectionEndProperty, TextPresenter.SelectionEndProperty),
                             (TextBox.FontFamilyProperty, global::Avalonia.Controls.Documents.TextElement.FontFamilyProperty),
                             (TextBox.FontSizeProperty, global::Avalonia.Controls.Documents.TextElement.FontSizeProperty),
                             (TextBox.FontWeightProperty, global::Avalonia.Controls.Documents.TextElement.FontWeightProperty),
                             (TextBox.FontStyleProperty, global::Avalonia.Controls.Documents.TextElement.FontStyleProperty),
                             (TextBox.ForegroundProperty, global::Avalonia.Controls.Documents.TextElement.ForegroundProperty),
                             (TextBox.CaretBrushProperty, TextPresenter.CaretBrushProperty),
                             (TextBox.SelectionBrushProperty, TextPresenter.SelectionBrushProperty),
                             (TextBox.SelectionForegroundBrushProperty, TextPresenter.SelectionForegroundBrushProperty),
                             (TextBox.TextWrappingProperty, TextPresenter.TextWrappingProperty),
                             (TextBox.LineHeightProperty, TextPresenter.LineHeightProperty),
                         })
                {
                    presenter.Bind(presenterProperty, box.GetObservable(boxProperty));
                }
                var scroll = new ScrollViewer { Name = "PART_ScrollViewer", Content = presenter,
                    HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
                scope.Register("PART_ScrollViewer", scroll); return scroll;
            });
        }
        _editor.TextChanged += (_, _) =>
        {
            if (!_editing || _syncingText) return;
            var text = _editor.Text ?? string.Empty;
            if (string.Equals(ModelText, text, StringComparison.Ordinal)) return;
            ModelText = text;
            UpdateRichText();
            TextCommitted?.Invoke(this, ModelText);
        };
        // Ctrl+Click 打开链接（Typora 交互，单击仍落光标）：命中 LinkUrl run 才拦截默认按下。
        _editor.AddHandler(PointerPressedEvent, OnEditorPointerPressed, RoutingStrategies.Tunnel);
        _editor.LostFocus += (_, _) => CommitIfFocusLeft();
        _editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.PropertyChanged += (_, e) =>
        {
            if (_editing && e.Property.Name is "CaretIndex" or "SelectionStart" or "SelectionEnd") EditorSelectionChanged?.Invoke();
        };
    }

    private void EnsureSourceInput()
    {
        if (IsSourceOverlay == (_editor is MermaidSourceInput)) return;
        var wasEditing = _editing;
        var caret = _editor.CaretIndex;
        var originalText = _editOriginalText;
        var originalLanguage = _editOriginalLanguage;
        var textEntering = _editor.BeforeTextInput;
        _editing = false;
        EndInlineEditor();
        _editingLayer.Children.Remove(_editor);
        _editor = IsSourceOverlay ? new MermaidSourceInput() : new MarkdownInputTextBox();
        ConfigureSourceInput();
        _editor.BeforeTextInput = textEntering;
        _editingLayer.Children.Add(_editor);
        _layer.RowDefinitions = new RowDefinitions(IsSourceOverlay ? "Auto,Auto" : "Auto,20");
        if (wasEditing)
        {
            BeginEdit(caret);
            _editOriginalText = originalText;
            _editOriginalLanguage = originalLanguage;
        }
    }

    /// <summary>块内容变化的回调（参数为新文本）。</summary>
    public Action<MarkdownBlockView, string>? TextCommitted { get; set; }

    /// <summary>请求把焦点移到相邻块（参数为相对方向：-1 上一块，+1 下一块）。</summary>
    public Action<int>? NavigateRequested { get; set; }
    public Func<int, double?, bool>? BoundaryNavigateRequested { get; set; }
    public Func<int, bool>? MergeRequested { get; set; }
    public Action<int>? IndentRequested { get; set; }

    public Action<MarkdownBlockView>? EditingStarted { get; set; }
    public Action? EditorSelectionChanged { get; set; }
    internal Action<TextInputEventArgs>? TextEntering { get => _editor.BeforeTextInput; set => _editor.BeforeTextInput = value; }

    public Action<MarkdownBlockView, int>? SplitRequested { get; set; }

    public MarkdownBlockModel Model { get; }

    private bool _renderQuoteAsParagraph;
    internal bool RenderQuoteAsParagraph
    {
        get => _renderQuoteAsParagraph;
        set { _renderQuoteAsParagraph = value; UpdateRichText(); }
    }

    /// <summary>正文字号；标题与代码块按类型设置。</summary>
    public double RunFontSize
    {
        get => _runFontSize;
        set
        {
            _runFontSize = value;
            _editor.FontSize = value;
            UpdateRichText();
        }
    }

    internal double RunLineHeight
    {
        get => _richText.LineHeight;
        set { _richText.LineHeight = value; _editor.LineHeight = value; }
    }

    /// <summary>外部修改模型后刷新显示并通知宿主。</summary>
    public void NotifyModelChanged()
    {
        RefreshFromModel();
        TextCommitted?.Invoke(this, Model.Text);
    }

    public bool IsEditing => _editing;

    public void InsertText(string text)
    {
        if (!_editing) BeginEditAtEnd();
        var start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
        var length = Math.Abs(_editor.SelectionEnd - _editor.SelectionStart);
        _editor.Text = CurrentText.Remove(start, length).Insert(start, text);
        _editor.CaretIndex = start + text.Length;
    }

    /// <summary>正在编辑时的底层文本框（用于格式化动作），否则为 null。</summary>
    internal TextBox? ActiveEditor => _editing ? _editor : null;
    internal TextBox EditingInput => _editor;

    /// <summary>当前编辑文本（非编辑态返回模型文本）。</summary>
    internal string CurrentText => _editing ? _editor.Text ?? string.Empty : ModelText;

    private string ModelText
    {
        get => Model.Kind == MarkdownBlockKind.Raw ? Model.Raw : Model.Text;
        set { if (Model.Kind == MarkdownBlockKind.Raw) Model.Raw = value; else Model.Text = value; }
    }

    /// <summary>进入编辑态并把光标放到行尾（等价于点入该块）。</summary>
    public void BeginEditAtEnd() => BeginEdit(ModelText.Length);

    /// <summary>进入编辑态，并把光标放到指定偏移。</summary>
    public void BeginEdit(int caretIndex)
    {
        if (_editing)
        {
            _editor.CaretIndex = Math.Clamp(caretIndex, 0, _editor.Text?.Length ?? 0);
            _editor.Focus();
            return;
        }

        _editOriginalText = ModelText;
        _editOriginalLanguage = Model.Language;
        _syncingText = true;
        _editor.Text = ModelText;
        if (_codeLanguage is not null) _codeLanguage.Text = Model.Language;
        _syncingText = false;
        _editing = true;
        UpdateRichText();
        _inlineEditing = !IsSourceOverlay && (_isCode || Model.Kind != MarkdownBlockKind.Raw);
        if (IsSourceOverlay)
        {
            _editingLayer.Children.Remove(_editor);
            SetDisplay(_preview!);
            _diagramOwner = TopLevel.GetTopLevel(this);
            _diagramOwner?.AddHandler(PointerPressedEvent, OnDiagramOutsidePointer, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
        else if (_inlineEditing)
        {
            _editingLayer.IsVisible = true;
            PositionInlineEditor();
        }
        else
        {
            _editingLayer.Children.Remove(_editor);
            _root.Child = _editor;
        }
        var focusGranted = _editor.Focus();
        CodeWF.Toolkit.Logging.Logger.Debug($"beginedit focus={focusGranted} immediate={_editor.IsFocused} " +
            $"editorAttached={_editor.IsAttachedToVisualTree()} visible={IsEffectivelyVisible} layerVisible={_editingLayer.IsVisible} inline={_inlineEditing}");
        _editor.CaretIndex = Math.Clamp(caretIndex, 0, _editor.Text?.Length ?? 0);
        EditingStarted?.Invoke(this);
        if (!_editor.IsFocused)
        {
            // 真机上 BeginEdit 可能发生在视图可见性传播完成之前（切布局/文档切换瞬间），
            // 同步 Focus 静默失败；推到布局稳定后重试一次（headless 时序不同，探针一直绿）。
            var retryCaret = Math.Clamp(caretIndex, 0, _editor.Text?.Length ?? 0);
            Dispatcher.UIThread.Post(() =>
            {
                if (_editing && !_editor.IsFocused && IsEffectivelyVisible)
                {
                    _editor.Focus();
                    _editor.CaretIndex = Math.Clamp(retryCaret, 0, _editor.Text?.Length ?? 0);
                    _editor.SelectionStart = _editor.SelectionEnd = _editor.CaretIndex;
                }
            }, DispatcherPriority.Background);
        }
    }

    /// <summary>回渲染（提交文本）。</summary>
    public void CommitAndRender()
    {
        if (!_editing)
        {
            return;
        }

        _editing = false;
        EndInlineEditor();
        var text = _editor.Text ?? string.Empty;
        if (!string.Equals(text, ModelText, StringComparison.Ordinal))
        {
            ModelText = text;
            TextCommitted?.Invoke(this, text);
        }
        UpdateRichText();
    }

    /// <summary>纯文本块（非覆盖/代码/含图/含式）：编辑态常驻，失焦不翻回渲染态。
    /// 模型已逐键同步，失焦提交只剩「翻态」效果——那正是双态架构的脆弱源；渲染保真由
    /// InlinePresenter（源码+富文本样式+语法标记隐藏）承担，复杂块仍走失焦提交回渲染。</summary>
    public bool IsPureText =>
        !_isCode && !_isTableCell && !IsDiagram
        && Model.Kind is MarkdownBlockKind.Paragraph or MarkdownBlockKind.Heading
            or MarkdownBlockKind.ListItem or MarkdownBlockKind.Quote;

    private void CommitIfFocusLeft() => Dispatcher.UIThread.Post(() =>
    {
        if (IsPureText) return;
        if (!_editor.IsKeyboardFocusWithin && _codeLanguage?.IsKeyboardFocusWithin != true) CommitAndRender();
    }, DispatcherPriority.Input);

    private void OnDiagramOutsidePointer(object? sender, PointerPressedEventArgs args)
    {
        if (args.Source is Visual source && (source == this || source.GetVisualAncestors().Contains(this))) return;
        CommitAndRender();
    }

    private void StopWatchingDiagramPointer()
    {
        _diagramOwner?.RemoveHandler(PointerPressedEvent, OnDiagramOutsidePointer);
        _diagramOwner = null;
    }

    private void CancelEdit()
    {
        ModelText = _editOriginalText; Model.Language = _editOriginalLanguage;
        _editing = false; EndInlineEditor(); UpdateRichText(); TextCommitted?.Invoke(this, ModelText);
    }

    /// <summary>外部替换内容（例如预览回写）时刷新显示。</summary>
    public void RefreshFromModel()
    {
        if (_editing)
        {
            return;
        }

        UpdateRichText();
    }

    /// <summary>
    /// 外部已直接改写模型文本（增量结构操作：拆块截头/合并续接），编辑态下强制同步
    /// 编辑器与覆盖定位——否则失焦提交会把编辑器里的旧全文写回截断后的模型造成内容回滚。
    /// 非编辑态等价 RefreshFromModel。
    /// </summary>
    public void SyncEditorFromModel(int caretIndex)
    {
        if (!_editing)
        {
            UpdateRichText();
            return;
        }

        _syncingText = true;
        _editor.Text = Model.Text;
        if (_codeLanguage is not null) _codeLanguage.Text = Model.Language;
        _syncingText = false;
        UpdateRichText();
        if (_inlineEditing) PositionInlineEditor();
        _editor.CaretIndex = Math.Clamp(caretIndex, 0, _editor.Text?.Length ?? 0);
        _editor.SelectionStart = _editor.SelectionEnd = _editor.CaretIndex;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual interactive && IsInteractiveChild(interactive)) return;
        if (e.Source is Visual languageSource && _codeLanguage is not null && (languageSource == _codeLanguage || languageSource.GetVisualAncestors().Contains(_codeLanguage))) return;
        if (_editing || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var offset = GetPointerTextOffset(e);
        BeginEdit(offset);
        _pointerSelectionAnchor = offset;
        _editor.SelectionStart = _editor.SelectionEnd = offset;
        // 初次按下发生在只读预览上，TextBox 没有收到按下事件，必须保留拖选起点。
        _selectionPointer = e.Pointer;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    internal static bool IsInteractiveChild(Visual source) => source.GetVisualAncestors().Prepend(source)
        .Any(element => element is global::Avalonia.Controls.Primitives.ScrollBar or Button);

    /// <summary>Ctrl+Click：光标命中 LinkUrl run 时经共享策略打开（仅 http/https/mailto），拦截默认落光标。</summary>
    private void OnEditorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsPureText) return;
        var point = e.GetCurrentPoint(_editor);
        if (!point.Properties.IsLeftButtonPressed || (e.KeyModifiers & KeyModifiers.Control) == 0) return;
        var offset = GetPointerTextOffset(e);
        var url = MarkdownTextRunParser.Parse(ModelText)
            .Where(run => run.LinkUrl is not null
                && offset >= run.SourceStart && offset < run.SourceStart + run.Text.Length)
            .Select(run => run.LinkUrl)
            .FirstOrDefault();
        if (url is null) return;
        Shared.Rendering.MarkdownUrlPolicy.Open(url);
        e.Handled = true;
    }

    internal int GetPointerTextOffset(PointerEventArgs e)
    {
        if (_editing && _editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is { } presenter)
            return Math.Clamp(presenter.TextLayout.HitTestPoint(e.GetPosition(presenter)).TextPosition, 0, ModelText.Length);
        var offset = _preview is null
            ? MarkdownTextRunParser.GetSourceOffset(ModelText, _richText.GetTextOffset(e.GetPosition(_richText)))
            : ModelText.Length;
        if (_preview is not null)
        {
            var text = _preview.GetVisualDescendants().OfType<SelectableTextBlock>()
                .FirstOrDefault(block => !_isCode || block.Classes.Contains("MdCodeBlockText"));
            if (text?.TextLayout is { } layout)
            {
                var visible = layout.HitTestPoint(e.GetPosition(text)).TextPosition;
                offset = _isCode || Model.Kind == MarkdownBlockKind.Raw ? visible
                    : MarkdownTextRunParser.GetSourceOffset(ModelText, visible);
            }
        }
        return Math.Clamp(offset, 0, ModelText.Length);
    }

    internal void SetTextSelection(int start, int end)
    {
        start = Math.Clamp(start, 0, ModelText.Length); end = Math.Clamp(end, 0, ModelText.Length);
        if (_editing)
        {
            _editor.CaretIndex = end; _editor.SelectionStart = start; _editor.SelectionEnd = end;
            return;
        }
        if (_preview is null) return;
        var text = _preview.GetVisualDescendants().OfType<SelectableTextBlock>()
            .FirstOrDefault(block => !_isCode || block.Classes.Contains("MdCodeBlockText"));
        if (text is null) return;
        text.SelectionStart = _isCode || Model.Kind == MarkdownBlockKind.Raw ? start : MarkdownTextRunParser.GetTextOffset(ModelText, start);
        text.SelectionEnd = _isCode || Model.Kind == MarkdownBlockKind.Raw ? end : MarkdownTextRunParser.GetTextOffset(ModelText, end);
    }

    internal Rect? GetTextBounds(int offset)
    {
        Visual? text;
        global::Avalonia.Media.TextFormatting.TextLayout? layout;
        int textLength;
        int visibleOffset;
        if (_editing)
        {
            if (_editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is not { } presenter) return null;
            text = presenter;
            layout = presenter.TextLayout;
            textLength = _editor.Text?.Length ?? 0;
            visibleOffset = offset;
        }
        else
        {
            var previewText = _preview?.GetVisualDescendants().OfType<SelectableTextBlock>()
                .FirstOrDefault(block => !_isCode || block.Classes.Contains("MdCodeBlockText"));
            if (previewText is null) return null;
            text = previewText;
            layout = previewText.TextLayout;
            textLength = previewText.Text?.Length ?? 0;
            visibleOffset = _isCode || Model.Kind == MarkdownBlockKind.Raw
                ? offset
                : MarkdownTextRunParser.GetTextOffset(ModelText, offset);
        }

        var position = layout.HitTestTextPosition(Math.Clamp(visibleOffset, 0, textLength));
        return text.TranslatePoint(position.TopLeft, this) is { } origin ? new Rect(origin, position.Size) : null;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_selectionPointer != e.Pointer || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var offset = GetPointerTextOffset(e);
        _editor.CaretIndex = offset;
        _editor.SelectionStart = _pointerSelectionAnchor;
        _editor.SelectionEnd = offset;
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_selectionPointer != e.Pointer) return;
        _selectionPointer = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        // 中文输入法激活时，回车/退格等按键以 Key.ImeProcessed 形态到达（真机实测），
        // 所有 case Key.Enter 全部不匹配——回车/边界导航/合并静默失效。用 PhysicalKey 归一化。
        var key = e.Key == Key.ImeProcessed
            ? e.PhysicalKey switch
            {
                PhysicalKey.Enter => Key.Enter,
                PhysicalKey.Backspace => Key.Back,
                PhysicalKey.Delete => Key.Delete,
                PhysicalKey.Escape => Key.Escape,
                PhysicalKey.Tab => Key.Tab,
                PhysicalKey.ArrowLeft => Key.Left,
                PhysicalKey.ArrowRight => Key.Right,
                PhysicalKey.ArrowUp => Key.Up,
                PhysicalKey.ArrowDown => Key.Down,
                _ => e.Key
            }
            : e.Key;
        CodeWF.Toolkit.Logging.Logger.Debug("block keydown " + e.Key + " -> " + key);
        if (e.KeyModifiers == KeyModifiers.None && _editor.SelectionStart == _editor.SelectionEnd)
        {
            var caret = _editor.CaretIndex;
            var length = CurrentText.Length;
            var direction = key is Key.Left or Key.Up or Key.Back ? -1 : 1;
            if (key is Key.Left or Key.Right && (direction < 0 ? caret == 0 : caret == length)
                && BoundaryNavigateRequested?.Invoke(direction, null) == true) { e.Handled = true; return; }
            if (key is Key.Up or Key.Down && _editor.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault() is { } presenter)
            {
                var rect = presenter.TextLayout.HitTestTextPosition(caret);
                if ((direction < 0 ? rect.Top < rect.Height : rect.Bottom >= presenter.TextLayout.Height - .5)
                    && BoundaryNavigateRequested?.Invoke(direction, rect.X) == true) { e.Handled = true; return; }
            }
            if (key is Key.Back or Key.Delete && (direction < 0 ? caret == 0 : caret == length)
                && MergeRequested?.Invoke(direction) == true) { e.Handled = true; return; }
        }
        switch (key)
        {
            case Key.Escape:
                CancelEdit();
                e.Handled = true;
                break;

            case Key.Enter when e.KeyModifiers == KeyModifiers.Control:
                CommitAndRender(); e.Handled = true; break;

            case Key.Enter when !_isCode && !HasImageSyntax && Model.Kind != MarkdownBlockKind.Raw && e.KeyModifiers == KeyModifiers.None:
                // Typora 语义：回车=在光标处拆出新段落（焦点进新块），空段落可连续存在
                // （末尾连敲即持续加空行）。有选区时先删选区再拆。
                e.Handled = true;
                var enterText = _editor.Text ?? string.Empty;
                var enterStart = Math.Clamp(Math.Min(_editor.SelectionStart, _editor.SelectionEnd), 0, enterText.Length);
                var enterEnd = Math.Clamp(Math.Max(_editor.SelectionStart, _editor.SelectionEnd), enterStart, enterText.Length);
                if (enterEnd > enterStart)
                {
                    _editor.Text = enterText.Remove(enterStart, enterEnd - enterStart);
                    _editor.CaretIndex = enterStart;
                }
                CommitAndRender();
                SplitRequested?.Invoke(this, _editor.CaretIndex);
                break;

            case Key.Enter when HasImageSyntax && e.KeyModifiers == KeyModifiers.None:
                // 图片源码一行为主：回车即提交，不走段内换行/拆块。
                CommitAndRender(); e.Handled = true; break;

            case Key.Enter when e.KeyModifiers == KeyModifiers.Shift:
                var start = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
                var length = Math.Abs(_editor.SelectionEnd - _editor.SelectionStart);
                _editor.Text = CurrentText.Remove(start, length).Insert(start, _isCode ? "\n" : "  \n");
                _editor.CaretIndex = start + (_isCode ? 1 : 3);
                e.Handled = true;
                break;

            case Key.Tab:
                e.Handled = true;
                // 列表项：Tab/Shift+Tab 缩进/降级（muya 键盘矩阵）；代码块：插入/退一格
                //（选中多行整段缩进）；其余块保持提交+移焦点。
                if (Model.Kind == MarkdownBlockKind.ListItem)
                {
                    IndentRequested?.Invoke(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                    break;
                }
                if (_isCode)
                {
                    var unit = "    ";
                    var codeStart = Math.Min(_editor.SelectionStart, _editor.SelectionEnd);
                    var codeEnd = Math.Max(_editor.SelectionStart, _editor.SelectionEnd);
                    var codeText = _editor.Text ?? string.Empty;
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    {
                        // 退格：行首四空格（选中多行则逐行）删除一层。
                        var lineBegin = codeText.LastIndexOf('\n', Math.Max(0, codeStart - 1)) + 1;
                        var blockStart = codeText.IndexOf('\n', codeEnd) is { } stop && stop >= 0 ? stop : codeText.Length;
                        var segment = codeText[lineBegin..blockStart];
                        var replaced = segment.Replace("\n    ", "\n");
                        replaced = replaced.Length >= 4 && replaced.StartsWith("    ")
                            ? replaced[4..]
                            : replaced.TrimStart(' ').Length < segment.TrimStart(' ').Length ? replaced.TrimStart(' ') : replaced;
                        if (replaced == segment) break;
                        _editor.Text = codeText[..lineBegin] + replaced + codeText[blockStart..];
                        _editor.CaretIndex = codeStart;
                        _editor.SelectionStart = _editor.SelectionEnd = codeStart;
                        break;
                    }
                    var selection = codeEnd - codeStart;
                    if (selection == 0)
                    {
                        _editor.Text = codeText[..codeStart] + unit + codeText[codeStart..];
                        _editor.CaretIndex = codeStart + unit.Length;
                        _editor.SelectionStart = _editor.SelectionEnd = _editor.CaretIndex;
                        break;
                    }
                    // 选中区每行行首插一层缩进（含选中首行）；选区平移覆盖整段缩进文本。
                    var selBegin = codeText.LastIndexOf('\n', Math.Max(0, codeStart - 1)) + 1;
                    var indented = codeText[selBegin..codeEnd].Replace("\n", "\n" + unit);
                    _editor.Text = codeText[..selBegin] + unit + indented + codeText[codeEnd..];
                    var addedUnits = 1 + (indented.Length - (codeEnd - selBegin)) / unit.Length;
                    _editor.CaretIndex = codeStart + unit.Length;
                    _editor.SelectionStart = codeStart + unit.Length;
                    _editor.SelectionEnd = codeEnd + addedUnits * unit.Length;
                    break;
                }
                CommitAndRender();
                NavigateRequested?.Invoke(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                break;
        }
    }

    private void UpdateRichText()
    {
        if (_isCode) EnsureSourceInput();
        if (_editing && !_inlineEditing && !IsSourceOverlay) return;
        if (IsDiagram)
        {
            _editor.Foreground = _options.TextBrush;
            _editor.Background = _options.CodeBackgroundBrush;
            _editor.BorderBrush = _options.SeparatorBrush;
        }
        var useViewer = !_isTableCell || ModelText.Contains("![", StringComparison.Ordinal) || ModelText.Contains('$');
        if (useViewer)
        {
            // 纯文本 inline 编辑中冻结预览：编辑框已覆盖显示，逐键击重解析预览
            // （如输入 ">" 会被写成 ">\n" 立即渲染成空引用框）既闪假象又拖慢输入；
            // 预览只服务失焦后的整块渲染。代码块前景透明透出高亮，必须继续刷新。
            if (_editing && _inlineEditing && !_isCode) return;
            var preview = _preview ??= CreatePreview();
            preview.Classes.Set("MdLiveListContent", Model.Kind == MarkdownBlockKind.ListItem);
            preview.Classes.Set("MdBeforeHeading", Classes.Contains("MdBeforeHeading"));

            preview.SyntaxOptions = _options.SyntaxOptions;
            preview.ImageBasePath = _options.ImageBasePath;
            preview.TypographyTheme = _options.TypographyTheme;
            preview.TypographySize = _options.TypographySize;
            preview.ContentFontFamily = _options.ContentFontFamily;
            preview.ParagraphFontSize = _isTableCell ? _runFontSize : _options.FontSize;
            preview.CodeBlockFontSize = _options.CodeFontSize;
            preview.CodeBlockWrap = _options.CodeWrap;
            preview.Markdown = Model.Kind == MarkdownBlockKind.ListItem || RenderQuoteAsParagraph
                ? Model.Text : MarkdownBlockParser.WriteCanonical([Model]);
            SetDisplay(preview);
            return;
        }

        _richText.BindPalette(_options);
        _richText.FontWeight = Model.HeadingLevel > 0 ? FontWeight.Bold : FontWeight.Normal;
        _richText.RunFontSize = _runFontSize;
        _richText.SetRuns(_isCode
            ? MarkdownTextRunParser.Plain(Model.Text, code: true)
            : MarkdownTextRunParser.Parse(Model.Text, _options.SyntaxOptions));
        SetDisplay(_richText);
    }

    private void SetDisplay(Control display)
    {
        if (!_layer.Children.Contains(display)) { _layer.Children.Clear(); _layer.Children.Add(display); _layer.Children.Add(_editingLayer); }
        Grid.SetRow(display, IsSourceOverlay ? 1 : 0);
        if (IsSourceOverlay)
        {
            if (_editing && !_layer.Children.Contains(_editor))
            {
                _editingLayer.Children.Remove(_editor);
                _layer.Children.Add(_editor);
            }
        }
        if (!ReferenceEquals(_root.Child, _layer)) _root.Child = _layer;
    }
    private void PositionInlineEditor()
    {
        var text = _preview?.GetVisualDescendants().OfType<SelectableTextBlock>()
            .FirstOrDefault(block => !_isCode || block.Classes.Contains("MdCodeBlockText"));
        if (text is not null && text.TranslatePoint(default, _layer) is { } point)
        {
            if (_editingText is not null && !ReferenceEquals(_editingText, text)) _editingText.Opacity = 1;
            _editingText = text; text.Opacity = _isCode ? 1 : 0;
            // 代码沿用预览的语法高亮；选区只由上方的输入层绘制，避免遗留只读选区。
            if (_isCode) text.SelectionStart = text.SelectionEnd = 0;
            Canvas.SetLeft(_editor, point.X); Canvas.SetTop(_editor, point.Y); _editor.Width = Math.Max(1, text.Bounds.Width);
            _editor.FontSize = text.FontSize; _editor.FontWeight = text.FontWeight; _editor.FontFamily = text.FontFamily;
            _editor.LineHeight = text.LineHeight; _editor.Foreground = _isCode ? Brushes.Transparent : text.Foreground;
            _editor.TextWrapping = _isCode ? TextWrapping.NoWrap : TextWrapping.Wrap;
            if (_isCode && text.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { } codeScroll)
            {
                // 输入层位于预览上方，只能覆盖代码的可见文字范围，不能挡住滚动条。
                var clip = new Rect(text.Bounds.Size);
                if (codeScroll.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault() is { } viewport
                    && viewport.TranslatePoint(default, text) is { } viewportOrigin)
                    clip = clip.Intersect(new Rect(viewportOrigin, viewport.Bounds.Size));
                foreach (var bar in codeScroll.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ScrollBar>().Where(bar => bar.IsVisible))
                {
                    if (bar.TranslatePoint(default, text) is not { } origin) continue;
                    if (bar.Orientation == Orientation.Horizontal)
                        clip = clip.Intersect(new Rect(clip.X, clip.Y, clip.Width, Math.Max(0, origin.Y - clip.Y)));
                }
                _editor.Clip = new RectangleGeometry(clip);
            }
            if (_codeLanguage is not null && text.GetVisualAncestors().OfType<Border>().FirstOrDefault(border => border.Classes.Contains("MdCodeBlock")) is { } frame
                && frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), _layer) is { } bottom)
            {
                Canvas.SetLeft(_codeLanguage, Math.Max(0, bottom.X - _codeLanguage.Width));
                Canvas.SetTop(_codeLanguage, bottom.Y + 3);
            }
        }
        else
        {
            // 预览无可用文字几何（空文档 Viewer 零段落，或无预览）：Canvas 上未定位且
            // 空文本期望宽为 0，编辑框不可见——光标"消失"。回退到块顶原点 + 面板宽。
            _richText.Opacity = 0;
            _editor.Width = Math.Max(1, _layer.Bounds.Width > 1 ? _layer.Bounds.Width : _richText.Bounds.Width);
            _editor.FontSize = _richText.FontSize;
            _editor.FontWeight = _richText.FontWeight;
            Canvas.SetLeft(_editor, 0);
            Canvas.SetTop(_editor, 0);
            _editingLayer.IsVisible = true;
        }
    }
    private void EndInlineEditor()
    {
        StopWatchingDiagramPointer();
        _layer.Children.Remove(_editor);
        _editor.Clip = null;
        _inlineEditing = false; _editingLayer.IsVisible = false; _richText.Opacity = 1;
        if (ReferenceEquals(_root.Child, _editor)) _root.Child = null;
        if (!_editingLayer.Children.Contains(_editor)) _editingLayer.Children.Add(_editor);
        if (_editingText is not null) _editingText.Opacity = 1;
        _editingText = null;
    }

    // 即时编辑保持「底部语言输入框」的既有约定（测试 CodeLanguageAppearsBelow... 守住）；
    // 头部与输入框并存属冗余，整合进头部可编辑留后续版本。
    private MarkdownViewer CreatePreview() => new()
    {
        DocumentBottomPadding = 0,
        IsTaskEditingEnabled = false,
        ShowCodeBlockHeader = false,
        CodeFontFamily = _options.CodeFontFamily,
        IsDefaultContextMenuEnabled = _options.IsDefaultContextMenuEnabled,
        // 块内的图表/图片编辑由本视图（上方源码框）承担，内嵌预览保持纯渲染：
        // 关闭阅读态放大（IsMediaZoomEnabled）避免点击预览弹遮罩对话框抢占块编辑入口。
        IsDiagramEditingEnabled = false,
        IsMediaZoomEnabled = false
    };
}
