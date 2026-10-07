# CodeWF.Markdown 包结构重组（4 包）+ 编辑器所见即所得控件

## 0. 已确认的决策

| 项 | 决定 |
|---|---|
| 包划分依据 | **是否引入第三方 NuGet**：核心零第三方（除平台级 Avalonia/Markdig/CodeWF.AvaloniaControls）、Full 引入全部能力依赖、Themes 只依赖核心、Editor 只依赖核心 |
| 包数量 | **4 个**：`CodeWF.Markdown`、`CodeWF.Markdown.Full`、`CodeWF.Markdown.Themes`、`CodeWF.Markdown.Editor` |
| 核心目标框架 | `net8.0;net10.0;net11.0`（编辑器同为三目标） |
| Full 目标框架 | **仅 `net10.0`**（Mermaider 只有 net10 资产，单目标后无需条件编译） |
| Themes | `net8.0;net10.0;net11.0`，**不再依赖 Images** |
| 导出能力 | 并入 Full（仅额外依赖 SkiaSharp，无新增第三方包） |
| 编辑器 | 1 个包 2 个控件：纯源码编辑器 `MarkdownEditorView` + 单栏所见即所得 `MarkdownLiveEditorView`；纯渲染器即核心的 `MarkdownViewer` |
| 实时预览含义 | **单栏所见即所得**（Typora 式：输入 `**粗**` 立即显示粗体，表格直接渲染） |
| 旧包名 | **停发**（Images/Math/Mermaid/Highlighting/Export 及 Editor 旧引用方式）；14.0.0 破坏性变更 |
| 冗余代码 | 不复制任何渲染代码：核心与 Full 是**同一个渲染器 + 能力注册差异**；不再引入 Lite 式双实现 |
| 核心是否保留 CodeWF.AvaloniaControls | 保留（虚拟化面板 + 自动配对纯逻辑） |

## 1. 目标包结构

| # | 包 | TFM | 直接依赖 | 内容 |
|---|---|---|---|---|
| ① | `CodeWF.Markdown` | net8/10/11 | Avalonia、Markdig、CodeWF.AvaloniaControls、Lang.Avalonia.Json、System.Xaml 运行时 | Markdown 解析/渲染（标题/段落/列表/引用/表格/链接/分隔线/代码块**单色**/图片**替代文本降级**/公式**原文降级**）、`MarkdownViewer`、`MarkdownExportStyle`、导出相关的纯模型（`MarkdownExportDocument` 等）、i18n |
| ② | `CodeWF.Markdown.Full` | net10 | ①、TextMateSharp(+Grammars)、Sylinko.CSharpMath.Avalonia、Mermaider(+Sugiyama 传递)、AnimatedImage.Avalonia、Svg.Skia、Svg.Controls.Skia.Avalonia、SkiaSharp | 代码高亮、数学公式、Mermaid、图片（GIF/SVG/预览窗）、**导出（PNG/PDF/Word/公众号 HTML）**、`MarkdownImage` 的 ControlTheme（运行时注入） |
| ③ | `CodeWF.Markdown.Themes` | net8/10/11 | ① | 18 套排版主题 + 控件模板（去掉对 Images 的反向依赖） |
| ④ | `CodeWF.Markdown.Editor` | net8/10/11 | ①、AvaloniaEdit | `MarkdownEditorView`（源码编辑器：语法着色/动作/查找/自动配对）、`MarkdownLiveEditorView`（所见即所得）、配套服务（动作/查找/变更/模板/本地化接缝） |

命名说明：`Full` 作为"全量能力"包名已确认；①③④ 沿用现名，故消费者只需把旧能力包换成 `CodeWF.Markdown.Full` 一个引用。

> 具体引用方式与代码见 **§9 使用方式**（基础能力 / 完整能力 / 编辑器 / 导出）。

## 2. 关键架构做法

1. **单份渲染器**：`src/CodeWF.Markdown` 是唯一渲染实现。能力差异通过既有接缝实现：
   - 代码高亮：`MarkdownCodeHighlighter`（未注册 → 单色）
   - 公式：`MarkdownViewer.RegisterMathViewFactory`（未注册 → 原文）
   - 图片：`MarkdownViewer.RegisterImageControlFactory`（未注册 → 替代文本）
   - Mermaid：`MarkdownBlockRendererPipeline` 里的渲染器注册（未注册 → 代码块）
   因此 ① 与 ② 共用同一份 DLL 源码，**不存在两套渲染代码**。
