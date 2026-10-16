# Vex 工作区原型

主应用：[Vex.html](Vex.html)。安装器：[Windows](Vex-Installer-Windows.html)、[Linux](Vex-Installer-Linux.html)、[macOS](Vex-Installer-macOS.html)。各文件均自包含，可离线打开；每个安装原型只显示对应平台的安装窗口与交互，详见[安装器原型说明](安装器原型说明.md)。

本轮原型以单文档、多视图写作和导图章节编排为目标。界面采用固定视图组合，没有文档标签栏、Dock 停靠、面板拖动、关闭或工具窗口。

## 预览

直接打开 `Vex.html`，默认使用即时编辑，并记住工作区布局。也可以从仓库根目录启动本地服务：

```powershell
python -m http.server 8791 --bind 127.0.0.1
```

- 三视图：[源码 + 渲染 + 导图](http://127.0.0.1:8791/design/Vex.html?layout=three&palette=aurora&theme=light)。
- 结构写作：[源码 + 导图](http://127.0.0.1:8791/design/Vex.html?layout=source-map&palette=aurora&theme=light)。
- 正文写作：[即时编辑 + 导图](http://127.0.0.1:8791/design/Vex.html?layout=instant-map&palette=aurora&theme=light)。

`layout` 支持 `instant`、`source`、`read`、`map`、`source-render`、`source-map`、`instant-map`、`three`。兼容旧的 `view=edit|read|mindmap`、`edit=instant|source`；另支持 `typography`、`palette`、`theme=light|dark|system`。

## 工作区约定

一个 Vex 进程同时只打开一份文档。左侧“文件 / 大纲”是固定导航页签，不是打开文档的标签栏；在“文件”列表中选择另一份文件，会替换当前文档，并同时刷新源码、即时编辑、阅读、导图及独立窗口。未保存修改先处理保存或放弃，原布局保持。

“布局”提供八种固定组合：四种单视图，源码 + 渲染、源码 + 导图、即时编辑 + 导图，以及上方源码 / 渲染、下方导图的三块布局。直接点击切换，不需要拖动或停靠。顶部“编辑 / 阅读 / 导图”保留单视图快捷入口。编辑区内部切换源码 / 即时编辑，二者在整个工作区互斥。

布局面板与排版面板采用相同的试选方式：点击选项立即应用并保持面板展开，方便连续比较；点击外部、关闭按钮、再次点击入口或按 Esc 收起。

“关于”展示英文名 Vex、中文名维刻及 Slogan“维刻 Vex · 写下想法，看见脉络。”。主界面标题栏保持 Vex 简称，悬停品牌和浏览器标题也体现完整品牌。

## 即时编辑

正文直接使用排版后的节点承接光标。点击标题、段落、列表或引用，不创建 textarea、不增加最小高度或内边距、不切换字体，也不显示输入框边框。点击前后保留同一 DOM 节点及其位置、尺寸和滚动位置；只聚焦不会改动 Markdown。

输入直接回写同一份 Markdown，其他视图同步更新。保留已有加粗、斜体、行内代码和标题样式；支持 Ctrl+B / Ctrl+I，或格式工具栏修改选中文字。输入闭合的加粗、斜体和行内代码标记时就地排版。标题中 Enter 接正文段落，Ctrl+Enter 完成当前编辑，Esc 可撤销当前段落修改。

这是浏览器交互原型，富文本与 Markdown 的转换基于简化解析器。它验证原位编辑体验，不代表完整 Markdown 编辑内核、跨块选择、复杂嵌套语法和实际中文输入法已经完成生产级验证。

## 导图内编辑章节

章节是 Markdown 标题及其正文、子章节的整体。源码、渲染、即时编辑、大纲和导图共同更新。

- 单击标题节点打开章节详情，直接修改标题和本节正文。正文支持 Markdown，离开输入框即同步；子章节分别保留。
- 双击标题或按 F2，就地修改标题。Enter 确认，Esc 放弃本次标题输入；中文组合输入期间不提交。
- 选中标题后，Enter 新增同级、Tab 新增子章节；也可使用画布工具栏和详情按钮。
- 详情提供提升、降级、上移、下移。移动携带整节正文与子章节，自动调整相应标题层级；禁止循环包含和超过 H6 的操作。
- 拖动章节到目标节点上缘 / 下缘调整顺序，拖到中部成为子章节。画布空白处仍可拖动平移。
- 删除前显示影响范围，可以选择只移除标题、保留内容。删除、移动、标题及正文修改均可撤销 / 重做，撤销保持当前布局。
- “仅标题”适合先确定大纲；再次点击恢复内容摘要。原有摘要长度、全文查看、折叠、平移、缩放和定位源码继续保留。

内容修改自动同步；各视图保留自己的滚动、缩放和折叠状态。章节操作不反复重置整个画布。原型使用简化 Markdown 解析器，正式接入需要完整 AST 与可靠源码区间映射。

## 独立窗口与多屏

组合布局中点击视图标题右侧的独立窗口按钮，或从“布局”选择“分到独立窗口”，会打开真实浏览器窗口，可移动到其他屏幕。后者保留主窗口的编辑面板，把阅读、导图拆出。主窗口显示已拆出视图及“移回”入口；关闭副窗口也会把视图移回。

副窗口始终跟随当前文件。源码或即时编辑拆出后，主窗口不再重复显示编辑面板。导图副窗口可以修改章节，内容回到主窗口并同步其他副窗口。保存和撤销由主窗口统一处理。文件选择始终留在主窗口，副窗口没有自己的文档标签或文件选择器。

浏览器可能要求允许弹出窗口。重开原型时视图先恢复在主窗口，避免自动弹窗；原生桌面版本的屏幕识别、跨 DPI 和完整多窗口布局恢复需后续实现。本轮没有修改原生应用或控件库。

## 现有体验

保留八套配色、浅 / 深 / 系统三态、十八套 Markdown 排版、紧凑布局、文件读取与保存副本、HTML 导出、查找替换、菜单、关于与更新状态演示、六步临时文档引导（写作、文件与大纲、章节、布局、排版、保存与分享）。引导结束后恢复原文、撤销历史、工作区布局、文件筛选和偏好。

示例与编辑内容仅保留在内存，刷新后重置。布局、主题与显示偏好在原型专用 localStorage 中保存。原型保存会下载副本，不覆盖用户原文件。关于的版本来自构建属性，品牌左对齐，版本、开发平台、作者和链接统一为两列，版权和检查更新置于页脚；更新日志与鸣谢直接展示正文。鸣谢按当前项目直接引用的重要 NuGet 包分组，以逗号分隔链接；Markdown 只展示和直接引用完整包，Lite 由完整包间接引入。检查更新使用一个按钮演示检查、下载、安装及重试状态，界面不显示演示场景选项。

## 维护与验证

维护源码在 `scripts/prototype/`，交付文件在 `design/`：

```text
scripts/prototype/index.template.html
scripts/prototype/assets/workbench.js       当前文档与既有编辑、阅读、导图
scripts/prototype/assets/workspace.js/css   固定视图组合、当前文档、副窗口
scripts/prototype/assets/chapter-editor.js  章节标题、正文、结构操作
design/Vex.html                            生成的单文件原型
design/Vex-Installer-Windows.html          Windows EXE 安装原型
design/Vex-Installer-Linux.html            Linux DEB 安装原型
design/Vex-Installer-macOS.html            macOS DMG 安装原型
design/tests/audit-installers.cjs           三平台独立安装交互验证
design/tests/audit-workspace.cjs            新工作区与多窗口交互验证
```

```powershell
pwsh scripts/build_standalone_prototype.ps1
pwsh scripts/build_standalone_prototype.ps1 -Check
npm ci --prefix design/tests
# 首次使用时：在 design/tests 内运行 npx playwright install chromium
npm test --prefix design/tests
npm run test:installers --prefix design/tests
```

可通过 `VEX_BROWSER_EXECUTABLE` 指定现有 Chromium / Edge，`CODEWF_TEMP_ROOT` 和 `CODEWF_AUDIT_TEMP` 指定截图及临时文件目录。本轮验证使用 `D:\github\temp_files`。

自动检查覆盖原有主题、菜单、编辑、引导，以及八种固定布局、原位编辑几何稳定、编辑形态互斥、章节修改与撤销、真实副窗口的双向同步、替换当前文件、无文档标签与 Dock 操作和窄窗口几何。截图和离屏检查不代表真实多屏、跨 DPI、完整 Markdown 语法或中文输入法已完成原生验收。
