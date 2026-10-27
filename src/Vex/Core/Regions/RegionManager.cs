using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace Vex.Core.Regions;

/// <summary>区域管理器实现</summary>
public class RegionManager : IRegionManager
{
    private readonly Dictionary<string, IRegion> _regions = new();
    private readonly Dictionary<string, List<Type>> _registeredViews = new();
    private readonly IServiceProvider _serviceProvider;

    public RegionManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void RegisterViewWithRegion<TView>(string regionName) where TView : Control
    {
        if (!_registeredViews.ContainsKey(regionName))
        {
            _registeredViews[regionName] = new List<Type>();
        }

        var viewType = typeof(TView);
        if (!_registeredViews[regionName].Contains(viewType))
        {
            _registeredViews[regionName].Add(viewType);
        }

        // 如果区域已创建，立即添加视图
        if (_regions.TryGetValue(regionName, out var region))
        {
            var view = (Control)_serviceProvider.GetRequiredService(viewType);
            region.AddView(view);
        }
    }

    public IRegion GetOrCreateRegion(string regionName)
    {
        if (_regions.TryGetValue(regionName, out var region))
        {
            return region;
        }

        // 创建新区域
        region = new Region(regionName);
        _regions[regionName] = region;

        // 添加已注册的视图
        if (_registeredViews.TryGetValue(regionName, out var viewTypes))
        {
            foreach (var viewType in viewTypes)
            {
                var view = (Control)_serviceProvider.GetRequiredService(viewType);
                region.AddView(view);
            }
        }

        return region;
    }
}
