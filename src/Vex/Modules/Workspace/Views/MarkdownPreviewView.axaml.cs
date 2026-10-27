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
    private void ApplyDocumentGeometry()
    {
        var presenting = _viewModel?.IsPresenting == true;
        var zoom = _viewModel?.PresentationZoom ?? 1;
        DocumentTransform.LayoutTransform = presenting ? new Avalonia.Media.ScaleTransform(zoom, zoom) : null;
        DocumentTransform.Margin = presenting ? new Thickness(44, 40, 44, 60) : default;
        if (presenting) PreviewMarkdownViewer.Width = Math.Min(1104, Math.Max(100, PreviewScrollViewer.Bounds.Width - 88)) / zoom;
        else PreviewMarkdownViewer.ClearValue(WidthProperty);
        var clientWidth = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? 1280;
        // 原型 .md-body 留白：单栏阅读 max(820px,50vw) 居中、padding 30/52/88；分栏 padding 36/48/84 铺满。
        // 限宽/居中放在包裹层 LayoutTransformControl 上（ScrollViewer 直接子级会被强制拉伸）。
        if (presenting)
        {
            DocumentTransform.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            DocumentTransform.MaxWidth = double.PositiveInfinity;
            PreviewMarkdownViewer.MaxWidth = 1104 / zoom;
            PreviewMarkdownViewer.Margin = default;
        }
        else if (IsReadingMode)
        {
            DocumentTransform.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            DocumentTransform.MaxWidth = Math.Max(820, clientWidth * 0.5);
            PreviewMarkdownViewer.MaxWidth = double.PositiveInfinity;
            PreviewMarkdownViewer.Margin = new Thickness(52, 30, 52, 88);
        }
        else
        {
            DocumentTransform.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            DocumentTransform.MaxWidth = double.PositiveInfinity;
            PreviewMarkdownViewer.MaxWidth = double.PositiveInfinity;
            PreviewMarkdownViewer.Margin = clientWidth <= 1100 ? new Thickness(36, 36, 36, 84) : new Thickness(48, 36, 48, 84);
        }
    }

    public MarkdownPreviewView(MarkdownPreviewViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public MarkdownPreviewView()
    {
        InitializeComponent();
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
        if (_savedReadingPosition is null)
        {
            _restoringScrollPosition = false;
            LayoutUpdated -= OnRestoreLayoutUpdated;
            return;
        }

        // Markdown 绑定先更新，后台的全文模型稍后才到。不能把全文锚点恢复到
        // 仍在显示的演示短节上，否则库按短节块数夹取后就会被误认为已完成。
        if (PreviewMarkdownViewer.CurrentModel.Source == _viewModel?.Markdown
            && PreviewMarkdownViewer.RestoreReadingPosition(_savedReadingPosition))
        {
            _restoringScrollPosition = false;
            LayoutUpdated -= OnRestoreLayoutUpdated;
            return;
        }
        // Resume when the asynchronously rebuilt document has an actual layout.
    }

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

        if (PreviewMarkdownViewer.TryGetSourceLineBounds(_viewModel.PreviewSourceLine, out var bounds))
        {
            var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
            PreviewScrollViewer.Offset = new Vector(0d, Math.Clamp(bounds.Y, 0d, scrollableHeight));
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
