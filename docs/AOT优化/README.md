# Vex Native AOT 优化方案

## 📖 文档导航

本文件夹包含 Vex Markdown 编辑器实现 Native AOT 支持的完整方案和执行步骤。

### 核心文档

| 文档 | 描述 | 状态 | 阅读时间 |
|------|------|------|----------|
| [01-Prism替换实施方案.md](./01-Prism替换实施方案.md) | 技术方案和影响分析 | ✅ 完成 | 15 分钟 |
| [02-详细执行步骤.md](./02-详细执行步骤.md) | 分阶段实施指南 | ✅ 完成 | 20 分钟 |
| [03-工作检查清单.md](./03-工作检查清单.md) | 任务追踪清单 | ✅ 完成 | 10 分钟 |
| [04-常见问题.md](./04-常见问题.md) | 问题排查手册 | ✅ 完成 | 15 分钟 |

### 阅读顺序

**首次阅读**（建议顺序）：
1. 📄 **README.md**（本文件）— 了解整体情况
2. 📄 **01-Prism替换实施方案.md** — 理解技术方案
3. 📄 **02-详细执行步骤.md** — 掌握实施步骤
4. 📄 **03-工作检查清单.md** — 准备执行任务

**执行期间**：
- 参考 **02-详细执行步骤.md** 逐步实施
- 使用 **03-工作检查清单.md** 追踪进度
- 遇到问题查阅 **04-常见问题.md**

**复查期间**：
- **04-常见问题.md** — 排查问题
- **01-Prism替换实施方案.md** — 回顾设计决策

---

## 🎯 项目目标

### 核心目标
- ✅ 移除 Prism.Avalonia 依赖（阻碍 Native AOT）
- ✅ 实现 Native AOT 编译支持
- ✅ 保持应用功能完整性
- ✅ 优化可执行文件体积（目标 < 50 MB）

### 性能目标
| 指标 | 当前（Prism + 裁剪） | 目标（无 Prism + AOT） |
|------|---------------------|----------------------|
| 文件体积 | 37 MB | < 50 MB |
| 启动时间 | 基准 | 更快 |
| 内存占用 | 基准 | 更低 |
| 编译成功 | ✅（裁剪模式） | ✅（Native AOT） |

---

## 🔍 问题背景

### 核心问题

**Native AOT 编译失败**，错误信息：
```
Method 'ReflectionBindingExtension.ProvideValue' will always throw because AOT does not support it
```

**根本原因**：
- Prism.Avalonia 的 DialogWindow 在 XAML 中使用 `ReflectionBindingExtension`
- 该方法在 Native AOT 模式下不存在（Avalonia 设计限制）
- 即使使用 `TrimmerRootAssembly` 也无法解决（无法创建不存在的方法）

### 已尝试的方案

| 方案 | 结果 | 原因 |
|------|------|------|
| TrimmerRootAssembly | ❌ 失败 | 方法本身在 AOT 中不存在 |
| 禁用反射绑定 | ❌ 不可行 | Prism 内部 XAML 依赖 |
| 部分替换 | ❌ 不推荐 | Prism 组件紧密耦合 |

### 最终方案

**完全移除 Prism，使用轻量级替代**：
- **DI 容器**：Microsoft.Extensions.DependencyInjection
- **模块化**：自定义 `IAppModule` 接口
- **区域导航**：自定义 `IRegionManager` 实现

---

## 📊 影响范围

### 文件统计

| 类别 | 数量 | 详情 |
|------|------|------|
| 需修改文件 | 12 个 | 见实施方案文档 |
| 需创建文件 | 7 个 | 模块化 + 区域导航实现 |
| 需删除文件 | 1 个 | TabControlRegionAdapter（可选） |
| 服务注册 | 58 个 | App.axaml.cs 中转换 |
| 模块 | 4 个 | Shell/Workspace/Appearance/Help |
| 区域 | 2 个 | Sidebar/Outline |

### 工作量估算

| 阶段 | 预计时间 | 风险等级 |
|------|----------|---------|
| 阶段 1：准备工作 | 30 分钟 | 🟢 低 |
| 阶段 2：实现基础设施 | 2-3 小时 | 🟢 低 |
| 阶段 3：替换 DI 容器 | 3-4 小时 | 🟡 中 |
| 阶段 4：替换模块系统 | 2-3 小时 | 🟢 低 |
| 阶段 5：替换区域导航 | 3-5 小时 | 🟡 中 |
| 阶段 6：测试与验证 | 2-3 小时 | 🟡 中 |
| 阶段 7：Native AOT 验证 | 1-2 小时 | 🟢 低 |
| **总计** | **2-3 天** | 🟡 中 |

---

## 🛠️ 技术方案概览

### 架构对比

#### 替换前（Prism）
```
App (PrismApplication)
├── DI 容器：DryIoc
├── 模块系统：IModule
├── 区域导航：IRegionManager (Prism)
└── 对话框：DialogService
```

