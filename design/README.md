# Vex 界面原型（design/）

本目录是 Vex（维刻）界面的**可交互重设计原型**。只有一个页面：`index.html` 是完整的主窗体，**所有其他界面都通过标题菜单、状态栏等入口在窗体内以浮层呼出**（与真实 App 的交互一致），没有页面间跳转。

## 浏览方式

```
# 方式一：直接双击
design/index.html

# 方式二：本地服务
cd design && python -m http.server 8791
# 打开 http://127.0.0.1:8791/
```

## 界面呼出地图

| 入口（菜单 / 控件） | 呼出的界面 |
|---|---|
| 文件 → 属性...（Alt+Enter） | 文件属性窗口 |
| 文件 → 删除... | 删除确认窗口 |
| 文件 → 打开最近文件 / 关闭 | 未保存更改确认覆盖层 |
| 文件 → 导出 → PDF | 错误提示覆盖层 |
| 编辑 → 查找... / 替换...（Ctrl+F / Ctrl+H） | 查找替换栏（编辑器顶部展开） |
| 视图 → 字数统计窗口 / 状态栏「词数」 | 字数统计窗口 |
| 视图 → 大纲 / 文档列表 | 侧栏「文件 / 大纲」页签切换 |
| 视图 → 源代码模式 / 预览模式 / 侧边栏 | 主区窗格真实切换，侧边栏项右侧勾选反映显隐 |
| 帮助 → MCP 设置 | MCP 设置窗口 |
| 帮助 → 更新日志 / 鸣谢 | 文档窗口（内容切换） |
| 帮助 → 关于 | 关于窗口 |
| 帮助 → 主题色（Alt+1..6 / 右下悬浮球） | 六套主题即时切换 |

> 打印预览为导出 HTML 到系统浏览器、新手引导为 CodeWF Guide 控件自带 UI，均无自绘界面，不设原型。

## 主题色切换

右下角**主题悬浮球**（或 `Alt + 1..6`、URL 参数 `?theme=dark` 等）切换与 App 一致的六套主题色（定义见 `src/Vex.Controls.Themes/Themes/Shared/AppPalette.axaml`）：

| 主题 | 说明 | 强调色 |
|------|------|--------|
| 浅色 light | 极地蓝，默认 | `#1677FF` |
| 深色 dark | 石墨 | `#60A5FA` |
| 水生 aquatic | 湖心青 | `#0EA5B7` |
| 沙漠 desert | 暖阳橙 | `#C77B2A` |
| 黄昏 dusk | 薄暮紫 | `#8A63D2` |
| 夜空 night-sky | 深海蓝（深色） | `#7AA2FF` |

选择写入 `localStorage`，刷新 / 重开浏览器后保持。

## 结构与约定

```
design/
├── index.html          主窗体原型（唯一页面：标题栏 + 菜单 + 侧栏 + 编辑器 + 预览 + 状态栏
│                       + 全部浮层：对话框 / 独立窗口 / 查找替换栏）
├── assets/
│   ├── theme.css       六套主题令牌（变量）+ 全部组件样式 + 交互层样式
│   ├── theme.js        主题切换器（悬浮球 / localStorage / Alt 快捷键 / ?theme= 参数）
│   └── interact.js     菜单数据（与 ShellTitleMenuView.axaml 同构）+ 菜单呼出分发 + toast
└── README.md
```

- **交互与实现一致**：菜单结构逐项对照 `ShellTitleMenuView.axaml` + `zh-CN.json`；段落 / 格式等插入类动作用 toast 反馈（对应 App 状态栏提示），有独立界面的菜单项在窗体内呼出对应浮层。
- **设计升级点**：主窗体各区域的改版要点体现在与实现的对照评审中（标题栏品牌 Logo + 未保存圆点、编辑器语法着色与行号、侧栏卡片化与过滤、状态栏徽章化等）。
- **窗口内边距节律**：滚动容器的 Padding 加在滚动容器外层（如文档窗口正文），保证 Right 的滚动条、Bottom 与 Left 留白一致。
- 图标为内联 SVG sprite（feather 风格描边），随主题 `currentColor` 着色。

