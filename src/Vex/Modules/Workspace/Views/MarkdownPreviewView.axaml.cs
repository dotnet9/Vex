using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownPreviewView : UserControl
{
    private MarkdownPreviewViewModel? _viewModel;
    private MarkdownViewer.MarkdownReadingPosition? _savedReadingPosition;
    private bool _restoringScrollPosition;
    private int _appliedRefreshVersion;
    private long _lastReadingPositionSampleTick;
    private int _restoreAttempts;

    public MarkdownPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += (_, _) => SetViewModel(null);
        PreviewScrollViewer.ScrollChanged += OnPreviewScrollChanged;
        PreviewMarkdownViewer.TaskWriteBackRequested += OnTaskWriteBackRequested;
        DetachedFromVisualTree += (_, _) => PreviewMarkdownViewer.TaskWriteBackRequested -= OnTaskWriteBackRequested;
        SetViewModel(DataContext as MarkdownPreviewViewModel);
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
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
        if (_restoringScrollPosition)
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
        else if (e.PropertyName is nameof(MarkdownPreviewViewModel.TypographyTheme)
                 or nameof(MarkdownPreviewViewModel.TypographySize))
        {
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

    private void QueueRestoreReadingPosition()
    {
        if (_savedReadingPosition is null)
        {
            return;
        }

        _restoringScrollPosition = true;
        _restoreAttempts = 0;
        Dispatcher.UIThread.Post(RestoreReadingPosition, DispatcherPriority.Background);
    }

    private void RestoreReadingPosition()
    {
        if (_savedReadingPosition is null)
        {
            _restoringScrollPosition = false;
            return;
        }

        if (PreviewMarkdownViewer.RestoreReadingPosition(_savedReadingPosition))
        {
            _restoringScrollPosition = false;
            return;
        }

        // 渲染异步完成，块尚未布局时短暂重试，最多 5 次后放弃（不阻塞 UI）。
        if (++_restoreAttempts >= 5)
        {
            _restoringScrollPosition = false;
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            RestoreReadingPosition();
        };
        timer.Start();
    }

    private void QueueScrollToEditorPosition()
    {
        Dispatcher.UIThread.Post(ScrollToEditorPosition, DispatcherPriority.Background);
    }

    private void ScrollToEditorPosition()
    {
        if (_viewModel is null || _restoringScrollPosition)
        {
            return;
        }

        if (PreviewMarkdownViewer.TryGetSourceLineBounds(_viewModel.PreviewSourceLine, out var bounds))
        {
            var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
            PreviewScrollViewer.Offset = new Vector(0d, Math.Clamp(bounds.Y, 0d, scrollableHeight));
            return;
        }

        var height = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
        PreviewScrollViewer.Offset = new Vector(
            0d,
            height * Math.Clamp(_viewModel.PreviewScrollRatio, 0d, 1d));
    }
}
