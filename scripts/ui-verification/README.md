# Vex 离屏回归验证

加载本仓库的实际 App、主窗口、XAML 模板和 ViewModel，通过 Avalonia.Headless 与 Skia 绘制，不操作真实桌面。设置、草稿及最近记录使用内存服务，MCP 关闭，文件关联注册跳过。五份 Markdown 输入位于本目录的 `MarkdownSamples/`，每轮复制到输出目录后再测试保存流程。

需要 .NET 10 SDK；自研库通过 `Directory.Packages.props` 指定的 NuGet 包安装，不依赖其他仓库源码。

```powershell
dotnet run --project scripts/ui-verification/UiVerification.csproj -c Release `
    --artifacts-path ./artifacts/verification -- vex ./artifacts/ui-verification
if ($LASTEXITCODE -ne 0) { throw 'Vex UI verification failed' }
```

开发联调时将库打包到环境变量 `temp_nugets` 指定目录，通过仓库外的 NuGet 配置恢复；不提交本地源或绝对路径。日志、截图等临时输出可指定到仓库外临时目录。

验证范围：

- 真实窗口标题栏与内容区坐标、1 像素分隔线，避免重复预留标题栏高度；八套配色各自浅深模式及窄窗口。
- 21.875 像素源码行高、鼠标点击与键盘光标移动；五个文件连续切换。
- 相同正文仅路径大小写变化时，真实工作区状态和同一即时编辑器同步图片基址、保留选区；输出文件系统支持时再打开两个真实大小写不同的文件，核对快速打开保留两项。
- 未保存确认的取消、放弃和保存分支；确认期间标题菜单禁用；草稿恢复后的修改状态。
- 关于浮层在真实主窗口内显示与关闭。
- 关于主按钮和删除确认按钮在八色浅深组合中的真实普通、悬停、按下状态及文字对比度；共 96 种状态，按下后移出再释放，不执行更新或删除。
- 文件树层级、16 像素缩进、紧凑行高、图标与箭头对齐、折叠展开、过滤及文字对比度。
- 侧栏显隐、宽度、菜单勾选与设置持久化。
- 即时编辑、阅读只读与导图真实视口；摘要设置、折叠、源码对照与定位共用当前正文。
- 五步引导与跳过恢复：未保存原文、源码文档实例、即时编辑撤销重做、导图缩放和用户偏好。

Windows 发布验收使用 `scripts/publish.ps1 -RuntimeIdentifier win-x64`，正式 EXE 可通过 `--verify-runtime <独立目录>` 验证原生运行、输出语言资源与隔离偏好持久化。界面验证器另行以 NativeAOT 发布后执行；两者不注册实际文件关联。不能用框架运行结果代替 NativeAOT。发布构建保留 portable 编译符号，保证 Avalonia XAML 编译链路完整。

任何断言失败均返回非零退出码，PNG 输出到指定目录。离屏结果不替代 Windows 实际窗口、DPI、IME、文件关联和 NativeAOT 运行验收。

将运行参数 `vex` 换成 `path-identity` 可单独检查路径身份回归。Windows 默认文件系统上实际双文件分支会明确输出 `SKIP`，其余真实状态与编辑器断言仍执行；该结果不代表已实测 Linux 文件系统。

2026-10-16，1.5.8 复核：Windows App 正式脚本 NativeAOT 发布、正式 EXE 四语和隔离偏好自检通过，验证器另行 NativeAOT 发布后实际运行通过 638 项断言，包含同一关于浮层四语动态切换、实际元数据、精确路径与选区同步，以及八色浅深组合中主按钮与危险按钮的 96 种真实输入状态。依赖从仓库外本地 NuGet 源恢复，仓库不保存本地路径。详见 [复核记录](../../docs/UI-REVIEW-20261015.md)。
