# GitHub Releases

This document keeps copy-ready GitHub Release notes for Vex.
本文档记录 Vex 每个发布版本可直接复制使用的 GitHub Release 文案。

## v1.1.1 - 2026-05-27

### Release Title

Vex 1.1.1 - Markdown Inline Style Fix

### Release Notes

#### English

##### Added

- Paste from web pages now prefers clipboard HTML and converts it to Markdown.

##### Fixed

- Markdown preview now applies inline bold, italic, and strikethrough styles to the correct text ranges when mixed with plain text on the same line.
- Chinese inline bold now remains visibly bold after regular Chinese text in the same paragraph.

##### Improved

- PDF, PNG, and Word export now use `CodeWF.Markdown` 12.0.3.15 `MarkdownDocumentExporter` / `ExportKind`.
- Exported PDF text is now selectable and copyable.
- PDF, PNG, and Word export handle relative local images, `data:image`, HTTP(S), SVG, GIF, and WebP.
- Word and PDF exports embed image resources for offline sharing.
- Copy to WeChat Official Account, Zhihu, and Juejin now uses the shared `CodeWF.Markdown` rich HTML clipboard pipeline.
- Social-copy output now includes CF_HTML, embedded images, platform suffixes, and localized tool metadata.

##### Changed

- Word/OpenXML, PDF text layout, PNG rendering, and social HTML templates were moved from Vex into `CodeWF.Markdown`.
- Vex now consumes locally packed `CodeWF.Markdown` NuGet packages instead of cross-repository project references.

##### Verification

- `dotnet build Vex.slnx`
- Vex screenshot smoke with ``极简 **之** 力，`妙`笔成章。``
- `package_all.bat`

#### 简体中文

##### 新增

- 从网页粘贴内容时，优先读取剪贴板 HTML 并转换为 Markdown。

##### 修复

- Markdown 预览现在能在同一行普通文本混排时，把加粗、斜体、删除线正确应用到对应文本片段。
- 中文普通文本后面的行内加粗现在能稳定显示为粗体。

##### 优化

- PDF、PNG、Word 导出统一改用 `CodeWF.Markdown` 12.0.3.15 的 `MarkdownDocumentExporter` / `ExportKind`。
- PDF 正文现在可选择、可复制。
- PDF、PNG、Word 导出增强图片处理，支持相对本地图、`data:image`、HTTP(S)、SVG、GIF、WebP。
- Word 和 PDF 会嵌入图片资源，文件离线发送后仍可查看。
- 复制到微信公众号、知乎、稀土掘金改用 `CodeWF.Markdown` 的公共富 HTML 剪贴板链路。
- 自媒体复制补齐 CF_HTML、图片嵌入、平台尾注和多语言工具文案。

##### 调整

- Word/OpenXML、PDF 文本排版、PNG 渲染器和自媒体 HTML 模板已从 Vex 下沉到 `CodeWF.Markdown`。
- Vex 改为消费本地打包的 `CodeWF.Markdown` NuGet 包，不再使用跨仓库项目引用。

##### 验证

- `dotnet build Vex.slnx`
- 使用 ``极简 **之** 力，`妙`笔成章。`` 启动 Vex 并截图验证。
- `package_all.bat`

## v1.0.0 - 2026-05-24

### Release Title

Vex 1.0.0 - First Stable Release

### Release Notes

#### English

##### Added

- Markdown source editing, live preview, outline navigation, and document statistics.
- New, open, save, save as, open folder, and recent document workflows.
- Drag-and-drop opening for files and folders, plus startup argument opening.
- File rename/delete and external file change detection.
- Find and replace with match options, match count, replace next, and replace all.
- HTML, print preview, PNG, PDF, and Word `.docx` export.
- Copy to WeChat Official Account, Zhihu, and Juejin.
- Theme, typography, compact layout, line number, status bar, and always-on-top options.
- First-run guide, About window, quick start, acknowledgements, and localized help documents.
- Simplified Chinese, Traditional Chinese, English, and Japanese UI resources.
- Multi-RID publish scripts, release zip packaging, SHA256 files, and release manifest.
- Optional Windows MSIX layout/package script.

##### Improved

- Optimized outline scanning, statistics, find counting, and preview refresh for large documents.
- PNG/PDF export supports task lists, themed output, inline table styles, headers, and footers.
- HTML, print preview, PNG, PDF, and social-copy output use the current typography theme and compact layout.
- Improved dark-theme styling for the editor, preview, status bar, menus, overlays, and exported output.

##### Fixed

- Improved Windows 7 startup compatibility by routing event bus usage through `CodeWF.EventBus.EventBus.Default`.
- Fixed several dark-theme text, menu, and overlay color issues.
- Fixed long find/replace input breaking the layout.
- Fixed image export issues for local paths, URL-decoded paths, SVG, GIF, and WebP images.

##### Verification

- `dotnet build Vex.slnx`
- `package_all.bat`

#### 简体中文

##### 新增

- Markdown 源码编辑、实时预览、大纲导航和文档统计。
- 新建、打开、保存、另存为、打开文件夹和最近文档。
- 拖拽打开文件/文件夹，支持启动参数打开文档。
- 文件重命名、删除和外部文件变更检测。
- 查找替换，支持匹配选项、命中计数、替换下一个和全部替换。
- HTML、打印预览、PNG、PDF、Word `.docx` 导出。
- 复制到微信公众号、知乎、稀土掘金。
- 主题、排版、紧凑布局、行号、状态栏和窗口置顶选项。
- 首次启动引导、关于窗口、快速开始、鸣谢和多语言帮助文档。
- 简体中文、繁体中文、英文、日文界面资源。
- 多 RID 发布脚本、release zip 打包、SHA256 文件、release manifest。
- 可选 Windows MSIX 布局/打包脚本。

##### 优化

- 大文档的大纲扫描、统计、查找计数和预览刷新做了性能优化。
- PNG/PDF 导出支持任务列表、主题化输出、表格 inline 样式和页眉页脚。
- HTML、打印预览、PNG、PDF 和自媒体复制会读取当前排版主题与紧凑布局。
- 暗色主题下优化编辑器、预览、状态栏、菜单、浮层和导出样式。

##### 修复

- Windows 7 启动兼容性改善，事件总线改为直接使用 `CodeWF.EventBus.EventBus.Default`。
- 修复多处深色主题下文本、菜单、浮层颜色不一致的问题。
- 修复长查找/替换输入撑乱界面的问题。
- 修复部分本地图片路径、URL 解码、SVG/GIF/WebP 导出缺图问题。

##### 验证

- `dotnet build Vex.slnx`
- `package_all.bat`
