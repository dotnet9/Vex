# 实施计划：Vex 浮层/动效/Typora 对齐 + CodeWF.Markdown 后台解析与原型落地

> 本计划覆盖五个工作流与两处设计原型的像素级对齐，最终目标是把两仓库从「雏形」推进到「与 `design/` 原型一致、可交付」的状态。
> 全程不使用 Bash，构建统一用 `dotnet build/pack/test`；每个逻辑单元完成即提交推送，提交信息使用**简体中文 Conventional Commits**。

## 0. 前提与已核实事实

### 0.1 环境
- 仓库：`D:\github\libs\CodeWF.Markdown`（库，MIT，13.0.0.0 七包已上线）、`D:\github\apps\Vex`（应用，MIT，1.4.0 五平台安装包已发布），均 main 分支、干净。
- 本地 NuGet 源：`codewf-local` → `D:\github\libs\nuget-local`（已配置，内含 `13.0.0-dev.20261006.2..7` 各能力包）。
- 本机无 Bash 工具：所有命令走 PowerShell/`dotnet`；不执行涉及删除的脚本。
- 权威文档：`D:\github\md\改进建议-完整报告.md`（定案版）、`D:\github\apps\Vex\docs\Vex需求文档.md`、两处 `design/` 原型。

### 0.2 代码事实（均已读源码确认，非推断）
| 事实 | 位置 |
|---|---|
| 五个逻辑独立窗均为 `UrsaWindow`：删除确认、属性、字数统计、关于、MCP 设置、MCP 审计、文档窗口 | `ShellDeleteConfirmationWindow.axaml`、`ShellPropertiesWindow.axaml`、`ShellStatisticsWindow.axaml`、`AboutWindow.axaml`、`McpSettingsWindow.axaml`、`McpAuditWindow.axaml`、`MarkdownDocumentWindow.axaml` |
| 未保存确认/重命名/错误已迁移 Ursa 浮层（`OverlayDialog.ShowCustomAsync` / `OverlayMessageBox.ShowAsync`） | `ShellDialogsViewModel.cs:24-98` |
| `Motion.axaml` 只有 3 个 Duration + 2 个 CubicEase，**未被任何 StyleInclude 引用**（Phase B 实为空壳） | `VexControlsTheme.axaml:8-10` |
| `SemiPopupAnimations` 已启用；`ThemeService` 已用 `SemiTheme.Aquatic/Desert/Dusk/NightSky` | `App.axaml:22`、`ThemeService.cs:16-19` |
| 全仓（含原型）无 Focus/Typewriter/AutoPair/PlainText 任何标识；Quick Open 已有但行为是「聚焦文件列表/打开文件」，非 Ctrl+P 模糊搜索 | grep 全仓、`MainWindowViewModel.cs:365-375` |
| Toast 已按「未安装→排队，`OnApplyTemplate` 后安装」时序接入主窗口 | `ShellToastPublisher.cs:16-29`、`MainWindow.axaml.cs:62-66` |
| 侧栏开合是 `GridLength` 硬切（320/6 ↔ 0），无动画 | `ShellWindowLayoutViewModel.cs:76-84` |
| 每窗都有重复的 `GetMainWindow()` + `Show(owner)` 样板 | `ShellDocumentUtilityActions.cs:130-139`、`HelpService.cs:81-97`、`ShellActionCoordinator.cs:143,155` |
| 库已是渲染器注册表（11 块渲染器）+ 五能力包；`TryGetSourceLineBounds`/`TryGetSourceOffsetBounds` **已是公共 API**；`SaveReadingPosition/RestoreReadingPosition/RefreshRemoteImages/GetOutline/MarkdownTextStatistics` **尚不存在** | `MarkdownViewer.cs:583,594` |
| `MarkdownParser.Parse` 仍在 UI 线程同步调用（长文卡顿根因） | `MarkdownViewer.cs:750,786,838` |
| 任务列表 CheckBox 为 `IsHitTestVisible=false`（不可点） | `ListRenderer.cs:83-90` |
| Vex 预览反向用「URL 追参 hack」强制刷新远程图（库已提供 `RefreshRemoteImages()` 下沉点） | `MarkdownPreviewViewModel.cs:125-227` |
| Vex 预览滚动用**反射**探测 `TryGetSourceLineBounds` + 300ms 定时器 | `MarkdownPreviewView.axaml.cs:130-164` |
| 公式：`UseMath()` 被注释；库侧 `MarkdownMathView` 用官方 `MathPainter`，未覆写字体 | `App.axaml.cs:37-39`、`MarkdownMathView.cs` |
| `Sylinko.CSharpMath.Avalonia` 最新仍为 **12.0.0**（2026-04-08 发布，依赖 Avalonia ≥12.0.0）→ 无上游修复可升级，需本地诊断 | NuGet 包页「Versions」 |
| 库 `pack.bat` 与 CI 均按项目分别 `dotnet pack`（非 `-p:PackAsTool`）；Demo 由 `publish-demo.yml` 单独发布 | `pack.bat:25-36`、`.github/workflows/*.yml` |
| 报告 §4.1.6「TFM 矛盾」已过期：`Vex.csproj`、`publish_all.bat`、全部 pubxml 均为 `net10.0` | `Vex.csproj:5`、`publish_all.bat:24-28` |

