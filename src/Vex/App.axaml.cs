using System.Globalization;
using Avalonia;
using Avalonia.Markup.Xaml;
using CodeWF.DryIoc.EventBus;
using Lang.Avalonia;
using Lang.Avalonia.Json;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Vex.Core.Services;
using Vex.Modules.Appearance;
using Vex.Modules.Appearance.Services;
using Vex.Modules.Help;
using Vex.Modules.Help.Services;
using Vex.Modules.Shell;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace;
using Vex.Modules.Workspace.Services;

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

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.AddEventBus();
        containerRegistry.RegisterSingleton<IDocumentService, DocumentService>();
        containerRegistry.RegisterSingleton<IMarkdownExportService, MarkdownExportService>();
        containerRegistry.RegisterSingleton<IMarkdownEditorController, MarkdownEditorController>();
        containerRegistry.RegisterSingleton<IMarkdownOutlineService, MarkdownOutlineService>();
        containerRegistry.RegisterSingleton<IMarkdownStatisticsService, MarkdownStatisticsService>();
        containerRegistry.RegisterSingleton<IThemeService, ThemeService>();
        containerRegistry.RegisterSingleton<IHelpService, HelpService>();
        containerRegistry.RegisterSingleton<ShellAppearanceViewModel>();
        containerRegistry.RegisterSingleton<ShellDialogsViewModel>();
        containerRegistry.RegisterSingleton<ShellEditorActionsViewModel>();
        containerRegistry.RegisterSingleton<ShellEditorDisplayViewModel>();
        containerRegistry.RegisterSingleton<ShellFindBarViewModel>();
        containerRegistry.RegisterSingleton<ShellNavigationViewModel>();
        containerRegistry.RegisterSingleton<ShellRecentDocumentsViewModel>();
        containerRegistry.RegisterSingleton<ShellStatusViewModel>();
        containerRegistry.RegisterSingleton<ShellWindowLayoutViewModel>();
        containerRegistry.RegisterSingleton<MainWindowViewModel>();
        containerRegistry.Register<MainWindow>();
    }
}
