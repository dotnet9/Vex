using System;
using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Data;
using global::Avalonia.Media;
using global::Avalonia.Styling;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using Markdig.Syntax;
namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 块级渲染器与宿主 MarkdownViewer 之间的服务契约：主题属性绑定、样式类
/// 与共享控件构造。渲染器不直接依赖 Viewer 实例，能力包按此契约接入。
/// </summary>
public interface IMarkdownRenderContext
{
    double ParagraphLineHeight { get; }

    double ParagraphFontSize { get; }

    /// <summary>当前渲染的 Markdown 源文本（TOC 等需要整篇重解析的块使用）。</summary>
    string? MarkdownText { get; }

    /// <summary>相对图片路径的解析基路径。</summary>
    string? ImageBasePath { get; }

    /// <summary>
    /// 图片源解析回调：对 http(s) 图片源在远程获取前调用，返回本地文件路径即
    /// 离线加载（CDN URL 反查本地文件），返回 null 按原样远程获取；null 成员
    /// 表示宿主未提供回调。
    /// </summary>
    Func<string, string?>? ImageSourceResolver { get; }

    double UnorderedListMarkerWidth { get; }

    double OrderedListMarkerMinWidth { get; }

    double OrderedListMarkerCharacterWidth { get; }

    double OrderedListMarkerExtraWidth { get; }

    Thickness ListFirstParagraphMargin { get; }

    Thickness ListNestedParagraphMargin { get; }

    /// <summary>递归转换嵌套块（引用、列表、表格单元格内容）。</summary>
    Control? ConvertBlock(Block block, string? sourceMarkdown);

    SelectableTextBlock CreateParagraph(ParagraphBlock paragraph, bool stripTaskPrefix, Thickness? marginOverride);

    /// <summary>内联树 → Avalonia 内联集合（链接、行内代码、公式、图片等）。</summary>
    System.Collections.Generic.IEnumerable<global::Avalonia.Controls.Documents.Inline> ConvertInlines(
        Markdig.Syntax.Inlines.ContainerInline? container,
        bool stripTaskPrefix);

    /// <summary>为文本块提取链接区间并挂接手型光标与点击打开交互。</summary>
    void AttachLinkInteraction(
        SelectableTextBlock textBlock,
        Markdig.Syntax.Inlines.ContainerInline? container,
        bool stripTaskPrefix);

    /// <summary>
    /// 链接被点击（打开之前触发）：宿主可弹确认框（<see cref="MarkdownLinkClickEventArgs.RequiresConfirmation"/>
    /// 为 true 表示文字与目标疑似不符）并置 <see cref="MarkdownLinkClickEventArgs.Cancel"/> 阻止打开。
    /// </summary>
    void RaiseLinkClicked(MarkdownLinkClickEventArgs args);

    /// <summary>
    /// 代码块折叠状态（按块键 = 源码起始偏移存储，跨重渲染/虚拟化回收保留）。
    /// </summary>
    bool IsCodeBlockFolded(int blockKey);

    /// <summary>更新折叠状态并就地位刷新该代码块。</summary>
    void SetCodeBlockFolded(int blockKey, bool folded);

    /// <summary>折叠功能是否开启（关闭时渲染器不提供折叠按钮）。</summary>
    bool EnableCodeBlockFolding { get; }

    /// <summary>代码块按块键的换行状态；未单独切换过时回落到 Viewer 的全局 <c>CodeBlockWrap</c>。</summary>
    bool IsCodeBlockWrapped(int blockKey);

    /// <summary>保存某块的换行状态（控件已就地应用，不再触发重渲染）。</summary>
    void PersistCodeBlockWrap(int blockKey, bool wrapped);

    /// <summary>保存某块的折叠状态（控件已就地收起/展开，不再触发重渲染）。</summary>
    void PersistCodeBlockFolded(int blockKey, bool folded);

    /// <summary>
    /// 下载代码块：先触发宿主的保存事件，未处理时弹出系统保存对话框。
    /// </summary>
    Task SaveCodeBlockAsync(string code, string? language, string? path);

