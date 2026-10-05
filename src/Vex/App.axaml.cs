using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using Vex.Core.Regions;
using Vex.Core.Services;
using Vex.Modules.Appearance;
using Vex.Modules.Appearance.Services;
using Vex.Modules.Help;
using Vex.Modules.Help.Services;
using Vex.Modules.Mcp.Services;
using Vex.Modules.Shell;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace;
using Vex.Modules.Workspace.Services;
using Vex.Modules.Workspace.ViewModels;

namespace Vex;

public partial class App : PrismApplication
{
    public static App Instance { get; private set; } = null!;

    public override void Initialize()
    {
        DefaultFileOpeningService.Configure();
        AvaloniaXamlLoader.Load(this);
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = ResolveI18nFolder()
        };
        I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);
        base.Initialize();
    }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<ShellModule>();
        moduleCatalog.AddModule<WorkspaceModule>();
        moduleCatalog.AddModule<AppearanceModule>();
        moduleCatalog.AddModule<HelpModule>();
        base.ConfigureModuleCatalog(moduleCatalog);
    }

    protected override AvaloniaObject CreateShell()
    {
        Instance = this;
        ApplyInitialTheme();
        return Container.Resolve<MainWindow>();
    }

    /// <summary>
    /// 在解析主窗口前先应用保存的主题：默认变体交给系统，避免任何早于
    /// ShellAppearanceViewModel 构造的 UI 以错误的固定变体渲染。
    /// </summary>
    private void ApplyInitialTheme()
    {
        var settings = Container.Resolve<IAppSettingsStore>().Current;
        var themeService = Container.Resolve<IThemeService>();
        var options = themeService.GetThemeOptions();
        var option = options.FirstOrDefault(item => item.Key.Equals(settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
                     ?? options.First(item => item.Key.Equals("system", StringComparison.OrdinalIgnoreCase));
        themeService.ApplyTheme(option);
    }

    protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
    {
        base.ConfigureRegionAdapterMappings(regionAdapterMappings);
        regionAdapterMappings.RegisterMapping(typeof(TabControl), Container.Resolve<TabControlRegionAdapter>());
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IAppSettingsStore, AppSettingsStore>();
        containerRegistry.RegisterSingleton<IAppLocalizer, AppLocalizer>();
        containerRegistry.RegisterSingleton<IDocumentFileFactory, DocumentFileFactory>();
        containerRegistry.RegisterSingleton<IDocumentService, DocumentService>();
        containerRegistry.RegisterSingleton<IEditorAppearanceState, EditorAppearanceState>();
        containerRegistry.RegisterSingleton<IWorkspaceDocumentState, WorkspaceDocumentState>();
        containerRegistry.RegisterSingleton<IMarkdownExportService, MarkdownExportService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorTemplateService, MarkdownEditorTemplateService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorMutationService, MarkdownEditorMutationService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorActionService, MarkdownEditorActionService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorSearchService, MarkdownEditorSearchService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorController, MarkdownEditorController>();
        containerRegistry.RegisterSingleton<IMarkdownOutlineService, MarkdownOutlineService>();
        containerRegistry.RegisterSingleton<IMarkdownStatisticsService, MarkdownStatisticsService>();
        containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<IRecentDocumentStore, RecentDocumentStore>();
        containerRegistry.RegisterSingleton<IShellStatusPublisher, ShellStatusPublisher>();
        containerRegistry.RegisterSingleton<IShellDocumentWorkflowText, ShellDocumentWorkflowText>();
        containerRegistry.RegisterSingleton<IEditorDisplayState, EditorDisplayState>();
        containerRegistry.RegisterSingleton<IShellUnsavedChangesGuard, ShellUnsavedChangesGuard>();
        containerRegistry.RegisterSingleton<IShellDocumentUtilityActions, ShellDocumentUtilityActions>();
        containerRegistry.RegisterSingleton<IShellExternalPathResolver, ShellExternalPathResolver>();
        containerRegistry.RegisterSingleton<IAutoSaveDraftService, AutoSaveDraftService>();
        containerRegistry.RegisterSingleton<IShellDroppedPathReader, ShellDroppedPathReader>();
        containerRegistry.RegisterSingleton<IShellDropTargetHandler, ShellDropTargetHandler>();
        containerRegistry.RegisterSingleton<IShellStartupArgumentPublisher, ShellStartupArgumentPublisher>();
        containerRegistry.RegisterSingleton<IMcpToolDispatcher, McpToolDispatcher>();
        containerRegistry.RegisterSingleton<IMcpOperationConfirmationService, McpOperationConfirmationService>();
        containerRegistry.RegisterSingleton<IMcpOperationAuditService, McpOperationAuditService>();
        containerRegistry.RegisterSingleton<IMcpServerHost, McpServerHost>();
        containerRegistry.RegisterSingleton<ShellAppearanceViewModel>();
        containerRegistry.RegisterSingleton<ShellDocumentInfoViewModel>();
        containerRegistry.RegisterSingleton<ShellDialogsViewModel>();
        containerRegistry.RegisterSingleton<ShellEditorActionsViewModel>();
        containerRegistry.RegisterSingleton<ShellEditorDisplayViewModel>();
        containerRegistry.RegisterSingleton<ShellFilesViewModel>();
        containerRegistry.RegisterSingleton<ShellFindBarViewModel>();
        containerRegistry.RegisterSingleton<ShellHelpViewModel>();
        containerRegistry.RegisterSingleton<ShellKeyboardShortcutViewModel>();
        containerRegistry.RegisterSingleton<ShellNavigationViewModel>();
        containerRegistry.RegisterSingleton<ShellOutlineViewModel>();
        containerRegistry.RegisterSingleton<ShellRecentDocumentsViewModel>();
        containerRegistry.RegisterSingleton<ShellStatusBarViewModel>();
        containerRegistry.RegisterSingleton<ShellStatusViewModel>();
        containerRegistry.RegisterSingleton<ShellTitleMenuViewModel>();
        containerRegistry.RegisterSingleton<ShellWindowLayoutViewModel>();
        containerRegistry.RegisterSingleton<McpSettingsViewModel>();
        containerRegistry.RegisterSingleton<MarkdownEditorViewModel>();
        containerRegistry.RegisterSingleton<MarkdownPreviewViewModel>();
        containerRegistry.RegisterSingleton<ShellActionCoordinator>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }

    /// <summary>
    /// 语言包目录：常规部署在 BaseDirectory/I18n；macOS .app 打包会把
    /// 可执行文件以外的内容挪进 Contents/Resources，这里按存在性探测。
    /// </summary>
    private static string ResolveI18nFolder()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDirectory, "I18n"),
            Path.Combine(baseDirectory, "..", "Resources", "I18n"),
        };
        return candidates.FirstOrDefault(Directory.Exists)
            ?? Path.Combine(baseDirectory, "I18n");

}
}