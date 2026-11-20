# CodeWF.Avalonia.Markdown.Core

Markdown 块编辑核心：块模型与编辑操作（拆块/合并/空文档首块），零 UI 依赖。Block editing core for the markdown editor: block model and editing operations with no UI dependency.

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Avalonia.Markdown.Core?label=nuget)](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown.Core)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://github.com/dotnet9/Vex/blob/main/LICENSE)

## 安装

```bash
dotnet add package CodeWF.Avalonia.Markdown.Core
```

零第三方依赖（仅 .NET BCL）；目标框架 net11.0。一般由完整包 [CodeWF.Avalonia.Markdown](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown) 经传递引用带入，自研编辑器宿主也可单独引用。

## 定位

`MarkdownBlockEditorCore` 与 `Blocks/` 提供所见即所得块编辑的状态机与操作原语：

- 块模型：标题/段落/列表/引用/代码块等块类型与源码区间（`SourceSpan`）跟踪。
- 编辑操作：拆块、合并、空文档首块创建等原语，编辑器控件据此派生按键行为。
- 状态键：块级状态字典以块源码起始偏移为键，源码变更才清理，同文档重渲染保留折叠等状态。

纯逻辑库，可在无 UI 的测试与后台流程中直接使用。

## 文档

- 仓库（Vex · 维刻 Markdown 编辑器）：<https://github.com/dotnet9/Vex>
