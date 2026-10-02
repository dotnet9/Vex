# Vex MCP 功能实现方案

## 1. 目标

在 Vex 内置一个 MCP Server，使支持 MCP 的 AI 客户端可以像用户一样读取、创建、编辑、保存和预览 Markdown 文档。第一期以“当前打开的文档”和“已授权的本地文档路径”为核心，不做复杂自动化脚本和任意文件系统控制。

核心目标：

1. 在帮助菜单的“更新日志”前增加“MCP 设置”入口。
2. 新增符合当前主题风格的 MCP 设置窗体，用于设置监听地址、端口、启用状态和授权 token。
3. 将文档常用操作暴露为 MCP tools。
4. 所有新增序列化、工具注册、协议处理逻辑保持 Native AOT 兼容。
5. AI 编辑文档后，编辑器、预览、大纲和状态栏实时更新。
6. 默认安全、可关闭、可审计，避免 AI 任意读写用户磁盘。

## 2. 协议与形态

### 2.1 推荐形态

Vex 作为内置 MCP Server 运行，AI 客户端作为 MCP Client 连接 Vex。

推荐传输：

- 第一期：Streamable HTTP，本机监听 `127.0.0.1`，默认端口建议 `17891`。
- 备用：SSE 可作为兼容选项，但不作为第一期主路径。
- 不建议第一期实现 stdio，因为 Vex 是桌面 GUI 进程，stdio 生命周期和桌面应用不自然。

连接地址示例：

```text
http://127.0.0.1:17891/mcp
```

认证方式：

```http
Authorization: Bearer <token>
```

### 2.2 协议对象

第一期只实现 MCP tools，不实现 resources、prompts、sampling。

原因：

- 当前需求是“让 AI 操作软件编写读取文档”，tools 足够表达动作。
- resources 适合只读上下文枚举，后续可扩展为“当前文档”“最近文档”“大纲”等资源。
- prompts 暂无必要。
- sampling 会让 Vex 反向调用模型，当前不需要。

## 3. UI 设计

### 3.1 菜单入口

位置：

```text
帮助 -> MCP 设置 -> 更新日志 -> 新手引导 -> 鸣谢 -> ...
```

也就是在 `ShellTitleMenuView.axaml` 中 `Changelog` 前增加菜单项。

建议新增本地化键：

- `McpSettings`
- `McpSettingsTitle`
- `McpServerEnabled`
- `McpServerAddress`
- `McpServerPort`
- `McpAuthorizationToken`
- `McpGenerateToken`
- `McpCopyEndpoint`
- `McpConnectionStatus`
- `McpAllowedWorkspace`
- `McpRequireConfirmation`
- `McpSave`
- `McpCancel`

### 3.2 设置窗体

新增窗体：

```text
src/Vex/Modules/Shell/Views/McpSettingsWindow.axaml
src/Vex/Modules/Shell/Views/McpSettingsWindow.axaml.cs
src/Vex/Modules/Shell/ViewModels/McpSettingsViewModel.cs
```

窗体风格：

- 使用现有 `VexPanelBackgroundBrush`、`VexPanelBorderBrush`、`TextBlockDefaultForeground`、按钮样式和输入框样式。
- 不做营销式说明页，只做紧凑设置表单。
- 推荐尺寸：`640 x 460`，居中打开。
- 使用 `x:DataType` 和 compiled binding。

建议布局：

```text
标题：MCP 设置

[ ] 启用 MCP Server

监听地址    [127.0.0.1        ]
端口        [17891            ]
Token       [vex_mcp_32_bytes_token_here] [生成] [复制]
Endpoint    http://127.0.0.1:17891/mcp [复制]

允许访问范围
(*) 仅当前文档
( ) 当前打开文件夹
( ) 自定义目录 [路径] [选择]

[x] 修改、删除、打开外部路径前需要确认
[x] 只允许本机连接

状态：未启动 / 运行中 / 端口占用 / Token 缺失

[保存] [取消]
```

### 3.3 人工确认弹窗

第一期建议新增一个轻量确认窗口或复用现有错误/确认 overlay。

需要确认的 MCP 操作：

- 修改当前文档。
- 覆盖整个文档。
- 删除文档。
- 重命名文档。
- 打开非当前工作区路径。

只读操作不需要确认。

保存当前文档不需要确认；AI 调用保存工具时可以直接保存。另存为也不弹确认，但仍必须通过访问范围、扩展名和路径安全校验。

确认文案应显示：

- 客户端来源。
- 工具名。
- 目标文件。
- 改动摘要。

## 4. 配置模型

扩展 `AppSettings`：

```csharp
public bool? IsMcpServerEnabled { get; init; }

public string? McpServerHost { get; init; }

public int? McpServerPort { get; init; }

public string? McpAuthorizationToken { get; init; }

public string? McpAllowedWorkspacePath { get; init; }

public string? McpAccessScope { get; init; }

public bool? McpRequireConfirmation { get; init; }
```

默认值：

