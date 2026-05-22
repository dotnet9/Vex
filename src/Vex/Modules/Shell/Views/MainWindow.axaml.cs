using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Ursa.Controls;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow : UrsaWindow
{
    private bool _isCloseConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, WindowDragOver);
        AddHandler(DragDrop.DropEvent, WindowDrop);
    }

    public MainWindow(MainWindowViewModel viewModel, ShellActionCoordinator actionCoordinator)
        : this()
    {
        // 强制解析 ShellActionCoordinator，使标题栏菜单的 EventBus 动作路由在窗口创建时完成订阅。
        _ = actionCoordinator;
        DataContext = viewModel;
        viewModel.Layout.PropertyChanged += OnLayoutPropertyChanged;
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        ApplyWindowState(viewModel.Layout);
        Opened += async (_, _) => await viewModel.OpenStartupDocumentAsync(Environment.GetCommandLineArgs().Skip(1));
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
            e.Handled = true;
            await viewModel.NewDocument();
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
            e.Handled = true;
            await viewModel.CloseDocument();
        }
        else if (hasControl && !hasShift && IsZoomInKey(e.Key))
        {
            viewModel.EditorDisplay.ZoomIn();
            e.Handled = true;
        }
        else if (hasControl && !hasShift && IsZoomOutKey(e.Key))
        {
            viewModel.EditorDisplay.ZoomOut();
            e.Handled = true;
        }
        else if (hasControl && !hasShift && IsActualSizeKey(e.Key))
        {
            viewModel.EditorDisplay.ActualSize();
            e.Handled = true;
        }
        else if (e.Key == Key.F11)
        {
            viewModel.Layout.ToggleFullScreen();
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
        else if (e.Key == Key.Escape && viewModel.CloseFloatingPanel())
        {
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && viewModel.FindBar.IsVisible)
        {
            viewModel.CloseFindPanel();
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

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ShellWindowLayoutViewModel layout
            && e.PropertyName is nameof(ShellWindowLayoutViewModel.IsAlwaysOnTop) or nameof(ShellWindowLayoutViewModel.IsFullScreen))
        {
            ApplyWindowState(layout);
        }

    }

    private void ApplyWindowState(ShellWindowLayoutViewModel layout)
    {
        Topmost = layout.IsAlwaysOnTop;
        WindowState = layout.IsFullScreen ? WindowState.FullScreen : WindowState.Normal;
    }

    protected override async Task<bool> CanClose()
    {
        if (_isCloseConfirmed)
        {
            return true;
        }

        if (DataContext is MainWindowViewModel viewModel && viewModel.DocumentInfo.IsModified)
        {
            await viewModel.BeginWindowCloseAsync();
            return false;
        }

        return true;
    }

    private void OnCloseWindowRequested(object? sender, EventArgs e)
    {
        _isCloseConfirmed = true;
        Close();
    }

    private void WindowDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = GetFirstDroppedLocalPath(e) is null
            ? DragDropEffects.None
            : DragDropEffects.Copy;
        e.Handled = true;
    }

    private async void WindowDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var path = GetFirstDroppedLocalPath(e);
        if (path is not null)
        {
            await viewModel.OpenDroppedPathAsync(path);
        }
    }

    private static string? GetFirstDroppedLocalPath(DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return null;
        }

        foreach (var item in files)
        {
            var path = item.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path) && (File.Exists(path) || Directory.Exists(path)))
            {
                return path;
            }
        }

        return null;
    }
}