2. **核心移除无用依赖**：`CodeWF.Markdown.csproj` 里的 `Svg.Skia` 无任何代码使用（已核对：核心 54 个源文件无 `Svg.`/`SkiaSharp` 引用），删除。
3. **Themes 解耦 Images**：把 `Common.axaml` 中的 `ControlTheme x:Key="{x:Type md:MarkdownImage}"` 抽到 Full（新文件 `Themes/MarkdownImage.axaml`），由 `CodeWFMarkdownImagesExtensions.UseImages()` 在首次调用时注入 `Application.Current.Styles`（`ContextAwareStyles` 方式，幂等）。Themes 的 `ProjectReference → Images` 随之删除。
4. **XmlnsDefinition 去重**：`MarkdownImage.cs` 所在的 `Properties/AssemblyInfo.cs` 里对 `CodeWF.Markdown.Controls` 的 XmlnsDefinition 与核心重复，随能力代码迁入 Full 后合并为一条（保留核心的，Full 仅保留自己命名空间的声明）。
5. **版本**：`Directory.Build.props` → `14.0.0`（破坏性：旧包停发、TFM 收缩）。

## 3. 文件级改动（库仓库）

### 3.1 核心 `src/CodeWF.Markdown`
- `CodeWF.Markdown.csproj`：删除 `Svg.Skia` PackageReference；保留其余。
- 不改任何渲染代码（除 3.3 的 ControlTheme 抽出）。

### 3.2 新增 `src/CodeWF.Markdown.Full`
- 新建 `CodeWF.Markdown.Full.csproj`：`TargetFramework=net10.0`、`PackageId=CodeWF.Markdown.Full`、引用 ①（ProjectReference）+ 上表第三方包；`AvaloniaUseCompiledBindingsByDefault=true`；`AvaloniaResource` 包含 `Themes/MarkdownImage.axaml`。
- **移动（git mv，保留历史）**以下文件到 Full，命名空间不变（纯搬迁，零逻辑改动）：
  - `src/CodeWF.Markdown.Highlighting/*.cs`（2 个）
  - `src/CodeWF.Markdown.Images/*.cs`（6 个，含 `Properties/AssemblyInfo.cs`）
  - `src/CodeWF.Markdown.Math/*.cs`（5 个）
  - `src/CodeWF.Markdown.Mermaid/*.cs`（2 个）
  - `src/CodeWF.Markdown.Export/*.cs`（19 个）
- 删除上述 5 个旧项目目录与它们的 `bin/obj`；`CodeWF.Markdown.Mermaid.csproj` 的 net10-only 限制随包统一。
- 新增 `Themes/MarkdownImage.axaml`（从 `Shared/Themes/Common.axaml` 抽出）。
- `CodeWFMarkdownImagesExtensions.UseImages()` 增加 ControlTheme 注入。

### 3.3 主题 `src/CodeWF.Markdown.Themes`
- `CodeWF.Markdown.Themes.csproj`：删除 `ProjectReference → CodeWF.Markdown.Images`。
- `CodeWF.Markdown.Shared/Themes/Common.axaml`：移除 `MarkdownImage` 的 ControlTheme（迁至 Full）。

### 3.4 编辑器 `src/CodeWF.Markdown.Editor`
- 保留现有 `MarkdownEditorView` 与服务（动作/查找/变更/模板/本地化/调色板），**新增**：
  - `Controls/MarkdownLiveEditorView.cs`：所见即所得视图（详见 §4）
  - `Controls/Wysiwyg/*`：块视图与 Markdown 回写器（详见 §4）
  - `Properties/AssemblyInfo.cs` 补充 `Controls.Wysiwyg` 的 XmlnsDefinition
- csproj 保持三目标；不引用 Full（只用核心的 `MarkdownViewer` 与注册接缝）。