### 0.3 需在实施中一并修正的文档漂移
1. `docs/Vex需求文档.md` 第 21.11 条与报告 §八 写「英文提交」，与实际执行口径（简体中文）冲突 → 随本计划一并改为简体中文。
2. 库 `README.md:192` 仍写「一次发布四个包（含 Lite）」，与 13.0.0 七包现实不符 → 修正。
3. Vex `README.md:73` 引用 `CodeWF.Markdown 12.1.2.14` 旧版本号 → 更新为 13.0.0。

## 1. 已确认决策（来自两轮澄清）

| 决策 | 结论 |
|---|---|
| 计划范围 | ① Vex 窗内浮层迁移 ② Vex Phase B 动效 ③ Typora 8 项 ④ 库后台解析+虚拟化 ⑤ 公式修复 **＋** 库 Demo 主窗体按 `prototype.html` 落地 |
| 浮层迁移范围 | **全部转窗内 `OverlayDialog`**（删除/属性/字数/关于/MCP 设置/MCP 审计/文档窗口） |
| 动效深度 | 全量：token + 现有组件动效 + 自研 `NotifiableTransition`/`ContentExpansionAnimator` + 全局 `EnableMotion` 开关（持久化） |
| Typora 8 项 | **全部实现**（含以代码为准新做的 Focus/Typewriter/AutoPair/浮动格式工具栏/Copy Plain Text） |
| 原型对齐深度 | **像素级对齐 Token 与尺寸**（Vex `design/assets/theme.css` + 六主题；库 `design/prototype.html`），偏差记入验收清单 |
| 交付节奏 | 连续实施到全部完成，每逻辑单元「可构建可运行 → 提交推送 → 下一项」 |
| 提交信息 | 简体中文 Conventional Commits |

TDD 与测试策略（范围内）：库侧新增纯逻辑（变更合并、脏块计算、虚拟化窗口、统计/大纲 API）先写 `tests/CodeWF.Markdown.Tests` 用例；UI 与两仓库集成以「构建 + 启动截图」验收，不引入 UI 自动化框架（避免新第三方依赖与许可证审计成本）。

---

## 2. 跨仓库联调流程（每个库侧单元都要走）

```powershell
# 1) 库：改代码 → 升 dev 版本 → 打包到本地源
#    Directory.Build.props: <Version>13.0.0.0-dev.YYYYMMDD.n</Version>
cd D:\github\libs\CodeWF.Markdown
.\pack.bat                                   # 产物在 artifacts\packages
Copy-Item artifacts\packages\*.nupkg D:\github\libs\nuget-local -Force

# 2) Vex：切到该 dev 版本联调
#    Directory.Packages.props 六个 CodeWF.Markdown.* 版本号统一改为 13.0.0.0-dev.YYYYMMDD.n
cd D:\github\apps\Vex
dotnet restore Vex.slnx --force-evaluate
dotnet build Vex.slnx -v:minimal

# 3) 验证通过后：库去 dev 后缀 → git tag 由发布流程处理；Vex 切回正式版本，两仓库各自提交推送
```
- 库改动只提交推送库仓库，Vex 改动只提交推送 Vex 仓库。
- 推送前先 `git status` / `git log --oneline -3` 确认分支与远端状态。
- 单元未联调通过前，不提交、不推送任何一侧。