    /// <summary>
    /// 当前顶层块的解析输入坐标系原点（原始源码绝对偏移）。分段/尾部解析时
    /// Markdig 块的 <c>Span</c> 是解析输入内的相对坐标，渲染器派生块键
    /// （折叠状态、高亮会话等）必须加上该原点才是跨快照稳定的绝对块键；
    /// 整篇解析时为 0。
    /// </summary>
    int CurrentBlockOrigin { get; }

    IDisposable BindTheme<T>(
        AvaloniaObject target,
        AvaloniaProperty<T> targetProperty,
        StyledProperty<T> sourceProperty);

    /// <summary>默认块装饰可使用 Style 优先级，让宿主按语义类名覆盖局部排版。</summary>
    IDisposable BindTheme<T>(AvaloniaObject target, AvaloniaProperty<T> targetProperty,
        StyledProperty<T> sourceProperty, BindingPriority priority) => BindTheme(target, targetProperty, sourceProperty);

    SelectableTextBlock CreateSelectableText(params string[] classes);

    void AddMarkdownClass(Control control, params string[] classes);

    /// <summary>
    /// 数学视图工厂（CodeWF.Avalonia.Markdown.Math 包注册）；为 null 或返回 null 时
    /// 由调用方降级为原文渲染。
    /// </summary>
    MarkdownMathViewFactory? MathViewFactory { get; }

    Control? CreateMathView(string latex, double fontSize, MarkdownMathLineStyle lineStyle);

    /// <summary>
    /// 图片控件工厂（CodeWF.Avalonia.Markdown.Images 包注册）；为 null 或返回 null 时
    /// 由调用方降级为替代文本渲染。
    /// </summary>
    MarkdownImageControlFactory? ImageControlFactory { get; }

    Control? CreateImageControl(string source, string altText);

    /// <summary>为表格创建同块工具；默认保持原表格，不向文档面板插入额外条目。</summary>
    Control DecorateTable(Border table, MarkdownTableData data) => table;

    Control CreateFallbackText(string text, string className);

    /// <summary>
    /// 任务列表勾选框是否可交互（点击回写 Markdown）。
    /// 宿主未提供回写能力时应返回 false，保持只读展示。
    /// </summary>
    bool TaskMarkersInteractive { get; }

    /// <summary>
    /// 将任务勾选框接入回写：宿主记录源码偏移并在点击时抛出写回事件。
    /// </summary>
    void AttachTaskMarkerInteraction(CheckBox checkBox, int sourceMarkerOffset);

    // ---- 代码块渲染所需 ----

    /// <summary>
    /// 代码高亮能力（CodeWF.Avalonia.Markdown.Highlighting 包注册）；为 null 时
    /// 由 Core 降级为单色等宽渲染。
    /// </summary>
    MarkdownCodeHighlighter? CodeHighlighter { get; }

    /// <summary>
    /// 代码块高亮行级缓存会话（按块键 = 源码起始偏移存取，跨快照复用未变行）。
    /// 流式增长场景下默认高亮器用它只重算新增行；宿主不提供时高亮器整块重算。
    /// </summary>
    MarkdownCodeHighlightSession? GetCodeHighlightSession(int blockKey);

    bool CodeBlockIsDark { get; }

    FontFamily CodeFontFamily { get; }

    double CodeBlockFontSize { get; }
    bool CodeBlockWrap => false;

    double CodeBlockLineHeight { get; }

    bool HasSelection { get; }

    /// <summary>复制按钮点击：先触发宿主 CopyClick 事件，再写入剪贴板。</summary>
    void CopyCodeToClipboard(string code);

    Task CopySelectionAsync();

    /// <summary>宿主扩展点：允许替换/增强代码块工具条，sender 为宿主 Viewer。</summary>
    void RaiseCodeBlockToolRender(StackPanel header, StackPanel content, CodeBlock codeBlock);
}