### 3.5 解决方案/打包/CI/文档
- `CodeWF.Markdown.slnx`：移除 5 个旧项目、加入 `CodeWF.Markdown.Full`。
- `pack.bat`：项目列表改为 4 个。
- `.github/workflows/ci.yml`、`publish-nuget.yml`：项目列表改为 4 个（并更新注释中的 TFM 说明）。
- `README.md`：包表改为 4 行；新增「13.x → 14.0.0 迁移表」（旧包 → 新包）；更新示例代码命名空间（能力扩展方法命名空间不变，仅包名变）。
- `UpdateLog.md`：追加 14.0.0 变更说明。

### 3.6 测试 `tests/CodeWF.Markdown.Tests`
- 引用调整：`Export`/`Math` 项目引用 → `CodeWF.Markdown.Full`（含 `Aliases="FullThemes"` 的 Themes 引用保持不变）。
- 新增编辑器所见即所得测试（见 §6）。

## 4. 所见即所得控件设计（`MarkdownLiveEditorView`）

### 4.1 目标
单栏、就地编辑：输入 `**粗体**` 立即显示粗体；`# ` 变标题；表格直接渲染成表格；改动即时回写 Markdown 并以 `MarkdownChanged` 通知宿主。

### 4.2 结构
```
MarkdownLiveEditorView (UserControl)
├─ ScrollViewer > StackPanel (BlockHost)
│   └─ 每个 Markdig 块 → 一个可编辑块视图
└─ 内部状态：markdown 文本、块索引映射、防重入标记
```
块视图（`Controls/Wysiwyg/`）：

| Markdig 块 | 视图 | 编辑单元 |
|---|---|---|
| `HeadingBlock` | `EditableHeadingView` | 单行 `TextBox`（按级别套字号/字重，样式走核心 StyleKey） |
| `ParagraphBlock` | `EditableParagraphView` | 多行 `TextBox`，内联 `Span` 表现粗体/斜体/行内代码/链接 |
| `ListBlock` | `EditableListView` | 每项一个 `EditableParagraphView`；任务列表前置 `CheckBox` |
| `QuoteBlock` | `EditableQuoteView` | 左侧引用条 + 内嵌段落 |
| `FencedCodeBlock`/`CodeBlock` | `EditableCodeBlockView` | 多行 `TextBox`（等宽）+ 语言标签 |
| `TableBlock` | `EditableTableView` | `Grid`，每格 `TextBox`；增删行列按钮 |
| `ThematicBreakBlock` | `EditableRuleView` | 分隔线（不可编辑，右键删除） |
| 其他（`HtmlBlock`、`MathBlock` 等） | `EditableSourceBlockView` | 源码 `TextBox`（Full 未引用时公式/图表退化为源码，符合核心定位） |

### 4.3 双向映射
- 解析：`Markdig.Markdown.Parse(text)`。
- 回写：`WysiwygMarkdownWriter` 遍历块视图产出 Markdown：
  标题 `#`×n、段落空行分隔、粗体 `**`、斜体 `*`、行内代码 `` ` ``、链接 `[t](u)`、无序 `- `、有序 `1. `、任务 `- [ ] `、引用 `> `、代码围栏 ```` ```lang ````、表格管道格式、分隔线 `---`。
- 变更策略：**文本级改动**只回写所在块并按 100ms 合并触发 `MarkdownChanged`；**结构级改动**（表格增删行列、任务勾选）重建该块视图；**避免重建焦点控件**以保住插入点与 IME 组字。
- 外部同步：`SetText(markdown)` 与 `ApplyExternalEdit` 抑制回抛（与 `MarkdownEditorView` 同一语义，复用同一抑制标志）。

### 4.4 复用
- 格式化动作：复用编辑器的 `MarkdownEditorAction` 枚举与动作服务（选中文本加粗等）。
- 本地化：复用 `IMarkdownEditorLocalizer`。
- 自动配对：复用 `TextAutoPair`（段落/标题/代码块的 `TextBox` 上启用）。

### 4.5 分期
- **本期（P1）**：块渲染与就地编辑、文本回写、勾选任务、表格单元格编辑与行列增删、常用格式动作、`MarkdownChanged`/`SelectionChanged` 事件、`SetText`/`ApplyExternalEdit`。
- **后续（不在本计划内）**：回车自动续列表/退出列表、块级拖拽排序、行内公式就地编辑、图片块内嵌预览（需 Full）、块级撤销栈（P1 依赖 `TextBox` 自身撤销）。

