using Microsoft.Extensions.DependencyInjection;

namespace Vex.Core.Modularity;

/// <summary>应用模块接口</summary>
public interface IAppModule
{
    /// <summary>注册模块服务到 DI 容器</summary>
    void RegisterServices(IServiceCollection services);

    /// <summary>初始化模块（在所有服务注册完成后调用）</summary>
    void Initialize(IServiceProvider serviceProvider);
}