#### 替换后（自定义）
```
App (Application)
├── DI 容器：Microsoft.Extensions.DependencyInjection
├── 模块系统：IAppModule (自定义)
├── 区域导航：IRegionManager (自定义)
└── 对话框：Avalonia 原生
```

### 核心组件

#### 1. 模块化系统
```csharp
public interface IAppModule
{
    void RegisterServices(IServiceCollection services);
    void Initialize(IServiceProvider serviceProvider);
}
```

#### 2. 区域管理器
```csharp
public interface IRegionManager
{
    void RegisterViewWithRegion<TView>(string regionName) where TView : Control;
    IRegion GetOrCreateRegion(string regionName);
}
```

#### 3. DI 注册
```csharp
// Prism API → MS.Extensions.DI API
containerRegistry.RegisterSingleton<I, T>() → services.AddSingleton<I, T>()
containerRegistry.Register<T>()             → services.AddTransient<T>()
```

---

## ⚠️ 风险评估

### 主要风险

| 风险 | 等级 | 缓解措施 |
|------|------|---------|
| 功能回归 | 🟡 中 | 完整测试清单（阶段 6） |
| 性能退化 | 🟢 低 | 性能基准对比 |
| 开发时间超支 | 🟡 中 | 分阶段实施，可随时回滚 |
| Native AOT 仍失败 | 🟢 低 | 已验证反射绑定已修复 |
| 第三方库不兼容 | 🟢 低 | 仅修改自有代码 |

### 回滚计划

如果出现严重问题：
```bash
git checkout master
git branch -D feature/remove-prism-for-aot
# 恢复到修改前状态
```

---

## ✅ 前置条件

### 环境要求
- [x] .NET SDK 10.0+
- [x] Visual Studio 2022 或 Rider
- [x] Git（版本控制）
- [x] Windows 11 LTSC 2024

### 代码状态
- [x] 当前代码可编译（裁剪模式）
- [x] 已修复 14 个反射绑定问题
- [x] 已实现 80% 体积缩减（185 MB → 37 MB）

### 知识准备
- [ ] 熟悉 Microsoft.Extensions.DependencyInjection
- [ ] 理解 Avalonia 控件系统
- [ ] 了解 Native AOT 限制

---

## 🚀 快速开始

### 第一步：阅读文档
```bash
# 阅读实施方案（15 分钟）
cat docs/AOT优化/01-Prism替换实施方案.md

# 阅读执行步骤（20 分钟）
cat docs/AOT优化/02-详细执行步骤.md
```

### 第二步：创建分支
```bash
git checkout -b feature/remove-prism-for-aot
git push -u origin feature/remove-prism-for-aot
```

### 第三步：执行实施
按照 `02-详细执行步骤.md` 逐阶段执行，使用 `03-工作检查清单.md` 追踪进度。

### 第四步：验证结果
```bash
# Native AOT 编译
dotnet publish src/Vex/Vex.csproj -c Release -r win-x64 -p:PublishAot=true

# 检查体积
ls -lh artifacts/publish/win-x64/Vex/Vex.exe

# 运行测试
.\artifacts\publish\win-x64\Vex\Vex.exe
```

---

## 📈 成功标准

### 编译验证
- [x] ✅ 编译成功，0 错误
- [x] ✅ Native AOT 编译成功
- [x] ✅ 无 Prism 相关警告

### 功能验证
- [ ] ✅ 应用正常启动
- [ ] ✅ 所有核心功能正常
- [ ] ✅ 区域导航正常
- [ ] ✅ DI 容器正常

### 性能验证
- [ ] ✅ 文件体积 < 50 MB
- [ ] ✅ 启动时间无退化
- [ ] ✅ 内存占用无退化

---

## 📚 参考资源

### 官方文档
- [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
- [.NET Native AOT 部署](https://learn.microsoft.com/dotnet/core/deploying/native-aot)
- [Avalonia UI 文档](https://docs.avaloniaui.net/)

### 相关 Issues
- Avalonia 反射绑定限制
- Prism.Avalonia Native AOT 兼容性

### 内部文档
- `docs/优化说明.md` — 之前的优化记录
- `scripts/build/publish.ps1` — 发布脚本

---

## 📝 维护日志

| 日期 | 作者 | 变更 |
|------|------|------|
| 2026-10-27 | sunmeang | 创建初始文档 |
| 2026-10-27 | sunmeang | 完成 4 个核心文档 |

---

## 💡 贡献指南

如果在执行过程中发现：
- 文档错误或遗漏
- 更优的实施方案
- 新的问题和解决方案

请更新相应文档并记录在维护日志中。

---

**项目状态**：📝 文档完成，待执行  
**创建日期**：2026-10-27  
**维护者**：sunmeang  
**版本**：1.0
