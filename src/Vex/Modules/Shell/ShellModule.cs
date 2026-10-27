using Microsoft.Extensions.DependencyInjection;
using Vex.Core.Modularity;
using Vex.Core.Regions;
using Vex.Modules.Shell.Views;

namespace Vex.Modules.Shell;

public sealed class ShellModule : IAppModule
{
    public void RegisterServices(IServiceCollection services)
    {
        // 目前为空，保留用于未来扩展
    }

    public void Initialize(IServiceProvider serviceProvider)
    {
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        regionManager.RegisterViewWithRegion<ShellFilesView>(RegionNames.ShellSidebarRegion);
        regionManager.RegisterViewWithRegion<ShellOutlineView>(RegionNames.ShellOutlineRegion);
    }
}
