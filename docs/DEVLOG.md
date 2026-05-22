# Vex Development Log

## 0.1.0 - 2026-05-22

### zh-CN

- 完善项目身份信息：作者“沙漠尽头的狼”、出品“码坊 CodeWF”、网站 `https://codewf.com`。
- 创建 Vex Avalonia 桌面应用骨架，接入 Prism 8.x 模块目录。
- 创建 `Vex.Controls` 和 `Vex.Controls.Themes`，按 Semi.Avalonia 风格拆分控件和主题资源。
- 实现主窗口标题栏菜单、左中右三栏、状态栏和 Markdown 编辑/预览基础体验。
- 接入 CodeWF.EventBus，用于编辑器视图和 Shell ViewModel 之间传递编辑动作与 Markdown 内容变化。
- 接入 CodeWF.Markdown.Themes，提供排版主题和紧凑布局切换入口。
- 创建双语更新日志、快速开始和鸣谢文档。
- 使用 `.slnx` 解决方案格式，并将 CodeWF 相关依赖切换为 NuGet 包引用。
- 增加大纲点击跳转编辑器行号功能，通过 CodeWF.EventBus 在 Shell 与编辑器视图之间传递导航消息。
- 切换到 Semi.Avalonia、Irihi.Ursa 和 ReactiveUI.Avalonia，保留开源 Avalonia.Themes.Fluent 以适配 AvaloniaEdit。
- 通过 CodeWF.DryIoc.EventBus 注册 IEventBus，并使用 `[EventHandler]` + `Subscribe(this)` 注册 ViewModel/控制器处理函数。
- 菜单命令改为直接绑定 ViewModel 的 public 方法，保留 CodeWF.EventBus 作为编辑器动作和通知通信通道。
- 增加帮助菜单打开随程序复制的更新日志、快速开始和鸣谢文档。
- 增加 Windows AOT/Win7 发布支持配置，保留 VC-LTL 与 YY-Thunks NuGet 包引用。
- 核查第三方依赖来源，避免引用无源码的 AvaloniaEdit 第三方主题包。
- 使用中央传递钉版将旧 `System.Drawing.Common` 解析覆盖到 10.0.8，消除 Prism 8.x 依赖链带来的 NU1904 告警。
- 验证 `dotnet build Vex.slnx`、依赖漏洞扫描、桌面启动烟测、`win-x64` Release Native AOT 发布链路和 `linux-x64` self-contained single-file 发布链路。
- 增加 VS 文件夹发布 Profile，覆盖 `win-x64`、`linux-x64`、`linux-arm64`、`osx-x64` 和 `osx-arm64`。
- 增加根目录 `publish_vex_all.bat`，可一键按上述发布 Profile 依次发布 Vex 主工程。
- 验证 `publish_vex_all.bat` 可成功调用全部五个发布 Profile。

### en-US

- Added project identity metadata for author, CodeWF, and `https://codewf.com`.
- Created the Vex Avalonia desktop application shell with Prism 8.x module catalog wiring.
- Created `Vex.Controls` and `Vex.Controls.Themes`, with controls and theme resources separated in a Semi.Avalonia-style layout.
- Implemented the title-bar menu, three-pane workspace, status bar, and the initial Markdown editing/preview experience.
- Wired CodeWF.EventBus for editor action and Markdown content communication between the editor view and shell ViewModel.
- Integrated CodeWF.Markdown.Themes entry points for typography themes and compact layout switching.
- Added bilingual changelog, quick start, and acknowledgements documents.
- Switched the solution to `.slnx` and moved CodeWF dependencies to NuGet package references.
- Added outline-to-editor navigation via CodeWF.EventBus messages.
- Switched to Semi.Avalonia, Irihi.Ursa, and ReactiveUI.Avalonia while keeping the open Avalonia.Themes.Fluent package for AvaloniaEdit styling.
- Registered IEventBus through CodeWF.DryIoc.EventBus and used `[EventHandler]` plus `Subscribe(this)` for ViewModel/controller handlers.
- Moved menu actions to direct public ViewModel method bindings while keeping CodeWF.EventBus for editor actions and notifications.
- Added help menu actions that open bundled changelog, quick start, and acknowledgements documents.
- Added Windows AOT/Win7 publish support settings with VC-LTL and YY-Thunks NuGet package references.
- Reviewed third-party dependency sources and avoided source-unavailable AvaloniaEdit theme packages.
- Used central transitive pinning to resolve old `System.Drawing.Common` references to 10.0.8 and remove the Prism 8.x NU1904 warning.
- Verified `dotnet build Vex.slnx`, dependency vulnerability scanning, desktop smoke startup, the `win-x64` Release Native AOT publish path, and the `linux-x64` self-contained single-file publish path.
- Added Visual Studio folder publish profiles for `win-x64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
- Added the root `publish_vex_all.bat` script to publish the Vex application through each profile.
- Verified `publish_vex_all.bat` can successfully run all five publish profiles.
