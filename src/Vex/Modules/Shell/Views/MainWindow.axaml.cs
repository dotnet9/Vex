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
    private ShellWindowLayoutViewModel? _railLayout;
    public WorkspaceViewController WorkspaceViews { get; private set; } = null!;

    public MainWindow()
    {
        InitializeComponent();
        // 原型 deck 的还原钮语义随档位切换：正文档回进入演示时的字号，导图档适应画布（与主窗体 .map-controls 同款）。
        PresentationDeckDock.ResetRequested += (_, _) =>
        {
            if (DataContext is not MainWindowViewModel shell) return;
            if (shell.Workbench.PresentationMap) MindMapGuideTarget.FitMap();
            else shell.Workbench.ResetPresentationZoom();
        };
        var confirmation = MindMapGuideTarget.DetachConfirmation();
        Grid.SetRowSpan(confirmation, 4); ShellGuideRoot.Children.Add(confirmation);
        WorkspaceViews = new WorkspaceViewController(this, ViewSurface, FloatingViewShelf);
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
        SizeChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) { vm.Layout.IsCompactWindow = Bounds.Width < 1100; vm.Layout.WorkspaceWidth = Bounds.Width; }
        };
        // 原型 rail-item 的 title/aria-label 随开合状态切换（收起文件导航/展开文件导航…），语言切换同步重译。
        DataContextChanged += (_, _) =>
        {
            if (_railLayout is not null) _railLayout.PropertyChanged -= RailLayoutChanged;
            _railLayout = (DataContext as MainWindowViewModel)?.Layout;
            if (_railLayout is not null) _railLayout.PropertyChanged += RailLayoutChanged;
            SyncRailTips();
        };
        CodeWF.Toolkit.Lang.I18nManager.Instance.CultureChanged += (_, _) => SyncRailTips();
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
        RestoreWindowSize(settingsStore);
        DataContext = viewModel;
        viewModel.Workbench.GuideExitRequested += (_, _) => WorkbenchGuide.Finish();
        viewModel.CloseWindowRequested += OnCloseWindowRequested;
        viewModel.CommitPendingEditsRequested += (_, _) => MindMapGuideTarget.FlushInspector();
        Closing += MainWindow_OnClosing;
        Closed += (_, _) => WorkbenchGuide.Finish();
        Opened += MainWindow_OnOpened;
        Closed += MainWindow_OnClosed;
        // 叶纹装饰仅 sage（teal 槽位）配色显示；演示时隐藏。
        viewModel.Appearance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellAppearanceViewModel.SelectedPalette)) UpdateSageLeaf();
        };
        viewModel.Workbench.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.Workbench.IsPresenting)) UpdateSageLeaf();
        };
        UpdateSageLeaf(viewModel);
    }

    private void UpdateSageLeaf(MainWindowViewModel? viewModel = null)
    {
        viewModel ??= DataContext as MainWindowViewModel;
        if (SageLeafDecoration is null || viewModel is null) return;
        SageLeafDecoration.IsVisible = viewModel.Appearance.SelectedPalette?.Key == "teal"
            && !viewModel.Workbench.IsPresenting;
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
        if (vm.Workbench.ShowLayouts && !ancestors.Contains(LayoutGallery)) vm.Workbench.ShowLayouts = false;
        if (vm.Workbench.ShowTypography && !ancestors.Contains(TypographyGallery) && !ancestors.Contains(EditorOptions) && !ancestors.Contains(WorkspaceViews.RenderTypographyTrigger)) vm.Workbench.ShowTypography = false;
    }

    private void WindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key is Key.S or Key.W or Key.O or Key.N)
            MindMapGuideTarget.FlushInspector();
        if (e.Source is Visual dialogInput && dialogInput.GetVisualAncestors().Prepend(dialogInput).Any(view => view is ShellInsertTableOverlayView)) return;
        // 引导按键优先：演示中的 Esc/翻页等在引导第 5 幕期间由引导接管。
        if (WorkbenchGuide.IsVisible && WorkbenchGuide.HandleKey(e)) { e.Handled = true; return; }
        if (DataContext is MainWindowViewModel presentationVm && presentationVm.Workbench.IsPresenting)
        {
            if (presentationVm.Workbench.PresentationMap && e.Key is Key.Add or Key.OemPlus or Key.Subtract or Key.OemMinus)
            { MindMapGuideTarget.ZoomPresentation(e.Key is Key.Add or Key.OemPlus); e.Handled = true; }
            else if (e.KeyModifiers == KeyModifiers.None) e.Handled = presentationVm.Workbench.HandlePresentationKey(e.Key);
            return;
        }
        if (e.Key == Key.F6 && e.KeyModifiers == KeyModifiers.None && DataContext is MainWindowViewModel startVm)
        { startVm.Workbench.StartPresentation(); e.Handled = true; return; }
        if (e.Key == Key.Escape && MindMapGuideTarget.HandleEscape()) { e.Handled = true; return; }
        if (RouteWorkspaceHistory(e)) return;
        var editingMapTitle = e.Source is Visual input && input.GetVisualAncestors().Prepend(input).Any(item => item is TextBox)
            && input.GetVisualAncestors().Any(item => item is Vex.Modules.Workspace.Views.MarkdownMindMapHost);
        if (e.Key == Key.Escape && !editingMapTitle && DataContext is MainWindowViewModel vm && (vm.Workbench.ShowTypography || vm.Workbench.ShowLayouts || vm.Workbench.ShowMapSettings || vm.Workbench.HasSelectedNode))
        {
            var typography = vm.Workbench.ShowTypography;
            vm.Workbench.ShowLayouts = false; vm.Workbench.ShowTypography = false; vm.Workbench.ShowMapSettings = false; vm.Workbench.CloseInspector();
            if (typography) EditorOptions.FindControl<Button>("TypographyTarget")?.Focus();
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
        if (e.Key == Key.Escape && MindMapGuideTarget.HandleEscape()) { e.Handled = true; return; }
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

    private void RailLayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellWindowLayoutViewModel.ShowSidebar) or nameof(ShellWindowLayoutViewModel.ShowOutlineSidebar)) SyncRailTips();
    }

    private void SyncRailTips()
    {
        if (_railLayout is null) return;
        // 语言切换可能从工作线程触发（验证夹具即在后台线程改 Culture），控件访问必须回 UI 线程。
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(SyncRailTips); return; }
        ToolTipHelper.SetText(RailFilesToggle, DesignSelectionControls.Text(_railLayout.ShowSidebar ? "NavFilesCollapse" : "NavFilesExpand"));
        ToolTipHelper.SetText(RailOutlineToggle, DesignSelectionControls.Text(_railLayout.ShowOutlineSidebar ? "NavOutlineCollapse" : "NavOutlineExpand"));
    }

    /// <summary>原型把布局切换入口放在状态栏中央（原第二行工具栏已并入标题栏与状态栏）。</summary>

    public void BeginOnboardingGuide()
    {
        if (DataContext is MainWindowViewModel vm)
            WorkbenchGuide.Start(vm, this, _settingsStore, new Control[] { InstantGuideTarget, OutlineGuideTarget, MindMapGuideTarget, EditorOptions, TitleMenuView.FileMenuTarget });
    }

    private void MainWindow_OnClosing(object? sender, WindowClosingEventArgs e)
    {
        MindMapGuideTarget.FlushInspector();
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