- `IsMcpServerEnabled = false`
- `McpServerHost = "127.0.0.1"`
- `McpServerPort = 17891`
- `McpAccessScope = "CurrentDocument"`
- `McpRequireConfirmation = true`

Token 生成：

- 使用 `RandomNumberGenerator.GetBytes(32)`。
- 保存为 Base64Url 或 hex。
- 窗体内默认直接显示，方便用户复制到 MCP 客户端。

安全补充：

- 第一期不支持 `0.0.0.0`，除非用户明确切换高级选项。
- 地址必须限制为 loopback。
- Token 为空时不允许启动。

## 5. 服务分层

新增模块建议放在 `Modules/Mcp`，避免挤进 Shell 或 Workspace。

建议文件结构：

```text
src/Vex/Modules/Mcp/McpModule.cs
src/Vex/Modules/Mcp/Services/IMcpServerHost.cs
src/Vex/Modules/Mcp/Services/McpServerHost.cs
src/Vex/Modules/Mcp/Services/IMcpToolDispatcher.cs
src/Vex/Modules/Mcp/Services/McpToolDispatcher.cs
src/Vex/Modules/Mcp/Services/IMcpDocumentTools.cs
src/Vex/Modules/Mcp/Services/McpDocumentTools.cs
src/Vex/Modules/Mcp/Services/IMcpAuthorizationService.cs
src/Vex/Modules/Mcp/Services/McpAuthorizationService.cs
src/Vex/Modules/Mcp/Services/IMcpOperationConfirmationService.cs
src/Vex/Modules/Mcp/Services/McpOperationConfirmationService.cs
src/Vex/Modules/Mcp/Models/McpSettings.cs
src/Vex/Modules/Mcp/Models/McpToolContracts.cs
src/Vex/Modules/Mcp/Serialization/McpJsonContext.cs
```

注册方式：

- 在 `App.ConfigureModuleCatalog` 增加 `McpModule`。
- 在 `McpModule.RegisterTypes` 注册 MCP 相关服务。
- 在应用启动后根据设置决定是否启动 server。
- 设置保存后动态重启 server。

## 6. MCP Server 实现路线

### 6.1 推荐实现方式

优先使用官方或稳定的 .NET MCP SDK，但必须先验证 Native AOT：

1. 是否支持 source-generated System.Text.Json。
2. 是否通过反射扫描 `[McpServerTool]` 注册工具。
3. 是否依赖 dynamic code、Expression compile 或运行时类型发现。

如果 SDK 的工具注册依赖反射，第一期不要使用自动扫描。改用手写 `tools/list` 和 `tools/call` 分发器。

### 6.2 AOT 优先的手写协议方案

为了最小化 AOT 风险，第一期推荐：

- 使用 Kestrel 或 `HttpListener` 暴露本机 HTTP endpoint，具体选择待确认。
- 使用 `System.Text.Json` source generation 处理协议 DTO。
- 手写 JSON-RPC 请求解析。
- 手写 MCP tools/list 返回。
- 通过 `switch` 分发 `tools/call`。
- 所有工具输入输出使用 sealed record DTO。

优点：

- 不依赖反射工具扫描。
- 工具列表稳定可控。
- AOT 警告容易定位。
- 后续可以替换为成熟 SDK。

风险：

- 需要实现 MCP 基础消息格式。
- Streamable HTTP 细节需要严格跟随规范。

折中策略：

- 第一期先支持常见 MCP 客户端能调用的 `initialize`、`tools/list`、`tools/call`、`ping`。
- 对未知 method 返回 JSON-RPC error。
- 不实现复杂 session 恢复。

### 6.3 Kestrel 与 HttpListener 对比

当前先不确定最终方案，等确认后再进入实现。

#### Kestrel

优点：

- ASP.NET Core 推荐 HTTP Server，跨平台支持 Windows、Linux、macOS。
- 性能、并发连接、HTTPS、HTTP/1.1、HTTP/2、WebSocket 和中间件扩展能力更完整。
- 更容易接入依赖注入、配置、日志、认证中间件和后续 Streamable HTTP 细节。
- 后续如果扩展 MCP resources、长连接、SSE 或更多 endpoint，工程扩展性更好。

缺点：

- 需要引入或启用 ASP.NET Core hosting 相关依赖，桌面应用体积和发布复杂度会上升。
- Native AOT 下要额外验证 ASP.NET Core hosting、路由、JSON 选项和日志链路是否引入新的 IL 警告。
- 生命周期要和 Avalonia 桌面进程协调，启动、停止、端口占用和异常恢复都要处理好。
- 对第一期“本机单 endpoint + 少量 JSON-RPC 方法”来说能力偏多。

适用判断：

- 如果希望后续长期维护 MCP、支持更多协议细节、更多工具和更强认证能力，Kestrel 更稳。
- 如果最看重 Native AOT 可控和最小依赖，Kestrel 需要先做发布验证后再定。

#### HttpListener

优点：

