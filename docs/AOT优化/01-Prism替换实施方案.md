# Prism 替换实施方案

## 📋 项目概述

**目标**：移除 Prism.Avalonia 依赖，实现 Native AOT 编译支持

**策略**：全部采用方案 A（保留架构，自实现轻量级版本）

**预估工作量**：2-3 天

---

## 📊 影响范围分析

### 受影响文件统计

| 分类 | 文件数 | 影响程度 |
|------|--------|----------|
| 核心启动 | 1 | 重大 |
| 模块定义 | 4 | 中等 |
| 区域管理 | 3 | 中等 |
| XAML 视图 | 2 | 轻微 |
| ViewModel | 2 | 轻微 |
| **总计** | **12** | |

### 具体文件清单

#### 1. 核心启动文件（1个）
- `src/Vex/App.axaml.cs` ⭐⭐⭐⭐⭐
  - 继承 `PrismApplication` → 改为 `Application`
  - 注册 58 个服务
  - 初始化 4 个模块
  - 配置区域适配器

#### 2. 模块定义文件（4个）
- `src/Vex/Modules/Shell/ShellModule.cs`
- `src/Vex/Modules/Workspace/WorkspaceModule.cs`
- `src/Vex/Modules/Appearance/AppearanceModule.cs`
- `src/Vex/Modules/Help/HelpModule.cs`

每个模块包含：
- `RegisterTypes(IContainerRegistry)` - 服务注册
- `OnInitialized(IContainerProvider)` - 初始化逻辑

#### 3. 区域管理文件（3个）
- `src/Vex/Core/Regions/TabControlRegionAdapter.cs` ⭐⭐⭐
  - 依赖 `IRegionBehaviorFactory`
  - 实现 `RegionAdapterBase<TabControl>`
- `src/Vex/Core/Regions/IRegionTabItem.cs`
  - 标记接口
- `src/Vex/Core/Regions/RegionNames.cs`
  - 常量定义（无需修改）

#### 4. XAML 视图文件（2个）
- `src/Vex/Modules/Shell/Views/MainWindow.axaml`
  ```xml
  <ContentControl prism:RegionManager.RegionName="{x:Static regions:RegionNames.ShellSidebarRegion}" />
  ```
- `src/Vex/Modules/Shell/Views/ShellAuxiliaryPanelView.axaml`
  ```xml
  <ContentControl prism:RegionManager.RegionName="{x:Static regions:RegionNames.ShellOutlineRegion}" />
  ```

#### 5. ViewModel 文件（2个）
- `src/Vex/Modules/Shell/ViewModels/ShellFilesViewModel.cs`
  - 实现 `IRegionTabItem`
- `src/Vex/Modules/Shell/ViewModels/ShellOutlineViewModel.cs`
  - 实现 `IRegionTabItem`

---

## 🎯 替换内容详解

### 任务 1：DI 容器替换

#### 原有实现（Prism + DryIoc）
```csharp
public partial class App : PrismApplication
{
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IUpdateService, VexUpdateService>();
        containerRegistry.RegisterSingleton<IAppSettingsStore, AppSettingsStore>();
        // ... 58 个服务注册
    }
    
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<ShellModule>();
        // ... 4 个模块
    }
}
```

#### 新实现（Microsoft.Extensions.DependencyInjection）
```csharp
public partial class App : Application
{
    private IServiceProvider _serviceProvider = null!;
    
    public override void Initialize()
    {
        var services = new ServiceCollection();
        
        // 注册服务
        services.AddSingleton<IUpdateService, VexUpdateService>();
        services.AddSingleton<IAppSettingsStore, AppSettingsStore>();
        // ... 58 个服务
        
        // 构建服务提供者
        _serviceProvider = services.BuildServiceProvider();
        
        // 初始化模块
        InitializeModules();
        
        AvaloniaXamlLoader.Load(this);
        base.Initialize();
    }
}
```

**修改点**：
- ✅ 移除 `: PrismApplication` 继承
- ✅ 添加 `: Application` 继承
- ✅ 替换所有 `containerRegistry.RegisterSingleton` → `services.AddSingleton`
- ✅ 创建 `IServiceProvider`

---

### 任务 2：模块化系统实现

#### 新接口定义
```csharp
// 新文件：src/Vex/Core/Modularity/IAppModule.cs
namespace Vex.Core.Modularity;

public interface IAppModule
{
    /// <summary>注册模块服务</summary>
    void RegisterServices(IServiceCollection services);
    
    /// <summary>初始化模块</summary>
    void Initialize(IServiceProvider serviceProvider);
}
```

#### 模块初始化器
```csharp
// 新文件：src/Vex/Core/Modularity/ModuleInitializer.cs
namespace Vex.Core.Modularity;

public static class ModuleInitializer
{
    public static void RegisterModules(IServiceCollection services, params IAppModule[] modules)
    {
        foreach (var module in modules)
        {
            module.RegisterServices(services);
        }
    }
    
    public static void InitializeModules(IServiceProvider serviceProvider, params IAppModule[] modules)
    {
        foreach (var module in modules)
        {
            module.Initialize(serviceProvider);
        }
    }
}
```

#### 修改示例（ShellModule）
```csharp
// 修改前
public sealed class ShellModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 目前为空
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var regionManager = containerProvider.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion<ShellFilesView>(RegionNames.ShellSidebarRegion);
        regionManager.RegisterViewWithRegion<ShellOutlineView>(RegionNames.ShellOutlineRegion);
    }
}

// 修改后
public sealed class ShellModule : IAppModule
{
    public void RegisterServices(IServiceCollection services)
    {
        // 目前为空
    }

    public void Initialize(IServiceProvider serviceProvider)
    {
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        regionManager.RegisterViewWithRegion<ShellFilesView>(RegionNames.ShellSidebarRegion);
        regionManager.RegisterViewWithRegion<ShellOutlineView>(RegionNames.ShellOutlineRegion);
    }
}
```

