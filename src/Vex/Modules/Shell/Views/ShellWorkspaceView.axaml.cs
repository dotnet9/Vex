using Avalonia.Controls;
using Avalonia.Input;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>Owns the desktop workspace composition, sidebar resizing and navigation state.</summary>
public partial class ShellWorkspaceView : UserControl
{
    private MainWindowViewModel? _shell;
    public WorkspaceViewController ViewController { get; private set; } = null!;

    public ShellWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachAppearance();
    }

    internal void Initialize(MainWindow window)
    {
        ViewController = new WorkspaceViewController(window, ViewSurface, FloatingViewShelf);
        SidebarSplitter.DragStarted += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.BeginSidebarResize(); };
        SidebarSplitter.DragCompleted += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.CompleteSidebarResize(); };
        OutlineSplitter.DragStarted += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.BeginOutlineResize(); };
        OutlineSplitter.DragCompleted += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.CompleteOutlineResize(); };
        OutlineSplitter.KeyDown += (_, e) =>
        {
            if (DataContext is MainWindowViewModel vm && e.Key is Key.Left or Key.Right)
            { vm.Layout.OutlineWidth += e.Key == Key.Left ? 12 : -12; e.Handled = true; }
        };
        SidebarSplitter.KeyDown += (_, e) =>
        {
            if (DataContext is MainWindowViewModel vm && e.Key is Key.Left or Key.Right)
            { vm.Layout.SidebarWidth += e.Key == Key.Right ? 10 : -10; e.Handled = true; }
        };
    }

    private void AttachAppearance()
    {
        if (_shell is not null)
        {
            _shell.Appearance.PropertyChanged -= AppearanceChanged;
            _shell.Workbench.PropertyChanged -= AppearanceChanged;
        }
        _shell = DataContext as MainWindowViewModel;
        if (_shell is not null)
        {
            _shell.Appearance.PropertyChanged += AppearanceChanged;
            _shell.Workbench.PropertyChanged += AppearanceChanged;
        }
        UpdateSageLeaf();
    }

    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => UpdateSageLeaf();
    private void UpdateSageLeaf() => SageLeafDecoration.IsVisible = _shell?.Appearance.SelectedPalette?.Key == "teal"
        && !_shell.Workbench.IsPresenting;

}
