# Vex 仓库学习文档

本文是仓库文档的统一入口，基于 **Vex 1.7.3 / .NET 10 / Avalonia 12.1.4**，整理日期为 2026-10-21。需求描述产品应有的行为，设计说明当前源码如何实现，验收记录说明已经验证到哪里；三者不能互相替代。

## 阅读顺序

| 目标 | 建议顺序 | 读完应能回答 |
| --- | --- | --- |
| 使用产品 | [快速开始](快速开始.md) → [Markdown 示例](Markdown功能示例.md) | 如何安装、写作、整理、分享和备份 |
| 理解业务 | [需求文档](需求文档.md) → [验收与已知限制](验收与已知限制.md) | 解决什么问题，范围和验证边界是什么 |
| 学习实现 | [概要设计](概要设计.md) → [详细设计](详细设计.md) → [开发与验证](开发与验证.md) | 模块如何协作，如何定位、修改和验证代码 |
| 学习界面 | [原型与资源](原型与资源.md) → [开发计划](开发计划.md) | 设计准则、资源目录和原生界面如何对齐，还有哪些差距待补 |
| 接入 AI 客户端 | [MCP 接口](MCP接口.md) | 如何连接，29 个工具的参数和权限是什么 |
| 制作安装包 | [安装与发布](安装与发布.md) | 如何从 NuGet 依赖构建五个平台安装包 |

## 从源码开始

1. 先读 [Program.cs](../src/Vex/Program.cs) 与 [App.axaml.cs](../src/Vex/App.axaml.cs)，了解启动、容器和模块组合。
2. 沿 [MainWindowViewModel.cs](../src/Vex/Modules/Shell/ViewModels/MainWindowViewModel.cs) 跟踪打开、编辑、保存一次文档。
3. 对照 [WorkspaceDocumentState.cs](../src/Vex/Modules/Workspace/Services/WorkspaceDocumentState.cs) 和 [WorkspaceEditHistory.cs](../src/Vex/Modules/Workspace/Services/WorkspaceEditHistory.cs)，理解共享原文和撤销历史。
4. 从 [AppSettings.cs](../src/Vex/Core/Models/AppSettings.cs)、[AppSettingsStore.cs](../src/Vex/Core/Services/AppSettingsStore.cs) 学习偏好、文件折叠和大纲折叠的持久化。
5. 按改动范围选择验证器，再阅读发布脚本；不要先把所有测试、安装和发布流程一起运行。

## 仓库地图

| 目录 / 文件 | 内容 |
| --- | --- |
| [src/Vex](../src/Vex) | 桌面主程序、视图模型、文件服务、国际化、MCP |
| [src/Vex.Controls](../src/Vex.Controls) | 应用控件、主题资源和统一样式入口 |
| [design](../design/index.html) | 可离线预览的桌面、Windows / Linux / macOS 安装原型 |
| [演示文档](演示文档) | 随程序发布的六份可编辑样例与本地插图，也是原型的样例来源 |
| [scripts](../scripts) | 验证器、共享库打包联调、发布与安装器源码 |
| [.github/workflows](../.github/workflows) | 检查、NuGet 发布和五 RID 原生安装包发布 |
| [Vex.slnx](../Vex.slnx) | 两个生产项目的解决方案入口 |
| [Directory.Build.props](../Directory.Build.props) / [Directory.Packages.props](../Directory.Packages.props) | 产品版本与构建属性 / 中央 NuGet 版本 |
| [更新日志](更新日志.md) | 对外版本变更，同时供应用帮助、原型及发布说明生成使用 |

## 文档维护规则

- 长期知识集中在本目录，按需求、设计、操作、开发、发布、接口和验收职责维护，避免为每次修复新建一篇流水账。
- 代码行为变化时同步修改相应设计和验证边界；版本、资源数量、工具参数以源码为准。短期计时、截图、构建日志保存在仓外临时目录，仓库保留方法、结论和必要证据链接。
- 根目录保留 README 和 LICENSE；仓库说明尽量集中到本目录并使用中文文件名，MCP 等通用名称按实际用途保留。第三方许可证、来源清单和测试 Markdown 夹具保留在资源附近。
- [更新日志](更新日志.md) 面向普通用户，使用 `## V版本（日期）` 标题，每天只设一个组；同日较早版本在条目末尾标注版本。条目统一使用 `😄[新增]-`、`🔨[优化]-`、`🐛[修复]-` 或 `❌[删除]-`，说明实际体验变化，不记录中间测试和实现过程。
- 历史实现过程由 Git 追溯；当前文档不宣称所有平台、输入法和视觉比例已经得到同等验证。

许可与依赖见 [鸣谢](鸣谢.md)，协作和问题报告见 [贡献与安全](贡献与安全.md)。