---

## 3. 库侧：CodeWF.Markdown

### 3.1 渲染管线骨架调整（后台解析 + 变更合并 + 虚拟化）——工作流 ④

**现状**：`MarkdownViewer` 在属性变更时同步 `MarkdownParser.Parse`（`MarkdownViewer.cs:750/786/838`），块级 diff 已存在（`MarkdownDiffService.Compare`，ContentHash + 前缀后缀匹配），宿主面板为普通 `StackPanel`。

**目标文件**
- `src/CodeWF.Markdown/Controls/MarkdownViewer.cs`（拆出渲染调度）
- 新增 `src/CodeWF.Markdown/Rendering/MarkdownRenderScheduler.cs`（版本化调度：合并 + 快照门控）
- 新增 `src/CodeWF.Markdown/Rendering/MarkdownVirtualizingPanel.cs`
- 新增/扩展 `src/CodeWF.Markdown.Shared/Rendering/MarkdownDiffService.cs` 的脏检查
- `src/CodeWF.Markdown/Themes/...` 中 `PART_DocumentHost` 的宿主类型（`MarkdownViewer.cs:50`）

**实施步骤**
1. **版本化文本源**：引入 `MarkdownTextSnapshot { int Version; string Text; }`；`MarkdownProperty` 变更只写「待处理快照」，不做解析。
2. **变更合并**：调度器用一个 `DispatcherTimer`/`Post` 合并窗口（约 60–80ms）内的多次变更，只保留最新快照；同时记录变更区间（`Insert(start,len)`/`Remove(start,len)`/`Clear`）供脏检查使用。
3. **后台解析**：`Task.Run(() => MarkdownParser.Parse(text, Pipeline))`；完成后回到 UI 线程比较「结果版本 vs 当前版本」，过期即丢弃。
4. **diff 升级**（`MarkdownDiffService`）：
   - 保留 ContentHash/前缀后缀匹配；
   - 叠加 span 相交脏检查（`MarkdownTextSpan` 已存在）；
   - 块首行哈希 key 匹配（容忍中间插入，不整篇失效）；
   - `Follows` 链校验失败 → 自动降级全量渲染。
5. **虚拟化**：`PART_DocumentHost` 换成自实现虚拟化面板（继承 `Panel`，重写 `MeasureOverride`/`ArrangeOverride`）：
   - 只对「视口 ± overscan（默认 2 屏）」的块创建控件；其余用块测量高度占位；
   - 块高未量测前用估算高度（首行字符数/宽度的经验值），量测后回填并触发重排；
   - 大块（代码块、Mermaid、图片、表格）标记 `NonVirtualizable`，始终物化，避免抖动；
   - 复用现有 `RenderedBlock` 的 `Cleanup()`/disposables 生命周期，滚动离屏即释放。
6. **偏移映射适配**：`TryGetSourceLineBounds` / `TryGetSourceOffsetBounds` 改为「偏移 → 块索引 → 物化请求（如未物化先物化并量测）→ 返回 Bounds」的异步链，保留同步签名（未物化时返回最近已物化块边界并触发后续精确定位）。
7. **异步化收尾**：代码高亮（`MarkdownCodeHighlighter`）、Mermaid（`MermaidBlockRenderer`，已有 `Task.Run`）、导出 API 统一后台化，UI 线程只做控件挂载。

**验收**
- `dotnet test tests/CodeWF.Markdown.Tests`（新增 diff/虚拟化纯逻辑用例）全绿。
- Demo 用 `06-增量渲染压力.md` 压测：连续输入不再卡 UI；`dotnet build CodeWF.Markdown.slnx -c Release` 通过。
- 视觉回归：同一文档在改动前后逐块比对（截图），块顺序/内容/主题一致。

### 3.2 六项能力下沉 API + Vex 去 hack

