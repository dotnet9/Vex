# 更新日志

## 1.1.2.3 (2026-06-08)

- 🔨[优化]-补齐根目录 logo.svg、logo.png、logo.ico 三件套，子工程通过 MSBuild Link 引用根 logo，避免维护多份图标副本。
- 🔨[优化]-统一目标框架：NuGet 包项目支持 `net8.0;net10.0`，Demo、App、测试与内部应用项目升级到 `net11.0` / `net11.0-windows`。
- 🔨[优化]-保留运行时帮助、Markdown 示例、内置备忘录和业务设计文档，仅收敛仓库级重复文档入口。

---

## 旧分支更新日志归档

# 更新日志

## 1.1.2.2 (2026-06-08)

- 统一版本号维护入口，只在仓库根目录 `Directory.Build.props` 中定义 `<Version>`。
- 清理英文/双语文档入口，后续仅维护简体中文文档。
- 完善 NuGet 发布配置，补充 Source Link、符号包和标签格式规范。


## 1.1.2.1 - 2026-06-02

### 新增

- ✨[新增]-重新生成 Vex 极简 Logo，并同步更新 `logo.svg`、`logo.png` 和 `logo.ico`，保证任务栏小尺寸下仍能清楚识别。

### 优化

- 🔧[优化]-将 Vex 应用级主题资源从 `App.axaml` 拆分到 `Vex.Controls.Themes`，按控件独立维护标题栏、菜单、列表、滚动条、页签和 Tooltip 样式，减少应用入口样式堆叠。
- 🔧[优化]-主题资源改为优先覆盖 Semi 既有资源键，统一窗口、标题栏、控制按钮、菜单、Tooltip、文件列表、状态栏和 Markdown 预览的前景、背景、边框与交互态颜色。
- 🔧[优化]-标题栏菜单项增加横向间距，文件/大纲页签、左侧文件列表和各类浮层在亮色、暗色、水生、沙漠、黄昏和夜空主题下保持更协调的可读性。
- 🔧[优化]-最近文件菜单项悬浮提示显示绝对路径，避免不同目录下同名文件难以区分。
- 🔧[优化]-关于窗口和鸣谢窗口调整为更接近专业软件的紧凑信息布局，鸣谢文档使用统一 Markdown 渲染和当前排版主题。
- 🔧[优化]-水生和黄昏主题在 Vex 中继承浅色排版分支，避免浅色应用壳层与 Markdown 深色引用块资源混用。
- 🔧[优化]-更新 Avalonia、Semi.Avalonia、CodeWF.AvaloniaControls、CodeWF.Markdown 和 Lang.Avalonia.Json 依赖版本，接入本地 CodeWF Markdown 包源以便验证自研库更新。

### 测试

- 🧪[测试]-构建 `src\Vex\Vex.csproj -f net10.0-windows` 与 DialogCapture，均为 0 警告 0 错误。
- 🧪[测试]-截图验证 Vex 主窗口、关于窗口和鸣谢窗口在亮色、暗色、水生、沙漠、黄昏和夜空主题下的标题栏、文件列表、菜单、Tooltip 与 Markdown 渲染配色。

## 1.1.1 - 2026-05-27

### 修复

