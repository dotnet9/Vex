using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Vex.Core.Regions;

/// <summary>RegionManager 的 XAML 附加属性</summary>
public static class RegionManagerExtensions
{
    public static readonly AttachedProperty<string?> RegionNameProperty =
        AvaloniaProperty.RegisterAttached<ContentControl, string?>(
            "RegionName",
            typeof(RegionManagerExtensions));

    public static string? GetRegionName(ContentControl element)
    {
        return element.GetValue(RegionNameProperty);
    }

    public static void SetRegionName(ContentControl element, string? value)
    {
        element.SetValue(RegionNameProperty, value);
    }

    static RegionManagerExtensions()
    {
        RegionNameProperty.Changed.AddClassHandler<ContentControl>(OnRegionNameChanged);
    }

    private static void OnRegionNameChanged(ContentControl control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string regionName) return;

        // 获取服务提供者
        if (App.Instance?.ServiceProvider is not { } serviceProvider) return;

        var regionManager = serviceProvider.GetService<IRegionManager>();
        if (regionManager == null) return;

        var region = regionManager.GetOrCreateRegion(regionName);

        // 初始绑定
        control.Content = region.ActiveView;

        // 监听 ActiveView 变化
        region.PropertyChanged += OnRegionActiveViewChanged;

        void OnRegionActiveViewChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(IRegion.ActiveView))
            {
                control.Content = region.ActiveView;
            }
        }

        // 清理
        control.DetachedFromVisualTree += (_, _) =>
        {
            region.PropertyChanged -= OnRegionActiveViewChanged;
        };
    }
}
