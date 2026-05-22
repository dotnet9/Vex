using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
    }

    public MainWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyWindowState(viewModel);
        Opened += async (_, _) => await viewModel.OpenStartupDocumentAsync(Environment.GetCommandLineArgs().Skip(1));
    }

    private void TitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void MinimizeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private async void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var hasControl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var hasAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (hasControl && !hasShift && e.Key == Key.N)
        {
            viewModel.NewDocument();
            e.Handled = true;
        }
        else if (hasControl && !hasShift && e.Key == Key.O)
        {
            e.Handled = true;
            await viewModel.OpenAsync();
        }
        else if (hasControl && !hasShift && e.Key == Key.S)
        {
            e.Handled = true;
            await viewModel.SaveAsync();
        }
        else if (hasControl && hasShift && e.Key == Key.S)
        {
            e.Handled = true;
            await viewModel.SaveAsAsync();
        }
        else if (hasControl && !hasShift && e.Key == Key.P)
        {
            e.Handled = true;
            await viewModel.Print();
        }
        else if (hasControl && !hasShift && e.Key == Key.W)
        {
            viewModel.CloseDocument();
            e.Handled = true;
        }
        else if (hasControl && !hasShift && IsZoomInKey(e.Key))
        {
            viewModel.ZoomIn();
            e.Handled = true;
        }
        else if (hasControl && !hasShift && IsZoomOutKey(e.Key))
        {
            viewModel.ZoomOut();
            e.Handled = true;
        }
        else if (hasControl && !hasShift && IsActualSizeKey(e.Key))
        {
            viewModel.ActualSize();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            viewModel.ToggleFullScreen();
            e.Handled = true;
        }
        else if (hasAlt && e.Key == Key.Enter)
        {
            viewModel.ShowProperties();
            e.Handled = true;
        }
        else if (hasControl && e.Key == Key.F)
        {
            viewModel.ShowFindPanel();
            e.Handled = true;
        }
        else if (hasControl && e.Key == Key.H)
        {
            viewModel.ShowReplacePanel();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            viewModel.FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && viewModel.IsFindPanelVisible)
        {
            viewModel.CloseFindPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && viewModel.CloseFloatingPanel())
        {
            e.Handled = true;
        }
    }

    private static bool IsZoomInKey(Key key)
    {
        return key is Key.OemPlus or Key.Add;
    }

    private static bool IsZoomOutKey(Key key)
    {
        return key is Key.OemMinus or Key.Subtract;
    }

    private static bool IsActualSizeKey(Key key)
    {
        return key is Key.D0 or Key.NumPad0;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainWindowViewModel viewModel
            && e.PropertyName is nameof(MainWindowViewModel.IsAlwaysOnTop) or nameof(MainWindowViewModel.IsFullScreen))
        {
            ApplyWindowState(viewModel);
        }
    }

    private void ApplyWindowState(MainWindowViewModel viewModel)
    {
        Topmost = viewModel.IsAlwaysOnTop;
        WindowState = viewModel.IsFullScreen ? WindowState.FullScreen : WindowState.Normal;
    }
}
