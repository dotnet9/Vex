# CodeWF.Avalonia.MindView

脑图控件、Markdown 结构图、导入导出和配套主题合并为一个 NuGet 包。依赖 Avalonia、Markdig 和 CodeWF.Avalonia.Lang，宿主可独立选择 Semi / Ursa / Fluent 等主题。版本随 CodeWF.Avalonia 仓库统一版本。

在应用样式中加载 `<mind:MindViewThemes />`，XAML 命名空间为 `xmlns:mind="https://codewf.com"`。

## 编辑型脑图

```xml
<mind:MindMapEditor Roots="{Binding Roots}"
                    SelectedNode="{Binding SelectedNode, Mode=TwoWay}"
                    AccentColor="{Binding AccentColor}" />
```

`Roots` 是 `ObservableCollection<MindMapNode>`。控件内置结构编辑、标题备注编辑、拖拽、平移缩放和自动布局。宿主需要撤销历史、文件流程和业务规则时实现 `IMindMapEditorController`。`MindMapMiniMap` 用于画布概览。`MindMapDocumentCodec` 与 `MindMapFileFormatRegistry` 提供 Markdown、OPML、XMind 等格式能力。

浅深色跟随 `ActualThemeVariant`，支持继承 Light / Dark 的自定义主题变体。`AccentColor` 控制中心主题、选中边框和连接线；分支可保留各自的节点颜色。

## 只读 Markdown 导图

```csharp
var root = MarkdownMindMapDocument.Parse(markdown, "文档名", new MarkdownMindMapOptions
{
    SummaryLength = 30, ShowParagraphs = true, ShowLists = true,
    ShowCode = false, ShowTables = false
});
view.Root = root;
view.SourceRequested += (_, node) => NavigateToSource(node.SourceStart, node.SourceEnd);
```

`MarkdownMindMapView` 支持节点选择、全文提示、双击 / Enter 源码定位、分支折叠、拖动画布、滚轮平移、Ctrl+滚轮缩放与适应画布。查看和折叠不会修改 Markdown。`CaptureViewport` / `RestoreViewport` 可保存缩放、平移、选择和折叠状态。

单个开头 H1 作为中心主题；多 H1 或标题前有正文时使用文件名。使用 Markdig AST 识别标题、列表和正文，忽略 YAML 元数据，代码围栏内的标题不进入主干。段落摘要默认 30 个可见字素，可配置 1–120；emoji 和组合字符保持完整，省略号不占额度。`FullText` 保存完整内容，源码范围为起点包含、终点不包含。

宿主应合并连续输入，在后台构造 AST 后回到 UI 线程更新 `Root`。短文档可直接解析。深层或大型文档仍需宿主提供检索、分页等产品策略，控件当前会构造全部可见节点。

内嵌简体中文、繁体中文、英语和日语资源，使用共享 Lang 引擎的文化切换及宿主同键覆盖。

## 宿主配色与大纲联动

`MarkdownMindMapView` 的 `CanvasBrush`、`CardBrush`、`TextBrush`、`LineBrush`、`DotBrush` 可以绑定宿主语义资源；`AccentColor`、`SuccessColor`、`WarningColor` 负责分支色。未设置时仍可独立跟随浅深色。标题显示 H1–H6 层级，内容摘要使用虚线边框，折叠和选择不改写文档。

`RevealNode(id)` 展开祖先、选中并居中节点；`ClearSelection()` 清除选择；`VisibleNodeCount` / `VisibleNodesChanged` 供工具栏显示实时可见节点数。自动适应状态会随窗口尺寸重新适配，手动缩放、平移或定位后则保留画布中心对应的内容。节点 ID 随内容生成，前方插入段落不会让后续章节丢失折叠状态；同文重复内容按出现次序区分。

配套 Markdown 即时编辑器新增 `DocumentMaxWidth`、`DocumentPadding`、`DocumentBlockSpacing`，宿主可设置阅读行长和留白。`NavigateToSourceLine(line, beginEdit: false)` 只滚动到对应块，`beginEdit: true` 进入编辑。排版切换保留活动块选区。
