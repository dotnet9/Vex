# Vex 界面原型（design/）

本目录是 Vex（维刻）各界面的**重设计 HTML 原型**，用于在改版开发前评审视觉方案。纯静态 HTML + CSS + 原生 JS，无任何构建依赖，**双击 `index.html` 即可浏览**（或任意静态服务器）。

## 浏览方式

```
# 方式一：直接双击
design/index.html

# 方式二：本地服务
cd design && python -m http.server 8791
# 打开 http://127.0.0.1:8791/
```

## 主题色切换

所有页面（含总览页）右下角有**主题悬浮球**，展开后可选择六套主题色，与 App 的主题（`帮助 → 主题色`，定义见 `src/Vex.Controls.Themes/Themes/Shared/AppPalette.axaml`）一一对应：

| 主题 | 说明 | 强调色 |
|------|------|--------|
| 浅色 light | 极地蓝，默认 | `#1677FF` |
| 深色 dark | 石墨 | `#60A5FA` |
| 水生 aquatic | 湖心青 | `#0EA5B7` |
| 沙漠 desert | 暖阳橙 | `#C77B2A` |
| 黄昏 dusk | 薄暮紫 | `#8A63D2` |
| 夜空 night-sky | 深海蓝（深色） | `#7AA2FF` |

- 选择会写入 `localStorage`，**跨页面、跨标签页记忆**（同浏览器内）。
- 快捷键：`Alt + 1..6` 直接切换。
- URL 参数：任意页面可加 `?theme=dark`（或 `aquatic / desert / dusk / night-sky / light`）指定主题，便于分享与截图。

## 页面清单

| 页面 | 对应界面（源码） |
|------|------------------|
| `index.html` | 原型总览导航 |
| `pages/main-window.html` | 主窗口（标题栏 + 侧栏 + 编辑器 + 预览 + 状态栏，`MainWindow.axaml`） |
| `pages/menu-flyout.html` | 标题菜单与「主题色」子菜单（`ShellTitleMenuView.axaml`） |
| `pages/find-replace.html` | 查找替换栏（`ShellFindBarView.axaml`） |
| `pages/sidebar-files.html` | 文件侧栏：目录树 + 右键菜单 + 空状态（`ShellFilesView.axaml`） |
| `pages/sidebar-outline.html` | 大纲侧栏（`ShellOutlineView.axaml`） |
| `pages/dialog-unsaved.html` | 未保存更改确认（`ShellUnsavedConfirmationOverlayView.axaml`） |
| `pages/dialog-rename.html` | 重命名文件（`ShellRenameFileOverlayView.axaml`） |
| `pages/dialog-delete.html` | 删除确认窗口（`ShellDeleteConfirmationWindow.axaml`） |
| `pages/dialog-error.html` | 错误提示（`ShellErrorOverlayView.axaml`） |
| `pages/dialog-mcp-confirm.html` | MCP 操作确认（`McpOperationConfirmationWindow.axaml`） |
| `pages/window-statistics.html` | 字数统计窗口（`ShellStatisticsWindow.axaml`） |
| `pages/window-properties.html` | 文件属性窗口（`ShellPropertiesWindow.axaml`） |
| `pages/window-mcp-settings.html` | MCP 设置窗口（`McpSettingsWindow.axaml`） |
| `pages/window-help-doc.html` | 帮助文档窗口（`MarkdownDocumentWindow.axaml`，更新日志等） |
| `pages/window-thanks.html` | 鸣谢窗口（`MarkdownDocumentWindow.axaml` 复用，帮助菜单「鸣谢」） |
| `pages/window-about.html` | 关于窗口（`AboutWindow.axaml`） |

## 结构与约定

```
design/
├── index.html          总览导航（含主题色胶囊快捷切换）
├── assets/
│   ├── theme.css       六套主题令牌（变量）+ 全部组件样式 + 交互层样式
│   ├── theme.js        主题切换器（悬浮球注入 / localStorage / Alt 快捷键 / ?theme= 参数）
│   └── interact.js     交互层：页面导航器 / toast / 六大菜单（与实现同构）/ 窗口关闭返回
└── pages/              每个界面一个独立原型页
```

- **可交互原型**：所有页面右下角有九宫格「页面导航器」，16 个界面间直接跳转、不必回首页；菜单栏可点击展开（数据与 `ShellTitleMenuView.axaml` 一致），带对应原型页的菜单项（属性、删除、查找替换、字数统计、MCP 设置、更新日志、关于等）点击即跳转；对话框按钮、设置开关、过滤搜索、命中替换等交互均可实际操作，反馈以 toast 呈现（对应 App 的状态栏提示）。纯展示的静态形态仅保留说明性图卡（如主题色子菜单展开态）。
- **色值来源**：`theme.css` 中每套主题的变量逐一取自 `AppPalette.axaml` 对应 `ThemeDictionaries`，原型与真实主题保持一致；`--accent` 等语义色在此基础上统一了使用规则（强调色只用于主操作、选中态、链接与焦点）。
- **设计升级点**：每页顶部说明区列出了该界面相对现状的改版要点（统一圆角/阴影/间距节律、编辑器语法着色与行号、侧栏卡片化、对话框图标化与按钮分级、状态栏徽章化，以及若干新增能力如侧栏过滤、阅读进度条等，均以「新增能力」标注，落地时可裁剪）。
- **窗口内边距**：独立窗口与对话框的内容边距统一节律（内容区左右 24px，对话框统一 `padding: 22px`）；**含滚动区的窗口，Padding 必须加在滚动容器外层**（对照 `MarkdownDocumentWindow.axaml` 的 `Padding="36 26 36 30"` 加在包住 ScrollViewer 的 Border 上），保证 Right 的滚动条、Bottom 的内容与窗体边缘的留白和 Left 一致，不得贴边。
- 图标为内联 SVG sprite（feather 风格描边），随主题 `currentColor` 着色；新增页面时把用到的 `<symbol>` 复制进页面内的 sprite 即可。
