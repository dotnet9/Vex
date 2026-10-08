# Vex / CodeWF.Markdown 收尾任务（4 项）

## 2026-10-08 本轮结果

- 四项主要修复落地：侧栏菜单与勾选、六主题文件卡片、无编辑切换误报、实时渲染/输入/表格/源码保留。
- Demo 与 Vex 分别细核各自现行 design；Vex 标题栏 1px 下边框补齐并验证六主题实际像素。
- 草稿不删除、不标成已保存：恢复内容仍与磁盘基线比较。误报通过载入同步抑制修复，未采用旧建议中的草稿重置保存基线。
- 182 项 Release 测试通过，两个产品离屏交互/截图联调；最终开发版 14.0.1-dev.20261008.9，四包仅本地打包。
- 细节见 docs/Vex原型核对-2026-10-08.md 与库 docs/Demo原型核对-2026-10-08.md；源码行距、系统窗控/DPI/IME 未完全验收，不描述为全页逐像素一致。

> 交接文档：接手者请先读「现状」与「通用验证」，再按任务 1 → 4 顺序做。
> 任务 1/3 是小改动，任务 2 需要改控件模板，任务 4 工作量最大（要改块模型）。

## 现状（本次已完成、已验证）

- 库 `D:\github\libs\CodeWF.Markdown` 已重构为 **4 个包，全部 `net10.0`**：
  - `CodeWF.Markdown.Lite`：唯一渲染引擎（普通元素；代码单色、图片替代文本、公式原文，零能力依赖）
  - `CodeWF.Markdown`：能力（高亮/数学/Mermaid/图片/导出）**+ 编辑器**（`MarkdownEditorView` 源码编辑、`MarkdownLiveEditorView` 单栏实时）
  - `CodeWF.Markdown.Lite.Themes`：控件模板 + 排版令牌 + 18 套排版主题
  - `CodeWF.Markdown.Themes`：完整包样式入口 `MarkdownFullThemes`（= Lite.Themes + 图片控件外观）
  - 旧的 `CodeWF.Markdown.Editor/Export/Highlighting/Images/Math/Mermaid` 项目已删除；`pack.bat` 已按 4 包更新，`artifacts\packages` 下 4 个 nupkg 打包通过。
- 库测试：`dotnet test tests\CodeWF.Markdown.Tests` = **141 通过**。
- Demo（`src\CodeWF.Markdown.Sample`）：工具栏按原型恢复「编辑 / 分栏 / 实时 / 预览」四段；「预览」模式的只读渲染列不再被 `IsVisible` 误隐藏；预览列补了原型的面板头。
- Vex 已切到 4 包（`Vex.csproj` + `Directory.Packages.props`），`App.axaml` 样式入口改为 `MarkdownFullThemes`，rebuild **0 错**。
- 已修两个隐蔽坑，接手时不要回退：
  1. `MarkdownFullThemes` 跨程序集继承 `MarkdownThemes` 时，宿主的 Avalonia XAML 编译器解析不到继承来的 `TypographyTheme`/`TypographySize`（报 `AVLN2000`）→ 基类属性改 `virtual`、派生类 `override`（`src\CodeWF.Markdown.Themes\MarkdownFullThemes.axaml.cs`）。
  2. **NuGet 全局缓存同版本号不会刷新**：改完库必须提升 `Directory.Build.props` 的 `Version`（或手动删 `%USERPROFILE%\.nuget\packages\codewf.markdown*`），否则 Vex 一直用旧 dll，表现为"改了没生效"。

## 通用验证

```powershell
# 库
cd D:\github\libs\CodeWF.Markdown
dotnet build CodeWF.Markdown.slnx -c Debug
dotnet test tests\CodeWF.Markdown.Tests\CodeWF.Markdown.Tests.csproj -c Debug
# 改库后要重新打包 + 清缓存
.\pack.bat            # 产物在 artifacts\packages，先提高 Version
# Vex
cd D:\github\apps\Vex
dotnet build src\Vex\Vex.csproj -c Debug -f net10.0
```

