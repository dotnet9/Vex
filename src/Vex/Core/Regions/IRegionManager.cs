using Avalonia.Controls;

namespace Vex.Core.Regions;

/// <summary>区域管理器接口</summary>
public interface IRegionManager
{
    /// <summary>注册视图类型到指定区域（延迟创建）</summary>
    void RegisterViewWithRegion<TView>(string regionName) where TView : Control;

    /// <summary>获取或创建区域</summary>
    IRegion GetOrCreateRegion(string regionName);
}
