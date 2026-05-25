# Vex

Vex（维刻）是一个基于 .NET 10 与 Avalonia 12 的跨平台 Markdown 编辑器。

Slogan：极简之力，妙笔成章。

作者：沙漠尽头的狼  
出品：码坊 CodeWF  
网站：https://codewf.com

## Status

当前处于 `0.1.0` 基础开发阶段，已具备 Prism 模块化应用骨架、Typora 风格菜单、左中右三栏工作区、Markdown 编辑/预览链路、同目录文件列表、大纲跳转、查找替换、主题/排版切换、HTML/PDF/PNG/Word 导出，以及基于 UrsaWindow 的关键对话框。PDF、PNG 和 Word 导出会复用 `CodeWF.Markdown` 的图片加载与栅格化能力，支持本地相对图、`data:image`、HTTP(S) 图片、SVG/GIF/WebP 转 PNG，导出文件通过邮件、微信等发送后可离线查看嵌入图片。

## Build and Release

```powershell
dotnet build Vex.slnx -v:minimal
.\publish_vex_all.bat
.\publish_vex_all.bat --package
.\scripts\package_vex_msix.ps1 -RuntimeIdentifier win-x64 -PrepareOnly
.\scripts\package_vex_msix.ps1 -RuntimeIdentifier win-x64 -CertificatePath .\cert.pfx
```

`publish_vex_all.bat` publishes the configured runtime identifiers into `publish/<RID>/`.
Passing `--package` runs `scripts/package_vex_artifacts.ps1` after all publishes succeed and writes zip archives, SHA256 files, and a release manifest under `artifacts/release/`.
The packaging script does not overwrite existing artifacts unless `-Force` is passed to the PowerShell script directly.
`scripts/package_vex_msix.ps1` creates a Windows MSIX layout under `artifacts/installer/msix-layout/<RID>/`; without `-PrepareOnly`, it uses Windows SDK `makeappx.exe` to write `artifacts/installer/Vex-<Version>-<RID>.msix`, and signs it with `signtool.exe` when `-CertificatePath` is provided.
