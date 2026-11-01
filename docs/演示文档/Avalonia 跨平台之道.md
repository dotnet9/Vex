# Avalonia 跨平台之道

![Avalonia 分层架构插图](图片/avalonia-cover.svg)

[TOC]

2013 年 12 月 5 日，Steven Kirk 提交了项目的第一行代码，那时它还叫 **Perspex**。十一年后，这个后来更名为 Avalonia 的项目已成为 .NET Foundation 旗下的成熟跨平台 UI 框架：2023 年 7 月发布的 11.0 版带来全新渲染系统与移动端支持。

## 为什么不是 WPF

Avalonia 的 XAML 方言继承自 WPF——绑定、样式、触发器的写法几乎一致，WPF 开发者可以无缝迁移。但两者有一个本质区别：

> WPF 把控件绘制交给系统（User32/GDI），Avalonia **自己画所有控件**：基于 Skia 图形库，每个按钮、每条滚动条都由框架亲手绘制。

这正是「像素级一致」的底气——同一个界面，在 Windows、Linux 与 macOS 上看不出差别。代价则是失去原生控件外观，换来的是完整的视觉掌控权。

## 分层架构

| 层 | 职责 |
|:---|:---|
| 你的应用 | AXAML 界面 + ViewModel 逻辑 |
| Avalonia.Controls | 控件、模板、样式系统 |
| Avalonia.Base | 布局引擎、输入、渲染抽象 |
| Skia | 跨平台 2D 光栅化 |

## 三个值得了解的机制

- **编译期 XAML**：XAML 在构建时被编译成 IL（而非运行时解析），启动快且类型错误提前暴露
- **lookless controls**：控件不带固定外观，样式完全由主题决定——这正是 Vex 能深度定制度的原因
- **.NET Foundation 项目**：MIT 协议开源，由 Steven Kirk、Nikita Tsukanov 与 Dan Walmsley 领导维护

## Vex 与 Avalonia

Vex 的每一处界面细节——标签行、编辑器、水印、状态栏——都是 Avalonia 控件树的产物。按下 <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> 预览排版，背后是 Skia 在重新光栅化整篇文档。

---

延伸阅读：[Avalonia 文档](https://docs.avaloniaui.net/) · [GitHub 仓库](https://github.com/AvaloniaUI/Avalonia) · [十年回顾](https://avaloniaui.net/blog/10-years-of-avalonia)
