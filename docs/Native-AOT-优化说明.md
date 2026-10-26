# Native AOT 优化配置说明

## 📋 配置概览

本项目已启用 Native AOT（Ahead-of-Time）编译，用于显著减少发布体积和运行时内存占用。

## 🎯 优化效果预估

| 指标 | 优化前 | 优化后 | 减少幅度 |
|------|--------|--------|---------|
| 发布体积 | 185 MB | 70-120 MB | 40-60% |
| 运行内存 | - | - | 50-70% |
| 启动速度 | - | - | 2-4x 提升 |

## ⚙️ 配置详解

### 1. 激进裁剪模式（所有平台）
```xml
<TrimMode>link</TrimMode>
<EnableTrimAnalyzer>true</EnableTrimAnalyzer>
```
- `TrimMode=link`: 程序集级别裁剪，比默认的 `copyused` 更激进
- `EnableTrimAnalyzer`: 编译时分析裁剪警告

### 2. Native AOT 优化（Release + 指定 RID）
```xml
<IlcOptimizationPreference>Size</IlcOptimizationPreference>
```
- 优先优化体积而非速度
- 减少生成的本机代码大小

```xml
<IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
<IlcGenerateCompleteTypeMetadata>false</IlcGenerateCompleteTypeMetadata>
```
- 移除堆栈跟踪数据和完整类型元数据
- 进一步减小体积，但会影响异常堆栈信息

```xml
<IlcTrimMetadata>true</IlcTrimMetadata>
```
- 裁剪未使用的元数据

### 3. 运行时功能裁剪
```xml
<EventSourceSupport>false</EventSourceSupport>
<EnableUnsafeUTF7Encoding>false</EnableUnsafeUTF7Encoding>
<HttpActivityPropagationSupport>false</HttpActivityPropagationSupport>
<MetadataUpdaterSupport>false</MetadataUpdaterSupport>
```
- 移除不需要的运行时功能
- 减少运行时库体积

### 4. 兼容性配置
```xml
<PublishSingleFile Condition="'$(PublishAot)' != 'true'">true</PublishSingleFile>
```
- Native AOT 与 PublishSingleFile 互斥
- 未启用 Native AOT 时回退到单文件发布

## 🚀 发布命令

### Native AOT 发布（推荐）
```bash
# Windows x64
dotnet publish src/Vex/Vex.csproj -c Release -r win-x64 -p:PublishAot=true

# 或使用发布配置文件（已预配置 PublishAot=true）
dotnet publish src/Vex/Vex.csproj /p:PublishProfile=FolderProfile__win-x64
```

### 单文件发布（不使用 Native AOT）
```bash
dotnet publish src/Vex/Vex.csproj -c Release -r win-x64 -p:PublishAot=false
```

### 测试脚本
项目根目录提供了测试脚本：
```bash
./test-native-aot-publish.sh
```

## ⚠️ 注意事项

### 1. 编译时间
Native AOT 编译时间比普通发布长 2-5 倍，这是正常的。

### 2. 反射限制
Native AOT 对反射有限制，可能需要在 `TrimmerRoots.xml` 中保留某些类型：
```xml
<TrimmerRootDescriptor Include="Properties\Trimming\TrimmerRoots.xml" />
```

### 3. 兼容性测试
首次启用 Native AOT 后，需要全面测试所有功能：
- ✅ Markdown 编辑和预览
- ✅ 文件图标显示
- ✅ 多语言切换
- ✅ PDF 导入/导出
- ✅ 主题切换
- ✅ 设置保存/加载

### 4. 构建要求
- 需要 C++ 编译工具链（Visual Studio Build Tools 或完整版）
- Linux: `clang` 和 `zlib1g-dev`
- macOS: Xcode Command Line Tools

## 🔍 故障排查

### 问题 1: 编译失败 - 缺少 C++ 工具
**解决**：安装 Visual Studio Build Tools，勾选 "使用 C++ 的桌面开发"

### 问题 2: 运行时崩溃或功能异常
**原因**：裁剪过于激进，移除了必需的类型或方法

**解决步骤**：
1. 查看编译警告（IL2XXX、IL3XXX）
2. 在 `Properties/Trimming/TrimmerRoots.xml` 中保留相关类型
3. 或设置 `<IlcDisableReflection>false</IlcDisableReflection>`（已配置）

### 问题 3: 体积没有明显减小
**检查**：
- 确认发布命令包含 `-p:PublishAot=true`
- 检查 `bin` 目录是否有 `.obj` 文件（Native AOT 中间产物）
- 查看发布日志是否有 "Generating native code" 字样

## 📊 对比测试

发布后可以对比测试：

```bash
# 查看文件大小
ls -lh artifacts/publish/win-x64/Vex.exe
ls -lh artifacts/publish/win-x64-aot/Vex.exe

# 查看目录总大小
du -sh artifacts/publish/win-x64
du -sh artifacts/publish/win-x64-aot

# 运行时内存对比（使用任务管理器或 Process Explorer）
# 启动后等待 30 秒，记录 "工作集" 和 "专用字节数"
```

## 🎓 参考资料

- [Native AOT 部署概述](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
- [裁剪自包含部署和可执行文件](https://learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained)
- [为裁剪准备 .NET 库](https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)

## 📅 更新日期

2026/10/27 - 初始配置