## 实现规格

尺寸以当前 `assets/theme.css` 为准，不沿用旧计划或 Demo 的规格。

| 区域 | 规格与维护要点 |
| --- | --- |
| 标题栏 | 高 40px；底线在 y=39 绘制 1px，内容从 y=40 开始；品牌左留白 12px、gap 8px、logo 20px |
| 菜单/文档名 | 菜单字号 12.5px、padding 3px 9px、gap 2px；文档区左 margin 10px、圆点/名称 gap 6px，圆点反映真实修改状态 |
| 侧栏 | 默认 252px、可拖动；页签带图标、紧凑内容宽度、2px 选中下线；显隐、菜单勾选和保存宽度保持同步 |
| 文件树 | 单行节点、padding 5px 10px、15px 图标、7px 图文间距、每层缩进 16px；标题/时间 12.5/11px；摘要只在提示中显示；选中行用 accent/accent-soft 与左竖条 |
| 面板头/状态栏 | 高 30/32px，各有分隔线；面板标题使用独立短名「源码 / 预览」，菜单仍使用完整模式名 |
| 源码 | 字号 12.5px、行高 21.875px；颜色使用 Vex 编辑器语义令牌，URL 跟随 LinkKey |
| 默认预览 | margin 30px 22px 30px 30px（左/上/右/下）；H1 22px、1px 普通下边框、bottom padding 8px |
| H2/H3 | 17.5/15px、上下 margin 20/10 与 16/8px；H2 无下边框 |
| 引用/表格 | 引用左线 3px accent、6% accent 底、padding 6px 14px；表头使用 code-bg |

壳层维护在 `src/Vex/Modules/Shell/Views/` 和 `src/Vex.Controls.Themes/`；源码宿主在 `src/Vex/Modules/Workspace/Views/MarkdownEditorView.axaml`，复用 CodeWF.Markdown 的编辑器。源码行高由库的 `EditorLineHeight` 指定，根据实测字体高度换算 AvaloniaEdit 的 `LineHeightFactor`，保持字号不变。

## 回归验证

[scripts/ui-verification/README.md](../scripts/ui-verification/README.md) 提供可重复的离屏构建、截图与输入验证。它加载实际 Prism Shell/XAML；设置、草稿、最近记录使用内存服务，MCP 关闭，不操作真实键鼠或修改草稿。构建输出、截图和日志可重建，属于临时产物。

- Light/Dark/Aquatic/Desert/Dusk/NightSky 六主题和 980×640 窄窗，检查标题线实际像素、紧凑页签图标、文件卡片画刷、H1 和引用边框。
- 普通文件标题对比度至少 4.5:1；选中卡片跟随原型 accent/accent-soft，不声称所有选中色均达到 4.5:1。
- 五份真实样例连续切换不误报未保存；真实编辑后切换出现确认，取消保留修改；恢复草稿与磁盘不同仍标为未保存，磁盘是保存基线。
- 文件树验证根层/同级/多层目录的紧凑行高、16px 缩进、图文及展开箭头对齐、长文件名省略，以及真实点击展开/折叠与筛选。
- 源码行高、点击定位和换行后的键盘移动；侧栏显隐与菜单勾选、内存持久化字段同步。

Vex 正式版从 nuget.org 恢复四包。改库产品代码后的发布前联调需要提升开发版本、打包四包到本地源，再同步 Vex 版本；覆盖同版本 nupkg 不会自动刷新 NuGet 缓存。正式发布流程见 [docs/RELEASE.md](../docs/RELEASE.md)。

## 验证边界

本环境未获得浏览器原型截图，采用 CSS/结构与实际截图核对，不宣称全页逐像素一致。离屏不能模拟 Windows 非客户区；真实窗控、拖动/最大化、多 DPI、中文 IME 仍需实机检查，滚动条与字体度量可能与网页不同。

实时控件的复杂嵌套引用、组内编辑规范化和导出验证边界见 CodeWF.Markdown 的 `design/README.md`。本地开发包联调不等于正式 NuGet、标签或安装包发布。
