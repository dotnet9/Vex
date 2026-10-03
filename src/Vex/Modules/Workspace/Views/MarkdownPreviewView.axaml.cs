using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownPreviewView : UserControl
{
    private MarkdownPreviewViewModel? _viewModel;
    private double _savedReadingRatio;
    private bool _restoringScrollPosition;

    public MarkdownPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => SetViewModel(DataContext as MarkdownPreviewViewModel);
        DetachedFromVisualTree += (_, _) => SetViewModel(null);
        PreviewScrollViewer.ScrollChanged += OnPreviewScrollChanged;
        MarkdownCodeContrastFixer.Attach(PreviewMarkdownViewer);
        SetViewModel(DataContext as MarkdownPreviewViewModel);
    }

    // 持续记录阅读位置（滚动比例），主题/排版切换整篇重渲染后按比例恢复。
    private void OnPreviewScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_restoringScrollPosition)
        {
            return;
        }

        var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
        _savedReadingRatio = scrollableHeight <= 0
            ? 0
            : Math.Clamp(PreviewScrollViewer.Offset.Y / scrollableHeight, 0d, 1d);
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
        else if (e.PropertyName is nameof(MarkdownPreviewViewModel.TypographyTheme)
            or nameof(MarkdownPreviewViewModel.TypographySize))
        {
            // 主题/排版切换会整篇重渲染并重置滚动，按比例恢复切换前的阅读位置。
            QueueScrollToReadingPosition();
        }
    }

    private void QueueScrollToReadingPosition()
    {
        // 重渲染异步完成且会把 Offset 重置为 0：恢复窗口内（300ms）冻结保存值，
        // 定时器触发时重渲染已完成，按保存的比例恢复。
        _restoringScrollPosition = true;
        Dispatcher.UIThread.Post(RestoreReadingPosition, DispatcherPriority.Background);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            RestoreReadingPosition();
            _restoringScrollPosition = false;
        };
        timer.Start();
    }

    private void RestoreReadingPosition()
    {
        var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
        if (scrollableHeight > 0d)
        {
            PreviewScrollViewer.Offset = new Vector(0d, scrollableHeight * _savedReadingRatio);
        }
    }

    private void QueueScrollToEditorPosition()
    {
        Dispatcher.UIThread.Post(ScrollToEditorPosition, DispatcherPriority.Background);
    }

    private void ScrollToEditorPosition()
    {
        if (_viewModel is null)
        {
            return;
        }

        if (TryScrollToSourceLine())
        {
            return;
        }

        var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
        var targetY = scrollableHeight * Math.Clamp(_viewModel.PreviewScrollRatio, 0d, 1d);
        PreviewScrollViewer.Offset = new Vector(0d, targetY);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Optional CodeWF.Markdown line-bound API is probed at runtime and falls back to ratio scrolling when unavailable.")]
    private bool TryScrollToSourceLine()
    {
        if (_viewModel is null)
        {
            return false;
        }

        var method = PreviewMarkdownViewer.GetType().GetMethod(
            "TryGetSourceLineBounds",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: [typeof(int), typeof(Rect).MakeByRefType()],
            modifiers: null);
        if (method is null)
        {
            return false;
        }

        var arguments = new object?[] { _viewModel.PreviewSourceLine, default(Rect) };
        if (method.Invoke(PreviewMarkdownViewer, arguments) is not true || arguments[1] is not Rect bounds)
        {
            return false;
        }

        var scrollableHeight = Math.Max(0d, PreviewScrollViewer.Extent.Height - PreviewScrollViewer.Viewport.Height);
        if (scrollableHeight <= 0d)
        {
            return true;
        }

        var targetY = Math.Clamp(bounds.Y, 0d, scrollableHeight);
        PreviewScrollViewer.Offset = new Vector(0d, targetY);
        return true;
    }
}
