using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using CodeWF.Toolkit.Core.UpdateChecking;
using Microsoft.Extensions.DependencyInjection;
using Vex.Core.Modularity;
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

public partial class App : Application
{
    public static App? Instance { get; private set; }
    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public override void Initialize()
    {
        DefaultFileOpeningService.Configure();
        CodeWF.Avalonia.Markdown.Highlighting.CodeWFMarkdownHighlightingExtensions.UseHighlighting();
        Modules.Workspace.Views.VexMarkdownRendering.Register();
        // 数学渲染：库内已内置 Avalonia 12 兼容画布，修复了官方 CSharpMath.Avalonia 12.0.0
        // 在 Avalonia 12.1.3 下只剩分数线、字形全部缺失的问题。
        CodeWF.Avalonia.Markdown.MathRendering.CodeWFMarkdownMathExtensions.UseMath();
        CodeWF.Avalonia.Markdown.Mermaid.CodeWFMarkdownMermaidExtensions.EnsureRegistered();
        CodeWF.Avalonia.Markdown.CodeWFMarkdownHtmlExtensions.UseHtmlTables();
        AvaloniaXamlLoader.Load(this);
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = ResolveI18nFolder()
        };
        I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);
        I18nManager.Instance.CultureChanged += OnCultureChanged;
        ApplyControlCulture();

        // 配置 DI 容器
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // 初始化模块
        InitializeModules();

        base.Initialize();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 注册区域管理器
        services.AddSingleton<IRegionManager, RegionManager>();

        // 注册核心服务
        services.AddSingleton<IUpdateService, VexUpdateService>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        services.AddSingleton<EditorPreferences>();
        services.AddSingleton<ShellPreferencesViewModel>();
        services.AddSingleton<IAppLocalizer, AppLocalizer>();
        services.AddSingleton<IDocumentFileFactory, DocumentFileFactory>();
        services.AddSingleton<IWorkspaceFileService, WorkspaceFileService>();
        services.AddSingleton<IDocumentService, DocumentService>();
        services.AddSingleton<IEditorAppearanceState, EditorAppearanceState>();
        services.AddSingleton<IWorkspaceDocumentState, WorkspaceDocumentState>();
        services.AddSingleton<IMarkdownExportService, MarkdownExportService>();
        // 编辑器使用库内嵌文案并自动跟随全局语言，Vex 仅保留 HTML 粘贴转换适配。
        services.AddSingleton<CodeWF.Avalonia.Markdown.Editor.Services.IMarkdownEditorLocalizer, CodeWF.Avalonia.Markdown.Editor.Services.MarkdownEditorLocalizer>();
        services.AddSingleton<CodeWF.Avalonia.Markdown.Editor.Services.IMarkdownHtmlPasteConverter, VexMarkdownHtmlPasteConverter>();
        services.AddSingleton<IMarkdownOutlineService, MarkdownOutlineService>();
        services.AddSingleton<IMarkdownStatisticsService, MarkdownStatisticsService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IHelpService, HelpService>();
        services.AddSingleton<IDocumentWindowLauncher, ShellProcessLauncher>();
        services.AddSingleton<IRecentDocumentStore, RecentDocumentStore>();
        services.AddSingleton<IShellStatusPublisher, ShellStatusPublisher>();
        services.AddSingleton<IShellOverlayService, ShellOverlayService>();
        services.AddSingleton<IShellToastPublisher, ShellToastPublisher>();
        services.AddSingleton<IShellDocumentWorkflowText, ShellDocumentWorkflowText>();
        services.AddSingleton<IEditorDisplayState, EditorDisplayState>();
        services.AddSingleton<IShellUnsavedChangesGuard, ShellUnsavedChangesGuard>();
        services.AddSingleton<IShellDocumentUtilityActions, ShellDocumentUtilityActions>();
        services.AddSingleton<IShellExternalPathResolver, ShellExternalPathResolver>();
        services.AddSingleton<IAutoSaveDraftService, AutoSaveDraftService>();
        services.AddSingleton<IShellDroppedPathReader, ShellDroppedPathReader>();
        services.AddSingleton<IShellDropTargetHandler, ShellDropTargetHandler>();
        services.AddSingleton<IShellStartupArgumentPublisher, ShellStartupArgumentPublisher>();
        services.AddSingleton<IMcpToolDispatcher, McpToolDispatcher>();
        services.AddSingleton<IMcpOperationConfirmationService, McpOperationConfirmationService>();
        services.AddSingleton<IMcpOperationAuditService, McpOperationAuditService>();
        services.AddSingleton<IMcpServerHost, McpServerHost>();

        // 注册 ViewModels
        services.AddSingleton<ShellAppearanceViewModel>();
        services.AddSingleton<ShellDocumentInfoViewModel>();
        services.AddSingleton<ShellDialogsViewModel>();
        services.AddSingleton<ShellEditorActionsViewModel>();
        services.AddSingleton<ShellEditorDisplayViewModel>();
        services.AddSingleton<ShellFilesViewModel>();
        services.AddSingleton<ShellFindBarViewModel>();
        services.AddSingleton<ShellHelpViewModel>();
        services.AddSingleton<ShellKeyboardShortcutViewModel>();
        services.AddSingleton<ShellNavigationViewModel>();
        services.AddSingleton<ShellOutlineViewModel>();
        services.AddSingleton<ShellRecentDocumentsViewModel>();
        services.AddSingleton<ShellStatusBarViewModel>();
        services.AddSingleton<ShellStatusViewModel>();
        services.AddSingleton<ShellTitleMenuViewModel>();
        services.AddSingleton<ShellWindowLayoutViewModel>();
        services.AddSingleton<ShellWorkbenchViewModel>();
        services.AddSingleton<McpSettingsViewModel>();
        services.AddSingleton<MarkdownEditorViewModel>();
        services.AddSingleton<MarkdownPreviewViewModel>();
        services.AddSingleton<ShellActionCoordinator>();
        services.AddSingleton<MainWindowViewModel>();

        // 注册 Views
        services.AddTransient<MainWindow>();
        services.AddTransient<ShellFilesView>();
        services.AddTransient<ShellOutlineView>();

        // 注册模块服务
        ModuleInitializer.RegisterModules(services,
            new ShellModule(),
            new WorkspaceModule(),
            new AppearanceModule(),
            new HelpModule());
    }

    private void InitializeModules()
    {
        ModuleInitializer.InitializeModules(ServiceProvider,
            new ShellModule(),
            new WorkspaceModule(),
            new AppearanceModule(),
            new HelpModule());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Instance = this;
            ApplyInitialTheme();
            desktop.MainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 在解析主窗口前先应用保存的主题：默认变体交给系统，避免任何早于
    /// ShellAppearanceViewModel 构造的 UI 以错误的固定变体渲染。
    /// </summary>
    private void ApplyInitialTheme()
    {
        var settings = ServiceProvider.GetRequiredService<IAppSettingsStore>().Current;
        var themeService = ServiceProvider.GetRequiredService<IThemeService>();
        if (themeService is Vex.Modules.Appearance.Services.ThemeService concrete) concrete.LoadCustomPalettes(settings.CustomPalettes);
        ServiceProvider.GetRequiredService<Vex.Modules.Shell.ViewModels.ShellAppearanceViewModel>().LoadCustomTypographies(settings.CustomTypographies);
        var options = themeService.GetThemeOptions();
        var option = options.FirstOrDefault(item => item.Key.Equals(settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
                     ?? options.First(item => item.Key.Equals("system", StringComparison.OrdinalIgnoreCase));
        var palette = themeService.GetPaletteOptions().FirstOrDefault(item =>
            item.Key.Equals(settings.PaletteKey ?? settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
            ?? themeService.GetPaletteOptions().FirstOrDefault(item => item.Key == "indigo")
            ?? themeService.GetPaletteOptions()[0];
        themeService.ApplyTheme(option, palette);
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
    private static void OnCultureChanged(object? sender, EventArgs args)
    {
        if (Dispatcher.UIThread.CheckAccess()) ApplyControlCulture();
        else Dispatcher.UIThread.Post(ApplyControlCulture);
    }

    /// <summary>更新主题内置文案，保留宿主资源的同键覆盖。</summary>
    private static void ApplyControlCulture()
    {
        if (Application.Current is not { } application || I18nManager.Instance.Culture is not { } culture) return;
        // Semi 提供繁体字典，但使用地区名称 zh-TW；Ursa 当前包没有日语和繁体字典。
        var traditionalChinese = culture.Name.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase);
        var semiCulture = traditionalChinese ? CultureInfo.GetCultureInfo("zh-TW") : culture;
        var ursaCulture = traditionalChinese ? CultureInfo.GetCultureInfo("zh-CN")
            : culture.TwoLetterISOLanguageName == "ja" ? CultureInfo.GetCultureInfo("en-US") : culture;
        foreach (var theme in application.Styles.OfType<Semi.Avalonia.SemiTheme>()) theme.Locale = semiCulture;
        foreach (var theme in application.Styles.OfType<Ursa.Themes.Semi.UrsaSemiTheme>()) theme.Locale = ursaCulture;
    }

}
