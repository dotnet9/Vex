using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Ursa.Controls;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow : UrsaWindow
{
    private IShellDropTargetHandler? _dropTargetHandler;
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
        IShellDropTargetHandler dropTargetHandler,
        IShellStartupArgumentPublisher startupArguments,
        ShellKeyboardShortcutViewModel keyboardShortcuts)
        : this()
    {
        // 强制解析 ShellActionCoordinator，让标题栏菜单的 EventBus 动作路由在窗口创建时完成订阅。
        _ = actionCoordinator;
        _dropTargetHandler = dropTargetHandler;
        _startupArguments = startupArguments;
        _keyboardShortcuts = keyboardShortcuts;
        DataContext = viewModel;
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        Opened += (_, _) => _startupArguments.PublishStartupArguments(Environment.GetCommandLineArgs().Skip(1));
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
}
