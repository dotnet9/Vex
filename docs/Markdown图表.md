# Markdown 图表

Markdown 的普通语法不定义图表。Vex 原型使用本地 Mermaid 11.12.0，将标记为 `mermaid` 的围栏代码块渲染成图。插入菜单提供下列 22 种模板；左侧“Mermaid 图表图鉴.md”可集中查看、切换源码并修改。

## 选哪种图

| 适合表达什么 | 图表 | 代码起始标记 |
| --- | --- | --- |
| 步骤、条件与分支 | 流程图 | `flowchart` / `graph` |
| 参与者之间的消息 | 时序图 | `sequenceDiagram` |
| 对象状态的转换 | 状态图 | `stateDiagram-v2` |
| 使用步骤与体验评分 | 用户旅程图 | `journey` |
| 任务、工期、依赖、里程碑 | 甘特图 | `gantt` |
| 事件的先后演变 | 时间线 | `timeline` |
| 提交、分支与合并 | Git 分支图 | `gitGraph` |
| 待办与进行中的任务 | 看板 | `kanban` |
| 类型、属性与方法关系 | 类图 | `classDiagram` |
| 数据实体、字段与基数 | 实体关系图 | `erDiagram` |
| 需求与验证、满足关系 | 需求图 | `requirementDiagram` |
| 层次与发散思考 | 思维导图 | `mindmap` |
| 简单模块的排列和连接 | 块图 | `block-beta` |
| 人员、系统、容器与组件 | C4 架构图 | `C4Context` / 其他 C4 层级 |
| 服务、分组与资源连接 | 架构图 | `architecture-beta` |
| 二进制消息字段 | 数据包图 | `packet-beta` |
| 部分占整体的比例 | 饼图 | `pie` |
| 两个维度的优先级 | 象限图 | `quadrantChart` |
| 数值随类别的变化 | XY 坐标图（柱状、折线） | `xychart-beta` |
| 数量从哪里来、流向哪里 | 桑基图 | `sankey-beta` |
| 多维度能力或方案对比 | 雷达图 | `radar-beta` |
| 层级数据的面积占比 | 矩形树图 | `treemap-beta` |

最常用的入口是流程、时序、状态、类、ER、思维导图、甘特、饼图和柱状 / 折线。C4、架构图和带 `-beta` 的语法对引擎版本更敏感。

## 常用写法

````markdown
```mermaid
flowchart LR
  A[记录想法] --> B[整理结构]
  B --> C{需要修改吗}
  C -->|是| B
  C -->|否| D[发布]
```
````

甘特图建议明确日期格式、简短刻度和刻度间隔。短计划按天，长期计划按周或月；任务数量多时增加图高。

````markdown
```mermaid
gantt
  title 写作计划
  dateFormat YYYY-MM-DD
  axisFormat %m/%d
  tickInterval 1day
  section 初稿
    构思提纲 :done, a, 2026-10-17, 2d
    撰写正文 :active, b, after a, 3d
  section 发布
    校对 :c, after b, 2d
    发布 :milestone, after c, 0d
```
````

XY 坐标图中带中文、空格的文字使用引号：

````markdown
```mermaid
xychart-beta
  title "每周写作量"
  x-axis "星期" ["周一", "周二", "周三", "周四", "周五"]
  y-axis "字数" 0 --> 1000
  bar [300, 500, 450, 800, 600]
  line [300, 500, 450, 800, 600]
```
````

## 显示与兼容范围

- 阅读与即时编辑使用同一字体、主题资源和图表配置。文字测量与最终显示使用同一字体；图表不随正文排版缩成小字。宽图在自身容器内横向滚动，可聚焦后使用方向键。
- 甘特图保留足够任务和日期空间；C4 类型标签按自然字宽显示；XY 标题、刻度和轴标题留出独立间距。浅深色及配色切换不重新创建 SVG。
- 模板可渲染表示相应样例通过，不代表全部语法、子类型、复杂布局、外部图标或所有导出格式兼容。原型只使用随仓库提供的本地依赖；不自动加载外部图标包。
- Avalonia 应用使用 CodeWF.Avalonia.Markdown 内的图表能力，其渲染链路与浏览器原型不同。原型菜单是产品设计范围；应用和 PDF、PNG、HTML、Word 输出需分别验收，不能直接据此宣称 22 类完整兼容。
- 语法错误保留原文；即时编辑预览保留上一次成功图形并提示错误位置，修正后重新生成。原型图鉴的全部模板可用于后续应用兼容性验收。

参见 [Markdown 练习](Markdown功能示例.md)、[原型设计](design/原型设计.md)、[离线资源与许可](resources/原型离线依赖.md)。