- API 简单，适合本机 loopback 的轻量 HTTP 服务。
- 不需要引入 ASP.NET Core hosting，新增依赖少，方案更贴近第一期最小闭环。
- 手写 request/response 更直接，AOT 风险点集中在少量协议 DTO 和请求处理代码。
- 启停逻辑较轻，适合桌面应用内置小型控制端口。

缺点：

- 能力较基础，HTTP pipeline、认证、日志、异常处理中间件都要自己写。
- 跨平台和权限行为需要逐平台验证，尤其是 URL prefix、端口监听和系统支持情况。
- 长连接、Streamable HTTP 细节、SSE、并发请求取消等能力需要自行补齐。
- 未来如果 MCP endpoint 变复杂，维护成本可能高于 Kestrel。

适用判断：

- 如果第一期只做本机 `127.0.0.1`、Bearer token、`initialize`、`tools/list`、`tools/call`、`ping`，HttpListener 更轻。
- 如果要追求协议扩展和长期演进，HttpListener 可能会很快显得手工痕迹太重。

#### 待选择

建议在开始编码前二选一：

- 选择 Kestrel：优先工程扩展性和协议演进。
- 选择 HttpListener：优先最小依赖、AOT 可控和快速落地。

#### 当前选择

截至 2026-06-06，第一期实现已选择 `HttpListener`。

选择原因：

- 当前 MCP 入口只需要本机 loopback、Bearer token 和少量 JSON-RPC 方法。
- 不引入 ASP.NET Core hosting，新增依赖少，Native AOT 风险面更小。
- 所有协议 DTO 和工具输入输出都可以保持手写分发和 source-generated `System.Text.Json`。

当前折中：

- 仅实现轻量 HTTP JSON-RPC 调用，不实现 SSE、长连接 session 恢复和复杂 Streamable HTTP 扩展。
- 后续如果要做更完整的 MCP transport、更多 endpoint 或更强认证能力，再评估切换 Kestrel。

## 7. 工具清单

工具语义命名统一使用 `vex.` 前缀；实际对外暴露给 AI / OpenAI function calling 的工具名统一使用下划线格式，例如 `vex_get_current_document`。原因是 OpenAI function tool 名称要求匹配 `^[a-zA-Z0-9_-]+$`，不能包含 `.`。服务端会把旧的 `vex.xxx` 调用自动兼容为 `vex_xxx`。

### 7.1 只读工具

#### vex.get_current_document

读取当前文档。

输入：

```json
{}
```

输出：

```json
{
  "filePath": "D:/docs/demo.md",
  "fileName": "demo.md",
  "markdown": "# demo",
  "isDirty": true,
  "encoding": "utf-8"
}
```

#### vex.get_document_outline

读取当前文档大纲。

输入：

```json
{}
```

输出：

```json
{
  "items": [
    { "level": 1, "title": "标题", "line": 1 }
  ]
}
```

#### vex.get_selection

读取当前选区。

输入：

```json
{}
```

输出：

```json
{
  "text": "选中文本",
  "startOffset": 10,
  "length": 4
}
```

#### vex.list_open_documents

第一期如果 Vex 仍是单活动文档，可以返回当前文档和文件列表选中项；后续多文档再扩展。

### 7.2 编辑工具

#### vex.replace_current_document

整体替换当前 Markdown。

输入：

```json
{
  "markdown": "# 新内容",
  "reason": "重写文档结构"
}
```

行为：

- 更新 `MainWindowViewModel.Markdown`。
- 触发现有 `WorkspaceDocumentState.UpdateDocument`。
- 编辑器和预览实时刷新。
- 默认需要人工确认。

#### vex.apply_text_edit

按 offset 应用文本编辑。

输入：

```json
{
  "startOffset": 10,
  "length": 5,
  "replacement": "新文本",
  "reason": "修正文案"
}
```

行为：

- 校验 offset 范围。
- 调用现有编辑控制器或新增统一文档编辑服务。
- 保留 undo/redo 能力是理想目标；如果第一期不能进入 AvaloniaEdit undo 栈，需要在文档中标明。

#### vex.insert_text

在当前位置或指定 offset 插入文本。

输入：

```json
{
  "offset": 128,
  "text": "插入内容"
}
```

#### vex.replace_selection

替换当前选区。

输入：

```json
{
  "text": "替换内容"
}
```

### 7.3 文件工具

#### vex.open_document

打开允许范围内的文档。

输入：

```json
{
  "path": "D:/docs/demo.md",
  "encodingName": "utf-8"
}
```

要求：

- 路径必须在允许范围内。
- 扩展名必须由 `IDocumentService.IsSupportedDocumentPath` 接受。
- 非当前工作区默认需要确认。

#### vex.save_current_document

保存当前文档。

输入：

```json
{}
```

#### vex.save_current_document_as

保存到指定路径。

输入：

```json
{
  "path": "D:/docs/new.md"
}
```

要求：

- 路径必须在允许范围内。
- 目标存在时需要确认。

