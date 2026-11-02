using CodeWF.Avalonia.Controls.Helpers;
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
    public WorkspaceViewController WorkspaceViews => Workspace.ViewController;

    public MainWindow()
    {
        InitializeComponent();
        // 原型 deck 的还原钮语义随档位切换：正文档回进入演示时的字号，导图档适应画布（与主窗体 .map-controls 同款）。
        PresentationControls.PresentationDeckDock.ResetRequested += (_, _) =>
        {
            if (DataContext is not MainWindowViewModel shell) return;
            if (shell.Workbench.PresentationMap) Workspace.MindMapGuideTarget.FitMap();
            else shell.Workbench.ResetPresentationZoom();
        };
        var confirmation = Workspace.MindMapGuideTarget.DetachConfirmation();
        Grid.SetRowSpan(confirmation, 4); ShellGuideRoot.Children.Add(confirmation);
        Workspace.Initialize(this);
        SizeChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) { vm.Layout.IsCompactWindow = Bounds.Width < 1100; vm.Layout.WorkspaceWidth = Bounds.Width; }
        };
        TitleMenuView.BeginGuideRequested += TitleMenuView_OnBeginGuideRequested;
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
        DataContext = viewModel;
        viewModel.Workbench.GuideExitRequested += (_, _) => WorkbenchGuide.Finish();
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        viewModel.CommitPendingEditsRequested += (_, _) =>
        {
            Workspace.MindMapGuideTarget.FlushInspector();
            Workspace.PreviewGuideTarget.FlushDiagramEdits();
        };
        Closing += MainWindow_OnClosing;
        Closed += (_, _) => WorkbenchGuide.Finish();
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        InstallCaptionIcons(e.NameScope);
        _toastPublisher?.Install(e.NameScope.Find<VisualLayerManager>("PART_VisualLayerManager"));
    }

    private void WorkbenchPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || e.Source is not Visual source) return;
        if (WorkbenchGuide.IsVisible)
        {
            if (WorkbenchGuide.AllowsPointer(source)) return;
            WorkbenchGuide.PointerRejected(source);
            e.Handled = true;
            return;
        }
        var ancestors = source.GetVisualAncestors().Prepend(source).ToArray();
        if (vm.Workbench.ShowLayouts && !ancestors.Contains(Workspace.LayoutGallery)) vm.Workbench.ShowLayouts = false;
        if (vm.Workbench.ShowTypography && !ancestors.Contains(TypographyGallery) && !ancestors.Contains(Workspace.EditorOptions) && !ancestors.Contains(WorkspaceViews.RenderTypographyTrigger)) vm.Workbench.ShowTypography = false;
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        // 窗内模态框处理自己的按键，避免 Esc/F6/撤销等穿透到后面的工作台。
        if (ShellDialogHost.IsInModalStatus) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.S or Key.W or Key.O or Key.N)
            Workspace.MindMapGuideTarget.FlushInspector();
        // 引导按键优先：演示中的 Esc/翻页等在引导第 5 幕期间由引导接管。
        if (WorkbenchGuide.IsVisible && WorkbenchGuide.HandleKey(e)) { e.Handled = true; return; }
        if (DataContext is MainWindowViewModel presentationVm && presentationVm.Workbench.IsPresenting)
        {
            if (e.KeyModifiers == KeyModifiers.None && e.Key is Key.Escape or Key.F11 or Key.F6)
            { e.Handled = presentationVm.Workbench.HandlePresentationKey(e.Key); return; }
            // 滑块与输入框保留自己的编辑按键，按钮的空格激活也不能误翻页。
            if (e.Source is Visual focused)
            {
                var controls = focused.GetVisualAncestors().Prepend(focused);
                if (controls.Any(control => control is TextBox
                    || control is Slider && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown
                    || control is Button && e.Key == Key.Space)) return;
            }
            if (presentationVm.Workbench.PresentationMap && e.Key is Key.Add or Key.OemPlus or Key.Subtract or Key.OemMinus)
            { Workspace.MindMapGuideTarget.ZoomPresentation(e.Key is Key.Add or Key.OemPlus); e.Handled = true; }
            else if (e.KeyModifiers == KeyModifiers.None) e.Handled = presentationVm.Workbench.HandlePresentationKey(e.Key);
            return;
        }
        if (e.Key == Key.F6 && e.KeyModifiers == KeyModifiers.None && DataContext is MainWindowViewModel startVm)
        { startVm.Workbench.StartPresentation(); e.Handled = true; return; }
        if (e.Key == Key.Escape && Workspace.MindMapGuideTarget.HandleEscape()) { e.Handled = true; return; }
        if (RouteWorkspaceHistory(e)) return;
        var editingMapTitle = e.Source is Visual input && input.GetVisualAncestors().Prepend(input).Any(item => item is TextBox)
            && input.GetVisualAncestors().Any(item => item is Vex.Modules.Workspace.Views.MarkdownMindMapHost);
        if (e.Key == Key.Escape && !editingMapTitle && DataContext is MainWindowViewModel vm && (vm.Workbench.ShowTypography || vm.Workbench.ShowLayouts || vm.Workbench.ShowMapSettings || vm.Workbench.HasSelectedNode))
        {
            var typography = vm.Workbench.ShowTypography;
            var layouts = vm.Workbench.ShowLayouts;
            vm.Workbench.ShowLayouts = false; vm.Workbench.ShowTypography = false; vm.Workbench.ShowMapSettings = false; vm.Workbench.CloseInspector();
            // 原型 closeMenus(true) / typography.close(true)：Esc 关浮层后焦点回到把它打开的那枚钮。
            if (typography) WorkspaceViews.ActiveTypographyTrigger?.Focus();
            else if (layouts) Workspace.EditorModeButton.Focus();
            e.Handled = true; return;
        }
        e.Handled = _keyboardShortcuts?.HandleKeyDown(e.Key, e.KeyModifiers) == true;
    }

    private bool RouteWorkspaceHistory(KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Key is not (Key.Z or Key.Y) || DataContext is not MainWindowViewModel vm) return false;
        if (e.Source is Visual visual)
        {
            var ancestors = visual.GetVisualAncestors().Prepend(visual).ToArray();
            // 筛选、查找、章节表单和代码类型保留输入框自己的撤销，避免误改正文。
            var textBox = ancestors.OfType<TextBox>().FirstOrDefault();
            if (textBox is not null && (textBox.Classes.Contains("MdCodeLanguageInput")
                || !ancestors.Any(item => item is CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownLiveEditorView))) return false;
        }
        if (e.Key == Key.Y || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) vm.Workbench.RedoDocument(); else vm.Workbench.UndoDocument();
        e.Handled = true; return true;
    }
    internal void ForwardWorkspaceShortcut(KeyEventArgs e)
    {
        if (e.Key == Key.F6 && e.KeyModifiers == KeyModifiers.None && DataContext is MainWindowViewModel vm)
        { vm.Workbench.StartPresentation(); e.Handled = true; return; }
        if (e.Key == Key.Escape && Workspace.MindMapGuideTarget.HandleEscape()) { e.Handled = true; return; }
        if (RouteWorkspaceHistory(e)) return;
        e.Handled = _keyboardShortcuts?.HandleKeyDown(e.Key, e.KeyModifiers) == true;
    }

    private void MainWindow_OnOpened(object? sender, EventArgs e)
    {
        var startupArguments = Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Args ?? [] : [];
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

        _ = _mcpServerHost?.ApplySettingsAsync();
    }

    private void TitleMenuView_OnBeginGuideRequested(object? sender, EventArgs e) => BeginOnboardingGuide();

    /// <summary>原型把布局切换入口放在状态栏中央（原第二行工具栏已并入标题栏与状态栏）。</summary>

    public void BeginOnboardingGuide()
    {
        if (DataContext is MainWindowViewModel vm)
            WorkbenchGuide.Start(vm, this, _settingsStore, new Control[] { Workspace.InstantGuideTarget, Workspace.OutlineGuideTarget, Workspace.MindMapGuideTarget, Workspace.EditorOptions, TitleMenuView.FileMenuTarget });
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        Workspace.MindMapGuideTarget.FlushInspector();
        WorkbenchGuide.Finish();
        if (_isCloseConfirmed)
        {
            return;
        }

        if (DataContext is MainWindowViewModel viewModel)
        {
            e.Cancel = true;
            _ = viewModel.BeginWindowCloseAsync();
        }
    }

    private void OnCloseWindowRequested(object? sender, EventArgs e)
    {
        _isCloseConfirmed = true;
        _toastPublisher?.Uninstall();
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

    private void MainWindow_OnClosed(object? sender, EventArgs e)
    {
        _ = _mcpServerHost?.StopAsync();
    }
}
