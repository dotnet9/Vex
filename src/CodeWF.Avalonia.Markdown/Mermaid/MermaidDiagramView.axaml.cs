using System.ComponentModel;
using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Rendering;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Mermaid;

internal partial class MermaidDiagramView : UserControl
{
    private MermaidDiagramViewModel _state;
    private readonly MarkdownViewer _viewer;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private TopLevel? _topLevel;
    private readonly string? _imageBasePath;
    private bool _waitingForModel;

    public MermaidDiagramView(MarkdownViewer viewer, MermaidDiagramViewModel state, Control preview, IMarkdownRenderContext context)
    {
        InitializeComponent();
        _viewer = viewer;
        _imageBasePath = viewer.ImageBasePath;
        _state = state;
        DataContext = state;
        PreviewContent.Content = preview;
        context.BindTheme(SourceInput, TextBox.ForegroundProperty, MarkdownViewer.ForegroundProperty);
        context.BindTheme(SourceInput, TextBox.BackgroundProperty, MarkdownViewer.CodeBackgroundBrushProperty);
        context.BindTheme(SourceInput, TextBox.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty);
        context.BindTheme(SourceInput, TextBox.FontFamilyProperty, MarkdownViewer.CodeFontFamilyProperty);
        context.BindTheme(SourceBadge, Button.BackgroundProperty, MarkdownViewer.BackgroundProperty);
        context.BindTheme(SourceBadge, Button.BorderBrushProperty, MarkdownViewer.BorderLineBrushProperty);
        context.BindTheme(SourceBadge, Button.ForegroundProperty, MarkdownViewer.ForegroundProperty);
        // 阅读视图（IsDiagramEditingEnabled=false）：徽标隐藏；图表保持可命中，
        // 点击语义变为「放大查看」（纯只读遮罩对话框，与 Typora 一致），光标保持手型暗示可点。
        if (!viewer.IsDiagramEditingEnabled)
        {
            SourceBadge.IsVisible = false;
        }
        PreviewButton.Click += (_, args) => { ActivatePreview(); args.Handled = true; };
        SourceBadge.Click += (_, args) => { BeginEdit(); args.Handled = true; };
        PreviewButton.AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        SourceBadge.AddHandler(PointerPressedEvent, OnPreviewPointerPressed, RoutingStrategies.Tunnel);
        SourceInput.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape && !(args.Key == Key.Enter && args.KeyModifiers.HasFlag(KeyModifiers.Control))) return;
            Commit(); args.Handled = true; PreviewButton.Focus();
        };
        SourceInput.LostFocus += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (!IsKeyboardFocusWithin) Commit();
        });
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            if (_state.IsEditing) PreviewContent.Content = new MarkdownViewer
            {
                Markdown = _state.PreviewMarkdown,
                TypographyTheme = _viewer.TypographyTheme,
                TypographySize = _viewer.TypographySize,
            };
        };
        _state.PropertyChanged += OnStateChanged;
        DetachedFromVisualTree += (_, _) => Commit();
    }

    private void OnPreviewPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // 上一张源码收起后按钮会移位，不能等原坐标上的 MouseUp 再激活。
        args.Handled = true;
        ActivatePreview();
    }

    /// <summary>
    /// 预览点击语义分派：编辑态展开图上方源码编辑；纯阅读弹出放大查看对话框
    /// （纯只读遮罩层，与块级图片的阅读态一致）。等待模型续接的 BeginEdit 内部
    /// 仍保留阅读态闸门，任何迟到路径都不会误入编辑。
    /// </summary>
    private void ActivatePreview()
    {
        if (!_viewer.IsDiagramEditingEnabled)
        {
            OpenZoomDialog();
            return;
        }

        BeginEdit();
    }

    /// <summary>已打开的放大对话框（防重复弹出）；随对话框关闭清空。</summary>
    private MarkdownMediaZoomDialog? _zoomDialog;

    private void OpenZoomDialog()
    {
        if (_zoomDialog is not null) return;
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;
        // 对话框内用等价渲染管线（CreateMermaidBlock：无徽标、无源码框、主题变化自动重渲）
        // 只读重画同一份 mermaid 源，Viewbox 让整图适配 ~80% 区域（SVG 无级缩放）。
        var content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = MermaidBlockRenderer.CreateMermaidBlock(_state.Source, _viewer)
        };
        _zoomDialog = MarkdownMediaZoomDialog.Show(topLevel, content, _viewer, detachWatch: this);
        if (_zoomDialog is not null) _zoomDialog.Dismissed += () => _zoomDialog = null;
    }

    private void BeginEdit()
    {
        // 阅读视图禁用图表编辑：任何入口（点击、徽标、等待模型后的续接）都在这里挡下。
        if (!_viewer.IsDiagramEditingEnabled) return;
        if (_state.IsEditing) { SourceInput.Focus(); return; }
        // 点另一张图会先提交上一张；等异步正文模型落地后继续这次点击。
        if (_viewer.CurrentModel.Source != _viewer.Markdown)
        {
            if (_waitingForModel) return;
            _waitingForModel = true;
            _viewer.LayoutUpdated += OnModelLayoutUpdated;
            WatchOutsidePointer();
            return;
        }
        StopWaitingForModel();
        if (ResolveState() is not { } state) return;
        _state.PropertyChanged -= OnStateChanged;
        DataContext = _state = state;
        _state.PropertyChanged += OnStateChanged;
        _state.IsEditing = true;
        _viewer.PropertyChanged += OnViewerChanged;
        WatchOutsidePointer();
        SourceInput.Focus();
    }

    private void WatchOutsidePointer()
    {
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
        _topLevel?.AddHandler(PointerPressedEvent, OnOutsidePointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnModelLayoutUpdated(object? sender, EventArgs args)
    {
        if (_viewer.CurrentModel.Source == _viewer.Markdown) BeginEdit();
    }

    private void StopWaitingForModel()
    {
        _waitingForModel = false;
        _viewer.LayoutUpdated -= OnModelLayoutUpdated;
        _topLevel?.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
        _topLevel = null;
    }

    private MermaidDiagramViewModel? ResolveState()
    {
        if (_viewer.CurrentModel.Source != _viewer.Markdown) return null;
        var host = this.GetVisualAncestors().OfType<WindowedStackPanel>().FirstOrDefault();
        if (host is null) return null;
        var ancestors = this.GetVisualAncestors().ToHashSet();
        var blocks = _viewer.CurrentModel.Blocks;
        for (var index = 0; index < Math.Min(blocks.Count, host.ItemCount); index++)
        {
            if (host.GetRealizedItem(index) is not { } realized || !ancestors.Contains(realized)) continue;
            var syntax = blocks[index].SyntaxBlock;
            var fences = syntax is FencedCodeBlock fenced ? new[] { fenced }
                : syntax.Descendants<FencedCodeBlock>().ToArray();
            var ordinal = realized.GetVisualDescendants().OfType<MermaidDiagramView>()
                .Where(view => ReferenceEquals(view._viewer, _viewer)).TakeWhile(view => !ReferenceEquals(view, this)).Count();
            var target = fences.Where(fence => string.Equals(fence.Info?.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase))
                .ElementAtOrDefault(ordinal);
            return target is null ? null : new(_viewer.Markdown!, target, Math.Max(0, blocks[index].SourceSpan.Start - syntax.Span.Start));
        }
        return null;
    }

    private void OnViewerChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == MarkdownViewer.MarkdownProperty || args.Property == MarkdownViewer.ImageBasePathProperty) Commit();
    }

    private void OnOutsidePointerPressed(object? sender, PointerPressedEventArgs args)
    {
        // 从顶层监听，工具条/侧栏以及已经处理过指针的控件也能结束图表编辑。
        if (args.Source is Visual visual && (ReferenceEquals(visual, this) || visual.GetVisualAncestors().Contains(this))) return;
        Commit();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(MermaidDiagramViewModel.Source)) return;
        _previewTimer.Stop();
        if (_state.IsEditing) _previewTimer.Start();
    }

    internal void Commit()
    {
        if (_waitingForModel) StopWaitingForModel();
        if (!_state.IsEditing) return;
        _state.IsEditing = false;
        _viewer.PropertyChanged -= OnViewerChanged;
        _previewTimer.Stop();
        _topLevel?.RemoveHandler(PointerPressedEvent, OnOutsidePointerPressed);
        _topLevel = null;
        if (_viewer.Markdown == _state.OriginalMarkdown && _viewer.ImageBasePath == _imageBasePath && _state.CreateEdit() is { } edit)
            _viewer.RaiseEvent(edit);
    }
}