### 7.4 预览工具

#### vex.refresh_preview

刷新预览。

输入：

```json
{}
```

#### vex.get_rendered_html

返回当前 Markdown 渲染后的 HTML。

输入：

```json
{}
```

输出：

```json
{
  "html": "<h1>...</h1>"
}
```

用途：

- AI 可以检查渲染效果。
- 不直接操作 UI。

### 7.5 状态工具

#### vex.get_app_status

返回应用状态。

输出：

```json
{
  "version": "1.1.2.1",
  "theme": "dark",
  "typography": "Simple",
  "currentFilePath": "D:/docs/demo.md",
  "isDirty": true
}
```

### 7.6 界面操作工具

当前 Vex 菜单包含：

- 文件：新建、新窗口、打开文件、打开文件夹、快速打开、最近文档、编码重开、复制到公众号/知乎/掘金、保存、另存为、保存全部、属性、打开位置、删除、导出、打印、关闭。
- 编辑：撤销、重做、剪切、复制、粘贴、全选、查找、替换。
- 段落：正文、标题 1-6、表格、代码块、数学块、引用、有序列表、无序列表、任务列表、分割线。
- 格式：加粗、斜体、行内代码、链接、图片、清除格式。
- 视图：刷新预览、切换侧边栏、大纲、文档列表、源码模式、显示行号、显示状态栏、字数统计、全屏、置顶。
- 帮助：主题色、排版主题、紧凑布局、语言、MCP 设置、更新日志、新手引导、鸣谢、官网、反馈、关于。

第一期只公开基础且低风险的界面操作，不把所有菜单都交给 AI。删除、打印、新窗口、打开外部网站、反馈入口、全屏、置顶等更偏用户意图或系统级行为，默认不公开。

#### vex.ui_get_state

读取当前界面状态。

输出：

```json
{
  "theme": "dark",
  "typography": "Simple",
  "cultureName": "zh-CN",
  "isCompactLayout": false,
  "isSidebarVisible": true,
  "selectedSidebarTab": "files",
  "isPreviewVisible": true,
  "isSourceMode": false,
  "showLineNumbers": true,
  "isStatusBarVisible": true
}
```

#### vex.ui_set_theme

设置主题色。

输入：

```json
{
  "themeKey": "dark"
}
```

允许值：

- `system`
- `light`
- `dark`
- `aquatic`
- `desert`
- `dusk`
- `night-sky`

#### vex.ui_set_typography

设置 Markdown 排版主题。

输入：

```json
{
  "typographyKey": "Simple"
}
```

允许值来自当前 `ShellAppearanceViewModel.TypographyOptions`，包括 Basic、Simple、OrangeHeart、InkBlack、TechnologyBlue 等现有排版主题。

#### vex.ui_set_language

设置界面语言。

输入：

```json
{
  "cultureName": "zh-CN"
}
```

允许值：

- `zh-CN`
- `zh-Hant`
- `en-US`
- `ja-JP`

#### vex.ui_set_layout

设置基础布局状态。

输入：

```json
{
  "sidebarVisible": true,
  "statusBarVisible": true,
  "previewVisible": true,
  "sourceMode": false,
  "lineNumbersVisible": true,
  "compactLayout": false
}
```

说明：

- 字段可选，只修改传入字段。
- 不开放全屏和置顶。
- `sourceMode` 控制源码编辑面板显示；`previewVisible` 控制预览面板显示，两者可以同时关闭。

#### vex.ui_show_sidebar_tab

切换侧边栏页签。

输入：

```json
{
  "tab": "outline"
}
```

允许值：

- `files`
- `outline`

#### vex.ui_open_panel

打开基础面板或帮助窗口。

输入：

```json
{
  "panel": "find"
}
```

允许值：

- `find`
- `replace`
- `properties`
- `wordCount`
- `mcpSettings`
- `changelog`
- `thanks`
- `about`

说明：

- `website`、`feedback` 默认不开放，避免 AI 主动打开外部网页。
- `onboardingGuide` 默认不开放，避免打断当前工作流。

#### vex.ui_refresh_preview

刷新当前预览。

输入：

```json
{}
```

#### vex.ui_apply_editor_command

执行常用编辑命令。

输入：

```json
{
  "command": "bold"
}
```

允许值：

- `undo`
- `redo`
- `copy`
- `selectAll`
- `paragraph`
- `heading1`
- `heading2`
- `heading3`
- `bold`
- `italic`
- `inlineCode`
- `link`
- `image`
- `quote`
- `orderedList`
- `unorderedList`
- `taskList`
- `codeFence`
- `mathBlock`
- `table`
- `horizontalRule`
- `clearFormatting`

说明：

- `cut` 和 `paste` 第一期开启前需要评估剪贴板隐私，默认不开放。
- 这些命令复用现有 `EditorActionKind` 和编辑器服务，执行后应实时刷新预览。

#### vex.ui_export_current_document

导出当前文档。

输入：

```json
{
  "format": "HTML"
}
```

