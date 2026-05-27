# Vex

Vex（维刻）是一个基于 .NET 10 与 Avalonia 12 构建的跨平台 Markdown 编辑器，面向日常写作、技术文档整理、自媒体排版和多格式文档导出。

Slogan：极简之力，妙笔成章。

作者：沙漠尽头的狼  
出品：码坊 CodeWF  
网站：https://codewf.com

## 项目定位

Vex 希望提供一个轻量、清爽、可离线使用的 Markdown 写作环境。它不是单纯的代码编辑器，也不是只做预览的阅读器，而是围绕 Markdown 文档的完整工作流来设计：

- 写作时有源码编辑、实时预览、大纲导航和文档统计。
- 整理文档时可以打开单个文件或整个文件夹，快速在同目录文档之间切换。
- 发布时可以导出 HTML、PDF、PNG、Word，也可以复制为微信公众号、知乎、稀土掘金可直接粘贴的富 HTML。
- 分享时会尽量嵌入图片资源，让 PDF 和 Word 文件离线发送后仍能正常查看。

当前版本：`1.1.0`

## 主要功能

### Markdown 编辑

- 基于 AvaloniaEdit 的 Markdown 源码编辑器。
- 右侧实时预览，支持当前排版主题和紧凑布局。
- 支持常见 Markdown 编辑动作：标题、引用、列表、任务列表、表格、代码块、链接、图片等插入辅助。
- 支持从网页粘贴内容：优先读取剪贴板 HTML，并转换为 Markdown；无 HTML 或转换失败时回落到普通粘贴。

### 文件工作流

- 新建、打开、保存、另存为 Markdown 文档。
- 支持打开文件夹，左侧自动列出目录中的 Markdown/TXT 文档。
- 直接打开单个文件时，会自动加载同目录下的可编辑文档列表。
- 支持最近文件、拖拽打开、启动参数打开、文件重命名/删除，以及外部文件变更检测。

### 大纲、查找与统计

- 根据 Markdown 标题生成大纲，支持快速跳转。
- 状态栏显示保存状态、编码、行列位置、字数和字符数等文档信息。
- 支持查找和替换，包含区分大小写、整词匹配、正则匹配、命中计数和循环查找提示。
- 针对长文档的大纲扫描、统计、查找计数和预览刷新做了防抖与性能优化。

### 导出与分享

- 导出 HTML。
- 打印预览。
- 导出 PNG 长图。
- 导出 PDF，正文文本可选择、可复制，并支持页眉页脚。
- 导出 Word `.docx`，保留基础 Markdown 结构并嵌入图片。
- 复制到微信公众号、知乎、稀土掘金，生成适合网页编辑器粘贴的富 HTML 剪贴板内容。
- PDF、PNG 和 Word 导出复用 `CodeWF.Markdown` 12.0.3.14 的 `MarkdownDocumentExporter` / `ExportKind` 能力，支持本地相对图、`data:image`、HTTP(S) 图片、SVG/GIF/WebP 转 PNG。

### 外观与本地化

- 支持浅色/深色主题。
- 支持多套 Markdown 排版主题和紧凑布局。
- 支持行号、状态栏、窗口置顶等视图选项。
- 内置简体中文、繁体中文、英文、日文界面资源。
- 帮助文档、快速开始、更新日志和鸣谢文档会随发布产物一起输出。

### 发布产物

- 支持 `win-x64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64` 多 RID 发布。
- 提供一键发布和 release zip 打包脚本。
- Release zip 命名为 `Vex-v<Version>-<RID>.zip`，例如 `Vex-v1.1.0-win-x64.zip`。
- Release zip 会排除 `*.pdb` 调试符号文件，并生成 SHA256 文件和 release manifest，方便直接上传 GitHub Release。
- Windows 可选生成 MSIX 布局/安装包。

## 技术栈

- .NET 10
- Avalonia 12
- AvaloniaEdit
- Prism.Avalonia
- ReactiveUI.Avalonia
- Ursa.Avalonia / Semi.Avalonia
- CodeWF.Markdown
- CodeWF.EventBus
- Lang.Avalonia.Json

## 快速开始

```powershell
dotnet restore Vex.slnx
dotnet build Vex.slnx -v:minimal
dotnet run --project src\Vex\Vex.csproj -f net10.0
```

更多使用说明见 [docs/QuickStart.md](docs/QuickStart.md)。

## 构建与发布

```powershell
dotnet build Vex.slnx -v:minimal
.\publish_all.bat
.\package_all.bat
.\package_all.bat --force
.\scripts\package_vex_msix.ps1 -RuntimeIdentifier win-x64 -PrepareOnly
.\scripts\package_vex_msix.ps1 -RuntimeIdentifier win-x64 -CertificatePath .\cert.pfx
```

`publish_all.bat` 会将配置好的运行时发布到 `publish/<RID>/`。

`package_all.bat` 会先调用 `publish_all.bat`，再在 `artifacts/release/` 下生成 `Vex-v<Version>-<RID>.zip`、SHA256 文件和 release manifest。

Release 压缩包会排除 `*.pdb` 调试符号文件。已有产物默认不会覆盖；需要覆盖时使用 `package_all.bat --force`，或直接给 PowerShell 脚本传入 `-Force`。

`scripts/package_vex_msix.ps1` 会在 `artifacts/installer/msix-layout/<RID>/` 下创建 Windows MSIX 布局；不传 `-PrepareOnly` 时，会调用 Windows SDK `makeappx.exe` 生成 `artifacts/installer/Vex-<Version>-<RID>.msix`，并在提供 `-CertificatePath` 时使用 `signtool.exe` 签名。

## 文档

- [快速开始](docs/QuickStart.md)
- [更新日志](docs/CHANGELOG.md)
- [鸣谢](docs/ACKNOWLEDGEMENTS.md)
- [GitHub Release 文案](RELEASES.md)

## 开源致谢

Vex 使用并感谢以下开源项目：

- [Avalonia](https://avaloniaui.net/)
- [Prism.Avalonia](https://github.com/AvaloniaCommunity/Prism.Avalonia)
- [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia)
- [Ursa.Avalonia](https://github.com/irihitech/Ursa.Avalonia)
- [CodeWF.Markdown](https://github.com/dotnet9/CodeWF.Markdown)
- [CodeWF.EventBus](https://github.com/dotnet9/CodeWF.EventBus)
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)
- [Markdig](https://github.com/xoofx/markdig)

## License

MIT. See [LICENSE](LICENSE).