- 🐞[修复]-Markdown 预览升级到 `CodeWF.Markdown` 12.0.3.15，同一行内普通文本与加粗、斜体、删除线混排时，强调样式会正确应用到对应片段，行内代码样式保持不受影响。
- 🐞[修复]-PDF/PNG/Word 导出共用 `CodeWF.Markdown` 图片加载与栅格化能力，支持相对本地图、`data:image`、HTTP(S) 图片、SVG 栅格化和 GIF/WebP 转 PNG；PDF 正文现在可选择、可复制，PDF 与 Word 也会嵌入图片资源，离线发送后仍可查看。
- 🔧[优化]-PDF/PNG/Word 导出实现下沉到 `CodeWF.Markdown` 12.0.3.15 的 `MarkdownDocumentExporter`，Vex 端统一调用 `ExportKind` 入口，只负责选择保存路径和传入当前排版主题，不再维护本地 Word/OpenXML、PDF 文本排版和 PNG 渲染器。
- 🔧[优化]-复制到公众号、知乎、稀土掘金现在调用 `CodeWF.Markdown` 12.0.3.15 的 `MarkdownHtmlClipboardExtensions.TrySetMarkdownHtmlAsync(markdown, themeName, targetName, typographySize)`，Vex 端只传当前 Markdown、排版主题和发布目标。
- ✨[新增]-从网页复制内容后粘贴到中间编辑器时，Vex 会优先读取剪贴板 HTML，通过 `MarkdownHtmlClipboard.Html2Markdown(htmlContent)` 转成 Markdown；没有 HTML 或转换失败时回落到 AvaloniaEdit 原生粘贴。
- 🔧[优化]-内置帮助 Markdown 文档收口为中文文件，项目输出只复制 `UpdateLog.md`、`QuickStart.md`、`ACKNOWLEDGEMENTS.md` 和 `Thanks.md`，帮助菜单不再解析已删除的多语言文档名。
- 🔧[优化]-自媒体平台 profile、inline HTML 渲染、图片嵌入、CF_HTML 写入和尾注/工具名多语言文案已下沉到 `CodeWF.Markdown`，Vex 不再维护本地公众号/知乎/掘金 HTML 模板。
- 🔧[优化]-视图菜单移除“实际大小”“放大”“缩小”，同步删除窗口级缩放快捷键和状态栏缩放显示。
- 🐞[修复]-更新日志、鸣谢和关于窗口的标题栏显示明确标题；鸣谢改为加载带 Markdown 链接的 `docs/ACKNOWLEDGEMENTS.md`，链接可点击打开。
- 🔧[优化]-左侧文件/大纲页签 header 改为等宽横向居中布局，选中项加粗，贴近参考图效果。
- 🧪[测试]-构建 `Vex.slnx`，确认 `net10.0` 与 `net10.0-windows` 均为 0 警告 0 错误，并复查无视图缩放入口残留。
- ✨[新增]-直接打开单个 Markdown/txt 文件后，左侧文件列表会自动加载同目录下支持的 Markdown 文档。
- 🔧[优化]-属性、字数统计和删除确认改为独立 UrsaWindow 对话框，长名称/路径可选择复制；对应旧浮层实现已移除。
- ✨[新增]-文件导出新增 Word `.docx` 输出，保留基础 Markdown 结构、Word 样式和图片嵌入；HTML、PDF、PNG、Word 导出成功后会打开保存位置并定位文件。
- 🔧[优化]-HTML、PDF、PNG 和社交复制导出更一致地读取当前排版/导出样式；PDF 页眉页脚元数据字体优先选择中文可用字体，减少中文乱码。
- 🔧[优化]-帮助菜单将“主题色”和“排版”提升为二级菜单，移除“视图 / 搜索”重复项，并为行号、状态栏、窗口置顶补齐菜单勾选和配置持久化。
- 🐞[修复]-查找和替换输入框限制 200 字符、强制单行输入，避免粘贴超长内容撑乱界面。
- 🔧[优化]-复制到公众号、知乎、稀土掘金会写入多语言富 HTML 剪贴板元数据，并补齐掘金后缀。
- 🐞[修复]-Windows `HTML Format` 使用 UTF-8 CF_HTML 字节载荷，粘贴到网页编辑器时不再显示原始 HTML 文本。
- 🔧[优化]-三条自媒体复制路径通过公共 `MarkdownExportStyle` 链路把当前排版主题和紧凑布局写入 inline style，标题色、正文色、链接色、表格边框、引用色、代码块背景和掘金尾注都会跟随当前主题。
- 🧪[测试]-更新 CodeWF Markdown 与 AvaloniaControls NuGet 包版本，构建 `Vex.slnx -m:1`，并执行 `git diff --check`。
- 🧪[测试]-新增 `scripts/stress_vex_markdown_services.ps1`，可生成真实大 Markdown 文本并计时大纲与统计服务。
- 🧪[测试]-默认 120,000 行压测通过：10,336,464 字符，大纲 154ms，统计 498ms，并自动清理临时工作目录。
- 🐞[修复]-无路径且无文件名的文档导出时，打印预览和 PDF 页眉页脚改用 `DocumentDefaultFileName`/`DocumentDefaultHeading` 资源，不再在导出路径硬编码 `Untitled.md`。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证导出服务和 PDF 渲染器都读取默认文档名资源。
- 🔧[优化]-PNG/PDF 导出的表格单元格改为渲染段落 inline，保留粗体、斜体、删除线、行内代码和链接样式，不再全部压平成纯文本。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证表格单元格路径已移除纯文本压平逻辑。
- 🔧[优化]-大纲、PDF 页眉和 HTML 打印预览标题共用 `MarkdownHeadingScanner`，用 span 扫描跳过代码围栏内的示例标题，并减少重复逐行字符串分配。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证三条标题扫描路径都调用共享扫描器。
- 📚[文档]-刷新繁体中文和日文更新日志摘要，补充最近的导出、性能、暗色主题和 MSIX 打包改动。
- 🧪[测试]-构建 `Vex.slnx`，并确认本地化更新日志摘要仍由项目文件复制到输出目录。
- 🐞[修复]-HTML/打印/复制与 PNG/PDF 导出解析本地图片时会尝试 URL 解码后的路径，`my%20image.png` 可正确匹配带空格的本地文件名。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证两条图片导出链路都具备 URL 解码回退。
- 🔧[优化]-PNG/PDF 导出会识别任务列表状态，将 `- [ ]` 与 `- [x]` 渲染为 `[ ]`/`[x]` marker，不再退化为普通项目符号。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证导出渲染器接入 Markdig TaskList 状态。
- 🔧[优化]-Markdown 大纲扫描改为 `ReadOnlySpan<char>` 逐行解析，避免为每一行分配字符串；只在命中标题时创建标题文本，并补充跳过 `~~~` 代码围栏。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证大纲路径不再使用 `StringReader.ReadLine()`。
- 🔧[优化]-普通 `TextBlock` 默认前景色和右键 `ContextMenu` 背景、边框、菜单前景、悬停态改用 Vex 动态主题资源，减少暗色主题下文本或弹出菜单掉回浅色默认外观。
- 🧪[测试]-构建 `Vex.slnx`，并用 XAML 结构 smoke 验证文本与右键菜单样式均读取主题资源。
- 🐞[修复]-`package_vex_msix.ps1 -PrepareOnly` 不再因为目标 `.msix` 已存在而失败；包文件冲突只在真实打包时检查。
- 🧪[测试]-用临时 `publish/win-x64` 和预置 `.msix` 产物做 smoke，验证真实打包拒绝覆盖而 `PrepareOnly` 可生成 MSIX 布局和 manifest。
- 🔧[优化]-帮助菜单未知或空 topic 的状态栏和错误上下文不再回退固定英文 `Help`，空 topic 会使用当前语言的“帮助”菜单文案。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证 `ShellHelpViewModel` 已移除 `topic ?? "Help"`，四套资源均有对应帮助菜单与排期提示文案。
- 🔧[优化]-文件列表摘要改为有界流式扫描，最多读取前 8 行和 4096 个字符，避免超长单行 Markdown/txt 文件在文件夹加载时分配整行字符串。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证摘要路径使用 `StreamReader` 与 `ReadBoundedLine`，保留行数、字符数和摘要长度上限。
- 🔧[优化]-HTML 打印预览工具条、页眉页脚、打印背景和链接色改为读取当前排版导出样式，暗色排版主题下不再混入固定浅色工具条或白底元数据区域。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证打印预览 CSS 由当前导出样式动态生成，包含主题背景、正文色、边框色、链接色和分页保护规则。
- 🔧[优化]-打开大目录时先取前 300 个支持文件，再对这批文件排序展示，避免为了列表上限排序整个目录。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证文件夹扫描先 `Take(300)` 再排序。
- 🔧[优化]-替换下一个和全部替换改用 AvaloniaEdit 文档级 `Replace`，避免单次替换也重建整篇编辑器文本。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证替换路径使用 `editor.Document.Replace(...)`。
- 🔧[优化]-PDF 页面背景、页眉页脚元数据颜色和分页策略继续接入当前排版导出样式，暗色排版主题不再按白底假设处理页面样式。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证 PDF 背景、元数据颜色和空白带检测均读取导出样式。
- 🔧[优化]-查找 Count 路径改为单次扫描直接计算总数和当前命中索引，不再为全部命中分配 `SearchMatch` 列表。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证 Count 路径使用新的计数扫描，Find/Replace 路径仍保留完整命中列表。
- 🔧[优化]-查找栏全文匹配计数改为 180ms 防抖，连续输入或切换匹配选项时减少长文档重复扫描。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证查找计数防抖、面板打开立即计数和关闭取消挂起计数。
- 🔧[优化]-大文件连续输入时，未保存状态和草稿排队立即更新，预览、统计和大纲刷新合并到 220ms 防抖，减少每字符全量派生扫描。
- 🧪[测试]-构建 `Vex.slnx`，并用源码结构 smoke 验证文本变更防抖路径和轻量文档状态刷新入口。
- 🔧[优化]-标题菜单新增暗色主题下的动态前景、悬停和选中态颜色，主题、排版、语言和紧凑布局菜单会显示当前选中状态。
- 🧪[测试]-构建 `Vex.slnx`，并用 XAML 结构 smoke 验证主题、排版、语言和紧凑布局菜单的勾选状态配置。
- ✨[新增]-新增 `scripts/package_vex_msix.ps1`，可从 Windows 发布输出准备 full-trust MSIX 布局，并在 Windows SDK 可用时生成 MSIX、可选签名。
- 🧪[测试]-解析 MSIX 脚本语法，构建 `Vex.slnx`，并用临时 `publish/win-x64` 执行 `-PrepareOnly` smoke，验证 manifest、full-trust 入口、`runFullTrust` 能力、复制文件和 logo 资产。
- 🔧[优化]-帮助文档在 `zh-TW`、`zh-HK`、`zh-MO` 等传统中文区域优先回退繁体中文文档，再回退简体中文。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 console smoke 覆盖 `zh-TW`、`zh-HK`、`zh-SG` 与 `fr-FR` 的帮助文档解析结果。
- ✨[新增]-打印预览工具条支持纸张、边距和页眉页脚开关，正式打印时可显示固定文档标题页眉和文件页脚。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并用临时 console smoke 覆盖打印预览 HTML 的纸张/边距控件、页眉页脚节点和动态 `@page` 脚本。
- 🔧[优化]-HTML/打印、PNG 和 PDF 导出会读取当前 Markdown 排版主题与紧凑布局，共用导出样式映射，不再固定为单一浅色样式。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 console smoke 覆盖 `InkBlack + Small` 导出 HTML 的暗色背景、正文颜色、紧凑字号和链接色。
- 🔧[优化]-Markdown 统计的行数、段落、标题和横线统计合并为单次字符扫描，减少大文件逐行正则匹配开销。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 console smoke 覆盖 CRLF/LF 行数、段落、标题和横线统计。
- 🔧[优化]-帮助文档在未知非中文语言下优先回退英文文档，再回退简体中文，减少非中文环境直接显示中文文档的情况。
- 🔧[优化]-Markdown 大纲扫描改用手写 ATX 标题解析，减少长文档逐行正则匹配开销。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 console smoke 覆盖代码围栏跳过、三级标题和六级标题。
- 🔧[优化]-发布打包脚本支持逗号分隔 RID 参数，手动指定多个 RID 时不再被当成单个目录名。
- 🧪[测试]-用外层 `powershell -File` 传入逗号分隔 RID，验证可生成两个 RID 的 zip、SHA256 和 manifest。
- 🐛[修复]-系统 Shell 未能启动打印预览浏览器时会显示本地化错误详情，不再误报预览已打开。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并执行 `git diff --check`。
- 📝[文档]-README 新增 Build and Release 段落，说明构建、一键发布和压缩包打包入口。
- 🔧[优化]-Markdown 统计的正文词数/字符数改为单次字符扫描，减少大文件统计时的临时字符串和正则匹配分配。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 console smoke 覆盖英文、CJK、标题、段落、行数和阅读时间统计。
- 🔧[优化]-发布打包脚本改为先预检 manifest、发布目录和目标产物冲突，再开始压缩，避免失败时留下部分包。
- 🧪[测试]-使用临时发布目录验证正常打包，并预置第二个 RID 冲突确认脚本不会先生成第一个 RID 的部分产物。
- 🔧[优化]-打印预览临时 HTML 文件名会清理非法文件名字符并限制长度，避免特殊文档名导致预览创建失败。
- ✨[新增]-新增繁体中文与日文更新日志摘要文档，帮助菜单在 zh-Hant/ja-JP 环境下打开更新日志时不再回退到简体中文。
- 🧪[测试]-构建 `Vex.slnx` 并确认新增更新日志文档会复制到输出目录。
- 🐛[修复]-当前文档没有文件路径时的重载失败详情迁移到 i18n，不再固定显示英文异常。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并扫描常见英文 `throw new` 详情。
- 🔧[优化]-预览滚动比例使用编辑器维护的行数，避免大文件中仅移动光标也全量扫描 Markdown。
- 🧪[测试]-构建 `Vex.slnx`，并检查 `MarkdownTextChangedCommand` 调用点确认编辑器会传入 `Document.LineCount`。
- 🔧[优化]-关于窗口链接和 Shell 浮层遮罩改用主题资源，暗色主题下链接更清晰、遮罩更稳定。
- 🧪[测试]-构建 `Vex.slnx` 并扫描 AXAML 固定色值，确认控件级硬编码颜色已收敛到主题资源。
- 🐛[修复]-PDF/PNG 导出渲染失败详情继续迁移到 i18n，PDF 创建、位图解码和 SVG 栅格化失败不再固定显示英文内部异常。
- 🧪[测试]-解析四套 i18n JSON，并构建 `Vex.slnx` 验证导出渲染器本地化改动。
- ✨[新增]-新增 `scripts/package_vex_artifacts.ps1`，可把 `publish/<RID>/` 打包为 `artifacts/release/` 下的 zip、SHA256 和发布 manifest。
- 🔧[优化]-`publish_vex_all.bat --package` 支持在全部 RID 发布成功后生成压缩包发布产物，且默认不覆盖已有产物。
- 🧪[测试]-构建 `Vex.slnx`，使用临时发布目录执行打包 smoke，并验证一键发布脚本未知参数会输出用法后停止。
- 🔧[优化]-发布 Profile 不再自动删除 `publish/<RID>/` 目录中的已有文件，避免清理用户未确认的发布产物。
- 🧪[测试]-构建 `Vex.slnx`，并用 MSBuild 属性评估验证 win-x64 与 linux-x64 发布 Profile 的输出目录和删除策略。
- 🐛[修复]-帮助文档缺失时的错误详情改为本地化文案，不再固定显示英文内部消息。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并用临时 smoke 验证帮助文档缺失详情。
- 🐛[修复]-文件重命名失败详情改为本地化文案，空文件名、重名、非法字符和不支持扩展名等错误不再显示英文内部消息。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并用临时 smoke 验证重命名边界错误详情。
- 🔧[优化]-打印预览 HTML 新增屏幕工具条，自动打印被浏览器拦截时可手动点击打印或关闭，正式打印时工具条会隐藏。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 smoke 程序验证打印预览 HTML 包含工具条、打印隐藏样式和自动打印脚本。
- 🔧[优化]-PDF 分页优先选择连续空白带作为断点，减少切到段落、表格或代码块边缘的概率。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 smoke 程序生成 PDF 验证连续空白带分页。
- 🔧[优化]-PDF 导出新增页眉页脚，页眉显示文档标题，页脚显示文件名和 `当前页 / 总页数`，并为元数据区域预留空间避免正文重叠。
- 🧪[测试]-构建 `Vex.slnx`，并用临时 smoke 程序生成带页眉页脚的 PDF 验证。
- 🔧[优化]-帮助菜单的“鸣谢”会按当前语言打开本地化文档，并补齐繁体中文与日文的快速开始、鸣谢文档。
- 🐛[修复]-新手引导卡片箭头会在弹层被窗口边缘滑动修正后重新对准目标中心，文件菜单、子菜单和帮助菜单引导不再出现箭头偏移。
- 🐛[修复]-`CodeWF.Markdown` 和 `CodeWF.Markdown.Themes` 改回 nuget.org 当前可还原的 12.0.3.6，避免 12.0.3.7 尚不可用时阻塞构建。
- 🐛[修复]-Vex 显式引用实际使用的 `CodeWF.Markdown` 包，使 Markdown 包内 `I18n/CodeWF.Markdown/*.json` contentFiles 正常输出，代码块复制按钮不再显示 `CodeWF.MarkdownL.Copy`。
- ✨[新增]-关于窗口显示程序版本号和编译时间，编译时间通过 `CodeWF.Tools.Core` 的 `AssemblyExtensions.CompileTime()` 获取。
- 🔧[优化]-标题栏菜单减少一级菜单数量：主题和国际化移动到帮助菜单下；文件菜单保留常用项直接点击，只保留最近打开、重新编码打开和导出格式这类必要子菜单。
- 🔧[优化]-Vex 改回从 NuGet 平台还原 `CodeWF.AvaloniaControls`、`CodeWF.AvaloniaControls.Themes` 12.0.3.7 以及 `CodeWF.Markdown`、`CodeWF.Markdown.Themes` 12.0.3.6，移除临时本地包源和本地包文件。