---

## 任务 1 视图菜单「显示/隐藏侧边栏」→「侧边栏」+ 右侧勾选

需求：菜单项文案改为「侧边栏」（英文 Sidebar 等），显示/隐藏状态用**右侧勾**表达（勾选态 = 侧边栏显示）。
同项目里 `源码模式`/`预览`/`行号` 等已经是 `ToggleType="CheckBox"` + `IsChecked` 的写法，照抄即可。

改动点：
- `src\Vex\Modules\Shell\Views\ShellTitleMenuView.axaml:113`
  当前：`<MenuItem Header="{i18n:I18n {x:Static l:VexL.ToggleSidebar}}" Command="{Binding Layout.ToggleSidebar}" />`
  改为：加 `IsChecked="{Binding Layout.IsSidebarVisible}" ToggleType="CheckBox"`（命令仍用 `Layout.ToggleSidebar`，它内部是取反赋值，语义正确）。
- 文案：`src\Vex\I18n\zh-CN.json:87`、`zh-Hant.json:87`、`en-US.json:87`、`ja-JP.json:87` 的 `ToggleSidebar`：
  「显示 / 隐藏侧边栏」→「侧边栏」；`Show / Hide Sidebar`→`Sidebar`；`顯示 / 隱藏側邊欄`→`側邊欄`；`サイドバーを表示 / 非表示`→`サイドバー`。
  （`I18n\Language.cs:98` 的 key 常量不用动；四个 json 是运行期资源，`Language.tt` 只生成 key 常量。）
- 原型同步：`design\index.html` 视图菜单那段（约 245 行附近是菜单栏，"视图(V)" 菜单项在其中），以及 `design\assets\theme.js:855` 的 toast 文案（`侧边栏已显示/已隐藏` 可保留，但要确认菜单项名称与勾选表现与实现一致）。

验收：菜单里是「侧边栏」并带勾，点击后勾选状态与实际侧栏显隐一致，重启后状态保持（`IsSidebarVisible` 已持久化到设置）。

## 任务 2 水生（aquatic）/ 黄昏（dusk）主题下文件列表前景、背景色错乱

现象：截图（水生）里侧栏文件行底色发黑、标题/摘要文字对比度错；黄昏主题同样。
已核对的结论（不要把时间花在这里）：
- `src\Vex.Controls.Themes\Themes\Shared\AppPalette.axaml` 六个主题字典（Light / Dark / Aquatic / Desert / Dusk / NightSky）**键齐全**（每个 72 个 key，逐块比对无缺失），不是"缺 key 导致继承到深色值"。
- 文件行模板在 `src\Vex\Modules\Shell\Views\ShellFilesView.axaml:18-65`，用的 class 是 `file-card` / `file-bar` / `side-heading` / `muted`。
- 行样式在 `src\Vex.Controls.Themes\Themes\Controls\ListBox.axaml:174-234`。

