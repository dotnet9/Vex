using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit.Highlighting;
using Prism.Ioc;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownEditorView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private IMarkdownEditorController? _editorController;

    public MarkdownEditorView()
    {
        InitializeComponent();
        MarkdownEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("MarkDown");
        DataContextChanged += (_, _) => AttachViewModel(DataContext as MainWindowViewModel);
        AttachedToVisualTree += (_, _) =>
        {
            AttachEditorController();
            SyncEditorFromViewModel();
        };
        DetachedFromVisualTree += (_, _) => DetachEditorController();
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            AttachEditorController();
            SyncEditorFromViewModel();
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        DetachEditorController();
        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        AttachEditorController();
        SyncEditorFromViewModel();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.Markdown) or null)
        {
            SyncEditorFromViewModel();
        }
    }

    private void SyncEditorFromViewModel()
    {
        if (_viewModel is null)
        {
            return;
        }

        _editorController?.SyncText(_viewModel.Markdown);
    }

    private void AttachEditorController()
    {
        if (Design.IsDesignMode)
        {
            return;
        }

        _editorController ??= (IMarkdownEditorController)ContainerLocator.Container.Resolve(typeof(IMarkdownEditorController));
        _editorController.Attach(MarkdownEditor);
    }

    private void DetachEditorController()
    {
        _editorController?.Detach(MarkdownEditor);
    }
}
