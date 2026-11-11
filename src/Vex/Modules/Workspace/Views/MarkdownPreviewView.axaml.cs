using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using Vex.Modules.Workspace.ViewModels;

using Microsoft.Extensions.DependencyInjection;
namespace Vex.Modules.Workspace.Views;

public partial class MarkdownPreviewView : UserControl
{
    public static readonly StyledProperty<bool> IsReadingModeProperty = AvaloniaProperty.Register<MarkdownPreviewView, bool>(nameof(IsReadingMode));
    public bool IsReadingMode { get => GetValue(IsReadingModeProperty); set => SetValue(IsReadingModeProperty, value); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsReadingModeProperty && PreviewMarkdownViewer is not null)
        {
            PaneHeader.IsVisible = false;
            PreviewMarkdownViewer.Classes.Set("reading-flow", IsReadingMode);
            ApplyDiagramEditingAffordance();
            ApplyDocumentGeometry();
        }
        else if (change.Property == IsVisibleProperty && IsVisible && _viewModel is not null)
        {
            // 经即时等隐藏布局回来时正文往往原样复用（Markdown 未变就不重绘）：
            // 阅读期换下的纯图不能滞留在编辑对照里，按当前形态补一次换装。
            ApplyDiagramEditingAffordance();
        }
    }
    private MarkdownPreviewViewModel? _viewModel;
    private MarkdownViewer.MarkdownReadingPosition? _savedReadingPosition;
    private MarkdownViewer.MarkdownReadingPosition? _presentationReadingPosition;
    private string? _presentationReadingSource;
    private bool _restoringScrollPosition;
    private int _appliedRefreshVersion;
    private int _appliedNavigationSequence;
    private long _lastReadingPositionSampleTick;
    private bool _restoreQueued;
    private string? _restoreSource;
    private int _scrollRequestVersion;
    private int _restoreAttempts;
    private bool _restoreSettleQueued;
    private const int MaxRestoreAttempts = 24;
    private TopLevel? _displayHost;
    private IDisposable? _outlineNavigation;
    private IDisposable? _readingSample;
    private void ApplyDocumentGeometry()
    {
        var presenting = _viewModel?.IsPresenting == true;
        ApplyPresentationTypography(presenting);
        PreviewScrollViewer.Padding = presenting ? default : new Thickness(0, 0, 10, 0);
        // 原型 .md-body 的 zoom 是整篇均匀缩放：字号、行高、块距、表格与代码宽度同比放大，
        // 可用宽度按缩放回算，所以倍率越大每行越早换行、文档整体越高。只放大字号会留下
        // 库内绝对行高与边距，段落就会排在旧高度里被截断。
        var scale = Math.Max(.1, _viewModel?.DocumentScale ?? 1);
        DocumentTransform.LayoutTransform = scale == 1 ? null : new Avalonia.Media.ScaleTransform(scale, scale);
        DocumentTransform.Margin = default;
        if (presenting) PreviewMarkdownViewer.Width = Math.Max(100, Math.Min(1120, PreviewScrollViewer.Bounds.Width) - 104);
        var clientWidth = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? 1280;
        // LayoutTransformControl 按缩放后的可用宽度量测子级。
        DocumentTransform.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        DocumentTransform.MaxWidth = double.PositiveInfinity;
        if (presenting)
        {
            PreviewMarkdownViewer.MaxWidth = 1016;
            PreviewMarkdownViewer.Margin = new Thickness(52, 32, 52, 64);
        }
        else
        {
            var availableWidth = Math.Max(0, PreviewScrollViewer.Viewport.Width) / scale;
            var margin = IsReadingMode
                ? clientWidth <= 900 ? new Thickness(24, 20, 24, 60)
                    : clientWidth <= 1100 ? new Thickness(36, 24, 36, 70) : new Thickness(52, 30, 52, 88)
                : new Thickness(Math.Clamp(availableWidth * .08, 14, 32), 24,
                    Math.Clamp(availableWidth * .08, 14, 32), 56);
            PreviewMarkdownViewer.Margin = margin;
            // 单阅读正文随所在显示器扩展；组合预览继续使用原有的含留白 820px 栏宽。
            PreviewMarkdownViewer.MaxWidth = IsReadingMode
                ? _viewModel?.MaximumReadingWidth ?? 960 / scale
                : 820 - margin.Left - margin.Right;
            // 居中的变换容器会按短文本收缩；阅读列宽只由视口与缩放决定。
            var available = Math.Max(0, PreviewScrollViewer.Viewport.Width
                - PreviewScrollViewer.Padding.Left - PreviewScrollViewer.Padding.Right) / scale;
            PreviewMarkdownViewer.Width = Math.Max(0, Math.Min(PreviewMarkdownViewer.MaxWidth, available - margin.Left - margin.Right));
        }
    }

    public MarkdownPreviewView()
    {
        InitializeComponent();
        PreviewMarkdownViewer.ActualThemeVariantChanged += (_, _) =>
            ApplyPresentationTypography(_viewModel?.IsPresenting == true);
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<MarkdownPreviewViewModel>();
        // 还原钮没有可绑的命令属性类型差异，直接走控件事件；坞实例随模板固定，挂一次即可。
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += (_, _) => StopWatchingDisplay();
        DetachedFromVisualTree += (_, _) => SetViewModel(null);
        PreviewScrollViewer.ScrollChanged += OnPreviewScrollChanged;
        PreviewScrollViewer.SizeChanged += (_, _) => ApplyDocumentGeometry();
        DetachedFromVisualTree += (_, _) => PreviewMarkdownViewer.TaskWriteBackRequested -= OnTaskWriteBackRequested;
        // 大纲跳转不经过预览视图模型，直接订阅总线，避免被「属性值未变」过滤掉。
        DetachedFromVisualTree += (_, _) =>
        {
            EventBus.Default.Unsubscribe(this);
            _outlineNavigation?.Dispose(); _outlineNavigation = null;
            _readingSample?.Dispose(); _readingSample = null;
            StopReadingRestore();
            _scrollRequestVersion++;
        };
        SetViewModel(DataContext as MarkdownPreviewViewModel);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SetViewModel(DataContext as MarkdownPreviewViewModel);
        StopWatchingDisplay();
        _displayHost = TopLevel.GetTopLevel(this);
        if (_displayHost is not null)
        {
            if (_displayHost.Screens is { } screens) screens.Changed += OnDisplayMetricsChanged;
            _displayHost.ScalingChanged += OnDisplayMetricsChanged;
            if (_displayHost is Window window) window.PositionChanged += OnDisplayMetricsChanged;
        }
        OnDisplayMetricsChanged(this, EventArgs.Empty);
        EventBus.Default.Subscribe(this);
        PreviewMarkdownViewer.TaskWriteBackRequested -= OnTaskWriteBackRequested;
        PreviewMarkdownViewer.TaskWriteBackRequested += OnTaskWriteBackRequested;
        // 阅读位置由库统一按「块序号 + 块内比例」采集与恢复，这里只需把滚动宿主交给 Viewer。
        PreviewMarkdownViewer.ScrollHost = PreviewScrollViewer;
    }

    private void OnDisplayMetricsChanged(object? sender, EventArgs args)
    {
        if (_displayHost?.Screens?.ScreenFromVisual(this) is { } screen)
            _viewModel?.UpdateDisplayMetrics(screen.Bounds.Width, screen.Scaling);
        ApplyDocumentGeometry();
    }

    private void StopWatchingDisplay()
    {
        if (_displayHost is null) return;
        if (_displayHost.Screens is { } screens) screens.Changed -= OnDisplayMetricsChanged;
        _displayHost.ScalingChanged -= OnDisplayMetricsChanged;
        if (_displayHost is Window window) window.PositionChanged -= OnDisplayMetricsChanged;
        _displayHost = null;
    }

    // 预览里点击任务勾选框：库返回新 Markdown 与变更区间，交给编辑器按区间回写。
    private static void OnTaskWriteBackRequested(object? sender, CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownTaskWriteResult result)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Core.Messaging.MarkdownTaskWriteBackCommand(
            result.Markdown,
            result.ChangedSpan.Start,
            result.ChangedSpan.Length));
    }

    private void OnDiagramEditCommitted(object? sender, CodeWF.Avalonia.Markdown.Mermaid.MermaidEditEventArgs args)
    {
        if (_viewModel?.Markdown != args.OriginalMarkdown) return;
        _savedReadingPosition = PreviewMarkdownViewer.SaveReadingPosition();
        QueueRestoreReadingPosition(args.Markdown);
        _viewModel.CommitDiagramEdit(args.OriginalMarkdown, args.Markdown, args.Start);
    }

    // 阅读与编辑对照共用这一个预览实例：IsReadingMode 翻转时库内两个图表开关
    // （MermaidEditing 渲染包装 + IsDiagramEditingEnabled 点击闸）由绑定一起换档，
    // 但已渲染的图表块不会自行重建，这里补一次整篇重绘让编辑包装换上/换下。
    // 只在窗格在场时做（经即时等隐藏布局回到编辑对照由 OnIsVisibleChanged 补一次），
    // 演示态有自己的锚点链路且入退场都整篇换文档，也不在这里插手。
    private void ApplyDiagramEditingAffordance()
    {
        if (_viewModel?.IsPresenting == true || _viewModel?.IsPreviewVisible != true) return;
        // 切走前先把仍持有焦点的图表源码按保存/关闭同一条链路提交，重绘才不会丢掉未提交的编辑。
        CodeWF.Avalonia.Markdown.Mermaid.MermaidEditing.CommitPendingEdits(PreviewMarkdownViewer);
        PreviewMarkdownViewer.Rerender();
        QueueRestoreReadingPosition();
    }

    public void FlushDiagramEdits() => CodeWF.Avalonia.Markdown.Mermaid.MermaidEditing.CommitPendingEdits(PreviewMarkdownViewer);

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_restoringScrollPosition || _outlineNavigation is not null || _viewModel?.IsPresenting == true)
        {
            return;
        }

        // 尾采样不能被前一次节流丢掉，否则停止滚动后高亮会留在上一章。
        _readingSample ??= DispatcherTimer.RunOnce(() =>
        {
            _readingSample = null;
            if (!IsEffectivelyVisible || _restoringScrollPosition || _outlineNavigation is not null
                || _viewModel?.IsPresenting == true) return;
            for (var y = 16d; y <= Math.Min(PreviewScrollViewer.Viewport.Height, 128); y += 16)
            {
                if (PreviewScrollViewer.TranslatePoint(new Point(PreviewScrollViewer.Viewport.Width / 2, y), PreviewMarkdownViewer) is { } probe
                    && PreviewMarkdownViewer.TryGetBlockAt(probe, out var hit))
                {
                    EventBus.Default.Publish(new MarkdownReadingPositionChangedCommand(hit.SourceLine));
                    return;
                }
            }
            if (PreviewMarkdownViewer.SaveReadingPosition() is { } anchor
                && anchor.BlockIndex < PreviewMarkdownViewer.CurrentModel.Blocks.Count)
                EventBus.Default.Publish(new MarkdownReadingPositionChangedCommand(
                    PreviewMarkdownViewer.CurrentModel.Blocks[anchor.BlockIndex].SyntaxBlock.Line + 1));
        }, TimeSpan.FromMilliseconds(80));

        // 采样有节流：长文档下 SaveReadingPosition 需要遍历已渲染块。
        var now = Environment.TickCount64;
        if (now - _lastReadingPositionSampleTick < 200)
        {
            return;
        }

        _lastReadingPositionSampleTick = now;
        if (PreviewMarkdownViewer.SaveReadingPosition() is { } position)
        {
            _savedReadingPosition = position;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        SetViewModel(DataContext as MarkdownPreviewViewModel);
    }

    private void SetViewModel(MarkdownPreviewViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyRemoteImageRefresh(_viewModel.RemoteImageRefreshVersion);
            QueueScrollToEditorPosition();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MarkdownPreviewViewModel.MaximumReadingWidth)) ApplyDocumentGeometry();
        if (e.PropertyName == nameof(MarkdownPreviewViewModel.Markdown) && _viewModel?.IsPresenting != true
            && _restoringScrollPosition && _restoreSource != _viewModel?.Markdown)
        {
            _restoringScrollPosition = false;
            _savedReadingPosition = null;
        }
        if (e.PropertyName == nameof(MarkdownPreviewViewModel.Markdown) && _viewModel?.IsPresenting == true)
        {
            var version = _scrollRequestVersion;
            Dispatcher.UIThread.Post(() =>
            {
                if (_viewModel?.IsPresenting == true && version == _scrollRequestVersion)
                    PreviewScrollViewer.Offset = default;
            }, DispatcherPriority.Loaded);
            return;
        }
        // 退出演示先恢复全文，再发 IsPresenting 通知；不能排入旧编辑光标定位覆盖阅读锚点。
        if (e.PropertyName == nameof(MarkdownPreviewViewModel.Markdown) && _presentationReadingPosition is not null)
            return;
        if (e.PropertyName is nameof(MarkdownPreviewViewModel.PreviewSourceLine)
            or nameof(MarkdownPreviewViewModel.PreviewScrollRatio)
            or nameof(MarkdownPreviewViewModel.Markdown))
        {
            QueueScrollToEditorPosition();
        }
        else if (e.PropertyName is nameof(MarkdownPreviewViewModel.RemoteImageRefreshVersion))
        {
            ApplyRemoteImageRefresh(_viewModel?.RemoteImageRefreshVersion ?? 0);
        }
        else if (e.PropertyName == nameof(MarkdownPreviewViewModel.IsPresenting))
        {
            _scrollRequestVersion++;
            if (_viewModel?.IsPresenting == true)
            {
                _presentationReadingPosition = PreviewMarkdownViewer.SaveReadingPosition();
                _presentationReadingSource = PreviewMarkdownViewer.CurrentModel.Source;
            }
            else
            {
                _savedReadingPosition = _viewModel?.Markdown == _presentationReadingSource ? _presentationReadingPosition : null;
                _presentationReadingPosition = null; _presentationReadingSource = null;
                // 退出演示的几何重排会同步刷一次滚动，采样器赶在恢复之前就把刚接住的锚点改写掉。
                // 先立恢复中的牌子：这段采样不写锚点，收尾仍由 RestoreReadingPosition 负责。
                if (_savedReadingPosition is not null) _restoringScrollPosition = true;
            }
            ApplyDocumentGeometry();
            if (_viewModel?.IsPresenting == true) { PreviewScrollViewer.Focus(); PreviewScrollViewer.Offset = default; }
            else QueueRestoreReadingPosition();
        }
        else if (e.PropertyName is nameof(MarkdownPreviewViewModel.PresentationZoom)
                 or nameof(MarkdownPreviewViewModel.TypographyTheme)
                 or nameof(MarkdownPreviewViewModel.TypographySize))
        {
            if (_viewModel?.IsPresenting == true)
            {
                var position = PreviewMarkdownViewer.SaveReadingPosition();
                ApplyDocumentGeometry();
                if (position is not null) Dispatcher.UIThread.Post(() =>
                {
                    if (_viewModel?.IsPresenting == true) PreviewMarkdownViewer.RestoreReadingPosition(position);
                }, DispatcherPriority.Loaded);
                return;
            }
            // 主题/排版切换会整篇重渲染并重置滚动，按块位置恢复切换前的阅读位置。
            QueueRestoreReadingPosition();
        }
        else if (e.PropertyName == nameof(MarkdownPreviewViewModel.DocumentScale) && _viewModel?.IsPresenting != true)
        {
            // 内容缩放改变整篇高度，视口中心会落到别的章节：先记住当前锚点，重排后回位。
            // 退出演示时缩放倍率与 IsPresenting 一起回落，而这一拍视口还停在演示几何里：
            // 重新采样会把刚接住的全文锚点换成重排后的落点，恢复就「原地达标」，正文停在开头。
            if (!_restoringScrollPosition && PreviewMarkdownViewer.SaveReadingPosition() is { } anchor) _savedReadingPosition = anchor;
            ApplyDocumentGeometry();
            QueueRestoreReadingPosition();
        }
    }

    private void ApplyRemoteImageRefresh(int version)
    {
        if (version <= _appliedRefreshVersion)
        {
            return;
        }

        _appliedRefreshVersion = version;
        // 库内失效远程图片缓存并重建图片块，宿主不再给 URL 追刷新参数。
        PreviewMarkdownViewer.RefreshRemoteImages();
    }

    /// <summary>
    /// 点击大纲跳转同步预览。重复点击同一项时选中值与光标都不会变化，
    /// 因此按事件序号判断是否为一次新的跳转，保证「点一次就到我这里」。
    /// </summary>
    [EventHandler]
    private void OnDocumentSwitched(MarkdownDocumentSwitchedCommand command)
    {
        StopReadingRestore();
        _outlineNavigation?.Dispose(); _outlineNavigation = null;
        _readingSample?.Dispose(); _readingSample = null;
        var version = ++_scrollRequestVersion;
        _savedReadingPosition = null;
        _lastReadingPositionSampleTick = 0;
        Dispatcher.UIThread.Post(() => { if (version == _scrollRequestVersion && _viewModel?.IsPresenting == false) PreviewScrollViewer.Offset = default; },
            DispatcherPriority.Loaded);
    }

    [EventHandler]
    private void OnNavigateToLine(NavigateToLineCommand command)
    {
        if (command.Sequence == _appliedNavigationSequence || _viewModel is null)
        {
            return;
        }

        _appliedNavigationSequence = command.Sequence;
        StopReadingRestore();
        _outlineNavigation?.Dispose();
        _readingSample?.Dispose(); _readingSample = null;
        _viewModel.PreviewSourceLine = command.Line;
        _viewModel.PreviewScrollRatio = 0d;
        var version = ++_scrollRequestVersion;
        var source = _viewModel.Markdown;
        var attempts = 0;
        var stable = 0;
        // 等模型及虚拟化估高收敛后再确认落点；新的点击或切文档立即取消旧请求。
        _outlineNavigation = DispatcherTimer.Run(() =>
        {
            if (version != _scrollRequestVersion || source != _viewModel?.Markdown
                || !IsEffectivelyVisible || _viewModel.IsPresenting || ++attempts > 100)
            {
                _outlineNavigation = null;
                return false;
            }
            if (PreviewMarkdownViewer.CurrentModel.Source == source
                && PreviewMarkdownViewer.TryGetSourceLineBounds(command.Line, out var bounds)
                && PreviewMarkdownViewer.TranslatePoint(new Point(0, bounds.Top), PreviewScrollViewer) is { } target)
            {
                var limit = Math.Max(0, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
                var offset = Math.Clamp(PreviewScrollViewer.Offset.Y + target.Y - 14, 0, limit);
                stable = Math.Abs(offset - PreviewScrollViewer.Offset.Y) < 1 ? stable + 1 : 0;
                PreviewScrollViewer.Offset = new Vector(PreviewScrollViewer.Offset.X, offset);
                if (stable >= 3)
                {
                    _savedReadingPosition = PreviewMarkdownViewer.SaveReadingPosition();
                    _outlineNavigation = null;
                    return false;
                }
            }
            return true;
        }, TimeSpan.FromMilliseconds(50));
    }

    private void QueueRestoreReadingPosition(string? expectedSource = null)
    {
        if (_savedReadingPosition is null)
        {
            return;
        }

        _restoringScrollPosition = true;
        _restoreAttempts = 0;
        _restoreSettleQueued = false;
        _restoreSource = expectedSource ?? _viewModel?.Markdown;
        _scrollRequestVersion++;
        LayoutUpdated -= OnRestoreLayoutUpdated;
        LayoutUpdated += OnRestoreLayoutUpdated;
        Dispatcher.UIThread.Post(RestoreReadingPosition, DispatcherPriority.Background);
    }

    private void OnRestoreLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_restoringScrollPosition) { LayoutUpdated -= OnRestoreLayoutUpdated; return; }
        if (_restoreQueued || PreviewMarkdownViewer.CurrentModel.Source != _restoreSource) return;
        _restoreQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            try { if (_restoringScrollPosition) RestoreReadingPosition(); }
            finally { _restoreQueued = false; }
        }, DispatcherPriority.Background);
    }

    private void RestoreReadingPosition()
    {
        if (_savedReadingPosition is not { } anchor)
        {
            StopReadingRestore();
            return;
        }

        // Markdown 绑定先更新，后台的全文模型稍后才到。不能把全文锚点恢复到
        // 仍在显示的演示短节上，否则库按短节块数夹取后就会被误认为已完成。
        if (PreviewMarkdownViewer.CurrentModel.Source != _restoreSource) return;
        if (ReadingAnchorLanded(anchor))
        {
            // 落锚不等于收尾：长文档的离屏块还按估高排布，随后的布局仍会把视口顶回前几节。
            // 等调度队列空一次（全文建完）再复验，期间的偏移由这条路径继续纠正。
            if (_restoreSettleQueued) return;
            _restoreSettleQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _restoreSettleQueued = false;
                if (!_restoringScrollPosition) return;
                if (_savedReadingPosition is { } held && !ReadingAnchorLanded(held)) RestoreReadingPosition();
                else StopReadingRestore();
            }, DispatcherPriority.ContextIdle);
            return;
        }

        // 偏移收敛后还没量测完的块会继续挪位，因此按块序号回读对不上就重新定位，次数封顶防死循环。
        if (++_restoreAttempts >= MaxRestoreAttempts)
        {
            StopReadingRestore();
            return;
        }
        PreviewMarkdownViewer.RestoreReadingPosition(anchor);
    }

    private void StopReadingRestore()
    {
        _restoringScrollPosition = false;
        LayoutUpdated -= OnRestoreLayoutUpdated;
    }

    // 同一段落内仍可能偏移；几何和虚拟化收敛后，块序号与块内位置都应回到原处。
    private bool ReadingAnchorLanded(CodeWF.Avalonia.Markdown.Controls.MarkdownViewer.MarkdownReadingPosition anchor) =>
        PreviewMarkdownViewer.SaveReadingPosition() is { } probe && probe.BlockIndex == anchor.BlockIndex
        && Math.Abs(probe.BlockRatio - anchor.BlockRatio) < .01;

    private void QueueScrollToEditorPosition()
    {
        if (_restoringScrollPosition || _outlineNavigation is not null || _viewModel?.IsPresenting == true) return;
        var version = ++_scrollRequestVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version == _scrollRequestVersion) ScrollToEditorPosition();
        }, DispatcherPriority.Background);
    }

    private void ScrollToEditorPosition()
    {
        if (_viewModel is null || _restoringScrollPosition || _viewModel.IsPresenting)
        {
            return;
        }

        // 库返回的边界在正文自己的坐标系里，缩放时换算到滚动坐标才能对上落点。
        if (PreviewMarkdownViewer.TryGetSourceLineBounds(_viewModel.PreviewSourceLine, out var bounds)
            && PreviewMarkdownViewer.TranslatePoint(new Point(0, bounds.Top), PreviewScrollViewer) is { } target)
        {
            var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
            PreviewScrollViewer.Offset = new Vector(0d, Math.Clamp(PreviewScrollViewer.Offset.Y + target.Y, 0d, scrollableHeight));
            return;
        }

        // 目标块尚未渲染/量测：用本次定位带来的滚动比例做临时落点，
        // 渲染完成后的 Markdown 属性变化会再触发一次精确定位。
        var height = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
        PreviewScrollViewer.Offset = new Vector(
            0d,
            height * Math.Clamp(_viewModel.PreviewScrollRatio, 0d, 1d));
        Dispatcher.UIThread.Post(
            () => PreviewMarkdownViewer.TryGetSourceLineBounds(_viewModel.PreviewSourceLine, out _),
            DispatcherPriority.Background);
    }
}