允许值：

- `HTML`
- `PDF`
- `PNG`
- `Word`

说明：

- 该工具会触发现有保存文件选择流程，仍由用户选择输出位置。
- 不允许 AI 静默导出到任意路径；如需静默导出，应放到第二期并增加路径授权。

#### vex.ui_copy_rendered_html

复制面向平台的富 HTML。

输入：

```json
{
  "target": "wechat"
}
```

允许值：

- `wechat`
- `zhihu`
- `juejin`

说明：

- 复用当前“复制到公众号/知乎/掘金”菜单能力。
- 会写入剪贴板，第一期建议需要确认，避免覆盖用户剪贴板。

### 7.7 不建议第一期公开的界面操作

以下功能第一期不暴露给 AI：

- 删除文件。
- 打印。
- 新窗口。
- 全屏。
- 置顶。
- 打开官网。
- 打开反馈。
- 新手引导。
- 清空最近文档。
- 打开文件所在位置。
- 剪切、粘贴。

原因：

- 这些操作要么有破坏性，要么影响系统窗口状态，要么会访问外部应用或剪贴板。
- 后续可以逐项加入，但应单独加确认和审计。

## 8. 实时编辑与预览

当前 Vex 已经有文档状态通道：

```text
WorkspaceDocumentState.UpdateDocument
MarkdownDocumentChangedCommand
MarkdownPreviewViewModel
MarkdownEditorViewModel
```

第一期实现原则：

1. MCP 编辑工具不要直接改文件后悄悄返回。
2. 必须更新当前 UI 文档状态。
3. 文档状态变化必须走现有事件通道。
4. 预览刷新继续复用现有 Markdown preview 绑定。

建议新增统一编辑服务：

```text
IWorkspaceDocumentEditService
WorkspaceDocumentEditService
```

职责：

- `ReplaceDocument(string markdown)`
- `ApplyTextEdit(int startOffset, int length, string replacement)`
- `InsertText(int offset, string text)`
- `ReplaceSelection(string text)`
- `SaveCurrentAsync()`
- 保证所有 UI 状态更新在 Avalonia UI 线程执行。

线程要求：

- MCP HTTP 请求在后台线程进入。
- 所有触碰 ViewModel、AvaloniaEdit、窗口状态的操作用 `Dispatcher.UIThread.InvokeAsync`。
- 文件 I/O 可在后台执行，但结果应用回 UI 线程。

实时预览：

- 整体替换或局部编辑后立即更新 `Markdown` 属性。
- `MarkdownDocumentChangedCommand` 触发预览 ViewModel。
- 不需要新增单独的“预览渲染循环”。

## 9. 安全策略

### 9.1 访问范围

第一期提供三档：

- `CurrentDocument`：只能读取/修改当前文档。
- `CurrentFolder`：只能访问当前打开文件夹。
- `CustomFolder`：只能访问用户指定目录。

路径校验：

- 使用 `Path.GetFullPath` 规范化。
- 禁止空路径、相对逃逸、UNC 远程路径，除非后续显式支持。
- 对比根目录时使用平台对应的 `StringComparer`。
- 只允许 `.md`、`.markdown`、`.mdown`、`.txt`。

### 9.2 授权

- 所有 MCP 请求必须带 Bearer token。
- Token 不在日志明文输出。
- 设置窗口可重新生成 token。
- Token 变更后立即重启 server 或刷新认证服务。

### 9.3 确认与审计

新增审计模型：

```text
McpOperationRecord
```

字段：

- 时间。
- 工具名。
- 目标路径。
- 操作类型。
- 是否确认。
- 成功/失败。
- 错误摘要。

第一期可只保留内存最近 100 条，后续再落盘。

## 10. AOT 兼容要求

新增代码必须满足：

1. 不使用反射扫描工具方法。
2. 不使用 `dynamic`。
3. 不使用 `Expression.Compile`。
4. 不使用 Newtonsoft.Json。
5. 所有 MCP DTO 都纳入 `McpJsonContext`。
6. 不用 `[UnconditionalSuppressMessage]` 压制新增 IL 警告。
7. 构建时至少验证：

```powershell
dotnet build Vex.slnx -c Release --no-incremental
dotnet publish src\Vex\Vex.csproj -c Release -f net10.0-windows -r win-x64 /p:PublishProfile=FolderProfile__win-x64
```

建议 `McpJsonContext`：

```csharp
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(ToolsListResult))]
[JsonSerializable(typeof(ToolCallRequest))]
[JsonSerializable(typeof(GetCurrentDocumentResult))]
[JsonSerializable(typeof(ApplyTextEditInput))]
internal sealed partial class McpJsonContext : JsonSerializerContext;
```

工具 schema 不要运行时反射生成，直接定义静态 JSON 或强类型 schema DTO。

## 11. 本地化

需要更新：

```text
src/Vex/I18n/Language.cs
src/Vex/I18n/zh-CN.json
src/Vex/I18n/zh-Hant.json
src/Vex/I18n/en-US.json
src/Vex/I18n/ja-JP.json
```

