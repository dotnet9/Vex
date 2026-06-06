using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CodeWF.AvaloniaControls.Controls;
using Ursa.Controls;
using Vex.Core.Services;
using Vex.Modules.Mcp.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class MainWindow : UrsaWindow
{
    private IShellDropTargetHandler? _dropTargetHandler;
    private IAppSettingsStore? _settingsStore;
    private ShellKeyboardShortcutViewModel? _keyboardShortcuts;
    private IShellStartupArgumentPublisher? _startupArguments;
    private IMcpServerHost? _mcpServerHost;
    private bool _isCloseConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureOnboardingGuideTargets();
        TitleMenuView.BeginGuideRequested += TitleMenuView_OnBeginGuideRequested;
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
        ShellKeyboardShortcutViewModel keyboardShortcuts,
        IMcpServerHost mcpServerHost)
        : this()
    {
        // 强制解析 ShellActionCoordinator，让标题栏菜单的 EventBus 动作路由在窗口创建时完成订阅。
        _ = actionCoordinator;
        _dropTargetHandler = dropTargetHandler;
        _settingsStore = settingsStore;
        _startupArguments = startupArguments;
        _keyboardShortcuts = keyboardShortcuts;
        _mcpServerHost = mcpServerHost;
        RestoreWindowSize(settingsStore);
        DataContext = viewModel;
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
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
        if (_settingsStore is null || _settingsStore.Current.HasSeenOnboardingGuide == true)
        {
            return;
        }

        _settingsStore.Update(settings => settings with { HasSeenOnboardingGuide = true });
        Dispatcher.UIThread.Post(BeginOnboardingGuide, DispatcherPriority.Background);
    }

    private void TitleMenuView_OnBeginGuideRequested(object? sender, EventArgs e)
    {
        BeginOnboardingGuide();
    }

    private void ConfigureOnboardingGuideTargets()
    {
        GuideFileMenuStep.Target = TitleMenuView.FileMenuTarget;
        GuideFileOpenStep.Target = TitleMenuView.OpenFolderMenuTarget;
        GuideFileExportStep.Target = TitleMenuView.ExportMenuTarget;
        GuideParagraphMenuStep.Target = TitleMenuView.TableMenuTarget;
        GuideFormatMenuStep.Target = TitleMenuView.LinkMenuTarget;
        GuideViewMenuStep.Target = TitleMenuView.SourceModeMenuTarget;
        GuideViewOutlineMenuStep.Target = TitleMenuView.OutlineMenuTarget;
        GuideThemeMenuStep.Target = TitleMenuView.ThemeDarkMenuTarget;
        GuideHelpMenuStep.Target = TitleMenuView.BeginGuideMenuTarget;
    }

    private void BeginOnboardingGuide()
    {
        ConfigureOnboardingGuideTargets();
        TitleMenuView.CloseGuideMenus();
        OnboardingGuide.GoTo(0);
        OnboardingGuide.Show();
    }

    private void OnboardingGuide_OnStepOpening(object? sender, GuideStepEventArgs e)
    {
        PrepareOnboardingGuideStep(e.Step);
        TitleMenuView.SetGuideMenuOpen(GetGuideMenuKey(e.Step));
    }

    private void PrepareOnboardingGuideStep(IGuideStepOption step)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (ReferenceEquals(step, GuideSidebarFilesStep))
        {
            viewModel.Layout.ShowFiles();
            QueueOnboardingGuideRefresh();
            return;
        }

        if (ReferenceEquals(step, GuideSidebarOutlineStep))
        {
            viewModel.Layout.ShowOutline();
            QueueOnboardingGuideRefresh();
        }
    }

    private void QueueOnboardingGuideRefresh()
    {
        Dispatcher.UIThread.Post(OnboardingGuide.Refresh, DispatcherPriority.Background);
    }

    private void OnboardingGuide_OnCompleted(object? sender, EventArgs e)
    {
        TitleMenuView.CloseGuideMenus();
    }

    private void OnboardingGuide_OnClosed(object? sender, EventArgs e)
    {
        TitleMenuView.CloseGuideMenus();
    }

    private string? GetGuideMenuKey(IGuideStepOption step)
    {
        if (ReferenceEquals(step, GuideFileMenuStep)
            || ReferenceEquals(step, GuideFileOpenStep)
            || ReferenceEquals(step, GuideFileExportStep))
        {
            return ShellTitleMenuView.FileGuideMenu;
        }

        if (ReferenceEquals(step, GuideParagraphMenuStep))
        {
            return ShellTitleMenuView.ParagraphGuideMenu;
        }

        if (ReferenceEquals(step, GuideFormatMenuStep))
        {
            return ShellTitleMenuView.FormatGuideMenu;
        }

        if (ReferenceEquals(step, GuideViewMenuStep)
            || ReferenceEquals(step, GuideViewOutlineMenuStep))
        {
            return ShellTitleMenuView.ViewGuideMenu;
        }

        if (ReferenceEquals(step, GuideThemeMenuStep))
        {
            return ShellTitleMenuView.ThemeColorGuideMenu;
        }

        if (ReferenceEquals(step, GuideHelpMenuStep))
        {
            return ShellTitleMenuView.HelpGuideMenu;
        }

        return null;
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

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        SaveWindowSize();
        _ = _mcpServerHost?.StopAsync();
    }
}
