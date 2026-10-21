# Mermaid 图表图鉴

## 流程图

```mermaid
flowchart LR
  A[记录想法] --> B[梳理结构]
  B --> C{再次阅读}
  C -->|调整| B
  C -->|完成| D[导出分享]
```


## 时序图

```mermaid
sequenceDiagram
  participant W as 写作者
  participant E as 编辑器
  participant P as 预览
  W->>E: 输入 Markdown
  E->>P: 同步内容
  P-->>W: 显示结果
```


## 状态图

```mermaid
stateDiagram-v2
  [*] --> Draft
  Draft --> Review: 提交
  Review --> Draft: 修改
  Review --> Published: 发布
  Published --> [*]
```


## 用户旅程图

```mermaid
journey
  title 一次写作的旅程
  section 构思
    记录灵感: 5: 写作者
    整理提纲: 4: 写作者
  section 完成
    撰写正文: 4: 写作者
    校对分享: 5: 写作者, 读者
```


## 甘特图

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


## 时间线

```mermaid
timeline
  title 文章的成长
  2026-10-17 : 收集灵感
  2026-10-19 : 完成提纲
  2026-10-22 : 写成初稿
  2026-10-24 : 校对发布
```


## Git 分支图

```mermaid
gitGraph
  commit id: "初稿"
  branch revise
  checkout revise
  commit id: "补充论据"
  checkout main
  merge revise
  commit id: "发布"
```


## 看板

```mermaid
kanban
  todo[待办]
    a[收集资料]
  doing[进行中]
    b[撰写初稿]
  done[已完成]
    c[整理提纲]
```


## 类图

```mermaid
classDiagram
  class Document {
    +String title
    +String content
    +save()
  }
  class Workspace {
    +openDocument()
  }
  Workspace "1" o-- "*" Document : 管理
```


## 实体关系图

```mermaid
erDiagram
  WORKSPACE ||--o{ DOCUMENT : contains
  DOCUMENT ||--o{ SECTION : includes
  DOCUMENT {
    string title
    string content
  }
  SECTION {
    string heading
  }
```


## 需求图

```mermaid
requirementDiagram
  requirement local_files {
    id: 1
    text: Local Markdown files
    risk: low
    verifymethod: test
  }
  element editor {
    type: application
  }
  editor - satisfies -> local_files
```


## 思维导图

```mermaid
mindmap
  root((写作))
    构思
      观察
      灵感
    整理
      提纲
      论据
    表达
      初稿
      校对
```


## 块图

```mermaid
block-beta
  columns 3
  a["编辑器"] space b["预览"]
  a --> b
  c["本地 Markdown 文件"]:3
```


## C4 架构图

```mermaid
C4Context
  title 本地写作空间
  Person(writer, "写作者", "记录与整理想法")
  System(vex, "维刻 Vex", "Markdown 工作区")
  System_Ext(files, "本地文件", "Markdown 文档")
  Rel(writer, vex, "写作")
  Rel(vex, files, "读写")
```


## 架构图

```mermaid
architecture-beta
  group app(cloud)[Workspace]
  service editor(server)[Editor] in app
  service files(disk)[Files] in app
  editor:R -- L:files
```


## 数据包图

```mermaid
packet-beta
  title 文档消息头
  0-7: "Version"
  8-15: "Type"
  16-31: "Length"
  32-63: "Document ID"
```


## 饼图

```mermaid
pie showData
  title 写作时间分配
  "构思" : 25
  "写作" : 50
  "校对" : 25
```


## 象限图

```mermaid
quadrantChart
  title 选题优先级
  x-axis Low effort --> High effort
  y-axis Low value --> High value
  quadrant-1 Plan
  quadrant-2 Do first
  quadrant-3 Later
  quadrant-4 Reconsider
  Note: [0.25, 0.8]
  Essay: [0.7, 0.75]
```


## XY 坐标图

```mermaid
xychart-beta
  title "每周写作量"
  x-axis "星期" ["周一", "周二", "周三", "周四", "周五"]
  y-axis "字数" 0 --> 1000
  bar [300, 500, 450, 800, 600]
  line [300, 500, 450, 800, 600]
```


## 桑基图

```mermaid
sankey-beta

Ideas,Draft,40
Ideas,Notes,20
Draft,Published,30
Draft,Revision,10
```


## 雷达图

```mermaid
radar-beta
  title "写作自评"
  axis clarity["清晰"], structure["结构"], detail["细节"], rhythm["节奏"]
  curve draft["初稿"]{3, 4, 3, 2}
  curve revised["修订"]{5, 4, 4, 4}
  max 5
```


## 矩形树图

```mermaid
treemap-beta
  "文章"
    "构思"
      "灵感": 20
      "提纲": 15
    "正文"
      "论述": 45
      "案例": 20
```