如果 `Language.cs` 由 T4 生成，则按现有生成流程更新，不手写破坏生成约定。

## 12. 第一阶段实施步骤

### 阶段 A：设置 UI 与配置

1. 扩展 `AppSettings` 和 `AppSettingsStore`。
2. 增加 MCP 本地化键。
3. 在帮助菜单中增加“MCP 设置”。
4. 创建 `McpSettingsWindow` 和 `McpSettingsViewModel`。
5. 支持保存、生成 token、复制 endpoint。

验收：

- 菜单位置正确。
- 窗体主题一致。
- 设置重启后保留。
- token 在窗体内直接显示，便于复制。

### 阶段 B：文档编辑服务

1. 新增 `IWorkspaceDocumentEditService`。
2. 将当前文档读取、整体替换、局部编辑、保存动作集中到服务。
3. 确保 UI 线程切换正确。
4. MCP 未接入前也可以由内部调用验证。

验收：

- 调用服务替换 Markdown 后，编辑器和预览同时更新。
- 局部编辑后文档 dirty 状态正确。

### 阶段 C：MCP Server 基础协议

1. 新增 `McpModule`。
2. 新增 `McpServerHost`。
3. 实现 token 校验。
4. 实现 `initialize`、`ping`、`tools/list`、`tools/call`。
5. 手写第一批 tools schema。

验收：

- 未启用时不监听端口。
- token 错误返回 401。
- token 正确可列出 tools。

### 阶段 D：工具接入

优先实现：

1. `vex.get_current_document`
2. `vex.replace_current_document`
3. `vex.apply_text_edit`
4. `vex.open_document`
5. `vex.save_current_document`
6. `vex.get_document_outline`
7. `vex.get_rendered_html`
8. `vex.get_app_status`
9. `vex.ui_get_state`
10. `vex.ui_set_theme`
11. `vex.ui_set_typography`
12. `vex.ui_set_language`
13. `vex.ui_set_layout`
14. `vex.ui_show_sidebar_tab`
15. `vex.ui_open_panel`
16. `vex.ui_apply_editor_command`

验收：

- AI 可读取当前文档。
- AI 可修改当前文档并实时看到预览更新。
- AI 可保存当前文档。
- AI 可切换基础主题、排版、语言和布局。
- AI 可打开查找、替换、属性、字数统计、MCP 设置、更新日志、鸣谢和关于。
- 越权路径被拒绝。

### 阶段 E：AOT 验证

1. Release build。
2. Windows Native AOT publish。
3. 启动 AOT 产物。
4. 连接 MCP。
5. 调用只读和写入工具。

验收：

- 新增 MCP 代码不引入新的 IL 警告。
- AOT 产物中 MCP 设置窗体可打开。
- AOT 产物中 MCP server 可启动和调用。

## 13. 常见风险与规避

### 13.1 MCP SDK 反射注册风险

风险：

SDK 通过 attribute 反射扫描工具，Native AOT 下可能丢方法或产生 IL 警告。

规避：

第一期手写 `tools/list` 和 `tools/call`。

### 13.2 UI 线程风险

风险：

后台 HTTP 请求直接改 ViewModel 或 AvaloniaEdit，导致线程异常。

规避：

统一通过 `Dispatcher.UIThread.InvokeAsync`。

### 13.3 AI 任意改文件风险

风险：

模型调用工具删除或覆盖用户文件。

规避：

默认仅当前文档，写入需确认，路径白名单。

### 13.4 实时预览不同步

风险：

MCP 只写文件不更新 UI。

规避：

所有编辑工具必须走 `IWorkspaceDocumentEditService`，不允许直接 `File.WriteAllText`。

### 13.5 端口冲突

风险：

默认端口被占用导致启动失败。

规避：

设置窗体展示状态，允许修改端口。

## 14. 验收清单

