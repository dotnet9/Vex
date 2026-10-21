using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using CodeWF.Toolkit.Core.UpdateChecking;
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
        var palette = themeService.GetPaletteOptions().FirstOrDefault(item =>
            item.Key.Equals(settings.PaletteKey ?? settings.ThemeKey, StringComparison.OrdinalIgnoreCase))
            ?? themeService.GetPaletteOptions()[0];
        themeService.ApplyTheme(option, palette);
    }

    protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
    {
        base.ConfigureRegionAdapterMappings(regionAdapterMappings);
        regionAdapterMappings.RegisterMapping(typeof(TabControl), Container.Resolve<TabControlRegionAdapter>());
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IUpdateService, VexUpdateService>();
        containerRegistry.RegisterSingleton<IAppSettingsStore, AppSettingsStore>();
        containerRegistry.RegisterSingleton<EditorPreferences>();
        containerRegistry.RegisterSingleton<ShellPreferencesViewModel>();
        containerRegistry.RegisterSingleton<IAppLocalizer, AppLocalizer>();
        containerRegistry.RegisterSingleton<IDocumentFileFactory, DocumentFileFactory>();
        containerRegistry.RegisterSingleton<IDocumentService, DocumentService>();
        containerRegistry.RegisterSingleton<IEditorAppearanceState, EditorAppearanceState>();
        containerRegistry.RegisterSingleton<IWorkspaceDocumentState, WorkspaceDocumentState>();
        containerRegistry.RegisterSingleton<IMarkdownExportService, MarkdownExportService>();
        // 编辑器使用库内嵌文案并自动跟随全局语言，Vex 仅保留 HTML 粘贴转换适配。
        containerRegistry.RegisterSingleton<CodeWF.Avalonia.Markdown.Editor.Services.IMarkdownEditorLocalizer, CodeWF.Avalonia.Markdown.Editor.Services.MarkdownEditorLocalizer>();
        containerRegistry.RegisterSingleton<CodeWF.Avalonia.Markdown.Editor.Services.IMarkdownHtmlPasteConverter, VexMarkdownHtmlPasteConverter>();
        containerRegistry.RegisterSingleton<IMarkdownOutlineService, MarkdownOutlineService>();
        containerRegistry.RegisterSingleton<IMarkdownStatisticsService, MarkdownStatisticsService>();
        containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<IDocumentWindowLauncher, ShellProcessLauncher>();
        containerRegistry.RegisterSingleton<IRecentDocumentStore, RecentDocumentStore>();
        containerRegistry.RegisterSingleton<IShellStatusPublisher, ShellStatusPublisher>();
        containerRegistry.RegisterSingleton<IShellOverlayService, ShellOverlayService>();
        containerRegistry.RegisterSingleton<IShellToastPublisher, ShellToastPublisher>();
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
        containerRegistry.RegisterSingleton<ShellWorkbenchViewModel>();
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