## 0.1.0 - 2026-05-22

### 新增

- 🐛[修复]-`CodeWF.Markdown.Themes` 升至 NuGet 平台 12.0.3.6，Markdown 预览会按当前文档路径解析相对图片，SVG 正常渲染，GIF 可持续播放。
- 🔧[优化]-HTML 导出、打印预览和复制自媒体会把本地图片内联为 data URI，避免临时 HTML 或跨目录导出后相对图片丢失。
- 🔧[优化]-PNG/PDF 导出渲染器同步支持本地 SVG 图片栅格化，导出结果不再因 SVG 图片加载失败而缺图。
- 🐛[修复]-`CodeWF.AvaloniaControls` 升至 NuGet 平台 12.0.3.6，修正 Guide 边缘对齐步骤的弹层方位，文件菜单等靠边引导不再跑出窗口。
- ✨[新增]-编辑器普通回车支持智能换行，自动延续缩进、引用、无序列表、有序列表和任务列表，空列表项回车会结束列表。
- 🧪[测试]-从 NuGet 平台还原 CodeWF 12.0.3.6 包；构建 `Vex.slnx`，完成 NuGet 漏洞扫描，并截图验证 SVG/GIF 预览与文件菜单引导位置。
- ✨[新增]-新增新手引导，首次启动自动展示，之后可从帮助菜单再次打开。
- ✨[新增]-引导会覆盖文件、段落、格式、视图、主题和帮助等关键菜单，并在菜单步骤自动展开对应菜单后再高亮说明。
- ✨[新增]-引导新增“大纲入口”和“大纲导航”步骤，会先指向视图菜单里的“大纲”，再自动切换到左侧大纲页签展示说明。
- 🔧[优化]-新手引导按钮、标题和描述接入四套本地化资源，切换语言后引导文案同步变化。
- 🔧[优化]-`CodeWF.AvaloniaControls` 12.0.3.6 改为从 NuGet 平台还原，移除临时本地包源和本地包文件。
- 🧪[测试]-使用 NuGet 平台源还原，构建 `Vex.slnx`、执行 `git diff --check` 和 NuGet 漏洞扫描，并截图验证引导首屏、文件菜单展开、大纲步骤和英文引导首屏。
- 🔧[优化]-打印预览生成独立打印模式 HTML，加入打印媒体样式、断页保护，并在浏览器加载后自动打开打印入口。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并检查打印 HTML 生成逻辑包含 `@media print` 与 `window.print()`。
- 🔧[优化]-优化大文档编辑路径，大纲生成和统计扫描减少整篇文本拆分造成的临时分配。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并用临时 Release micro-benchmark 烟测 100k+ 字符 Markdown 的统计和大纲扫描。
- 🔧[优化]-修改状态、自动保存草稿和外部文件重载的 Markdown 比较改为逐字符换行归一化，减少大文档反复比较时的整篇字符串分配。
- 🧪[测试]-构建 `Vex.slnx` 并执行 `git diff --check`。
- ✨[新增]-查找/替换栏新增“区分大小写”和“整词匹配”选项，计数、查找和替换会统一遵守选项。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`、执行 `git diff --check`，并截图验证查找栏布局。
- ✨[新增]-查找/替换栏新增“正则匹配”选项，计数、查找下一个、替换下一个和全部替换支持正则表达式，并保留区分大小写与整词过滤。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`、执行 `git diff --check`，并截图验证查找栏正则选项显示正常。
- 🔧[优化]-查找下一个从文末回到文首时，状态栏会明确提示已循环到开头。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`、执行 `git diff --check`，并截图验证连续查找后的循环提示。
- 🔧[优化]-深色模式下查找栏和多个浮层改用主题资源，减少白底面板破坏深色界面的问题。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证深色查找栏和关于浮层。
- ✨[新增]-文件菜单支持导出 PDF，当前会生成图像型分页 PDF，优先保证文档可分发预览和本地图片随文件嵌入。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`、执行 `git diff --check`，并用临时 smoke 程序生成 PDF 验证。
- 🔧[优化]-帮助菜单打开内置文档时会优先使用当前语言版本，缺失时再回退到简体中文。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并检索确认帮助入口不再固定中文文件名。
- ✨[新增]-补齐英文快速开始和英文鸣谢文档，英文环境下帮助菜单可直接打开对应英文内容。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并确认英文帮助文档会复制到输出目录。
- 🔧[优化]-同步需求文档里的当前能力快照和未完成重点清单，后续迭代可继续按最新状态推进。
- 🧪[测试]-执行 `git diff --check`。
- 🔧[优化]-PDF 导出分页会优先在页尾附近的空白行断开，减少切到正文、表格或代码行的情况。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并用临时 smoke 程序重新生成 PDF。
- 🔧[优化]-收紧标题栏菜单间距，当前文件名改为紧跟菜单右侧显示，并从帮助菜单移除重复的“快速开始（引导）”。
- ✨[新增]-更新日志和鸣谢改为独立 UrsaWindow 窗口，使用 CodeWF.Markdown `MarkdownViewer` 渲染；鸣谢加载随程序输出的 `docs/Thanks.md`。
- 🔧[优化]-关于改为独立 UrsaWindow 窗口，不再使用 Shell 内自绘关闭按钮，网站地址可点击打开。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`、执行 `git diff --check`，并截图验证标题栏、帮助菜单和三个帮助窗口。
- 🔧[优化]-更新日志和鸣谢窗口尺寸收小，更新日志、鸣谢、关于窗口均只保留关闭按钮且不显示在系统任务栏。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证三个帮助窗口的标题栏按钮。
- ✨[新增]-文件菜单新增复制到公众号、知乎和稀土掘金，可将当前 Markdown 转为 HTML 富文本并写入剪贴板。
- 🧪[测试]-校验四套 i18n JSON，构建 `Vex.slnx`，执行 `git diff --check`，并截图验证文件菜单新增复制入口。
- ✨[新增]-导出菜单新增 PNG 导出，可将当前 Markdown 文档渲染为长图。
- 🔧[优化]-预览同步在当前 MarkdownViewer 包缺少源码行定位 API 时会自动回退到比例滚动。
- 🧪[测试]-校验四套 i18n JSON，构建 `Vex.slnx`，执行 `git diff --check`，并用临时 Avalonia smoke 程序生成 PNG 验证。
- ✨[新增]-文件列表右键菜单增加打开、打开文件位置和删除，删除会复用现有确认流程。
- ✨[新增]-文件列表支持右键重命名 Markdown/txt 文件，重命名后同步更新当前文档、标题和最近文件。
- ✨[新增]-当前打开文件支持外部变更监听，其他程序保存后会自动刷新编辑区、预览和文件列表摘要；有未保存编辑时不会自动覆盖。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并截图验证文件列表右键菜单、重命名弹层和外部文件变更自动刷新。
- 🔧[优化]-`CodeWF.Markdown.Themes` 12.0.3.3 改为从 NuGet 平台还原，移除临时本地包源和本地 nupkg。
- 🧪[测试]-使用 NuGet 平台源执行还原、构建 `Vex.slnx`，并完成 NuGet 漏洞扫描。
- ✨[新增]-文件打开、文件夹加载、保存、删除、导出、打印、帮助文档和系统打开失败时显示错误提示浮层。
- 🧪[测试]-解析四套 i18n JSON，构建 `Vex.slnx`，并截图验证锁定文件打开失败时显示错误浮层。
- ✨[新增]-编辑器光标移动时，右侧预览会按文档位置同步滚动。
- ✨[新增]-预览同步优先使用 CodeWF.Markdown 源码行定位，无法定位时回退到比例滚动。
- ✨[新增]-自动保存未保存草稿，崩溃或强制退出后重新打开同一文档会恢复草稿。
- 🐛[修复]-直接打开单个 Markdown 文件时，左侧文件列表同步显示该文件。
- ✨[新增]-主题色菜单补齐 Semi 的跟随系统、浅色、深色、水生、沙漠、黄昏、夜空。
- ✨[新增]-排版菜单补齐 CodeWF.Markdown.Themes 内置排版主题。
- 🔧[优化]-语言切换同步 Semi 和 Ursa 控件资源。
- 🔧[优化]-打开文件夹改为后台扫描，并跳过无权限目录或读取失败的文件摘要。
- ✨[新增]-表格、链接、图片插入支持上下文识别，可将选中的 URL、图片路径或分隔文本生成 Markdown。
- 🧪[测试]-构建 `Vex.slnx`，并截图验证长文档跳转到末尾后预览同步滚动。
- ✨[新增]-用户设置会通过 App.config 保存主题、排版、紧凑布局、语言、窗口尺寸、侧栏/预览/状态栏布局、编辑器缩放和行号显示状态。
- 🔧[优化]-深色模式覆盖主窗口背景、侧栏、编辑器、预览区、状态栏和状态徽标，编辑器当前行高亮会跟随主题切换。
- 🔧[优化]-首次启动未保存语言时，会根据系统语言选择简体中文、繁体中文、日文或英文，不再固定为单一默认语言。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check` 和 NuGet 漏洞扫描，并截图验证设置持久化接入后主窗口、编辑器、预览和状态栏正常渲染。
- [优化] 将查找/替换面板拆分为独立 `ShellFindBarView` 与 `ShellFindBarViewModel`，通过 CodeWF.EventBus 与编辑器通信，降低主窗口和主 ViewModel 复杂度。
- [优化] 将主题、排版、紧凑布局和语言切换迁移到独立 `ShellAppearanceViewModel`，主窗口通过 Prism IoC 组合外观模块。
- [优化] 将撤销、重做、剪贴板、全选和 Markdown 插入命令迁移到独立 `ShellEditorActionsViewModel`，统一通过 CodeWF.EventBus 路由到编辑器。
- [优化] 将状态栏文本和光标位置迁移到独立 `ShellStatusViewModel`，由 CodeWF.EventBus 订阅状态和编辑器光标消息。
- [优化] 将编辑器缩放、字号和状态栏缩放文本迁移到独立 `ShellEditorDisplayViewModel`。
- [优化] 将最近文件菜单状态和持久化迁移到独立 `ShellRecentDocumentsViewModel`。
- [优化] 将侧栏文件列表、大纲、侧栏 Tab 选择和导航选中状态迁移到独立 `ShellNavigationViewModel`，文件选择通过 CodeWF.EventBus 请求主 Shell 打开。
- [优化] 将统计、关于、属性、删除确认和未保存确认浮层状态迁移到独立 `ShellDialogsViewModel`，浮层通过 `Dialogs.*` 绑定并由 CodeWF.EventBus 回写状态栏。
- [优化] 将侧栏、预览区、状态栏、源码模式、置顶和全屏状态迁移到独立 `ShellWindowLayoutViewModel`，源码模式通过 CodeWF.EventBus 回写状态并聚焦编辑器。
- [优化] 将窗口标题、保存状态、编码、文件大小、统计和属性面板文本迁移到独立 `ShellDocumentInfoViewModel`，界面统一通过 `DocumentInfo.*` 绑定。
- [优化] 将帮助菜单动作迁移到独立 `ShellHelpViewModel`，关于入口复用浮层模块，状态反馈通过 CodeWF.EventBus 发布。
- [新增] 统计模型新增段落数、标题数和预估阅读时间，统计浮层同步展示 Paragraphs、Headings 和 Reading。
- [新增] Markdown 编辑器新增右键菜单，支持撤销重做、剪贴板、全选、常用格式、标题/引用/列表和清除样式动作。
- [优化] 编辑器光标移动时会通过 CodeWF.EventBus 更新状态栏行列号，不再只依赖文本变化刷新。
- [新增] 视图菜单新增“显示行号”，可切换 AvaloniaEdit 编辑器行号列显示。
- [优化] “保存全部打开的文件”改为独立当前文档保存流程，并明确提示多文档保存尚不可用。
- [新增] 文件夹扫描、拖放和启动路径打开补齐 `.mdown` 文档扩展支持。
- [新增] 编辑器支持 Tab/Shift+Tab 缩进与反缩进，选区会按行批量处理。
- [新增] 接入 `Vex.VexL` JSON 本地化资源，标题栏菜单、侧栏、查找栏和状态栏首批文案支持运行时语言切换。
- [优化] 标题栏菜单拆分为独立 `ShellTitleMenuView` 与 `ShellTitleMenuViewModel`，通过 Prism AutoWireViewModel 和 CodeWF.EventBus 降低主窗口耦合。
- [优化] 主窗口改为继承 Ursa `UrsaWindow`，标题栏控制按钮和拖动区域交由 Ursa 内置窗口模板负责。
- [优化] 状态栏拆分为独立 `ShellStatusBarView` 与 `ShellStatusBarViewModel`，主窗口不再直接承载状态栏布局。
- [优化] 文件和大纲侧栏拆分为独立 `ShellFilesView`、`ShellOutlineView` 及对应 ViewModel，并通过 Prism Region 注入到侧栏 `TabControl`。
- [优化] 文件列表、大纲列表、文件选择恢复和侧栏页签切换改为 CodeWF.EventBus 消息通信，减少 ViewModel 之间的直接引用。
- [优化] 窗口级快捷键路由迁移到独立 `ShellKeyboardShortcutViewModel`，主窗口 code-behind 只负责转交键盘事件。
- [优化] Markdown 编辑器右键菜单改为使用 `Vex.VexL` 本地化资源绑定，撤销、重做、剪贴板、格式和列表等动作不再硬编码中文。
- [优化] 中文、繁中、日文侧栏空状态文案改为本地语言显示，减少英文 fallback。
- [规划] 需求文档补充 Typora 风格可视化编辑模式，后续 CodeWF.Markdown 需支持可编辑预览开关，Vex 默认隐藏源码编辑器并允许预览区直接编辑。
- [优化] Markdown 编辑器文本变更算法拆分为 `IMarkdownEditorMutationService` 与 `MarkdownEditorMutationService`，控制器专注 EventBus、同步、搜索和导航。
- [优化] `MainWindowViewModel` 精简到约 480 行，新窗口启动迁移到 Shell action 协调器，文档扩展名校验统一下沉到 `IDocumentService`。
- [优化] 关于浮层的产品标语、作者、品牌和说明迁移到 `Vex.VexL` 本地化资源，View 中不再硬编码这些中文/英文文案。
- [优化] 统计、属性、删除确认和未保存确认浮层标签继续迁移到 `Vex.VexL`，阅读时间文本改由 `ShellDocumentInfoViewModel` 生成本地化格式。
- [优化] `ShellOverlaysView` 拆分为统计、关于、属性、删除确认和未保存确认 5 个独立 UserControl，宿主 View 只保留组合职责。
- [优化] 新增 `IShellStatusPublisher` 统一发布状态栏消息，多个 Shell 子 ViewModel 不再重复持有 EventBus 和 `SetStatus` 包装方法。
- [优化] `IShellStatusPublisher` 增加本地化资源发布能力，行号、最近文件、帮助菜单和语言切换状态反馈改为复用 `Vex.VexL`。
- [优化] 新增 Shell 运行时本地化服务和文档流程文案服务，`MainWindowViewModel` 的状态反馈与未保存确认文案改为复用 `Vex.VexL`。
- [优化] 对话浮层、源码模式和查找栏的固定状态反馈继续迁移到 `Vex.VexL`，减少 Shell 子 ViewModel 中的硬编码英文。
- [优化] 将运行时本地化门面提升到 Core 层，Workspace 编辑器搜索结果消息也改为复用 `Vex.VexL`。