- [x] 帮助菜单中“更新日志”前出现“MCP 设置”。
- [x] MCP 设置窗体符合当前主题。
- [x] 可设置 host、port、token、启用状态、访问范围。
- [x] 未设置 token 无法启动。
- [x] `tools/list` 返回稳定工具列表，并提供静态 input schema。
- [x] `tools/list` 对外返回 OpenAI function calling 兼容的下划线工具名，避免 `.` 导致 `invalid_request_error`。
- [x] `vex.get_current_document` 可读取当前文档。
- [x] `vex.replace_current_document` 可实时更新编辑器和预览。
- [x] `vex.apply_text_edit` 可局部编辑。
- [x] `vex.insert_text` 可插入文本。
- [x] `vex.replace_selection` 可替换当前选区。
- [x] `vex.save_current_document` 可保存。
- [x] `vex.save_current_document` 不弹出确认。
- [x] `vex.get_document_outline` 可读取大纲。
- [x] `vex.get_rendered_html` 可读取当前渲染 HTML。
- [x] `vex.get_app_status` 可读取应用状态。
- [x] `vex.get_operation_audit` 可读取最近 MCP 操作审计。
- [x] `vex.ui_get_state` 可读取界面状态。
- [x] `vex.ui_set_theme` 可切换主题色。
- [x] `vex.ui_set_typography` 可切换排版主题。
- [x] `vex.ui_set_language` 可切换语言。
- [x] `vex.ui_set_layout` 可切换基础布局。
- [x] `vex.ui_show_sidebar_tab` 可切换侧边栏页签。
- [x] `vex.ui_open_panel` 可打开基础面板。
- [x] `vex.ui_apply_editor_command` 可执行常用编辑命令。
- [x] `vex.ui_export_current_document` 可触发现有导出流程，输出位置仍由用户选择。
- [x] `vex.ui_copy_rendered_html` 可复制面向平台的富 HTML，并默认需要确认。
- [x] 不向 AI 暴露删除、打印、新窗口、全屏、置顶、官网、反馈、新手引导、清空最近文档、打开文件所在位置、剪切、粘贴。
- [x] 越权路径被拒绝。
- [x] 修改、打开外部路径、复制富 HTML 默认弹出确认；保存当前文档不确认。
- [x] Release 构建通过。
- [x] Windows Native AOT 发布通过。
- [x] 新增代码不使用反射工具扫描和反射 JSON 序列化。
- [ ] AOT 产物中 MCP 设置窗体人工打开验证。
- [ ] AOT 产物中 MCP server 人工连接调用验证。

## 15. 推荐第一期范围

第一期只做“AI 操作当前文档”的完整闭环：

1. MCP 设置窗体。
2. 本机 Streamable HTTP endpoint。
3. Bearer token。
4. tools/list、tools/call。
5. 当前文档读取。
6. 整体替换、局部替换、插入文本。
7. 保存当前文档。
8. 当前大纲和 HTML 预览读取。
9. 基础界面操作：读取界面状态、切换主题、切换排版、切换语言、切换布局、打开基础面板、执行常用编辑命令。
10. 修改确认；保存当前文档不确认。
11. Native AOT 发布验证。

第二期再扩展：

- MCP resources。
- 最近文档资源。
- 多文档管理。
- 文件夹批量扫描。
- 更细粒度 diff 预览。
- 外部客户端连接配置导出。
- 操作审计落盘。

## 16. 当前实施进度与逻辑推演

更新时间：2026-06-06。

### 16.1 已落地文件

MCP 设置与菜单：

- `src/Vex/Modules/Shell/Views/McpSettingsWindow.axaml`
- `src/Vex/Modules/Shell/Views/McpSettingsWindow.axaml.cs`
- `src/Vex/Modules/Shell/ViewModels/McpSettingsViewModel.cs`
- `src/Vex/Modules/Shell/Views/ShellTitleMenuView.axaml`
- `src/Vex/Modules/Shell/ViewModels/ShellTitleMenuViewModel.cs`
- `src/Vex/Modules/Shell/Services/ShellActionCoordinator.cs`

MCP 协议与工具：

- `src/Vex/Modules/Mcp/Services/McpServerHost.cs`
- `src/Vex/Modules/Mcp/Services/IMcpServerHost.cs`
- `src/Vex/Modules/Mcp/Services/McpToolDispatcher.cs`
- `src/Vex/Modules/Mcp/Services/IMcpToolDispatcher.cs`
- `src/Vex/Modules/Mcp/Models/McpContracts.cs`
- `src/Vex/Modules/Mcp/Serialization/McpJsonContext.cs`

安全、确认与审计：

- `src/Vex/Modules/Mcp/Services/IMcpOperationConfirmationService.cs`
- `src/Vex/Modules/Mcp/Services/McpOperationConfirmationService.cs`
- `src/Vex/Modules/Mcp/Views/McpOperationConfirmationWindow.axaml`
- `src/Vex/Modules/Mcp/Views/McpOperationConfirmationWindow.axaml.cs`
- `src/Vex/Modules/Mcp/Services/IMcpOperationAuditService.cs`
- `src/Vex/Modules/Mcp/Services/McpOperationAuditService.cs`
- `src/Vex/Modules/Mcp/Models/McpOperationRecord.cs`

编辑器选区：

- `src/Vex/Core/Messaging/EditorSelectionQuery.cs`
- `src/Vex/Modules/Workspace/Services/MarkdownEditorController.cs`

配置与本地化：

- `src/Vex/Core/Models/AppSettings.cs`
- `src/Vex/Core/Services/AppSettingsStore.cs`
- `src/Vex/I18n/Language.cs`
- `src/Vex/I18n/zh-CN.json`
- `src/Vex/I18n/zh-Hant.json`
- `src/Vex/I18n/en-US.json`
- `src/Vex/I18n/ja-JP.json`

### 16.2 当前工具清单

对外工具名使用下划线格式；旧点号格式调用由服务端兼容归一化。

只读工具：