| # | 库新增 | 程序集 | Vex 侧替换 |
|---|---|---|---|
| 1 | `ExportKind.Html` + HTML 生成走同一 ExportStyle/图片加载链路 | `CodeWF.Markdown.Export` | 删除 Vex 自有 HTML 模板（`MarkdownExportService`），打印预览 HTML、MCP 渲染、社交复制回落全改调库 |
| 2 | `MarkdownViewer.RefreshRemoteImages()`（内部版本号失效图片缓存） | `CodeWF.Markdown.Images` | 删除 `MarkdownPreviewViewModel.CollectRemoteImageUrlReplacements`/`AddRefreshQuery` 追参 hack（`MarkdownPreviewViewModel.cs:125-227`） |
| 3 | `SaveReadingPosition()` / `RestoreReadingPosition()` | `CodeWF.Markdown` | 删除 `MarkdownPreviewView` 的反射探测与 300ms 定时器（`:130-164`） |
| 4 | `MarkdownDocumentModel.GetOutline()`（AST 提取，天然规避代码块 `#`） | `CodeWF.Markdown` | 删除 Vex 自研 `MarkdownOutlineService`/`MarkdownHeadingScanner` 的三处重复解析 |
| 5 | `MarkdownTextStatistics.Calculate(model)`（CJK 字数/段落/阅读时长） | `CodeWF.Markdown` | `MarkdownStatisticsService` 只做展示映射 |
| 6 | 任务列表勾选回写：CheckBox 可点击 → 按块 `SourceSpan` 改写 Markdown → 抛出文本变更事件 | `CodeWF.Markdown` | 编辑器接收文本变更即可（配合工作流 ③ 的「任务勾选回写」） |

**边界（不做的）**：打印预览编排（浏览器交互）、AvaloniaEdit 着色映射、草稿/文件监听/MCP/i18n 均留在 Vex。

**任务勾选回写细节**：`ListRenderer.CreateTaskMarker` 去掉 `IsHitTestVisible=false`，改为可点击并暴露 `TaskStateChanged(int sourceOffset, bool isChecked)`；库内按块 `SourceSpan` 精确定位 `- [ ]` / `- [x]` 并输出「新版 Markdown + 变更区间」，宿主以单一事件消费，避免整篇重设。

### 3.3 公式不可见修复——工作流 ⑤
1. **诊断**：在 `CodeWF.Markdown.Sample` 开一个最小复现页（`$x^2$`、`\frac{a}{b}`），确认是「测不到尺寸」「画到 Bitmap 外」还是「字体缺字形」。
2. **修复选项（按优先级）**：
   - a. 在 `MarkdownMathView` 覆写 `MathPainter` 字体族为随库内置的 Latin Modern Math（CSharpMath 自带，GUST 许可）或系统可用数学字体；
   - b. 修正 `MeasureOverride`/`Render` 的尺寸与画布原语（`AvaloniaCanvas` 传入 `Bounds.Size`，检查是否因 DPI/原点导致整体画出）；
   - c. 若为包与 Avalonia 12.1.3 不兼容且本地无法绕过：**记录为已知限制**，改为「公式以纯文本 + 等宽风格渲染」（优雅降级），并在 `README`/`UpdateLog` 说明；同时保留 `UseMath()` 代码路径与开关，上游修复后一行启用。
3. **验收**：块级公式与行内公式在 Vex 预览、HTML/PNG/PDF/Word 导出与社交复制四条链路均可见；`App.axaml.cs` 打开 `UseMath()`。
4. **许可证红线**：不引入新的第三方数学库；若必须替换，先按 `docs/Vex需求文档.md` 4.4 完成许可证审计并记录在提交说明。

### 3.4 Demo 主窗体按 `design/prototype.html` 像素级落地
**目标文件**：`src/CodeWF.Markdown.Sample/Views/MainWindow.axaml`（现 28KB 单文件 → 按原型分区拆分）、`ViewModels/MainWindowViewModel.cs`（36.7KB → 拆出侧栏/工具栏/状态栏子 VM）、新增 `Themes/` 资源字典。

