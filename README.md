# Vex

Vex（维刻）是一个基于 .NET 10 与 Avalonia 12 的跨平台 Markdown 编辑器。

Slogan：极简之力，妙笔成章。

作者：沙漠尽头的狼  
出品：码坊 CodeWF  
网站：https://codewf.com

## Status

当前版本为 `1.1.0`，已具备 Prism 模块化应用骨架、Typora 风格菜单、左中右三栏工作区、Markdown 编辑/预览链路、同目录文件列表、大纲跳转、查找替换、主题/排版切换、HTML/PDF/PNG/Word 导出、复制到微信公众号/知乎/稀土掘金，以及基于 UrsaWindow 的关键对话框。PDF、PNG 和 Word 导出复用 `CodeWF.Markdown` 12.0.3.12 的 `MarkdownDocumentExporter`/`ExportKind` 公共能力，支持本地相对图、`data:image`、HTTP(S) 图片、SVG/GIF/WebP 转 PNG，导出文件通过邮件、微信等发送后可离线查看嵌入图片。自媒体复制复用 `CodeWF.Markdown` 的 `MarkdownHtmlClipboardExtensions.TrySetMarkdownHtmlAsync`，Vex 只传当前 Markdown、排版主题和目标平台，由公共库渲染微信公众号/知乎/稀土掘金 inline HTML 并写入 `text/html`、`public.html` 和 Windows `HTML Format`。

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
