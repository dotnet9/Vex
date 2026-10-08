# Demo / Vex 离屏验证

加载实际 App、XAML、模板和 ViewModel，用 Avalonia.Headless + Skia 后台绘制。输入只发到离屏窗口；Vex 设置/草稿/最近记录为内存服务，MCP 关闭。不操作真实桌面，不修改草稿或关闭已有应用。

需要 .NET 10 SDK。默认布局是 E:/github/Apps/Vex 和 E:/github/Libs/CodeWF.Markdown；其他布局通过 `-p:MarkdownRepoRoot=...` 指定库路径。

## 依赖来源

Vex 默认从 nuget.org 恢复 Directory.Packages.props 中指定的正式版四包，无需本地打包。下面的打包步骤仅用于修改库代码后、正式发布前的开发联调。

在库仓库执行。改产品代码后先递增 Directory.Build.props 的 Version，再同步 Vex 四包版本。覆盖同版本 nupkg 不会刷新 NuGet 缓存。

```powershell
foreach ($projectName in @('CodeWF.Markdown.Lite','CodeWF.Markdown','CodeWF.Markdown.Lite.Themes','CodeWF.Markdown.Themes')) {
    dotnet pack "src/$projectName/$projectName.csproj" -c Debug -o E:/github/Libs/nuget-local
    if ($LASTEXITCODE -ne 0) { throw "Packing $projectName failed" }
}
```

使用开发版本时，恢复源需要同时包含本地源和 nuget.org。正式版验证直接使用下面的命令。

## 构建和运行

在 Vex 仓库执行，隔离输出避免覆盖运行中的二进制。

```powershell
dotnet build scripts/ui-verification/UiVerification.csproj -c Debug `
    --artifacts-path E:/github/Apps/Vex/artifacts/verification

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

- Demo：明/暗 × 1400/980 × 四模式；源码往返、22.1px 行高及点击/键盘光标定位、选中图标/竖条、活动按钮、标题高度；实际点击/输入/Esc、共享引用编辑、源码同步、性能插入/停止、双预览。
- Vex：六主题/980px；21.875px 源码行高及换行后键盘移动、标题线实际像素、紧凑带图标页签、正文边框、文件卡画刷和普通标题对比度；五文件切换、未保存浮层、草稿基线、侧栏勾选/宽度/持久化字段。
- PNG/日志保存在参数指定目录，覆盖首屏、中部/底部和标题局部；断言失败返回非零。

不渲染浏览器原型，也不模拟 Windows 标题按钮、DPI、IME。CSS 数值、截图视觉检查和 UI 断言共同构成证据，程序通过不等于全页逐像素一致。

原型维护规格见本仓库 `design/README.md` 和 CodeWF.Markdown 的 `design/README.md`。`artifacts/verification`、两个仓库的 `artifacts/ui-verification` 都是可重建输出；验证完成后可删除。正式版从 nuget.org 恢复后，本地联调包也可清理。

## 清理输出

关闭本轮验证进程后，在 Vex 仓库运行：

```powershell
pwsh -NoProfile -File scripts/clean-development-artifacts.ps1 -WhatIf
pwsh -NoProfile -File scripts/clean-development-artifacts.ps1
```

脚本清理两个仓库的 `artifacts`、测试结果和空 `.plan`。开发版只删除同开发日期下更早的迭代包；正式版确认可在 nuget.org 恢复后，删除相同正式版本对应的本地开发包。非空计划目录会停止清理，便于先核对其中内容。支持 `-MarkdownRepoRoot` 和 `-LocalPackageSource` 指定其他布局；不关闭运行中的应用，应用占用输出时应先自行关闭。

正式版号因发布修复递增时，可用 `-SupersededDevelopmentVersions 14.0.1` 明确清理之前用于联调的开发版本；脚本拒绝删除比当前正式依赖更新的版本。
