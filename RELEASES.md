# GitHub Releases

This document keeps the GitHub release title and release notes for each published Vex version.
Add new releases at the top.

本文档记录 Vex 每个发布版本对应的 GitHub Release 标题和说明。
后续发布请在顶部追加新版本。

## v1.0.0 - 2026-05-24

### Release Title

Vex 1.0.0 - First Stable Release

### Release Notes

#### English

Vex 1.0.0 is the first stable release of Vex, a lightweight Markdown editor built with .NET 10 and Avalonia 12. This release marks the project as feature-complete enough for daily writing, editing, previewing, and document export workflows.

Highlights:

- Markdown editing with live preview, outline navigation, document statistics, smart list continuation, and source-mode focused writing.
- File workflows for new/open/save/save as, folder-based document browsing, recent documents, drag-and-drop opening, startup argument opening, file rename/delete, and external file reload detection.
- Find and replace with match options, current/total match count, replacement actions, and debounced large-document scanning.
- Export and sharing support for HTML, print preview, PNG, image-based PDF, Word `.docx`, copy-as-HTML, local/data/HTTP(S) images, SVG/GIF/WebP PNG normalization, task lists, themed output, and PDF headers/footers. PDF and Word exports embed image assets so shared files remain viewable offline.
- Multiple visual themes, typography options, compact layout support, dark-mode refinements, localized Help documents, localized changelogs, and localized error details.
- First-run guide, About window version/build information, acknowledgements, quick start documents, and multilingual UI resources for Simplified Chinese, Traditional Chinese, English, and Japanese.
- Release tooling for multi-RID publishing, packaged release archives, SHA256 files, release manifests, and optional Windows MSIX layout/package creation.
- Event bus usage now directly routes through `CodeWF.EventBus.EventBus.Default`, avoiding the DryIoc event-bus registration path and improving Windows 7 startup compatibility.

Recommended build verification:

- `dotnet build Vex.slnx`
- `publish_vex_all.bat --package`

#### 简体中文

Vex 1.0.0 是 Vex 的首个稳定版本。Vex 是基于 .NET 10 与 Avalonia 12 构建的轻量级 Markdown 编辑器，本版本的功能已经足以覆盖日常写作、编辑、预览和文档导出流程。

主要亮点：

- 支持 Markdown 编辑、实时预览、大纲导航、文档统计、智能列表续写，以及专注源码编辑的源码模式。
- 支持新建、打开、保存、另存为、文件夹文档浏览、最近文档、拖拽打开、启动参数打开、文件重命名/删除，以及外部文件变更检测与重载。
- 支持查找替换、匹配选项、当前/总命中计数、替换操作，并对大文档扫描做了防抖优化。
- 支持 HTML、打印预览、PNG、图像型 PDF、Word `.docx`、复制 HTML、本地/`data:image`/HTTP(S) 图片、SVG/GIF/WebP 转 PNG、任务列表、主题化导出，以及 PDF 页眉页脚；PDF 与 Word 会嵌入图片资源，文件分享后可离线查看。
- 支持多套视觉主题、排版选项、紧凑布局、暗色模式细节优化、本地化帮助文档、本地化更新日志和本地化错误详情。
- 提供首次启动引导、关于窗口版本/构建信息、鸣谢、快速开始文档，以及简体中文、繁体中文、英文、日文多语言 UI 资源。
- 提供多 RID 发布、压缩包产物、SHA256 文件、发布 manifest，以及可选 Windows MSIX 布局/打包脚本。
- 事件总线已统一直接使用 `CodeWF.EventBus.EventBus.Default`，避开 DryIoc 事件总线注册路径，改善 Windows 7 启动兼容性。

建议发布前验证：

- `dotnet build Vex.slnx`
- `publish_vex_all.bat --package`
