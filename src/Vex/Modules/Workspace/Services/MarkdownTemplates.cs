namespace Vex.Modules.Workspace.Services;

internal static class MarkdownTemplates
{
    public static string? Get(string key) => key switch
    {
        "flowchart" => "\n\n```mermaid\nflowchart LR\n  A[记录想法] --> B[梳理结构]\n  B --> C{再次阅读}\n  C -->|调整| B\n  C -->|完成| D[导出分享]\n```\n\n",
        "sequence" => "\n\n```mermaid\nsequenceDiagram\n  participant W as 写作者\n  participant E as 编辑器\n  participant P as 预览\n  W->>E: 输入 Markdown\n  E->>P: 同步内容\n  P-->>W: 显示结果\n```\n\n",
        "state" => "\n\n```mermaid\nstateDiagram-v2\n  [*] --> Draft\n  Draft --> Review: 提交\n  Review --> Draft: 修改\n  Review --> Published: 发布\n  Published --> [*]\n```\n\n",
        "journey" => "\n\n```mermaid\njourney\n  title 一次写作的旅程\n  section 构思\n    记录灵感: 5: 写作者\n    整理提纲: 4: 写作者\n  section 完成\n    撰写正文: 4: 写作者\n    校对分享: 5: 写作者, 读者\n```\n\n",
        "gantt" => "\n\n```mermaid\ngantt\n  title 写作计划\n  dateFormat YYYY-MM-DD\n  section 初稿\n    构思提纲 :done, a, 2026-10-17, 2d\n    撰写正文 :active, b, after a, 3d\n  section 发布\n    校对 :c, after b, 2d\n    发布 :milestone, after c, 0d\n```\n\n",
        "timeline" => "\n\n```mermaid\ntimeline\n  title 文章的成长\n  2026-10-17 : 收集灵感\n  2026-10-19 : 完成提纲\n  2026-10-22 : 写成初稿\n  2026-10-24 : 校对发布\n```\n\n",
        "git" => "\n\n```mermaid\ngitGraph\n  commit id: \"初稿\"\n  branch revise\n  checkout revise\n  commit id: \"补充论据\"\n  checkout main\n  merge revise\n  commit id: \"发布\"\n```\n\n",
        "kanban" => "\n\n```mermaid\nkanban\n  todo[待办]\n    a[收集资料]\n  doing[进行中]\n    b[撰写初稿]\n  done[已完成]\n    c[整理提纲]\n```\n\n",
        "class" => "\n\n```mermaid\nclassDiagram\n  class Document {\n    +String title\n    +String content\n    +save()\n  }\n  class Workspace {\n    +openDocument()\n  }\n  Workspace \"1\" o-- \"*\" Document : 管理\n```\n\n",
        "er" => "\n\n```mermaid\nerDiagram\n  WORKSPACE ||--o{ DOCUMENT : contains\n  DOCUMENT ||--o{ SECTION : includes\n  DOCUMENT {\n    string title\n    string content\n  }\n  SECTION {\n    string heading\n  }\n```\n\n",
        "requirement" => "\n\n```mermaid\nrequirementDiagram\n  requirement local_files {\n    id: 1\n    text: Local Markdown files\n    risk: low\n    verifymethod: test\n  }\n  element editor {\n    type: application\n  }\n  editor - satisfies -> local_files\n```\n\n",
        "mindmap" => "\n\n```mermaid\nmindmap\n  root((写作))\n    构思\n      观察\n      灵感\n    整理\n      提纲\n      论据\n    表达\n      初稿\n      校对\n```\n\n",
        "block" => "\n\n```mermaid\nblock-beta\n  columns 3\n  a[\"编辑器\"] space b[\"预览\"]\n  a --> b\n  c[\"本地 Markdown 文件\"]:3\n```\n\n",
        "c4" => "\n\n```mermaid\nC4Context\n  title 本地写作空间\n  Person(writer, \"写作者\", \"记录与整理想法\")\n  System(vex, \"维刻 Vex\", \"Markdown 工作区\")\n  System_Ext(files, \"本地文件\", \"Markdown 文档\")\n  Rel(writer, vex, \"写作\")\n  Rel(vex, files, \"读写\")\n```\n\n",
        "architecture" => "\n\n```mermaid\narchitecture-beta\n  group app(cloud)[Workspace]\n  service editor(server)[Editor] in app\n  service files(disk)[Files] in app\n  editor:R -- L:files\n```\n\n",
        "packet" => "\n\n```mermaid\npacket-beta\n  title 文档消息头\n  0-7: \"Version\"\n  8-15: \"Type\"\n  16-31: \"Length\"\n  32-63: \"Document ID\"\n```\n\n",
        "pie" => "\n\n```mermaid\npie showData\n  title 写作时间分配\n  \"构思\" : 25\n  \"写作\" : 50\n  \"校对\" : 25\n```\n\n",
        "quadrant" => "\n\n```mermaid\nquadrantChart\n  title 选题优先级\n  x-axis Low effort --> High effort\n  y-axis Low value --> High value\n  quadrant-1 Plan\n  quadrant-2 Do first\n  quadrant-3 Later\n  quadrant-4 Reconsider\n  Note: [0.25, 0.8]\n  Essay: [0.7, 0.75]\n```\n\n",
        "xy" => "\n\n```mermaid\nxychart-beta\n  title \"每周写作量\"\n  x-axis [Mon, Tue, Wed, Thu, Fri]\n  y-axis \"Words\" 0 --> 1000\n  bar [300, 500, 450, 800, 600]\n  line [300, 500, 450, 800, 600]\n```\n\n",
        "sankey" => "\n\n```mermaid\nsankey-beta\n\nIdeas,Draft,40\nIdeas,Notes,20\nDraft,Published,30\nDraft,Revision,10\n```\n\n",
        "radar" => "\n\n```mermaid\nradar-beta\n  title \"写作自评\"\n  axis clarity[\"清晰\"], structure[\"结构\"], detail[\"细节\"], rhythm[\"节奏\"]\n  curve draft[\"初稿\"]{3, 4, 3, 2}\n  curve revised[\"修订\"]{5, 4, 4, 4}\n  max 5\n```\n\n",
        "treemap" => "\n\n```mermaid\ntreemap-beta\n  \"文章\"\n    \"构思\"\n      \"灵感\": 20\n      \"提纲\": 15\n    \"正文\"\n      \"论述\": 45\n      \"案例\": 20\n```\n\n",
        "footnote" => "[^note]\n\n[^note]: ",
        _ => null
    };
}
