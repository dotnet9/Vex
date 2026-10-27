using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using Vex.Modules.Workspace.ViewModels;

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
            ApplyDocumentGeometry();
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
    private void ApplyDocumentGeometry()
    {
        var presenting = _viewModel?.IsPresenting == true;
        // 原型 .md-body 的 zoom 是整篇均匀缩放：字号、行高、块距、表格与代码宽度同比放大，
        // 可用宽度按缩放回算，所以倍率越大每行越早换行、文档整体越高。只放大字号会留下
        // 库内绝对行高与边距，段落就会排在旧高度里被截断。
        var scale = Math.Max(.1, _viewModel?.DocumentScale ?? 1);
        DocumentTransform.LayoutTransform = scale == 1 ? null : new Avalonia.Media.ScaleTransform(scale, scale);
        DocumentTransform.Margin = presenting ? new Thickness(44, 40, 44, 60) : default;
        if (presenting) PreviewMarkdownViewer.Width = Math.Min(1104, Math.Max(100, PreviewScrollViewer.Bounds.Width - 88)) / scale;
        else PreviewMarkdownViewer.ClearValue(WidthProperty);
        var clientWidth = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? 1280;
        // 原型 .md-body 留白：单栏阅读 max(820px,50vw) 居中、padding 30/52/104；分栏 max(800px,42vw) 居中、padding 36/48/104。
        // 限宽写在正文上（缩放坐标系内），等价 CSS max-width：物理宽度 = min(可用宽, 上限×缩放)，放大时贴住窗格也不会横向溢出。
        DocumentTransform.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        DocumentTransform.MaxWidth = double.PositiveInfinity;
        if (presenting)
        {
            PreviewMarkdownViewer.MaxWidth = 1104 / scale;
            PreviewMarkdownViewer.Margin = default;
        }
        else if (IsReadingMode)
        {
            var margin = new Thickness(52, 30, 52, 104);
            PreviewMarkdownViewer.Margin = margin;
            // 原型的 max-width 是含留白的边框盒（全局 border-box），Avalonia 的 MaxWidth 只算内容：把留白扣回去。
            PreviewMarkdownViewer.MaxWidth = Math.Max(820, clientWidth * 0.5) - margin.Left - margin.Right;
        }
        else
        {
            var margin = new Thickness(48, 36, 48, 104);
            PreviewMarkdownViewer.Margin = margin;
            PreviewMarkdownViewer.MaxWidth = Math.Max(800, clientWidth * 0.42) - margin.Left - margin.Right;
        }
    }

    public MarkdownPreviewView()
    {
        InitializeComponent();
        // 还原钮没有可绑的命令属性类型差异，直接走控件事件；坞实例随模板固定，挂一次即可。
        ReadingZoomDock.ResetRequested += (_, _) => _viewModel?.ResetReadingZoom();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += (_, _) => SetViewModel(null);
        PreviewScrollViewer.ScrollChanged += OnPreviewScrollChanged;
        PreviewScrollViewer.SizeChanged += (_, _) => ApplyDocumentGeometry();
        DetachedFromVisualTree += (_, _) => PreviewMarkdownViewer.TaskWriteBackRequested -= OnTaskWriteBackRequested;
        // 大纲跳转不经过预览视图模型，直接订阅总线，避免被「属性值未变」过滤掉。
        DetachedFromVisualTree += (_, _) => EventBus.Default.Unsubscribe(this);
        SetViewModel(DataContext as MarkdownPreviewViewModel);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        SetViewModel(DataContext as MarkdownPreviewViewModel);
        EventBus.Default.Subscribe(this);
        PreviewMarkdownViewer.TaskWriteBackRequested -= OnTaskWriteBackRequested;
        PreviewMarkdownViewer.TaskWriteBackRequested += OnTaskWriteBackRequested;
        // 阅读位置由库统一按「块序号 + 块内比例」采集与恢复，这里只需把滚动宿主交给 Viewer。
        PreviewMarkdownViewer.ScrollHost = PreviewScrollViewer;
    }

    // 预览里点击任务勾选框：库返回新 Markdown 与变更区间，交给编辑器按区间回写。
    private static void OnTaskWriteBackRequested(object? sender, CodeWF.Avalonia.Markdown.Shared.Rendering.MarkdownTaskWriteResult result)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Core.Messaging.MarkdownTaskWriteBackCommand(
            result.Markdown,
            result.ChangedSpan.Start,
            result.ChangedSpan.Length));
    }

    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_restoringScrollPosition || _viewModel?.IsPresenting == true)
        {
            return;
        }

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
        if (e.PropertyName == nameof(MarkdownPreviewViewModel.Markdown))
        {
            // 渲染是分批异步的：稍等一拍再挂表格工具，重复挂载有去重保护。
            Dispatcher.UIThread.Post(() =>
            {
                TableTools.Attach(PreviewMarkdownViewer);
                DiagramEditor.Attach(PreviewMarkdownViewer);
            }, DispatcherPriority.Background);
        }
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
            if (PreviewMarkdownViewer.SaveReadingPosition() is { } anchor) _savedReadingPosition = anchor;
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
        _restoringScrollPosition = false;
        _savedReadingPosition = null;
        _lastReadingPositionSampleTick = 0;
        Dispatcher.UIThread.Post(() => { if (!_viewModel?.IsPresenting ?? false) PreviewScrollViewer.Offset = default; },
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
        _viewModel.PreviewSourceLine = command.Line;
        _viewModel.PreviewScrollRatio = 0d;
        Dispatcher.UIThread.Post(ScrollToEditorPosition, DispatcherPriority.Background);
    }

    private void QueueRestoreReadingPosition()
    {
        if (_savedReadingPosition is null)
        {
            return;
        }

        _restoringScrollPosition = true;
        _restoreAttempts = 0;
        _restoreSettleQueued = false;
        _restoreSource = _viewModel?.Markdown;
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
        if (PreviewMarkdownViewer.CurrentModel.Source != _viewModel?.Markdown) return;
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

    // 库按「视口中心落在哪个块」采样；中心块序号对上，块内比例自然一致。
    private bool ReadingAnchorLanded(CodeWF.Avalonia.Markdown.Controls.MarkdownViewer.MarkdownReadingPosition anchor) =>
        PreviewMarkdownViewer.SaveReadingPosition() is { } probe && probe.BlockIndex == anchor.BlockIndex;

    private void QueueScrollToEditorPosition()
    {
        if (_restoringScrollPosition || _viewModel?.IsPresenting == true) return;
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
