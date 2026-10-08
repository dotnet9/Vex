# Demo / Vex 离屏验证

加载实际 App、XAML、模板和 ViewModel，用 Avalonia.Headless + Skia 后台绘制。输入只发到离屏窗口；Vex 设置/草稿/最近记录为内存服务，MCP 关闭。不操作真实桌面，不修改草稿或关闭已有应用。

需要 .NET 10 SDK。默认布局是 E:/github/Apps/Vex 和 E:/github/Libs/CodeWF.Markdown；其他布局通过 `-p:MarkdownRepoRoot=...` 指定库路径。

## 打包依赖

在库仓库执行。改产品代码后先递增 Directory.Build.props 的 Version，再同步 Vex 四包版本。覆盖同版本 nupkg 不会刷新 NuGet 缓存。

```powershell
foreach ($projectName in @('CodeWF.Markdown.Lite','CodeWF.Markdown','CodeWF.Markdown.Lite.Themes','CodeWF.Markdown.Themes')) {
    dotnet pack "src/$projectName/$projectName.csproj" -c Debug -o E:/github/Libs/nuget-local
    if ($LASTEXITCODE -ne 0) { throw "Packing $projectName failed" }
}
```

开发包未发布，首次运行需要上述四包；其他依赖需已恢复到 NuGet 缓存，全新机器可在 RestoreSources 加入 nuget.org。

## 构建和运行

在 Vex 仓库执行，隔离输出避免覆盖运行中的二进制。

```powershell
dotnet build scripts/ui-verification/UiVerification.csproj -c Debug `
    --artifacts-path E:/github/Apps/Vex/artifacts/verification `
    -p:RestoreSources=E:/github/Libs/nuget-local

New-Item -ItemType Directory -Force E:/github/Libs/CodeWF.Markdown/artifacts/ui-verification | Out-Null
New-Item -ItemType Directory -Force E:/github/Apps/Vex/artifacts/ui-verification | Out-Null

dotnet artifacts/verification/bin/UiVerification/debug/UiVerification.dll sample `
    E:/github/Libs/CodeWF.Markdown/artifacts/ui-verification |
    Tee-Object E:/github/Libs/CodeWF.Markdown/artifacts/ui-verification/verification.log
if ($LASTEXITCODE -ne 0) { throw 'Demo verification failed' }

dotnet artifacts/verification/bin/UiVerification/debug/UiVerification.dll vex `
    E:/github/Apps/Vex/artifacts/ui-verification |
    Tee-Object E:/github/Apps/Vex/artifacts/ui-verification/verification.log
if ($LASTEXITCODE -ne 0) { throw 'Vex verification failed' }
```

同时引用源码 Demo 和 Vex 的 NuGet 依赖，同版本不同 DLL 也可能在复制时选到旧文件。因此库改动必须提升版本并重新打包。

## 覆盖与边界

- Demo：明/暗 × 1400/980 × 四模式；源码往返、选中图标/竖条、活动按钮、标题高度；实际点击/输入/Esc、共享引用编辑、源码同步、性能插入/停止、双预览。
- Vex：六主题/980px；标题线实际像素、紧凑带图标页签、正文边框、文件卡画刷和普通标题对比度；五文件切换、未保存浮层、草稿基线、侧栏勾选/宽度/持久化字段。
- PNG/日志保存在参数指定目录，覆盖首屏、中部/底部和标题局部；断言失败返回非零。

不渲染浏览器原型，也不模拟 Windows 标题按钮、DPI、IME。CSS 数值、截图视觉检查和 UI 断言共同构成证据，程序通过不等于全页逐像素一致。