按原型逐区实现（尺寸取自原型与 `design/README.md` 第四节）：
1. **标题栏 48px**：`logo + 「CodeWF.Markdown Demo」 + Demo 徽标 + 版本徽章(v13.0.0)`；右侧语言下拉 + 明暗切换（太阳/月亮，`RequestedThemeVariant`）+ 最小化/最大化/关闭（关闭键悬停红）。
2. **侧栏 200px（可折叠，折叠后 48px 图标态）**：六篇示例文档卡（图标 + 名称 + 一句话描述，选中主色左条），底部「折叠侧栏」按钮。
3. **工具栏 44px**：视图分段控件（编辑 / 分栏 / 预览，替代原 Tab）、排版主题下拉、字号（标准/紧凑）、`性能演示 ▾`（增量替换/中部插入/尾部追加/停止）、`导出 ▾`（PNG/PDF/Word + 复制公众号/知乎/掘金 HTML）。
4. **编辑/预览双栏**：各带 `pane-head`（编辑器 · AvaloniaEdit / 预览 · MarkdownViewer + 当前排版名）。
5. **状态栏 28px**：保存圆点 + 已保存 + 示例名 + 字数 + **解析耗时** + 渲染模式（增量/全量）+ 编码 + 版本 + GitHub 链接。
   - 解析耗时：在 `MarkdownParser.Parse` 外层加轻量计时（或提为库 API `ParseWithTiming`），只用于 Demo 展示。
6. **Token**：按 `design/README.md` 第六节九组 token（`BgWindow/BgSidebar/BgToolbar/Border/TextPrimary/TextMuted/Accent/AccentSoft/CodeBg`）建明暗双变体资源字典，与库 `MarkdownStyleKeys` 对齐。
7. 保留第二 Tab「对比模式」（原多 Viewer 页，仅改名与措辞）。

**像素级对齐验收**：Demo 运行截图与 `prototype.html` 并排比对（标题栏/侧栏/工具栏/双栏头/状态栏五区，明暗各一次），逐项记录偏差并在本单元内收敛；偏差清单进入提交说明。

---

## 4. Vex 侧

### 4.1 工作流 ①：五组独立窗 → 窗内 Ursa 浮层

**新增**
- `src/Vex/Modules/Shell/Services/IShellOverlayService.cs` / `ShellOverlayService.cs`：统一封装`OverlayDialog.ShowCustomAsync(...)` / `OverlayMessageBox.ShowAsync(...)`，收敛目前散落的 `GetMainWindow()` + `Show(owner)` 样板（`ShellDocumentUtilityActions.cs:130-139`、`HelpService.cs:81-97`、`ShellActionCoordinator.cs:143,155`）。
- 浮层承载视图（`UserControl` + `Classes="dialog-card"`，宽度 420–720 视内容）：
  `ShellPropertiesOverlayView`、`ShellStatisticsOverlayView`、`ShellAboutOverlayView`、`McpSettingsOverlayView`、`McpAuditOverlayView`、`MarkdownDocumentOverlayView`；删除确认改为 `ShellDeleteConfirmationOverlayView`（复用现有 `ShellDeleteConfirmationWindowModel` 语义）。
- 长内容在浮层内滚动：`MaxHeight` 绑定宿主高度百分比（如 78%），内部 `ScrollViewer`。

**保留/删除**
- 七个 `*.axaml`/`*.axaml.cs` 窗口文件删除（内容迁入对应 overlay 视图），`UrsaWindow` 的标题栏、`ShowInTaskbar`、`WindowStartupLocation` 等属性不再需要。
- 未保存确认（`ShellUnsavedDialogView`）与错误提示已于 Ursa 浮层，不动。

**行为与语义**
- 删除确认：由「模态 `ShowDialog<bool>`」变为浮层确认；`Esc` = 取消，`Enter` 默认聚焦「取消」，删除后仍执行原删除流程与状态栏提示（数据安全语义保持：仍需显式点「删除」）。
- 属性/字数：由「可其他窗口并存」变为单浮层，重复打开同一浮层需前置去重（同 key 再次打开先关闭旧实例）。
- 关于/文档窗口：不再有独立任务栏项，符合原型「窗内浮层」定位。
- MCP 设置：浮层内保留原有「保存即重载」行为与 `CloseRequested` 事件（`McpSettingsViewModel.cs:42,175,180`），浮层以事件关闭自身。
- `Esc` 统一关闭信息型浮层（对应 `docs/Vex需求文档.md` 5.5）。

