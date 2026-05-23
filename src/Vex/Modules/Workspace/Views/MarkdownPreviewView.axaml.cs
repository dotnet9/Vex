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

    public MarkdownPreviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => SetViewModel(DataContext as MarkdownPreviewViewModel);
        DetachedFromVisualTree += (_, _) => SetViewModel(null);
        SetViewModel(DataContext as MarkdownPreviewViewModel);
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