## 5. 迁移与兼容

- 14.0.0 起只发布 ①②③④；旧包最后可用 13.2.0。
- 迁移映射：`Highlighting/Math/Mermaid/Images/Export` → `CodeWF.Markdown.Full`（一个引用替代五个）；`Themes`、`Editor` 保持原名。
- 代码兼容：能力扩展方法命名空间**不变**（`CodeWF.Markdown.Highlighting`、`.Images`、`.MathRendering`、`.Mermaid`、`CodeWF.Markdown`(Export)），下层应用仅需替换包引用，**调用代码零改动**。
- 已知破坏点（写进 README 迁移表）：Full 只支持 net10；核心不再携带图片/公式/高亮能力（需引 Full）；`Svg.Skia` 从核心移除。

## 6. 验证

| 项 | 方法 | 通过标准 |
|---|---|---|
| 库构建 | `dotnet build CodeWF.Markdown.slnx -c Release` | 0 错误 |
| 库测试 | `dotnet test tests/CodeWF.Markdown.Tests -c Release -f net10.0` | 全绿（现有 120 项 + 新增 WYSIWYG 往返测试） |
| 包内容 | `dotnet pack` 后解包检查 nuspec | ①不依赖任何能力包；②依赖 ①+能力包；③只依赖 ①；④依赖 ① |
| 编辑器往返 | 新增单测：markdown → 视图 → 回写 → markdown 幂等（标题/段落/列表/任务/引用/代码/表格/分隔线/粗斜体/行内代码/链接） | 幂等且格式合法 |
| Demo | `dotnet build/run`（net11.0）+ 既有截图脚本 | 与原型同尺度比对无回归（图片/公式/Mermaid 正常） |
| Vex | 更新到 14.0.0（core+Full+Themes+Editor）后构建、运行 | 切换文件不弹保存、公式/图表/图片渲染正常、编辑器可用 |
| Vex AOT | 既有 AOT 发布命令 | 通过（若因 Full 新增反射失败，补 trimmer root —— 见风险） |

## 7. 风险与备选

1. **所见即所得的工作量最大**：P1 只做"块渲染 + 就地编辑 + 回写"，回车自动续列表等留待后续；若你希望一次做全，时间要翻倍。
2. **AOT/裁剪**：Full 引入 TextMateSharp/Svg.Skia/CSharpMath，反射面变大，Vex 的 AOT 可能需要新增 trimmer roots；验证不通过时按错误补 root，不动架构。
3. **同步回写引发的光标跳动**：策略是"只重建变更块、文本级不重建"，若仍有跳动，降级为"编辑期间不回写、失焦/切换块时回写"。
4. **旧包停发的连带影响**：仓库外 `AvaGithubDesktop`（12.1.1.2）等应用继续用旧版本不受影响，升级时按迁移表改一行包引用。

## 8. 明确不在本次范围

- `MarkdownLiveEditorView` 的 P2 能力（§4.5 列出）。
- 其他仓库（Vex 之外的应用）的升级改造。
- 运行时插件式按需加载能力（与"包少好维护"冲突，不做）。

## 9. 使用方式（引用哪个包、怎么用）

### 9.1 快速对照表

| 我要做的事 | 引用哪个包 | 调用代码是否要改 |
|---|---|---|
| 只渲染普通 Markdown（标题/段落/列表/引用/表格/链接/代码块单色/图片替代文本/公式原文） | `CodeWF.Markdown` | — |
| 全量渲染（代码高亮/公式/图片 GIF·SVG/图表 Mermaid）+ 导出 | `CodeWF.Markdown.Full` | — |
| 18 套排版主题 | `CodeWF.Markdown.Themes` | — |
| 源码编辑器 / 所见即所得 | `CodeWF.Markdown.Editor` | — |

**关键点：能力扩展方法的命名空间不变，14.0.0 的所有改动都在"包"这一层，`using` 与调用代码零改动。**

### 9.2 基础能力（只引核心）

```xml
<!-- .csproj -->
<PackageReference Include="CodeWF.Markdown" Version="14.0.0" />
```

