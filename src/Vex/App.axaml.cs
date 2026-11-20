using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using CodeWF.Toolkit.Core.UpdateChecking;
using Microsoft.Extensions.DependencyInjection;
using Vex.Core.Services;
using Vex.Core.Theme;
using Vex.Modules.Appearance.Services;
using Vex.Modules.Help.Services;
using Vex.Modules.Mcp.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Services;
using Vex.Modules.Workspace.ViewModels;

namespace Vex;

public partial class App : Application
{
    public static App Instance { get; protected set; } = null!;

    /// <summary>应用容器。视图构造函数通过它自取 ViewModel，验证工程通过它换入测试替身。</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        DefaultFileOpeningService.Configure();
        CodeWF.Avalonia.Markdown.Highlighting.CodeWFMarkdownHighlightingExtensions.UseHighlighting(
            "csharp", "json", "xml", "bash", "powershell", "typescript", "markdown", "yaml");
        Modules.Workspace.Views.VexMarkdownRendering.Register();
        // 数学渲染：库内已内置 Avalonia 12 兼容画布，修复了官方 CSharpMath.Avalonia 12.0.0
        // 在 Avalonia 12.1.3 下只剩分数线、字形全部缺失的问题。
        CodeWF.Avalonia.Markdown.MathRendering.CodeWFMarkdownMathExtensions.UseMath();
        CodeWF.Avalonia.Markdown.Mermaid.CodeWFMarkdownMermaidExtensions.EnsureRegistered();
        CodeWF.Avalonia.Markdown.CodeWFMarkdownHtmlExtensions.UseHtmlTables();
        AvaloniaXamlLoader.Load(this);
        var langPlugin = new JsonLangPlugin();
        I18nManager.Instance.Register(langPlugin, new CultureInfo("zh-CN"), out _);
        // 本地化 json 内嵌在各程序集（Vex 主工程 + Vex.Core + 自研库 Markdown），AddResource 扫嵌入资源合并键池；
        // I18n 标记扩展走全局池查键，库程序集不入池则库内 axaml 的键会回退成键名原文。
        // 宿主输出目录文件仍可覆盖（ApplyFileOverrides 优先级最高），无目录时纯嵌入生效。
        I18nManager.Instance.AddResource(typeof(App).Assembly, typeof(Vex.Core.Messaging.ShellActionKind).Assembly,
            typeof(CodeWF.Avalonia.Markdown.MarkdownLocalization).Assembly);
        I18nManager.Instance.CultureChanged += OnCultureChanged;
        ApplyControlCulture();

        var services = new ServiceCollection();
        ConfigureServices(services);
        OnConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    /// <summary>宿主追加/替换注册的钩子；后注册者获胜，验证工程用它换入测试替身。</summary>
    protected virtual void OnConfigureServices(IServiceCollection services)
    {
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 核心服务（原 Prism 注册表平移，生命周期保持单例）。
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
        // Git 管理界面：凭据库按平台选择（Windows 凭据管理器/加密文件），面板 VM 单例供 rail 双模式共用。
        services.AddSingleton<Vex.Git.ICredentialStore>(_ => Vex.Git.CredentialStoreFactory.Create());
        services.AddSingleton<Vex.Modules.Git.ViewModels.ShellGitViewModel>();
        services.AddSingleton<IMcpToolDispatcher, McpToolDispatcher>();
        services.AddSingleton<IMcpOperationConfirmationService, McpOperationConfirmationService>();
        services.AddSingleton<IMcpOperationAuditService, McpOperationAuditService>();
        services.AddSingleton<IMcpServerHost, McpServerHost>();

        // ViewModel：原 ViewModelLocator 反射装配改为容器单例 + 视图构造自取。
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
        services.AddSingleton(sp => new ShellOutlineViewModel(sp.GetRequiredService<IShellStatusPublisher>(), sp.GetRequiredService<ShellWindowLayoutViewModel>(), sp.GetRequiredService<IAppSettingsStore>(), sp.GetRequiredService<IWorkspaceDocumentState>()));
        services.AddSingleton<ShellRecentDocumentsViewModel>();
        services.AddSingleton<ShellDocumentToolsViewModel>();
        services.AddSingleton<ShellTitleMenuViewModel>();
        services.AddSingleton(sp => new Vex.Modules.Templates.Services.TemplateCatalog(
            Vex.Modules.Templates.Services.TemplateCatalog.ResolveBuiltinRoot(),
            () => sp.GetRequiredService<Vex.Core.Services.IAppSettingsStore>().Current.TemplatesPath));
        services.AddSingleton<ShellWindowLayoutViewModel>();
        services.AddSingleton<ShellWorkbenchViewModel>();
        services.AddSingleton<McpSettingsViewModel>();
        services.AddSingleton<MarkdownEditorViewModel>();
        services.AddSingleton<MarkdownPreviewViewModel>();
        services.AddSingleton<ShellActionCoordinator>();
        services.AddSingleton<MainWindowViewModel>();

        // 主窗口：完整构造函数由容器自动选中，服务随构造注入。
        services.AddSingleton<MainWindow>();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Instance = this;
            ApplyInitialTheme();
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
            desktop.MainWindow.Show();
        }
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 在解析主窗口前先应用保存的主题：默认变体交给系统，避免任何早于
    /// ShellAppearanceViewModel 构造的 UI 以错误的固定变体渲染。
    /// </summary>
    protected static void ApplyInitialTheme()
    {
        var settings = Services.GetRequiredService<IAppSettingsStore>().Current;
        var themeService = Services.GetRequiredService<IThemeService>();
        if (themeService is Vex.Modules.Appearance.Services.ThemeService concrete) concrete.LoadCustomPalettes(settings.CustomPalettes);
        Services.GetRequiredService<ShellAppearanceViewModel>().LoadCustomTypographies(settings.CustomTypographies);
        var options = themeService.GetThemeOptions();
        var option = options.FirstOrDefault(item => item.Key.Equals(settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
                     ?? options.First(item => item.Key.Equals("system", StringComparison.OrdinalIgnoreCase));
        var palette = themeService.GetPaletteOptions().FirstOrDefault(item =>
            item.Key.Equals(settings.PaletteKey ?? settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
            // 默认配色与品牌绿同族（青碧 teal），靛蓝不再是启动默认。
            ?? themeService.GetPaletteOptions().FirstOrDefault(item => item.Key == "teal")
            ?? themeService.GetPaletteOptions()[0];
        themeService.ApplyTheme(option, palette);
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
