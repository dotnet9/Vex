# CodeWF.Avalonia.Markdown.Lite

Markdown 渲染基础包：基于 Avalonia 与 Markdig 的解析渲染引擎，只渲染常规元素（标题/段落/列表/引用/表格/链接/代码块单色/图片替代文本/公式原文），内置 18 套排版主题。Markdown rendering engine with built-in typography themes, no heavy capability dependencies.

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Avalonia.Markdown.Lite?label=nuget)](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown.Lite)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://github.com/dotnet9/Vex/blob/main/LICENSE)

## 安装

```bash
dotnet add package CodeWF.Avalonia.Markdown.Lite
```

依赖 Avalonia、Markdig 与 `CodeWF.Avalonia.Lang`（随包自动安装），不引入 SkiaSharp 等重组件；目标框架 net11.0，XAML 命名空间 `https://codewf.com`（前缀约定 `markdown`）。

## 主题接入

```xml
<Application.Styles>
  <markdown:MarkdownThemes />
</Application.Styles>
```

排版主题共 18 套（Basic 为默认），经 `MarkdownTypographyThemeRegistry` 选择，`OverrideTypographyResources` 支持全局/局部运行时切换，无需重建控件。

## 核心能力

| 控件/组件 | 说明 |
|---|---|
| `MarkdownViewer` | 渲染视图：大纲面板（`MarkdownOutlinePanel`）、文内检索、选区与阅读位置保持，`WindowedStackPanel` 虚拟化长文档 |
| `MarkdownStreamingDocument` | 流式渲染：与 `MarkdownStreamSegmenter` / `MarkdownTailRemender` 配合做未完成语法的前端补全（remend），解析失败保留旧快照 |
| `MarkdownTypewriterStream` / `MarkdownStreamPacer` | 打字机流与节奏控制 |
| `MarkdownImageSourceLoader` / `MarkdownImageDiskCache` | 图片加载与磁盘缓存（安全白名单约束在加载器内） |

图片、公式、Mermaid、代码高亮与导出等重组件能力在完整包 [CodeWF.Avalonia.Markdown](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown) 中提供。

## 文档

- 功能示例：<https://github.com/dotnet9/Vex/blob/main/docs/Markdown功能示例.md>
- 仓库（Vex · 维刻 Markdown 编辑器）：<https://github.com/dotnet9/Vex>
