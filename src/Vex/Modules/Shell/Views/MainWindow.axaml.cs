using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CodeWF.Avalonia.Controls.Controls;
using Vex.Core.Services;
using Vex.Modules.Mcp.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow : CodeWFWindow
{
    private IShellDropTargetHandler? _dropTargetHandler;
    private IAppSettingsStore? _settingsStore;
    private ShellKeyboardShortcutViewModel? _keyboardShortcuts;
    private IShellStartupArgumentPublisher? _startupArguments;
    private IMcpServerHost? _mcpServerHost;
    private IShellToastPublisher? _toastPublisher;
    private bool _isCloseConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        SidebarSplitter.DragStarted += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.BeginSidebarResize(); };
        SidebarSplitter.DragCompleted += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.CompleteSidebarResize(); };
        SizeChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Layout.IsCompactWindow = Bounds.Width < 1100; };
        TitleMenuView.BeginGuideRequested += TitleMenuView_OnBeginGuideRequested;
        WorkbenchToolbar.BeginGuideRequested += TitleMenuView_OnBeginGuideRequested;
        AddHandler(KeyDownEvent, WindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, WorkbenchPointerPressed, RoutingStrategies.Tunnel);
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
        ShellKeyboardShortcutViewModel keyboardShortcuts,
        IMcpServerHost mcpServerHost,
        IShellToastPublisher toastPublisher)
        : this()
    {
        // 强制解析 ShellActionCoordinator，让标题栏菜单的 EventBus 动作路由在窗口创建时完成订阅。
        _ = actionCoordinator;
        _dropTargetHandler = dropTargetHandler;
        _settingsStore = settingsStore;
        _startupArguments = startupArguments;
        _keyboardShortcuts = keyboardShortcuts;
        _mcpServerHost = mcpServerHost;
        _toastPublisher = toastPublisher;
        RestoreWindowSize(settingsStore);
        DataContext = viewModel;
        viewModel.Workbench.GuideExitRequested += (_, _) => WorkbenchGuide.Finish();
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        Closing += MainWindow_OnClosing;
        Closed += (_, _) => WorkbenchGuide.Finish();
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _toastPublisher?.Install(e.NameScope.Find<VisualLayerManager>("PART_VisualLayerManager"));
    }

    private void WorkbenchPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || e.Source is not Visual source) return;
        var ancestors = source.GetVisualAncestors().Prepend(source).ToArray();
        if (vm.Workbench.ShowTypography && !ancestors.Contains(TypographyGallery) && !ancestors.Contains(EditorOptions)) vm.Workbench.ShowTypography = false;
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel presentationVm && presentationVm.Workbench.IsPresenting)
        { e.Handled = presentationVm.Workbench.HandlePresentationKey(e.Key); return; }
        if (WorkbenchGuide.IsVisible && WorkbenchGuide.HandleKey(e)) { e.Handled = true; return; }
        if (e.Key == Key.Escape && DataContext is MainWindowViewModel vm && (vm.Workbench.ShowTypography || vm.Workbench.ShowMapSettings || vm.Workbench.HasSelectedNode))
        { vm.Workbench.ShowTypography = false; vm.Workbench.ShowMapSettings = false; vm.Workbench.CloseInspector(); e.Handled = true; return; }
        e.Handled = _keyboardShortcuts?.HandleKeyDown(e.Key, e.KeyModifiers) == true;
    }

    private void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        var startupArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (startupArguments.Length > 0 && DataContext is MainWindowViewModel viewModel)
        {
            Dispatcher.UIThread.Post(
                () => _ = viewModel.OpenStartupDocumentAsync(startupArguments),
                DispatcherPriority.Background);
        }
        else
        {
            _startupArguments?.PublishStartupArguments(startupArguments);
        }

        QueueFirstRunOnboardingGuide();
        _ = _mcpServerHost?.ApplySettingsAsync();
    }

    private void QueueFirstRunOnboardingGuide()
    {
        WorkbenchToolbar.QuickStartButton.IsVisible = _settingsStore?.Current.HasSeenOnboardingGuide != true;
    }

    private void TitleMenuView_OnBeginGuideRequested(object? sender, EventArgs e) => BeginOnboardingGuide();

    public void BeginOnboardingGuide()
    {
        if (DataContext is MainWindowViewModel vm)
            WorkbenchGuide.Start(vm, this, _settingsStore, new Control[] { WorkbenchToolbar.ViewTarget, InstantGuideTarget, TypographyGallery, MindMapGuideTarget.GuideSettingsTarget, TitleMenuView.FileMenuTarget });
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        WorkbenchGuide.Finish();
        if (_isCloseConfirmed)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel && viewModel.DocumentInfo.IsModified)
        {
            e.Cancel = true;
            _ = viewModel.BeginWindowCloseAsync();
        }
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

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        SaveWindowSize();
        _ = _mcpServerHost?.StopAsync();
    }
}