- `vex_get_current_document`
- `vex_get_document_outline`
- `vex_get_selection`
- `vex_get_rendered_html`
- `vex_get_app_status`
- `vex_get_operation_audit`
- `vex_ui_get_state`

编辑工具：

- `vex_replace_current_document`
- `vex_apply_text_edit`
- `vex_insert_text`
- `vex_replace_selection`

文件工具：

- `vex_open_document`
- `vex_save_current_document`

界面工具：

- `vex_ui_set_theme`
- `vex_ui_set_typography`
- `vex_ui_set_language`
- `vex_ui_set_layout`
- `vex_ui_show_sidebar_tab`
- `vex_ui_open_panel`
- `vex_ui_refresh_preview`
- `vex_ui_apply_editor_command`
- `vex_ui_export_current_document`
- `vex_ui_copy_rendered_html`

### 16.3 逻辑推演结果

协议推演：

- `initialize`、`ping`、`tools/list`、`tools/call` 均返回 JSON-RPC response。
- `notifications/initialized` 和其他无 `id` notification 返回 HTTP `204 No Content`，避免对 notification 返回错误 response。
- 有 `id` 的未知 method 返回 JSON-RPC `-32601`。
- `tools/list` 的 schema 是静态 JSON 文本，启动时解析为 `JsonElement`，不通过反射生成。

安全推演：

- 未启用 MCP 时不监听端口。
- Token 为空时不允许启动。
- 监听地址限制为 `127.0.0.1`、`localhost`、`::1`。
- Bearer token 校验失败返回 `401`。
- `open_document` 做扩展名校验和授权路径校验。
- `CurrentDocument` 只允许当前文档路径。
- `CurrentFolder` 当前实现解释为“当前文档所在目录”，不是独立的“已打开文件夹状态”。
- `CustomFolder` 只允许用户指定目录内文件。
- UNC 路径默认拒绝。
- 删除、打印、新窗口、全屏、置顶、外部网页、反馈、新手引导、剪切、粘贴等未暴露给 AI。

确认推演：

- 文档编辑、打开文档、复制富 HTML 默认需要确认。
- 保存当前文档不确认，符合用户要求。
- 导出当前文档走现有保存位置选择流程，AI 不可静默指定输出路径。
- 只读工具不确认。

审计推演：

- 工具调用成功、失败、取消都会记录最近 100 条内存审计。
- 审计记录不包含 token、Markdown 正文、替换文本或选区文本。
- `vex.get_operation_audit` 本身也会产生一条只读审计记录。

实时预览推演：

- MCP 编辑工具更新 `MainWindowViewModel.Markdown`。
- `Markdown` setter 会刷新文档派生状态、工作区文档状态和预览绑定。
- 当前实现没有单独抽出 `IWorkspaceDocumentEditService`，而是复用 `MainWindowViewModel` 的 MCP 专用入口；第一期可以接受，但后续应考虑抽服务以改善可测试性。

AOT 推演：

- 新增 MCP DTO 都纳入 `McpJsonContext`。
- MCP 工具分发使用 `switch`，没有反射扫描工具方法。
- MCP JSON 序列化调用都显式传入 source-generated `JsonTypeInfo`。
- 未新增 Newtonsoft.Json、`dynamic`、`Expression.Compile` 或 IL warning suppression。

### 16.4 已验证命令

已通过：

```powershell
dotnet build src\Vex\Vex.csproj -c Debug -f net10.0-windows --no-incremental
dotnet build Vex.slnx -c Release --no-incremental
dotnet publish src\Vex\Vex.csproj -c Release -f net10.0-windows -r win-x64 /p:PublishProfile=FolderProfile__win-x64
```

已知输出：

- 普通构建仍有既有 `NU1507` 包源映射警告。
- Native AOT publish 仍有 Avalonia、ReactiveUI、Prism、DryIoc、Ursa 等既有依赖链 trim/AOT 警告。
- 新增 MCP 代码未出现 `System.Text.Json` 反射序列化类告警。

### 16.5 仍需继续推进

优先级高：

- 启动 AOT 产物，人工打开 MCP 设置窗体。
- 用真实 MCP/HTTP 客户端连接 `http://127.0.0.1:17891/mcp/`，验证 `initialize`、`tools/list`、只读工具、写入工具、确认弹窗、保存和越权拒绝。
- 验证 `HttpListener` 在 `::1` 和 Windows 普通用户权限下的行为。

优先级中：

- 增加 `vex.save_current_document_as`，但必须先实现路径授权和目标已存在确认。
- 抽出 `IWorkspaceDocumentEditService`，降低 `McpToolDispatcher` 对 `MainWindowViewModel` 的耦合。
- 将 MCP server 状态变化绑定到设置窗体实时显示，而不是只在保存后刷新一次。
- 为 MCP 操作审计增加设置窗体内的只读查看入口。

优先级低：

- MCP resources。
- 最近文档资源。
- 多文档管理。
- 文件夹批量扫描。
- 更细粒度 diff 预览。
- 操作审计落盘。
