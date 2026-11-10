# Mermaid 图表图鉴

![从 C# 文档模型到 Avalonia 界面](图片/dotnet-avalonia-pipeline.svg)

用图表说明 C# 对象、.NET 服务与 Avalonia 桌面交互，比把每个细节塞进一段话更容易阅读。本页收录当前渲染器支持的 24 类图表，每种保留一个可直接编辑的短示例。

源码与文档：<https://mermaid.js.org>

在源码视图查看原始 Mermaid，在即时编辑中修改代码，再切换阅读检查结果。示例中的日期、时间与数值都是教学数据，不代表产品性能。

## 流程图

```mermaid
flowchart LR
  A[ASP.NET Core 请求] --> B[验证 DTO]
  B --> C{参数有效}
  C -->|否| D[返回 400]
  C -->|是| E[调用 C# 服务]
  E --> F[返回 JSON]
```


## 时序图

```mermaid
sequenceDiagram
  participant V as Avalonia View
  participant VM as ViewModel
  participant S as DocumentStore
  V->>VM: SaveCommand
  VM->>S: WriteAsync(path, text)
  S-->>VM: Task 完成
  VM-->>V: IsModified = false
```


## 状态图

```mermaid
stateDiagram-v2
  [*] --> Idle
  Idle --> Loading: 执行 OpenCommand
  Loading --> Ready: 读取成功
  Loading --> Error: IOException
  Error --> Loading: 重试
  Ready --> Idle: 关闭文档
```


## 用户旅程图

```mermaid
journey
  title 第一次运行 Avalonia 项目
  section 准备
    安装 .NET SDK: 4: 开发者
    创建桌面项目: 5: 开发者
  section 运行
    绑定 ViewModel: 4: 开发者
    验证窗口交互: 5: 开发者, 测试者
```


## 甘特图

```mermaid
gantt
  title Avalonia 功能迭代示例
  dateFormat YYYY-MM-DD
  axisFormat %m/%d
  tickInterval 1day
  section 实现
    定义 C# 模型 :done, a, 2026-10-17, 2d
    编写 AXAML :active, b, after a, 3d
  section 发布
    回归测试 :c, after b, 2d
    AOT 发布 :milestone, after c, 0d
```


## 时间线

```mermaid
timeline
  title .NET 主线演进
  2002 : .NET Framework
  2016 : .NET Core
  2020 : .NET 5
  2025 : .NET 10
```


## Git 分支图

```mermaid
gitGraph
  commit id: "建立模型"
  branch feature
  checkout feature
  commit id: "实现命令"
  checkout main
  merge feature
  commit id: "验证 AOT"
```


## 看板

```mermaid
kanban
  todo[待办]
    a[增加取消令牌]
  doing[进行中]
    b[实现保存命令]
  done[已完成]
    c[建立文档模型]
```


## 类图

```mermaid
classDiagram
  class Document {
    +string Path
    +string Markdown
  }
  class DocumentViewModel {
    +bool IsModified
    +SaveAsync() Task
  }
  DocumentViewModel "1" --> "1" Document : 编辑
```


## 实体关系图

```mermaid
erDiagram
  PROJECT ||--o{ DOCUMENT : contains
  DOCUMENT ||--o{ REVISION : tracks
  DOCUMENT {
    int Id PK
    string Path
  }
  REVISION {
    int Id PK
    int DocumentId FK
    string Markdown
  }
```


## 需求图

```mermaid
requirementDiagram
  requirement aot_release {
    id: 1
    text: Publish Windows x64 Native AOT
    risk: low
    verifymethod: test
  }
  element desktop_app {
    type: application
  }
  desktop_app - satisfies -> aot_release
```


## 思维导图

```mermaid
mindmap
  root((Avalonia 桌面应用))
    C#
      模型
      异步命令
    AXAML
      控件
      绑定
    .NET
      类库
      Native AOT
```


## 块图

```mermaid
block-beta
  columns 3
  a["AXAML View"] space b["C# ViewModel"]
  a --> b
  c[".NET 文档服务"]:3
```


## C4 架构图

```mermaid
C4Context
  title Avalonia 文档应用
  Person(developer, "开发者", "编写技术文档")
  System(desktop, "桌面应用", "C# 与 Avalonia")
  System_Ext(files, "本地文件系统", "Markdown 与图片")
  Rel(developer, desktop, "编辑与阅读")
  Rel(desktop, files, "异步读写")
```


## 架构图

```mermaid
architecture-beta
  group app(cloud)[DotNet]
  service api(server)[ASP NET Core] in app
  service database(database)[Database] in app
  api:R -- L:database
```


## 数据包图

```mermaid
packet-beta
  title .NET 二进制协议示例
  0-7: "Version"
  8-15: "Type"
  16-31: "Length"
  32-63: "Request ID"
```


## 饼图

```mermaid
pie showData
  title 桌面项目代码构成示例
  "C#" : 55
  "AXAML" : 30
  "测试" : 15
```


## 象限图

```mermaid
quadrantChart
  title Avalonia 改进优先级
  x-axis Low effort --> High effort
  y-axis Low value --> High value
  quadrant-1 Plan
  quadrant-2 Do first
  quadrant-3 Later
  quadrant-4 Reconsider
  FixClipping: [0.25, 0.8]
  NewTheme: [0.7, 0.75]
```


## XY 坐标图

```mermaid
xychart-beta
  title "C# 测试执行时间示例"
  x-axis "批次" ["一", "二", "三", "四", "五"]
  y-axis "毫秒" 0 --> 1000
  bar [600, 500, 450, 400, 350]
  line [600, 500, 450, 400, 350]
```


## 桑基图

```mermaid
sankey-beta

DotNet,CSharp,60
DotNet,AXAML,40
CSharp,Application,40
CSharp,Tests,20
AXAML,Application,40
```


## 雷达图

```mermaid
radar-beta
  title "桌面交互评审示例"
  axis keyboard["键盘"], clarity["清晰"], layout["布局"], feedback["反馈"]
  curve before["修改前"]{3, 4, 3, 2}
  curve after["修改后"]{5, 4, 4, 4}
  max 5
```


## 矩形树图

```mermaid
treemap-beta
  "Avalonia 项目"
    "应用"
      "ViewModels": 25
      "Views": 20
    "基础设施"
      "Services": 30
      "Tests": 25
```

## 韦恩图

用交集说明技术职责：C# 承载逻辑，AXAML 描述视图，.NET 提供运行与类库能力。

```mermaid
venn-beta
  set A["C#"]
  set B["AXAML"]
  set C[".NET"]
  union A, B["Avalonia 应用"]
  union A, C["服务与模型"]
```

## 目录树

用一个最小桌面项目检查文件层级、文件名、注释与高亮。

```mermaid
treeView-beta
    AvaloniaNotes/
        Models/
            Document.cs
        ViewModels/
            MainWindowViewModel.cs :::highlight ## 状态与命令
        Views/
            MainWindow.axaml
        App.axaml
        Program.cs
        AvaloniaNotes.csproj
```

---

继续阅读 [.NET 历史与版本关系](.NET%20历史与版本关系.md)，或回到 [技术文档导航](主题设计笔记.md)。


