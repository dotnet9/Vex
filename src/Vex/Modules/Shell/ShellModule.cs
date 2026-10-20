using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using Vex.Core.Regions;
using Vex.Modules.Shell.Views;

namespace Vex.Modules.Shell;

public sealed class ShellModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var regionManager = containerProvider.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion<ShellFilesView>(RegionNames.ShellSidebarRegion);
        regionManager.RegisterViewWithRegion<ShellOutlineView>(RegionNames.ShellOutlineRegion);
    }
}