- 创建 Vex（维刻）Markdown 编辑器基础版本。
- 完善作者、码坊 CodeWF 与官方网站信息。
- 新增 Typora 风格标题栏菜单、文件/大纲侧边栏、Markdown 编辑区、Markdown 预览区和状态栏。
- 新增文件的新建、打开、打开文件夹、保存、另存为、删除和打开文件位置入口。
- 新增编辑、段落、格式、视图、主题、国际化和帮助菜单的基础命令。
- 新增主题色、排版主题、紧凑布局和语言切换入口。
- 新增 `Vex.Controls` 与 `Vex.Controls.Themes` 控件主题包。
- 新增大纲点击跳转到编辑器对应标题行。
- ✨[新增]-帮助菜单支持打开随程序复制的更新日志、快速开始和鸣谢文档。
- ✨[新增]-为 Vex 主工程新增 `win-x64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64` 文件夹发布 Profile。
- ✨[新增]-新增根目录 `publish_vex_all.bat`，可一键依次执行所有 Vex 发布 Profile。
- ✨[新增]-新增裁剪发布免裁配置，保留 Vex、Avalonia、Prism、ReactiveUI、CodeWF 与 SVG 渲染相关程序集。
- ✨[新增]-标题栏与状态栏展示文档未保存状态，状态栏同步显示当前文件编码。
- ✨[新增]-文件菜单支持最近文件、清空最近文件、快速打开和关闭当前文档。
- ✨[新增]-新增查找/替换栏，支持查找下一个、替换下一个、全部替换以及 `Ctrl+F`、`Ctrl+H`、`F3`、`Esc` 快捷键。
- ✨[新增]-导出菜单支持将当前 Markdown 导出为独立 HTML 文件。
- ✨[新增]-格式菜单的清除样式可移除选区或当前行的常见 Markdown 标记。
- ✨[新增]-视图菜单的字数统计打开浮动统计面板，展示词数、字符数、行数、编码和保存状态。
- ✨[新增]-帮助菜单的关于入口打开关于面板，展示 Vex、维刻、作者、CodeWF（码坊）和官网信息。
- ✨[新增]-打印入口会生成临时 HTML 打印预览并交给系统默认浏览器打开。
- ✨[新增]-删除文件前显示确认浮层，提示永久删除并展示目标文件路径。
- ✨[新增]-菜单显示常用桌面快捷键，窗口级支持新建、打开、保存、另存为、打印、关闭、全屏和缩放快捷操作。
- ✨[新增]-支持从命令行或系统打开方式传入文件路径，启动后自动打开对应 Markdown 文件。
- ✨[新增]-支持启动时传入文件夹路径，自动加载左侧文档列表并打开首个 Markdown 文件。
- ✨[新增]-属性入口打开浮动属性面板，展示名称、状态、编码、大小和路径，并支持 `Alt+Enter` 打开。
- ✨[新增]-新建、打开、切换文件、编码重开、删除和退出前会在存在未保存内容时显示保存确认。
- ✨[新增]-查找栏显示当前匹配序号和总匹配数，例如 `1/12`。
- ✨[新增]-支持拖放打开 Markdown/TXT 文件和文件夹，文件夹会加载文档列表并打开首个文档。
- ✨[新增]-编辑器支持当前行高亮，便于长文档中定位正在编辑的位置。
- 🔧[优化]-`Esc` 可关闭属性、统计、关于等浮层；在删除确认中按 `Esc` 会取消删除。
- 🔧[优化]-保存确认浮层支持保存后继续、不保存继续和取消，避免风险操作直接丢弃当前编辑内容。
- 🔧[优化]-打开查找或替换时自动聚焦搜索输入框并选中文本。
- 🔧[优化]-拖放打开会复用未保存内容保护流程，不支持的文件类型会给出状态栏提示。
- 🔧[优化]-将统计、关于、属性和确认类浮层拆到独立 `ShellOverlaysView`，降低主窗口 XAML 复杂度。
- 🔧[优化]-为对话框状态模块补充必要中文维护注释，说明未保存确认的延续动作如何恢复。
- 🔧[优化]-为窗口布局模块补充中文维护注释，说明源码模式切换时如何保存并恢复原布局。
- 🔧[优化]-为文档展示状态模块补充中文维护注释，说明派生属性需要集中刷新，避免状态栏或属性面板漏更新。
- 🔧[优化]-为帮助模块补充中文维护注释，说明外部帮助文档、网站打开和 Shell 关于浮层的职责边界。
- 🔧[优化]-为 Markdown 统计服务补充中文维护注释，说明语法标记弱化和段落统计边界。
- 🔧[优化]-编辑器右键菜单复用 `EditorActions.*` 与 CodeWF.EventBus，不在 View 中新增文本变更逻辑。
- 🔧[优化]-为编辑器控制器补充中文维护注释，说明光标移动与文本变化共用状态更新消息的原因。
- 🔧[优化]-行号显示状态由 `ShellEditorDisplayViewModel` 管理，并通过 CodeWF.EventBus 写入状态栏反馈。
- 🔧[优化]-PDF/PNG 等未实现导出格式的状态栏提示改为明确的 `not implemented yet`。
- 🔧[优化]-`DocumentService` 抽出文档扩展名判断，确保文件夹扫描与文件选择器支持范围一致。
- 🔧[优化]-Tab 键处理通过 CodeWF.EventBus 转为编辑器动作，文本修改仍集中在 Workspace 控制器。
- 🔧[优化]-本地化资源沿用 CodeWF.Markdown 的 JSON + 强类型 key 类组织方式，`I18n\Language.tt` 可辅助后续维护资源 key。
- 🔧[优化]-文件类菜单动作改为由标题栏菜单 ViewModel 发布 `ShellActionCommand`，主 Shell 仍统一处理未保存确认和文件 I/O。
- 🔧[优化]-移除 Vex 自绘窗口控制按钮样式和事件处理，关闭未保存确认改为接入 `UrsaWindow.CanClose()`。

