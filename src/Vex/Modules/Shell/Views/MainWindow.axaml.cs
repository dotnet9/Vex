using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ursa.Controls;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow : UrsaWindow
{
    private IShellDropTargetHandler? _dropTargetHandler;
    private IAppSettingsStore? _settingsStore;
    private ShellKeyboardShortcutViewModel? _keyboardShortcuts;
    private IShellStartupArgumentPublisher? _startupArguments;
    private bool _isCloseConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, WindowDragOver);
        AddHandler(DragDrop.DropEvent, WindowDrop);
    }

    public MainWindow(
        MainWindowViewModel viewModel,
        ShellActionCoordinator actionCoordinator,
        IAppSettingsStore settingsStore,
        IShellDropTargetHandler dropTargetHandler,
        IShellStartupArgumentPublisher startupArguments,
        ShellKeyboardShortcutViewModel keyboardShortcuts)
        : this()
    {
        // 强制解析 ShellActionCoordinator，让标题栏菜单的 EventBus 动作路由在窗口创建时完成订阅。
        _ = actionCoordinator;
        _dropTargetHandler = dropTargetHandler;
        _settingsStore = settingsStore;
        _startupArguments = startupArguments;
        _keyboardShortcuts = keyboardShortcuts;
        RestoreWindowSize(settingsStore);
        DataContext = viewModel;
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        Opened += (_, _) => _startupArguments.PublishStartupArguments(Environment.GetCommandLineArgs().Skip(1));
        Closed += (_, _) => SaveWindowSize();
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = _keyboardShortcuts?.HandleKeyDown(e.Key, e.KeyModifiers) == true;
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
        e.DragEffects = _dropTargetHandler?.GetDragEffects(e) ?? DragDropEffects.None;
        e.Handled = true;
    }

    private void WindowDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        _dropTargetHandler?.PublishDroppedPath(e);
    }

    private void RestoreWindowSize(IAppSettingsStore settingsStore)
    {
        var settings = settingsStore.Current;
        if (settings.WindowWidth is { } width && IsUsableWindowSize(width))
        {
            Width = Math.Max(MinWidth, width);
        }

        if (settings.WindowHeight is { } height && IsUsableWindowSize(height))
        {
            Height = Math.Max(MinHeight, height);
        }
    }

    private void SaveWindowSize()
    {
        if (_settingsStore is null
            || WindowState == Avalonia.Controls.WindowState.FullScreen
            || !IsUsableWindowSize(Bounds.Width)
            || !IsUsableWindowSize(Bounds.Height))
        {
            return;
        }

        _settingsStore.Update(settings => settings with
        {
            WindowWidth = Math.Max(MinWidth, Bounds.Width),
            WindowHeight = Math.Max(MinHeight, Bounds.Height)
        });
    }

    private static bool IsUsableWindowSize(double value)
    {
        return value > 0 && double.IsFinite(value);
    }
}