**验收**：主窗口 1280×820 与最小 980×640 下逐窗截图；浮层宽高、内边距、圆角与 `design/index.html` 对应区块一致；Esc/按钮/遮罩点击行为符合原型；无残留 `ShowInTaskbar` 独立窗。

### 4.2 工作流 ②：Phase B 动效

1. **Token 层接入**：`VexControlsTheme.axaml` 增加 `<ResourceInclude Source="Themes/Shared/Motion.axaml" />`；`Motion.axaml` 补齐 Ant Design 动效 token（clean-room，注明规范来源 `Ant Design Design Tokens, MIT`）：`MotionDurationFast/Mid/Slow`（100/200/300ms）、`MotionEaseInOut`（`cubic-bezier(0.645,0.045,0.355,1)`）、`MotionEaseOut`、进出场组合。
2. **全局开关**：`AppSettings.EnableMotion`（默认 true）+ `MotionResources.Apply(enable)`（false 时把三档 Duration 资源归零/移除 Transitions）；接入 `ShellAppearanceViewModel` 菜单项（「视图 → 界面动效」CheckBox）与设置持久化。
3. **自研控件**（`src/Vex.Controls/Controls/`）
   - `NotifiableTransition`（约 50 行）：`Transition` 完成通知 + 超时兜底，供上层串行动效步骤。
   - `ContentExpansionAnimator`（约 270 行）：单 `progress` 驱动 layout + opacity，用于**侧栏开合**与**大纲树展开**，替换 `ShellWindowLayoutViewModel.cs:76-84` 的 `GridLength` 硬切（保留 `IsSidebarVisible` 语义与设置持久化，动画期间用过渡宽度）。
4. **现有组件动效**（不写代码优先用 `Styles`）
   - `ShellFindBarView`：改为基于 `:open`/`:close` 伪类 + Transitions 的展开/收起（高度 + opacity），替代现在仅靠 `IsVisible` 的瞬变。
   - 对话框/浮层：`dialog-card` 进出场 Zoom（`RenderTransform` + `Opacity` 通道），与工作流 ① 的浮层统一。
   - 菜单/提示：确认 `SemiPopupAnimations` 生效后不重复做。
5. **注意**：若采用 Composition 渲染线程动画，需处理 `RequestCommitAsync` 防首帧跳变；每加一处动效即在浅/深主题与六套主题色下截图核对。

**验收**：`EnableMotion=false` 时全局无动效且无功能回归；侧栏/大纲开合为连续动画不闪跳；查找条展开收起与原型观感一致。

### 4.3 工作流 ③：Typora 对齐八项

