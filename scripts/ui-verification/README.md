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
- 未保存确认的取消、放弃和保存分支；确认期间标题菜单禁用；草稿恢复后的修改状态。
- 关于浮层在真实主窗口内显示与关闭。
- 文件树层级、16 像素缩进、紧凑行高、图标与箭头对齐、折叠展开、过滤及文字对比度。
- 侧栏显隐、宽度、菜单勾选与设置持久化。
- 即时编辑、阅读只读与导图真实视口；摘要设置、折叠、源码对照与定位共用当前正文。
- 五步引导与跳过恢复：未保存原文、源码文档实例、即时编辑撤销重做、导图缩放和用户偏好。

Windows 发布验收使用 `scripts/publish.ps1 -RuntimeIdentifier win-x64`，再运行 `artifacts/publish/win-x64/Vex/Vex.exe`；不能用框架运行结果代替 NativeAOT。发布构建保留 portable 编译符号，保证 Avalonia XAML 编译链路完整。

任何断言失败均返回非零退出码，PNG 输出到指定目录。离屏结果不替代 Windows 实际窗口、DPI、IME、文件关联和 NativeAOT 运行验收。

2026-10-15 回归记录：使用仅含 nuget.org 的仓库外配置及独立缓存恢复 CodeWF.Avalonia 12.1.4.15，最终源码的 272 项断言全部通过，包含 980 × 640 窗口下的三视图与全部五步引导。日志位于 `artifacts/official-verification-final.log`，截图位于 `artifacts/ui-verification-official/`。真实键鼠操作、中文输入法、多 DPI、超长文档和其他系统尚未完成专项验收。
