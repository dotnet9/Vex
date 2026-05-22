using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeWF.DryIoc.EventBus;
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
        AvaloniaXamlLoader.Load(this);
        var langPlugin = new JsonLangPlugin
        {
            ResourceFolder = Path.Combine(AppContext.BaseDirectory, "I18n")
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
        return Container.Resolve<MainWindow>();
    }

    protected override void ConfigureRegionAdapterMappings(RegionAdapterMappings regionAdapterMappings)
    {
        base.ConfigureRegionAdapterMappings(regionAdapterMappings);
        regionAdapterMappings.RegisterMapping(typeof(TabControl), Container.Resolve<TabControlRegionAdapter>());
    }

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.AddEventBus();
        containerRegistry.RegisterSingleton<IAppLocalizer, AppLocalizer>();
        containerRegistry.RegisterSingleton<IDocumentFileFactory, DocumentFileFactory>();
        containerRegistry.RegisterSingleton<IDocumentService, DocumentService>();
        containerRegistry.RegisterSingleton<IEditorAppearanceState, EditorAppearanceState>();
        containerRegistry.RegisterSingleton<IWorkspaceDocumentState, WorkspaceDocumentState>();
        containerRegistry.RegisterSingleton<IMarkdownExportService, MarkdownExportService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorMutationService, MarkdownEditorMutationService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorSearchService, MarkdownEditorSearchService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorController, MarkdownEditorController>();
        containerRegistry.RegisterSingleton<IMarkdownOutlineService, MarkdownOutlineService>();
        containerRegistry.RegisterSingleton<IMarkdownStatisticsService, MarkdownStatisticsService>();
        containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<IRecentDocumentStore, RecentDocumentStore>();
        containerRegistry.RegisterSingleton<IShellStatusPublisher, ShellStatusPublisher>();
        containerRegistry.RegisterSingleton<IShellDocumentWorkflowText, ShellDocumentWorkflowText>();
        containerRegistry.RegisterSingleton<IShellUnsavedChangesGuard, ShellUnsavedChangesGuard>();
        containerRegistry.RegisterSingleton<IShellDocumentUtilityActions, ShellDocumentUtilityActions>();
        containerRegistry.RegisterSingleton<IShellExternalPathResolver, ShellExternalPathResolver>();
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
        containerRegistry.RegisterSingleton<MarkdownPreviewViewModel>();
        containerRegistry.RegisterSingleton<ShellActionCoordinator>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }
}
