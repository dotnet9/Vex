# MCP 使用与接口说明

Vex 内置本机 MCP 服务，让支持该连接方式的 AI 客户端读取、编辑和保存当前 Markdown 文档。本文合并使用、接口与实现说明，以当前 Avalonia 源码为准；浏览器原型的 MCP 仅演示交互。

## 启用与连接

1. 在“工具 → MCP 设置”或偏好中的 MCP 页配置服务。
2. 使用默认回环地址 127.0.0.1、端口 17891，生成 Token 并选择访问范围。
3. 保存配置后启用，确认状态显示服务运行中。
4. 在客户端配置 HTTP 地址与 Bearer Token，先执行只读工具验证。

默认端点为 http://127.0.0.1:17891/mcp/ 。接口使用 HTTP JSON-RPC，服务默认关闭。HTTP 客户端需要发送 Authorization: Bearer <Token>；实际客户端是否兼容需逐个验证，不将 HTTP POST 可用等同于所有传输特性均已实现。

标准 stdio 客户端可使用 [桥接脚本](../scripts/mcp_stdio_bridge.py)。将示例中的路径和令牌替换为本机值：

~~~json
{
  "mcpServers": {
    "vex": {
      "command": "python",
      "args": ["<仓库或安装目录>/mcp_stdio_bridge.py"],
      "env": {"VEX_MCP_TOKEN": "<服务中生成的Token>"}
    }
  }
}
~~~

仓库中的桥接脚本在 scripts 目录，正式发布目录不包含该脚本。安装后把脚本路径改为仓库或自行复制后的路径。自定义地址等环境参数以脚本参数说明为准；不要将真实 Token 放入仓库或截图。

## 协议与资源

支持 initialize、notifications/initialized、ping、tools/list、tools/call、resources/list、resources/read。initialize 返回的协议版本为 2025-06-18。请求、响应与 schema 使用静态模型和 System.Text.Json 源生成，不通过反射扫描方法注册工具。

资源包括 vex://current-document 与 vex://file/<完整路径>，读取同样受访问范围约束。不要把此资源 URI 当作系统文件浏览的无限授权。工具结果同时检查 JSON-RPC error 和工具层错误；用户拒绝、文档变化、路径越界或文件失败都不能显示为成功。

## 29 个工具

工具名使用下划线，旧点分命名由服务端兼容归一化。无参数工具使用空对象。参数名、枚举和约束以 [McpToolDispatcher.cs](../src/Vex/Modules/Mcp/Services/McpToolDispatcher.cs) 的静态 schema 为准。

| 工具 | 参数要点 / 用途 |
| --- | --- |
| vex_get_current_document | 当前文档内容与身份 |
| vex_get_document_outline | 标题大纲 |
| vex_get_selection | 当前选区 |
| vex_get_rendered_html | 获取渲染 HTML |
| vex_get_app_status | 应用状态 |
| vex_get_operation_audit | 操作审计 |
| vex_list_files | 授权范围内文件列表 |
| vex_replace_current_document | markdown，reason 可选；替换正文 |
| vex_apply_text_edit | startOffset、length、replacement，reason 可选 |
| vex_insert_text | text，offset 可选 |
| vex_replace_selection | text，reason 可选 |
| vex_replace_text | find、replacement，replace_all 可选；查找目标须符合唯一性约束 |
| vex_undo | 撤销 MCP 编辑 |
| vex_redo | 重做 MCP 编辑 |
| vex_open_document | path，encodingName 可选；先处理未保存内容 |
| vex_create_document | path、content，overwrite、reason 可选 |
| vex_save_current_document | 保存当前文件，必要时进入系统保存流程 |
| vex_refresh_preview | 刷新预览 |
| vex_ui_get_state | 读取界面状态 |
| vex_ui_set_theme | themeKey；枚举见下文 |
| vex_ui_set_typography | typographyKey，使用应用当前排版 Key |
| vex_ui_set_language | cultureName：zh-CN / zh-Hant / en-US / ja-JP |
| vex_ui_set_layout | sidebarVisible、statusBarVisible、previewVisible、sourceMode、lineNumbersVisible、compactLayout，可选布尔值 |
| vex_ui_show_sidebar_tab | tab：files / outline |
| vex_ui_open_panel | panel：find / replace / properties / wordCount / mcpSettings / changelog / thanks / about |
| vex_ui_refresh_preview | 从界面命令刷新预览 |
| vex_ui_apply_editor_command | command，按静态白名单执行常用编辑动作 |
| vex_ui_export_current_document | format：HTML / PDF / PNG / Word |
| vex_ui_copy_rendered_html | target：wechat / zhihu / juejin |

当前 vex_ui_set_theme 的 themeKey 枚举为 system、light、dark、aquatic、desert、dusk、night-sky；这并不覆盖所有新配色，也不是浏览器原型的合并目录。vex_ui_set_layout 为原有布尔布局接口，不将八种组合的名称作为其参数。

文本 offset 与 length 按 .NET 字符串 UTF-16 单元计数，不能直接使用 UTF-8 字节偏移；有表情、组合字符时应重新读取文档再计算。修改前读取目标、修改后复核内容，避免将旧位置应用于变化后的文本。

## 权限、确认与持久化

访问范围包括当前文档、当前工作区及自定义目录。路径按实际系统规则归一化、比较并检查范围。文档编辑、打开、新建、保存、复制富 HTML 等动作按配置执行确认；有未保存内容时仍走正常文件切换确认。

确认窗口可为单个工具记住本次运行的允许或拒绝；重新保存 MCP 设置会重置。配置中关闭确认不是新的任意文件访问授权，范围检查仍然有效。

服务不暴露删除文件、系统打印、任意外站、任意进程、默认程序更改等操作。可操作的界面命令经过静态白名单，不是远程操控整台电脑。

Token 当前作为 AppSettings 中的配置字段持久化，不能宣称已经使用系统凭据库或全盘加密。审计保存到系统 LocalApplicationData/Vex/mcp-audit.jsonl，超过约 1 MB 轮转。客户端配置、环境变量、剪贴板和备份也可能含令牌；换机与分享时先脱敏。

## 源码与验证

| 文件 / 目录 | 职责 |
| --- | --- |
| [McpServerHost.cs](../src/Vex/Modules/Mcp/Services/McpServerHost.cs) | HttpListener 生命周期、鉴权与 JSON-RPC 分发 |
| [McpToolDispatcher.cs](../src/Vex/Modules/Mcp/Services/McpToolDispatcher.cs) | schema、路径范围与工作区动作 |
| [McpJsonContext.cs](../src/Vex/Modules/Mcp/Serialization/McpJsonContext.cs) | AOT 序列化元数据 |
| src/Vex/Modules/Mcp/Services | 确认、审计与 Token 生成 |
| src/Vex/Modules/Shell/ViewModels/McpSettingsViewModel.cs | 配置界面和生效时机 |
| scripts/ui-verification | 应用界面与部分 MCP 回归 |

使用专用临时目录和测试文档验证：默认关闭、错误 Token、端口占用、只读工具、范围内外路径、取消确认、修改后视图同步、保存失败、撤销重做、resource 读取、审计和重启配置。真实客户端按 HTTP / stdio 两条路径分别联调，并在 Windows、Linux、macOS 对应系统确认服务托管与权限行为。

当前没有把任意客户端的 Inspector 成功或全部端到端互通写成既成事实。历史检查与平台边界见 [验收与已知限制](验收与已知限制.md)。