### 优化

- 解决方案文件切换为 `.slnx`。
- CodeWF 相关依赖改为 NuGet 包引用。
- 🔧[优化]-界面主题切换到 Semi.Avalonia 与 Ursa.Semi，并保留开源 Avalonia.Themes.Fluent 适配 AvaloniaEdit。
- 🔧[优化]-移除 CommunityToolkit.Mvvm，ViewModel 改为 ReactiveUI，并让菜单直接绑定 public 方法。
- 🔧[优化]-CodeWF.EventBus 改为通过 DryIoc 注册服务，并使用 `[EventHandler]` 方法处理编辑器动作与导航消息。
- 🔧[优化]-新增 Windows AOT/Win7 与 Linux/macOS self-contained single-file 发布配置。
- 🔧[优化]-发布 Profile 公共配置抽到 `FolderProfile.Common.props`，发布产物统一输出到根目录 `publish\<RuntimeIdentifier>\`，非 Windows self-contained single-file 发布启用裁剪。
- 🔧[优化]-通过中央传递钉版消除旧 `System.Drawing.Common` 解析带来的 NU1904 告警。
- 🔧[优化]-将 `Avalonia.Markup.Xaml.Loader` 传递依赖钉到 Avalonia 12.0.3，避免发布链路混入旧版本运行时加载器。
- 🔧[优化]-属性菜单显示当前文档状态、编码、大小和路径信息。
- 🔧[优化]-启动状态栏恢复为 Ready，文件/大纲侧边栏为空时显示空状态。
- 🔧[优化]-视图菜单的大纲/文档列表会自动展开侧边栏，源代码模式可临时隐藏侧栏和预览并再次切换恢复。
- 🧪[测试]-构建 `Vex.slnx`、执行依赖漏洞扫描、桌面启动烟测，并验证 `win-x64` Release Native AOT 与 `linux-x64` self-contained single-file 发布链路。
- 🧪[测试]-执行 `publish_vex_all.bat`，确认五个 Vex 发布 Profile 均可成功发布。
- 🧪[测试]-构建 `Vex.slnx`，确认查找/替换栏相关改动编译通过，并截图验证主窗口基础布局。
- 🧪[测试]-截图验证默认窗口的侧边栏空状态、三栏布局和状态栏 Ready 文案。
- 🧪[测试]-核查 Markdig 为 BSD-2-Clause 许可证，构建 `Vex.slnx` 并执行 NuGet 漏洞扫描。
- 🧪[测试]-构建 `Vex.slnx`，确认清除样式编辑器动作编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认视图模式切换逻辑编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认字数统计面板编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认关于面板编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认打印预览路径编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认删除确认浮层编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认快捷键绑定编译通过。
- 🧪[测试]-构建 `Vex.slnx`，并通过启动参数打开临时 Markdown 文件截图验证。
- 🧪[测试]-通过启动参数打开临时 Markdown 文件夹截图验证文件列表、默认文档和预览。
- 🧪[测试]-构建 `Vex.slnx`，确认属性面板与快捷键绑定编译通过。
- 🧪[测试]-构建 `Vex.slnx`，确认浮层 `Esc` 关闭逻辑编译通过。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证保存确认浮层居中显示且按钮不溢出。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证查找栏聚焦、计数和状态栏匹配提示。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证拖放路径打开后的标题、编辑区、预览和状态栏。
- 🧪[测试]-构建 `Vex.slnx`，并截图验证当前行高亮在编辑区正常显示。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证拆分后的关于浮层显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证对话框状态抽取后的 `Dialogs.*` 浮层绑定显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证窗口布局抽取后的 `Layout.*` 三栏布局、预览区和状态栏显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证文档展示状态抽取后的标题栏和状态栏 `DocumentInfo.*` 绑定显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证帮助模块可打开关于浮层并更新状态栏。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证扩展统计浮层的新字段显示正常且不溢出。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证编辑器右键菜单弹出层显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证光标移动到文末后状态栏行列号正确更新。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证行号列显示正常且不挤压正文。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证保存全部当前阶段的状态栏提示不误导。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证 `.mdown` 文件可通过启动参数正常打开。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证 Tab 缩进后编辑区和预览区同步更新。
- 🧪[测试]-构建 `Vex.slnx`，并分别截图验证默认中文与 `en-US` 下标题栏菜单、侧栏和状态栏本地化显示正常。
- 🧪[测试]-构建 `Vex.slnx`，并截图验证 Ursa 标题栏内置控制按钮、菜单、文档标题和三栏内容布局显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证 Prism Region 文件页签、文件列表、状态栏和大纲页签显示正常。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证 `Ctrl+F` 仍可打开并聚焦查找栏。
- 🧪[测试]-校验全部 Vex JSON 本地化资源、构建 `Vex.slnx` 并执行 `git diff --check`，确认状态消息本地化改动编译通过。
- 🧪[测试]-校验全部 Vex JSON 本地化资源、构建 `Vex.slnx`、执行 `git diff --check`，并启动 Debug 桌面程序确认主窗口句柄可创建。
- 🧪[测试]-校验全部 Vex JSON 本地化资源、构建 `Vex.slnx`、执行 `git diff --check`，并检索确认本轮迁移的英文状态文本不再留在 Shell ViewModel 中。
- 🧪[测试]-校验全部 Vex JSON 本地化资源、构建 `Vex.slnx`、执行 `git diff --check`，并检索确认旧编辑器搜索结果英文文案已迁移。
- 🔧[优化]-本地化文件选择器标题、文件类型名称、最近文件空文案、文件列表修改时间和大纲跳转反馈。
- 🔧[优化]-抽取 `IDocumentFileFactory`/`DocumentFileFactory`，让 `DocumentFile` 保持简单展示模型，Workspace 展示格式化逻辑可复用。
- 🧪[测试]-校验全部 Vex JSON 本地化资源、构建 `Vex.slnx`、执行 `git diff --check`，并用临时 Markdown 文件夹启动 Debug 程序；当前桌面会话仍阻止自动截图。
- 🔧[优化]-将语言切换与语言变更通知收口到 `IAppLocalizer`，Shell ViewModel 不再直接访问 `I18nManager`。
- 🧪[测试]-校验 JSON 资源、构建 `Vex.slnx`、执行 `git diff --check`、检索剩余本地化直接访问，并完成 Debug 启动烟测。
- 🔧[优化]-本地化 HTML 导出保存对话框和文件类型名称，并让导出的 HTML `lang` 属性跟随当前应用语言。
- 🧪[测试]-校验 JSON 资源、构建 `Vex.slnx`、执行 `git diff --check`，并完成 Debug 启动烟测。
- 🔧[优化]-将未保存确认守卫和文档工具动作拆成独立 Shell 服务，`MainWindowViewModel.cs` 从 576 行降到 495 行。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并完成 Debug 启动烟测。
- 🔧[优化]-新建文档默认模板和兜底文件名改走 `VexL` 资源。
- 🧪[测试]-校验 JSON 资源、构建 `Vex.slnx`、执行 `git diff --check`、检索旧硬编码模板，并截图确认默认文档本地化显示。
- 🔧[优化]-将编辑器查找/替换行为拆入 `MarkdownEditorSearchService`，`MarkdownEditorController.cs` 从 493 行降到 298 行。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并在搜索服务重构后截图确认启动窗口正常渲染。
- 🔧[优化]-将最近文件持久化拆入 `IRecentDocumentStore`/`RecentDocumentStore`，ViewModel 只维护最近文件菜单状态，文件 I/O 由服务承担。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并在最近文件存储重构后截图确认启动窗口正常渲染。
- 🔧[优化]-将右侧 Markdown 预览拆成 `MarkdownPreviewView`/`MarkdownPreviewViewModel`，通过文档状态和外观状态服务同步内容与排版。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图确认拆分后的预览能渲染初始文档。
- 🔧[优化]-将状态栏初始 `Ready` 文案接入 `StatusReady` 本地化资源，并在状态栏仍为空闲状态时随语言刷新。
- 🧪[测试]-校验 JSON 资源、构建 `Vex.slnx`、执行 `git diff --check`、检索硬编码 `Ready`，并截图确认启动状态文案已本地化。
- 🔧[优化]-将启动参数、拖放和最近文件路径分类拆入 `ShellExternalPathResolver`，文件系统检查不再散落在 `MainWindowViewModel`。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并用启动参数截图确认 Markdown 文件可通过解析服务直接打开。
- 🔧[优化]-将编辑器动作执行拆入 `MarkdownEditorActionService`，`MarkdownEditorController.cs` 从 298 行降到 166 行。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图确认编辑器和预览仍正常渲染。
- 🔧[优化]-新增 `MarkdownEditorTemplateService`，将格式化、链接、图片、表格、代码块和公式块的插入占位模板接入本地化资源。
- 🧪[测试]-校验 JSON 资源、构建 `Vex.slnx`、执行 `git diff --check`、检索旧硬编码模板，并完成启动截图。
- 🔧[优化]-新增 Prism AutoWire 的 `MarkdownEditorViewModel`，移除 `MarkdownEditorView.axaml.cs` 对 `MainWindowViewModel` 和 `ContainerLocator` 的直接依赖。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图确认编辑器仍能同步初始文档。
- 🔧[优化]-新增 `IEditorDisplayState`/`EditorDisplayState`，Workspace 编辑器通过 Core 服务契约读取编辑器显示设置，不再依赖 Shell 显示 ViewModel。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`、检索 Workspace 对 Shell 显示 ViewModel 的引用，并完成启动截图。
- 🔧[优化]-新增 `ShellDroppedPathReader`，将拖放本地路径读取、Storage API 和文件系统检查从 `MainWindow.axaml.cs` 抽离。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并在拖放路径读取服务重构后完成启动截图。
- 🔧[优化]-新增可复用 `FocusOnVisible` 附加行为，将查找栏聚焦和全选逻辑从 `ShellFindBarView.axaml.cs` 抽离。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图验证 `Ctrl+F` 可打开查找栏且搜索框获得焦点。
- 🔧[优化]-将 Shell 置顶和全屏窗口状态改为直接绑定 `ShellWindowLayoutViewModel`，移除 `MainWindow.axaml.cs` 中手写的 `PropertyChanged` 桥接。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图确认窗口状态绑定重构后 Shell 仍正常渲染。
- 🔧[优化]-拖放本地路径改为通过 `ShellDroppedPathCommand` 和 `ShellDropTargetHandler` 路由，`MainWindow.axaml.cs` 不再直接调用文档打开流程。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图确认拖放路由重构后 Shell 仍正常渲染。
- 🔧[优化]-启动参数改为通过 `ShellStartupArgumentsCommand` 和 `ShellStartupArgumentPublisher` 路由，启动文件/文件夹打开不再写在 `MainWindow.axaml.cs`。
- 🧪[测试]-构建 `Vex.slnx`、执行 `git diff --check`，并截图确认启动参数 Markdown 文件仍可通过 EventBus 路由打开。

