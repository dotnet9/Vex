# Lucide 菜单图标来源

本目录保留菜单图标使用的 Lucide SVG 源文件。运行时使用 `Vex.Controls.MenuIconCatalog` 和 `MenuIconGeometry.g.cs` 中的原生 Avalonia 几何数据，不联网下载、不运行时栅格化。

2026-10-24 补齐的 43 个 SVG 固定为 Lucide **0.468.0**，提交 `f12b0de177fbc2a6795e99be065887e72b237123`，上游路径为 `icons/<文件名>.svg`，原样保留。文件树底部折叠按钮另增同版本的 `chevrons-up.svg`；已有 `folder`、`list` 直接复用，不改原几何。此前已使用的代表性 SVG 和手写几何仍保留，不能将其表述为已全部自动生成。

在仓库根目录运行 `node scripts/generate-menu-icon-geometry.cjs`，将脚本清单中的 SVG 生成到 `src/Vex.Controls/MenuIconGeometry.g.cs`；`--check` 检查产物是否过期。转换覆盖路径、线段、折线、圆和矩形，遇到不支持的元素或填充直接报错。普通构建无需 Node.js。

文件显示模式按钮用 `list-tree` 的层级连线表示当前树状模式，用 `grid-2x2` 的四宫格表示当前平铺模式；提示文字始终说明下一次点击将切换到的模式。两个图标均复用上述固定版本的 SVG 和生成几何。

大纲面板顶部收起按钮使用同版本的 `panel-right-close`，以右侧面板分隔线和向右箭头表示隐藏右侧大纲；SVG 来自上述固定提交的 `icons/panel-right-close.svg`，原样保留，显示容器为 14px。

图标分配优先使用 `MenuIconCatalog.Key`，其次使用完整命令参数；不再匹配翻译文字，不再把未匹配项当作文件。只有文件入口、打开文件和最近文档等显式声明 `file` 的菜单使用文件图标。未匹配项临时显示 `help`，自动验收将其报告为遗漏；状态型菜单不分配操作图标。

图标来源：Lucide，ISC License，包含继承自 Feather 的版权声明，见 `LICENSE.txt`。
