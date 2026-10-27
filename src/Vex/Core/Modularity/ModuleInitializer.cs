using Microsoft.Extensions.DependencyInjection;

namespace Vex.Core.Modularity;

/// <summary>模块初始化辅助类</summary>
public static class ModuleInitializer
{
    /// <summary>注册所有模块的服务</summary>
    public static void RegisterModules(IServiceCollection services, params IAppModule[] modules)
    {
        foreach (var module in modules)
        {
            module.RegisterServices(services);
        }
    }

    /// <summary>初始化所有模块</summary>
    public static void InitializeModules(IServiceProvider serviceProvider, params IAppModule[] modules)
    {
        foreach (var module in modules)
        {
            module.Initialize(serviceProvider);
        }
    }
}