```xml
<!-- App.axaml：引入默认控件模板（主题包才带 18 套排版主题，此处不需要） -->
<Application.Styles>
  <FluentTheme />
  <md:MarkdownThemes />   <!-- xmlns:md="https://codewf.com" -->
</Application.Styles>
```

```xml
<!-- 页面里直接用渲染控件 -->
<md:MarkdownViewer Markdown="{Binding Markdown}" />
```

**不需要注册任何能力**：代码块自动单色、公式自动显示 LaTeX 原文、图片自动显示替代文本。
适合场景：日志/说明文档预览、安装体积敏感的桌面应用。

### 9.3 完整能力（直接引 Full）

```xml
<!-- .csproj：一个引用替代原来 5 个（Highlighting/Math/Mermaid/Images/Export） -->
<PackageReference Include="CodeWF.Markdown.Full" Version="14.0.0" />
<PackageReference Include="CodeWF.Markdown.Themes" Version="14.0.0" />  <!-- 想要排版主题时 -->
```

```csharp
// App.OnFrameworkInitializationCompleted / Initialize 里注册一次（命名空间与现在完全一致）
CodeWF.Markdown.Highlighting.CodeWFMarkdownHighlightingExtensions.UseHighlighting(); // TextMate 代码高亮
CodeWF.Markdown.Images.CodeWFMarkdownImagesExtensions.UseImages();                    // GIF/SVG/点击放大
CodeWF.Markdown.MathRendering.CodeWFMarkdownMathExtensions.UseMath();                 // CSharpMath 公式
CodeWF.Markdown.Mermaid.CodeWFMarkdownMermaidExtensions.EnsureRegistered();           // Mermaid 图表

// 导出（命名空间 = CodeWF.Markdown）
var style = CodeWF.Markdown.MarkdownExportStyle.Resolve("Simple", "Normal");
CodeWF.Markdown.MarkdownDocumentExporter.ExportWord(document, "out.docx", style);
CodeWF.Markdown.MarkdownDocumentExporter.ExportPdf(document, "out.pdf", style);
await CodeWF.Markdown.MarkdownHtmlClipboard.SetHtmlAsync(markdown, CopyKind.Wechat);
```

### 9.4 编辑器与所见即所得（Editor 包）

```xml
<PackageReference Include="CodeWF.Markdown.Editor" Version="14.0.0" />
```

```xml
<!-- 纯源码编辑器（等价现在 Vex 用法） -->
<editor:MarkdownEditorView x:Name="Editor" HeaderText="源码" ChipText="Markdown" />
```

```xml
<!-- 单栏所见即所得（Typora 式） -->
<editor:MarkdownLiveEditorView x:Name="Live" HeaderText="实时" />
```

```csharp
// 两者 API 对齐，宿主可在「源码 / 实时」之间切换同一个文档
editor.SetText(markdown);                     // 载入（不触发 MarkdownChanged）
editor.MarkdownChanged += (_, text) => Save(text);
await editor.ExecuteAsync(MarkdownEditorAction.Bold);   // 复用同一套格式化动作
live.SelectionChanged += (_, e) => UpdateStatusBar(e.Line, e.Column, e.LineCount);
```

> 所见即所得需要渲染能力来显示图片/公式/图表时，宿主同时引用 `CodeWF.Markdown.Full` 即可；只引 Editor 时，图片/公式在实时视图里退化为源码文本（与核心定位一致）。

### 9.5 旧用法 → 新用法（迁移只需改包引用）

| 现在（13.x） | 14.0.0 |
|---|---|
| `CodeWF.Markdown` + `.Highlighting` + `.Math` + `.Mermaid` + `.Images` + `.Export` 六个引用 | `CodeWF.Markdown.Full` 一个引用 |
| `CodeWF.Markdown.Themes` | 不变 |
| `CodeWF.Markdown.Editor`（13.2.0 新增） | 不变（并新增 `MarkdownLiveEditorView`） |
| 调用 `UseHighlighting()/UseImages()/UseMath()/EnsureRegistered()` | **不变**（命名空间与扩展方法名都保留） |

Vex 因此只需把 `Vex.csproj` 里的 5 个能力包引用换成 1 个 `CodeWF.Markdown.Full`，`App.axaml.cs` 那 4 行注册代码和后端导出调用都不用动。