## 归档：GitHub Release 文案

### v1.1.2 - 2026-05-27

##### 新增

- 从网页粘贴内容时，优先读取剪贴板 HTML 并转换为 Markdown。

##### 修复

- Markdown 预览现在能在同一行普通文本混排时，把加粗、斜体、删除线正确应用到对应文本片段。
- 中文普通文本后面的行内加粗现在能稳定显示为粗体。

##### 优化

- PDF、PNG、Word 导出统一改用 `CodeWF.Markdown` 12.0.3.16 的 `MarkdownDocumentExporter` / `ExportKind`。
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

### v1.0.0 - 2026-05-24

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
## 2026-06-08 仓库规范整理

- 统一文档维护入口：每个仓库只保留根目录 `README.md` 和根目录 `UpdateLog.md`，清理重复日志、英文文档和语言切换入口。
- 统一版本维护入口：包版本只在仓库根目录 `Directory.Build.props` 的 `<Version>` 节点维护，移除散落的程序集版本配置。
- 不再维护 `global.json`，SDK 选择交给本机或 CI 环境；NuGet 包和应用的目标框架在项目文件中明确声明。
- 统一 NuGet 包文档入口：包 README 统一引用仓库根 `README.md`，更新日志统一引用仓库根 `UpdateLog.md`。
