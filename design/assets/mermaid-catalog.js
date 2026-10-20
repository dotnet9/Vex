/* Templates supported by the bundled Mermaid 11.12 renderer. */
(() => {
  'use strict';
  const items = [
    {key:'flowchart',label:'流程图',group:'过程与行为',source:'flowchart LR\n  A[记录想法] --> B[梳理结构]\n  B --> C{再次阅读}\n  C -->|调整| B\n  C -->|完成| D[导出分享]'},
    {key:'sequence',label:'时序图',group:'过程与行为',source:'sequenceDiagram\n  participant W as 写作者\n  participant E as 编辑器\n  participant P as 预览\n  W->>E: 输入 Markdown\n  E->>P: 同步内容\n  P-->>W: 显示结果'},
    {key:'state',label:'状态图',group:'过程与行为',source:'stateDiagram-v2\n  [*] --> Draft\n  Draft --> Review: 提交\n  Review --> Draft: 修改\n  Review --> Published: 发布\n  Published --> [*]'},
    {key:'journey',label:'用户旅程图',group:'过程与行为',source:'journey\n  title 一次写作的旅程\n  section 构思\n    记录灵感: 5: 写作者\n    整理提纲: 4: 写作者\n  section 完成\n    撰写正文: 4: 写作者\n    校对分享: 5: 写作者, 读者'},
    {key:'gantt',label:'甘特图',group:'计划与演进',source:'gantt\n  title 写作计划\n  dateFormat YYYY-MM-DD\n  axisFormat %m/%d\n  tickInterval 1day\n  section 初稿\n    构思提纲 :done, a, 2026-10-17, 2d\n    撰写正文 :active, b, after a, 3d\n  section 发布\n    校对 :c, after b, 2d\n    发布 :milestone, after c, 0d'},
    {key:'timeline',label:'时间线',group:'计划与演进',source:'timeline\n  title 文章的成长\n  2026-10-17 : 收集灵感\n  2026-10-19 : 完成提纲\n  2026-10-22 : 写成初稿\n  2026-10-24 : 校对发布'},
    {key:'git',label:'Git 分支图',group:'计划与演进',source:'gitGraph\n  commit id: "初稿"\n  branch revise\n  checkout revise\n  commit id: "补充论据"\n  checkout main\n  merge revise\n  commit id: "发布"'},
    {key:'kanban',label:'看板',group:'计划与演进',source:'kanban\n  todo[待办]\n    a[收集资料]\n  doing[进行中]\n    b[撰写初稿]\n  done[已完成]\n    c[整理提纲]'},
    {key:'class',label:'类图',group:'结构与关系',source:'classDiagram\n  class Document {\n    +String title\n    +String content\n    +save()\n  }\n  class Workspace {\n    +openDocument()\n  }\n  Workspace "1" o-- "*" Document : 管理'},
    {key:'er',label:'实体关系图',group:'结构与关系',source:'erDiagram\n  WORKSPACE ||--o{ DOCUMENT : contains\n  DOCUMENT ||--o{ SECTION : includes\n  DOCUMENT {\n    string title\n    string content\n  }\n  SECTION {\n    string heading\n  }'},
    {key:'requirement',label:'需求图',group:'结构与关系',source:'requirementDiagram\n  requirement local_files {\n    id: 1\n    text: Local Markdown files\n    risk: low\n    verifymethod: test\n  }\n  element editor {\n    type: application\n  }\n  editor - satisfies -> local_files'},
    {key:'mindmap',label:'思维导图',group:'结构与关系',source:'mindmap\n  root((写作))\n    构思\n      观察\n      灵感\n    整理\n      提纲\n      论据\n    表达\n      初稿\n      校对'},
    {key:'block',label:'块图',group:'结构与关系',source:'block-beta\n  columns 3\n  a["编辑器"] space b["预览"]\n  a --> b\n  c["本地 Markdown 文件"]:3'},
    {key:'c4',label:'C4 架构图',group:'结构与关系',source:'C4Context\n  title 本地写作空间\n  Person(writer, "写作者", "记录与整理想法")\n  System(vex, "维刻 Vex", "Markdown 工作区")\n  System_Ext(files, "本地文件", "Markdown 文档")\n  Rel(writer, vex, "写作")\n  Rel(vex, files, "读写")'},
    {key:'architecture',label:'架构图',group:'结构与关系',source:'architecture-beta\n  group app(cloud)[Workspace]\n  service editor(server)[Editor] in app\n  service files(disk)[Files] in app\n  editor:R -- L:files'},
    {key:'packet',label:'数据包图',group:'结构与关系',source:'packet-beta\n  title 文档消息头\n  0-7: "Version"\n  8-15: "Type"\n  16-31: "Length"\n  32-63: "Document ID"'},
    {key:'pie',label:'饼图',group:'数据与比较',source:'pie showData\n  title 写作时间分配\n  "构思" : 25\n  "写作" : 50\n  "校对" : 25'},
    {key:'quadrant',label:'象限图',group:'数据与比较',source:'quadrantChart\n  title 选题优先级\n  x-axis Low effort --> High effort\n  y-axis Low value --> High value\n  quadrant-1 Plan\n  quadrant-2 Do first\n  quadrant-3 Later\n  quadrant-4 Reconsider\n  Note: [0.25, 0.8]\n  Essay: [0.7, 0.75]'},
    {key:'xy',label:'XY 坐标图',group:'数据与比较',source:'xychart-beta\n  title "每周写作量"\n  x-axis "星期" ["周一", "周二", "周三", "周四", "周五"]\n  y-axis "字数" 0 --> 1000\n  bar [300, 500, 450, 800, 600]\n  line [300, 500, 450, 800, 600]'},
    {key:'sankey',label:'桑基图',group:'数据与比较',source:'sankey-beta\n\nIdeas,Draft,40\nIdeas,Notes,20\nDraft,Published,30\nDraft,Revision,10'},
    {key:'radar',label:'雷达图',group:'数据与比较',source:'radar-beta\n  title "写作自评"\n  axis clarity["清晰"], structure["结构"], detail["细节"], rhythm["节奏"]\n  curve draft["初稿"]{3, 4, 3, 2}\n  curve revised["修订"]{5, 4, 4, 4}\n  max 5'},
    {key:'treemap',label:'矩形树图',group:'数据与比较',source:'treemap-beta\n  "文章"\n    "构思"\n      "灵感": 20\n      "提纲": 15\n    "正文"\n      "论述": 45\n      "案例": 20'}
  ];
  const snippet = key => '\n\n```mermaid\n' + (items.find(item => item.key === key) || items[0]).source + '\n```\n\n';
  const label = source => {
    const header = source.split('\n').map(line => line.trim()).find(line => line && !line.startsWith('%%')) || '';
    const item = items.find(item => header.startsWith(item.source.split(/\s/)[0])) || (/^(graph)\b/.test(header) ? items[0] : null) || (/^C4/.test(header) ? items.find(item => item.key === 'c4') : null);
    return item?.label || 'Mermaid 图表';
  };
  window.VexMermaid = {items,snippet,label};
})();
