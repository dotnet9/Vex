# 更新日志

## 0.1.0 - 2026-05-22

### 新增

- 创建 Vex（维刻）Markdown 编辑器基础版本。
- 完善作者、码坊 CodeWF 与官方网站信息。
- 新增 Typora 风格标题栏菜单、文件/大纲侧边栏、Markdown 编辑区、Markdown 预览区和状态栏。
- 新增文件的新建、打开、打开文件夹、保存、另存为、删除和打开文件位置入口。
- 新增编辑、段落、格式、视图、主题、国际化和帮助菜单的基础命令。
- 新增主题色、排版主题、紧凑布局和语言切换入口。
- 新增 `Vex.Controls` 与 `Vex.Controls.Themes` 控件主题包。
- 新增大纲点击跳转到编辑器对应标题行。
- ✨[新增]-帮助菜单支持打开随程序复制的更新日志、快速开始和鸣谢文档。
- ✨[新增]-为 Vex 主工程新增 `win-x64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64` 文件夹发布 Profile。
- ✨[新增]-新增根目录 `publish_vex_all.bat`，可一键依次执行所有 Vex 发布 Profile。
- ✨[新增]-新增裁剪发布免裁配置，保留 Vex、Avalonia、Prism、ReactiveUI、CodeWF 与 SVG 渲染相关程序集。

### 优化

- 解决方案文件切换为 `.slnx`。
- CodeWF 相关依赖改为 NuGet 包引用。
- 🔧[优化]-界面主题切换到 Semi.Avalonia 与 Ursa.Semi，并保留开源 Avalonia.Themes.Fluent 适配 AvaloniaEdit。
- 🔧[优化]-移除 CommunityToolkit.Mvvm，ViewModel 改为 ReactiveUI，并让菜单直接绑定 public 方法。
- 🔧[优化]-CodeWF.EventBus 改为通过 DryIoc 注册服务，并使用 `[EventHandler]` 方法处理编辑器动作与导航消息。
- 🔧[优化]-新增 Windows AOT/Win7 与 Linux/macOS self-contained single-file 发布配置。
- 🔧[优化]-发布 Profile 公共配置抽到 `FolderProfile.Common.props`，发布产物统一输出到根目录 `publish\<RuntimeIdentifier>\`，非 Windows self-contained single-file 发布启用裁剪。
- 🔧[优化]-通过中央传递钉版消除旧 `System.Drawing.Common` 解析带来的 NU1904 告警。
- 🔧[优化]-将 `Avalonia.Markup.Xaml.Loader` 传递依赖钉到 Avalonia 12.0.3，避免发布链路混入旧版本运行时加载器。
- 🧪[测试]-构建 `Vex.slnx`、执行依赖漏洞扫描、桌面启动烟测，并验证 `win-x64` Release Native AOT 与 `linux-x64` self-contained single-file 发布链路。
- 🧪[测试]-执行 `publish_vex_all.bat`，确认五个 Vex 发布 Profile 均可成功发布。