| 项 | 落点 | 说明 |
|---|---|---|
| 1 Focus Mode | `ShellWindowLayoutViewModel` + `MainWindow.axaml` + 设置持久化 | 隐藏侧栏/状态栏（保留标题栏），新菜单项 + 快捷键（建议 `F8`），状态栏提示 |
| 2 Typewriter Mode | `MarkdownEditorView.axaml.cs` + `MarkdownEditorController` | 光标行垂直居中：光标行变化后按 `VisualLine` 高度与视口做滚动补偿；开关持久化，输入法组字期间不打断 |
| 3 Auto Pair | `MarkdownEditorView.axaml.cs` + 新增 `MarkdownAutoPairService` | 成对输入 `* _ ` ~ ( [ { "` 与 `$`；选中文本时包裹；`Backspace` 删空对；跳过右半边补全；`Ctrl+Z` 单步撤销（与 `UndoStack` 协作） |
| 4 Quick Open | 新增 `ShellQuickOpenViewModel` + `ShellQuickOpenView`（浮层） | `Ctrl+P` 弹出模糊搜索：当前文件夹文档（复用 `_documentFiles`）+ 最近文件；键盘上下/回车/Esc；替换现有 `QuickOpenAsync` 的「聚焦文件列表」行为 |
| 5 浮动格式工具栏 | 新增 `MarkdownSelectionToolbarView`（`Popup` 挂在编辑器上） | 选区 ≥1 字符时浮出加粗/斜体/行内代码/链接/清除格式，复用 `EditorActionKind` 既有动作；`Esc`/点击外部收起 |
| 6 Copy as Plain Text | 编辑菜单新增项 | 读 `return_plain` 剪贴板文本（取 Markdown 选区或纯文本载荷），不写富 HTML；无选区时复制全文纯文本 |
| 7 图片拖入 + 复制到 assets | `ShellDropTargetHandler` + `IMarkdownEditorMutationService` | 拖入本地图片（含 `data:`/跨盘）→ 目标目录 `assets/`（不存在则创建）→ 按现有「复制/移动」策略落盘 → 插入 `![](assets/xxx.png)`；与现有拖放打开文件/文件夹的判定顺序互斥 |
| 8 任务勾选回写 | 依赖 3.2 #6 | 预览里点任务框 → 库返回新 Markdown → 编辑器应用（走现有文本变更通道，保持撤销栈） |

**I18n**：以上新增菜单项、快捷键提示、状态提示、浮层文案需同步 `zh-CN/zh-Hant/en-US/ja-JP` 四套 JSON（`src/Vex/I18n/`），不得留硬编码。

**验收**：逐项截图或录屏；Auto Pair/Typewriter 需专门验证中文输入法组合态不被破坏（对应 `docs/Vex需求文档.md` 19.3）。

### 4.4 与原型像素级对齐（跨 4.1–4.3）
以 `design/assets/theme.css` 为唯一 token 来源，逐区核对并收敛偏差：
- 标题栏：品牌 Logo 渐变块 18px、未保存圆点、文件名截断宽度。
- 侧栏：卡片化文件项、空态、选中态、页签与 `TabControl.axaml`。
- 编辑区：`Padding="6 28 34 28"`、行号色、当前行高亮、语法着色。
- 预览：`MarkdownViewer` 边距、排版主题名显示、`pane-head`。
- 查找栏、状态栏徽章、右键菜单、Quick Open 浮层。
- 六主题（light/dark/aquatic/desert/dusk/night-sky）各截一次；与 `AppPalette.axaml` 的 17 个控件级 Semi 键核对是否仍有硬覆盖（报告 §4.3 第 6 条，作为本单元内的顺带收敛，不扩大为独立重构）。

---

## 5. 提交单元与顺序（简体中文 Conventional Commits）

> 每单元结束必须：`dotnet build` 通过 → 涉及 UI 时启动截图 → 更新对应仓库 `UpdateLog.md`（中文，emoji 前缀）→ 提交 → 推送。库侧单元还必须先走第 2 节联调流程。

**库（CodeWF.Markdown）**
1. `refactor(renderer): 抽出渲染调度器，接入版本化快照与变更合并`
2. `perf(renderer): Markdig 解析移至后台线程并加入过期结果门控`
3. `perf(diff): 叠加脏区间与块首行匹配，失败自动降级全量`
4. `feat(renderer): 新增虚拟化文档宿主，按视口物化块控件`
5. `test(renderer): 补充差异与虚拟化窗口单元测试`
6. `feat(api): 新增 HTML 导出、阅读位置、远程图刷新、大纲、统计与任务回写 API`
7. `fix(math): 修复 CSharpMath 公式不可见并恢复 UseMath 接入`
8. `feat(demo): 按新原型重写 Demo 主窗体（品牌区/示例库/分段视图/导出/状态栏）`
9. `docs: 修正包线说明、README 与 Lite 退役描述`
> 库侧 1–5 与 6 之间按需合并：若调度器拆分与虚拟化必须一起落地才能通过测试，则压成 1–2 个提交，保持每提交可构建。

**Vex**
1. `feat(ui): 独立窗口迁移为 Ursa 窗内浮层并统一对话服务`
2. `feat(motion): 接入动效 Token 与全局动效开关`
3. `feat(motion): 新增过渡通知与内容展开动画控件，侧栏与大纲改为连续动画`
4. `feat(editor): 新增专注模式与打字机模式`
5. `feat(editor): 新增自动配对输入与选区浮动格式工具栏`
6. `feat(editor): 支持拖入图片并复制到 assets 目录`
7. `feat(shell): 快速打开改为 Ctrl+P 模糊搜索浮层`
8. `feat(editor): 新增复制为纯文本命令`
9. `feat(preview): 任务列表勾选回写 Markdown`
10. `refactor(preview): 预览滚动与远程图刷新改用库公共 API，移除反射与追参 hack`
11. `refactor(preview): 大纲与统计改用库 API，移除重复解析`
12. `style(theme): 按原型收敛标题栏/侧栏/编辑区/预览/状态栏的 Token 与尺寸`
13. `docs: 提交规范改为简体中文并更新开发日志`

---

## 6. 验证与验收

### 6.1 每个提交前
```powershell
# 库
dotnet build CodeWF.Markdown.slnx -c Release
dotnet test tests\CodeWF.Markdown.Tests\CodeWF.Markdown.Tests.csproj
dotnet list CodeWF.Markdown.slnx package --vulnerable --include-transitive
# Vex
dotnet build Vex.slnx -v:minimal
git diff --check
```
涉及 UI：启动桌面程序截图（不得仅凭构建通过判断）。
涉及发布配置：至少验证对应 publish profile 或一键脚本。

### 6.2 截图验收清单（对齐 `docs/Vex需求文档.md` 16.2 / 16.3）
- 默认启动主窗口、打开文件后三区、打开文件夹、查找/替换栏。
- 六个窗内浮层（属性/字数/删除/关于/MCP 设置/MCP 审计/文档窗口）。
- 源代码模式、最小窗口 980×640、六套主题色、多语言排版。
- Focus Mode / Typewriter / 浮动格式工具栏 / Quick Open / [Auto Pair 录屏]。
- 任务勾选回写前后 Markdown 文本对比；图片拖入后 `assets/` 落盘与插入文本。
- 库：Demo 与 `prototype.html` 并排（明暗各一次）；公式四链路可见；增量压力文档输入流畅度。
- 复杂 SVG 回归样例（`codewf-avalonia-guide-cover.svg`）在 Vex 预览与浏览器对比。

### 6.3 已知风险与应对
| 风险 | 应对 |
|---|---|
| 虚拟化破坏现有滚动同步、选区、SVG/图片、`TryGetSourceLineBounds` | 分支内先跑 06-增量渲染压力与 05-图片链接与长文两篇回归；偏移映射保留同步签名并返回最近块边界；异常时可通过开关退回非虚拟化宿主 |
| 浮层化后删除确认的模态语义弱化 | 默认聚焦「取消」、`Esc` 取消、仍需显式点「删除」，并在浮层内显示完整路径 |
| 自研动效影响启动帧/首帧跳变 | 用 `NotifiableTransition` 串行；Composition 路径按需 `RequestCommitAsync`；`EnableMotion=false` 作为兜底 |
| Auto Pair 与输入法冲突 | 组字态（`TextInputMethodClient` 无 preedit 前）不下发配对；专项用中文/日文输入法验证 |
| 公式上游无修复版本 | 先本地诊断；不可解则退化为「原文 + 等宽」，保留开关并记录限制 |
| Demo 主窗体单文件 28KB 拆分工作量 | 按原型分区拆为独立 `UserControl` + 子 VM，与库 3.1 的后台解析一起验证 |

## 7. 明确不做（边界）
- 不做 Phase D 单窗格无缝实时渲染（依赖库增量管线稳定 + preedit 输入法客户端，另立分支）。
- 不做 Phase C 的 Anchor/ImageViewer/Banner/PopConfirm、`Ursa.PrismExtension` 全面替换、MCP 官方 SDK 迁移、`SemiColor*` 语义层重构、FluentTheme 去留、AvaloniaEdit 主题替换。
- 不做打印预览编排下沉、AvaloniaEdit 着色映射下沉、多 Tab/多窗口、`vex://` 协议、PicGo、YAML Front Matter、快照版本历史、PDF 书签（均属报告「中期」项）。
- 不引入新的第三方依赖（动效 clean-room；数学库不替换）。
- 不改 `design/` 原型文件与 `docs/typora` 参考素材（只读参照）。
- 不新增第三方审计文档（结论写入提交说明与 `UpdateLog.md`）。
