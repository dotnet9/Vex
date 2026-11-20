# CodeWF.Avalonia.Markdown

Markdown 完整包：在 [Lite 渲染引擎](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown.Lite)之上提供图片（GIF/SVG/预览）、数学公式、Mermaid 图表、TextMate 代码高亮、行内样式与多格式导出、源码编辑器与所见即所得编辑控件。Full Markdown package: images, math, mermaid, highlighting, export and editors on top of the Lite engine.

[![NuGet](https://img.shields.io/nuget/v/CodeWF.Avalonia.Markdown?label=nuget)](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown)
[![License](https://img.shields.io/badge/license-MIT-blue)](https://github.com/dotnet9/Vex/blob/main/LICENSE)

## 安装

```bash
dotnet add package CodeWF.Avalonia.Markdown
```

依赖 Avalonia、AvaloniaEdit、AnimatedImage.Avalonia、Mermaider、SkiaSharp、Svg.Skia、Sylinko.CSharpMath.Avalonia、TextMateSharp(.Grammars)、Avalonia.Controls.ColorPicker，以及同系 `CodeWF.Avalonia.Markdown.Lite`、`CodeWF.Avalonia.Markdown.Core`、`CodeWF.Avalonia.Controls`、`CodeWF.Avalonia.Lang`、`CodeWF.Toolkit.Logging`（随包自动安装）；目标框架 net11.0，XAML 命名空间 `https://codewf.com`。

## 主题接入

```xml
<Application.Styles>
  <markdown:MarkdownFullThemes />
</Application.Styles>
```

`MarkdownFullThemes` 继承自 Lite 的 `MarkdownThemes`，含全部能力控件主题；排版主题与尺寸切换同 Lite。

## 能力一览

| 模块 | 说明 |
|---|---|
| 图片 | GIF/SVG 渲染与预览（AnimatedImage / Svg.Skia） |
| 公式 | 行内/块级数学公式（CSharpMath.Avalonia） |
| 图表 | Mermaid 图表卡片 |
| 高亮 | TextMate 语法高亮代码块 |
| 行内样式 | `span` style 字号/前景色/背景色，含选区浮动条与可读性回退 |
| 导出 | PNG / PDF / Word(Docx) / HTML 导出与公众号 HTML 复制（`MarkdownDocumentExporter` 等） |
| 编辑 | 源码编辑器与单栏实时编辑（所见即所得）控件，基于块编辑核心 [CodeWF.Avalonia.Markdown.Core](https://www.nuget.org/packages/CodeWF.Avalonia.Markdown.Core) |

流式渲染、大纲、检索与虚拟化等基础能力由 Lite 包提供，随本包传递引用。

## 文档

- 功能示例：<https://github.com/dotnet9/Vex/blob/main/docs/Markdown功能示例.md>
- 行内样式：<https://github.com/dotnet9/Vex/blob/main/docs/Markdown行内样式.md>
- 仓库（Vex · 维刻 Markdown 编辑器）：<https://github.com/dotnet9/Vex>
