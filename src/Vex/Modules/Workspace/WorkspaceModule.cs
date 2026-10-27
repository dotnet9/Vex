using Microsoft.Extensions.DependencyInjection;
using Vex.Core.Modularity;

namespace Vex.Modules.Workspace;

public sealed class WorkspaceModule : IAppModule
{
    public void RegisterServices(IServiceCollection services)
    {
        // 目前为空，保留用于未来扩展
    }

    public void Initialize(IServiceProvider serviceProvider)
    {
        // 目前为空，保留用于未来扩展
    }
}