怀疑点（按优先级）：
1. `ListBox.axaml:194` 的 `TreeView.side-tree TextBlock { Foreground = TextBlockDefaultForeground }` 这类**宽松选择器**会命中 `TreeViewItem` 自身内建模板里的 TextBlock（Avalonia 内建主题与 Semi 主题都会自带），在 `ThemeVariantScope` 下 `GetDefaultValue` 会覆盖其它绑定（编译期 `AVLN3001` 警告可作线索）。同文件 194–227 一整套 `TextBlock` 选择器都应改成控件/模板级：给 TreeViewItem 建 ControlTheme，或用 `:is(TreeViewItem)` / 限定层级收紧选择器。
2. 行底色：`Border.file-card.selected` 用 `VexAccentSoftBrush`，确认水生 (#CDEFF5) / 黄昏 (#E3DAF8) 的实际取值是否被 Semi.Avalonia 自己的主题资源遮蔽（Semi 的 `SemiTheme` 也定义了同名/近似资源，`App.axaml` 里 `SemiTheme`/`UrsaSemiTheme` 在 `VexControlsTheme` 之前加载，理论上后者优先级更高，需要实测确认）。
3. 排查手法建议：在 Vex 里切到水生主题，用 DevTools（F12 或 `AttachDevTools`）选中那一行，直接看实际生效的 `Background`/`Foreground` 与其来源（LocalValue / Style / Theme）。

验收：水生、黄昏、夜空三个主题下，侧栏文件列表的选中行/普通行/标题/摘要/时间戳对比度正常，无黑底或同色隐形。

## 任务 3 切换文件时误报「切换文件前保存更改」

现象：没有手工改动，切文件仍弹出 `切换文件前保存更改?`（截图 3：`打开 快速开始.md 前，是否保存对 MCP功能实现方案.md 的更改?`）。
已定位的真实隐患（需要验证后再改）：
- `src\Vex\Modules\Shell\ViewModels\MainWindowViewModel.cs:624`：
  `_lastSavedMarkdown = snapshot.Markdown;` —— 这里用的是**磁盘内容**，而上一行 `_document` 取自 `RestoreDraftIfAvailable(snapshot)`（可能带回草稿正文）。若草稿在打开流程之后才落地，`Markdown != _lastSavedMarkdown` 就会被判定为"已修改"。
  两处 `ApplyDocument` 调用路径要一起看：`OpenDocumentFileCoreAsync`（`:470`）与 `SaveAsync`（`:485`，传 `updateMarkdown:false, restoreDraft:false`）。
- 判定口径：`src\Vex\Modules\Shell\ViewModels\ShellDocumentInfoViewModel.cs:33` 的 `IsModified`（比较 `_markdown` 与 `_lastSavedMarkdown`，行尾归一化）+ `MainWindowViewModel.cs:947` 传入 `DocumentInfo.IsModified`。
- 编辑器回抛路径：`src\Vex\Modules\Workspace\ViewModels\MarkdownEditorViewModel.cs:105-193`（`_syncingFromDocument` 抑制回抛）、库控件 `SetText` 不触发 `MarkdownChanged`（`MarkdownEditorView.cs:310`，有 `_suppressChanged`）。
- 现场证据：`%LOCALAPPDATA%\Vex\Drafts\` 下存在两份旧草稿（本次排查时看到，一份指向上月自测文件 `vex-avalonia-markdown-editor.md`，一份是 MCP 演示的 `薄荷园手记.md`）。**先清理这两份再复现**，否则容易误判。

修复方向（建议）：
1. 打开文档时把草稿正文也当作"最后保存内容"的基线（即 `_lastSavedMarkdown` 用 `_document.Markdown` 而不是 `snapshot.Markdown`），或在恢复草稿成功后立即重置基线。
2. 打开新文档期间对文本回抛加一道闸：编辑器/VM 侧在"载入中"不要接受外部文本变更回抛（现有 `_syncingFromDocument` 只覆盖 SetText 期间，异步恢复可能在其后）。
3. 补一条回归测试：模拟 `ApplyDocument(带草稿)` 后 `DocumentInfo.IsModified == false`。

验收：打开文件夹 → 不输入任何字符 → 在文件列表里连续切换 5 个文件，**不弹**保存确认；手动改一个字再切换，**要弹**。

## 任务 4 实时模式的渲染质量（重点）

现状与差距（截图 4/5）：`MarkdownLiveEditorView`（`src\CodeWF.Markdown\Editor\Controls\MarkdownLiveEditorView.cs`）目前是"块级渲染 + 点入就地编辑"的最小实现，离 Typora 式实时编辑还差得远。已知的具体缺陷：

1. **引用块的行内标记没有渲染**（截图 4）
   `> 微信公众号排版工具。问题或建议，请公众号留言。**[Dotnet9](#jump_8)**` 显示成一整段原始文本。
   原因：`src\CodeWF.Markdown\Editor\Controls\Wysiwyg\MarkdownBlockParser.cs:342-377`（`AppendQuote`）把引用内段落处理成 `MarkdownBlockKind.Quote` + `ExtractInlineText(...)`，而 `ExtractInlineText` 是**保留标记**的拼接（`:502-569`，`AppendInline` 会重新拼出 `**`、`[...](...)`、`\``）；随后 `MarkdownBlockView` 对 `Quote` 走 `MarkdownTextRunParser.Parse(...)` 渲染……但引用里 `[Dotnet9](#jump_8)` 是锚点链接，需要确认 `MarkdownTextRunParser`（`MarkdownTextRunParser.cs:119-127`）是否把它当链接产出 underline + LinkBrush。实测是**没有**变成链接，需要按最小可复现样例逐个修。
2. **有序列表序号错成圆点**（截图 5）
   `MarkdownLiveEditorView.cs` 的 `DecorateBlock`（`MarkdownBlockKind.ListItem` 分支，约 `:395-410`）：
   `Text = model.OrderedNumber > 0 ? $"{model.OrderedNumber}. " : "• "`
   而 Markdig 对有序列表的续行项给的是 `OrderedNumber = 0`（不在 `OrderedStart` 上），于是 `1.` 之后的所有项都显示成 `•`。
   修法：`MarkdownBlockParser.AppendList`（`:258-340`）里改为按列表类型给每项编号（`list.IsOrdered` 时序号从 `OrderedStart` 递增，现已有 `number++` 逻辑但只对 `number > 0` 生效且写进了 `OrderedNumber = isTask ? 0 : number`，需核对续行项路径）。
3. 其它已知差距（做实时编辑时一并考虑，参考 Typora / MarkText / VNote 的做法）：
   - 块内**所见即所得编辑**当前是"整块切回源码 TextBox"（`MarkdownBlockView.BeginEdit`），Typora 是"光标所在块的标记符隐藏、其余保持渲染"，体验差距主要在这里。
   - 列表续行/软换行、Shift+Enter 软换行、嵌套列表层级缩进、表格增删行列（`MarkdownTableView.StructureChangeRequested` 目前只 `RaiseChanged`，没有真正加行/列）。
   - 图片在实时视图里只显示替代文本（Lite 引擎行为），完整包下应可显示图片。
   - 任务列表勾选回写、代码块语言标识、公式/表格的即时预览。
   - 光标定位精度：`MarkdownRichTextView.GetTextOffset` 目前是线性扫描最近字符，长段落会偏。
4. 需要的配套测试（现有 `tests\CodeWF.Markdown.Tests\Editor\MarkdownBlockParserTests.cs` / `MarkdownLiveEditorViewTests.cs` 已有往返与事件语义，继续加）：
   - 编号列表：`1. a\n2. b\n3. c` 解析后三项 `OrderedNumber` 依次为 1/2/3，写回文本等于原文。
   - 引用内行内样式：`> **粗** 和 [链接](https://x)` 解析成 Quote 块且 `MarkdownTextRunParser.Parse` 产出 Bold + LinkUrl。
   - 锚点链接 `[Dotnet9](#jump_8)` 产出 LinkUrl 且不是纯文本。

验收：把 `docs\MarkdownSamples` 里的示例（尤其"基础元素""图片与链接"）在实时模式下逐块点开对照预览区，文本、标题层级、引用、行内样式、有序/无序/任务列表、表格、代码块**与预览区渲染一致**；往返编辑后 Markdown 文本不回退（往返幂等测试继续通过）。

---

## 提交前记得

- 库改动 → 提升 `Directory.Build.props` 的 `Version`（例如 `14.0.0-dev.<日期>.<序号>`）→ `pack.bat` → 再更新 Vex 的 `Directory.Packages.props`。
- 两个仓库目前都还没提交（本文件所在提交之外）：`D:\github\libs\CodeWF.Markdown`、`D:\github\apps\Vex`。
- Vex 的 `Vex.csproj` 里 `CodeWF.Markdown` 与 `CodeWF.Markdown.Lite` 同时引用是**故意**的（前者已传递依赖后者，显式列出避免传递链变动时静默失效），不要"顺手"删掉。