**修改点**：
- ✅ 4 个模块文件
- ✅ `IModule` → `IAppModule`
- ✅ `IContainerRegistry` → `IServiceCollection`
- ✅ `IContainerProvider.Resolve<T>()` → `IServiceProvider.GetRequiredService<T>()`

---

### 任务 3：区域导航系统实现

#### 核心接口

##### IRegionManager
```csharp
// 新文件：src/Vex/Core/Regions/IRegionManager.cs
namespace Vex.Core.Regions;

public interface IRegionManager
{
    /// <summary>注册视图到区域</summary>
    void RegisterViewWithRegion<TView>(string regionName) where TView : Control;
    
    /// <summary>获取或创建区域</summary>
    IRegion GetOrCreateRegion(string regionName);
}
```

##### IRegion
```csharp
// 新文件：src/Vex/Core/Regions/IRegion.cs
namespace Vex.Core.Regions;

public interface IRegion
{
    string Name { get; }
    Control? ActiveView { get; set; }
    IList<Control> Views { get; }
    
    void AddView(Control view);
    void RemoveView(Control view);
}
```

#### 实现类

##### RegionManager
```csharp
// 新文件：src/Vex/Core/Regions/RegionManager.cs
namespace Vex.Core.Regions;

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
        
        _registeredViews[regionName].Add(typeof(TView));
        
        // 如果区域已创建，立即添加视图
        if (_regions.TryGetValue(regionName, out var region))
        {
            var view = (Control)_serviceProvider.GetRequiredService(typeof(TView));
            region.AddView(view);
        }
    }

    public IRegion GetOrCreateRegion(string regionName)
    {
        if (_regions.TryGetValue(regionName, out var region))
        {
            return region;
        }

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
```

##### Region
```csharp
// 新文件：src/Vex/Core/Regions/Region.cs
namespace Vex.Core.Regions;

public class Region : IRegion
{
    public string Name { get; }
    public Control? ActiveView { get; set; }
    public IList<Control> Views { get; } = new List<Control>();

    public Region(string name)
    {
        Name = name;
    }

    public void AddView(Control view)
    {
        if (!Views.Contains(view))
        {
            Views.Add(view);
            if (ActiveView == null)
            {
                ActiveView = view;
            }
        }
    }

    public void RemoveView(Control view)
    {
        Views.Remove(view);
        if (ActiveView == view)
        {
            ActiveView = Views.FirstOrDefault();
        }
    }
}
```

#### 附加属性（XAML 绑定）

```csharp
// 新文件：src/Vex/Core/Regions/RegionManagerExtensions.cs
namespace Vex.Core.Regions;

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
        if (App.Instance?.ServiceProvider is not { } serviceProvider) return;

        var regionManager = serviceProvider.GetService<IRegionManager>();
        if (regionManager == null) return;

        var region = regionManager.GetOrCreateRegion(regionName);
        
        // 绑定区域的 ActiveView 到 ContentControl.Content
        control.Content = region.ActiveView;
        
        // 监听 ActiveView 变化
        region.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(IRegion.ActiveView))
            {
                control.Content = region.ActiveView;
            }
        };
    }
}
```

**修改点**：
- ✅ 创建 6 个新文件
- ✅ 保留 `IRegionTabItem` 接口（无需修改）
- ✅ 删除或重构 `TabControlRegionAdapter.cs`

---

### 任务 4：TabControl 适配器重构

#### 分析
当前 `TabControlRegionAdapter.cs` 依赖：
- `RegionAdapterBase<TabControl>` (Prism)
- `IRegionBehaviorFactory` (Prism)
- `IRegion` (Prism)

#### 选项 A：简化为直接操作
移除适配器，在 ViewModel 中直接操作 `TabControl.Items`

#### 选项 B：保留适配器，移除 Prism 依赖
实现自己的 `IRegionAdapter<T>` 接口

**推荐**：选项 A（简化）

---

## 📦 NuGet 包变更

### 移除
```xml
<PackageReference Include="Prism.DryIoc.Avalonia" />
```

### 添加
```xml
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
```

**体积影响**：
- 移除：Prism + DryIoc ≈ 2-3 MB
- 添加：MS DI ≈ 200 KB
- **净减少**：约 2-2.8 MB

---

## ⚠️ 风险评估

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| DI 容器行为差异 | 低 | MS DI 是行业标准，行为一致 |
| 区域导航功能缺失 | 中 | 实现覆盖当前使用的所有功能 |
| 编译错误 | 低 | 分步实施，逐文件验证 |
| 运行时错误 | 中 | 充分测试，保留回滚点 |
| Native AOT 仍失败 | 低 | Prism 是唯一已知障碍 |

---

## ✅ 成功标准

1. **编译成功**：0 错误，0 警告（Prism 相关）
2. **功能完整**：所有现有功能正常工作
3. **Native AOT**：成功编译并运行
4. **体积优化**：Native AOT 版本 < 50 MB
5. **性能无退化**：启动时间、内存占用不增加

---

## 📝 待办事项概览

- [ ] 创建详细执行步骤文档
- [ ] 实施阶段 1：DI 容器替换
- [ ] 实施阶段 2：模块化系统
- [ ] 实施阶段 3：区域导航
- [ ] 实施阶段 4：TabControl 适配器
- [ ] 测试与验证
- [ ] Native AOT 编译测试
- [ ] 文档更新

---

**下一步**：查看 `02-详细执行步骤.md`
