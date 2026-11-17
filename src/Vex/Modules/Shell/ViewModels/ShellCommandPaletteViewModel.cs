using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>命令面板条目：标签/分类/快捷键与执行委托。</summary>
public sealed record ShellCommandItem(string Group, string Label, string? Gesture, Action Execute);

/// <summary>
/// 命令面板（F1）：全部编辑/插入/格式/视图/帮助命令的检索与执行。
/// 命令表编译期由 [VexCommand] 特性标注经 VexCommandCatalogGenerator 收集（AOT 安全），
/// 面板构建时按分组/标签走 localizer 四语言回退链，Mermaid 图表由 MarkdownTemplates 键表程序化补充。
/// </summary>
public sealed class ShellCommandPaletteViewModel : ReactiveObject
{
    private readonly List<ShellCommandItem> _all;
    private string _query = string.Empty;
    private ShellCommandItem? _selected;

    public ShellCommandPaletteViewModel(ShellTitleMenuViewModel menu)
    {
        var actions = menu.EditorActions;
        var list = new List<ShellCommandItem>();

        // ---- 编辑动作（EditorActionKind 枚举驱动，与工具带同一命令源）----
        foreach (var (kind, group, label) in new[]
                 {
                     (EditorActionKind.Undo, "编辑", "撤销"),
                     (EditorActionKind.Redo, "编辑", "重做"),
                     (EditorActionKind.Heading1, "插入", "标题 1"),
                     (EditorActionKind.Heading2, "插入", "标题 2"),
                     (EditorActionKind.Heading3, "插入", "标题 3"),
                     (EditorActionKind.Heading4, "插入", "标题 4"),
                     (EditorActionKind.Heading5, "插入", "标题 5"),
                     (EditorActionKind.Heading6, "插入", "标题 6"),
                     (EditorActionKind.UnorderedList, "插入", "无序列表"),
                     (EditorActionKind.OrderedList, "插入", "有序列表"),
                     (EditorActionKind.TaskList, "插入", "任务列表"),
                     (EditorActionKind.Quote, "插入", "引用"),
                     (EditorActionKind.Table, "插入", "表格"),
                     (EditorActionKind.CodeFence, "插入", "代码块"),
                     (EditorActionKind.MathBlock, "插入", "公式块"),
                     (EditorActionKind.HorizontalRule, "插入", "水平分割线"),
                     (EditorActionKind.Link, "插入", "链接"),
                     (EditorActionKind.Image, "插入", "图片"),
                     (EditorActionKind.Bold, "格式", "加粗"),
                     (EditorActionKind.Italic, "格式", "斜体"),
                     (EditorActionKind.Strikethrough, "格式", "删除线"),
                     (EditorActionKind.Highlight, "格式", "高亮"),
                     (EditorActionKind.Superscript, "格式", "上标"),
                     (EditorActionKind.Subscript, "格式", "下标"),
                     (EditorActionKind.InlineCode, "格式", "行内代码"),
                     (EditorActionKind.ClearFormatting, "格式", "清除格式"),
                 })
        {
            var kindCaptured = kind;
            list.Add(new ShellCommandItem(group, label, string.Empty, () => actions.InsertAction(kindCaptured)));
        }

        list.Add(new ShellCommandItem("编辑", "查找", "Ctrl+F", menu.ShowFindPanel));
        list.Add(new ShellCommandItem("编辑", "替换", "Ctrl+H", menu.ShowReplacePanel));
        // ---- Mermaid 图表（MarkdownTemplates 键表驱动）----
        foreach (var (key, label) in new[]
                 {
                     ("flowchart", "Mermaid 流程图"), ("sequence", "Mermaid 时序图"), ("state", "Mermaid 状态图"),
                     ("journey", "Mermaid 用户旅程图"), ("gantt", "Mermaid 甘特图"), ("timeline", "Mermaid 时间线"),
                     ("git", "Mermaid Git 分支图"), ("kanban", "Mermaid 看板"), ("class", "Mermaid 类图"),
                     ("er", "Mermaid 实体关系图"), ("requirement", "Mermaid 需求图"), ("mindmap", "Mermaid 思维导图"),
                     ("block", "Mermaid 块图"), ("c4", "Mermaid C4 架构图"), ("architecture", "Mermaid 架构图"),
                     ("packet", "Mermaid 数据包图"), ("pie", "Mermaid 饼图"), ("quadrant", "Mermaid 象限图"),
                     ("xy", "Mermaid 坐标图"), ("sankey", "Mermaid 桑基图"), ("radar", "Mermaid 雷达图"),
                     ("treemap", "Mermaid 矩形树图"), ("venn", "Mermaid 维恩图"), ("treeview", "Mermaid 目录树图"),
                 })
        {
            var keyCaptured = key;
            list.Add(new ShellCommandItem("插入 · 图表", label, string.Empty, () => actions.InsertSnippet(keyCaptured)));
        }

        // ---- 文件/视图/帮助（title menu VM 命令源）----
        var fileCommands = new (string Group, string Label, string? Gesture, Action Execute)[]
        {
            ("文件", "新建", "Ctrl+N", () => menu.NewDocument()),
            ("文件", "从模板新建", string.Empty, () => menu.NewFromTemplate()),
            ("文件", "新建窗口", "Ctrl+Shift+N", () => menu.NewWindow()),
            ("文件", "打开", "Ctrl+O", () => menu.OpenAsync()),
            ("文件", "打开文件夹", string.Empty, () => menu.OpenFolderAsync()),
            ("文件", "打开演示文档", string.Empty, () => menu.Help.OpenHelpTopic("samples")),
            ("文件", "快速打开", "Ctrl+P", () => menu.QuickOpenAsync()),
            ("文件", "保存", "Ctrl+S", () => menu.SaveAsync()),
            ("文件", "另存为", "Ctrl+Shift+S", () => menu.SaveAsAsync()),
            ("文件", "另存为模板", string.Empty, () => menu.SaveAsTemplate()),
            ("文件", "删除文档", string.Empty, () => menu.DeleteAsync()),
            ("文件", "导出 HTML", string.Empty, () => menu.Export("HTML")),
            ("文件", "导出 PDF", string.Empty, () => menu.Export("PDF")),
            ("文件", "导出 PNG 长图", string.Empty, () => menu.Export("PNG")),
            ("文件", "导出 Word", string.Empty, () => menu.Export("Word")),
            ("文件", "复制到微信公众号", string.Empty, () => menu.CopyHtml("wechat")),
            ("文件", "复制到知乎", string.Empty, () => menu.CopyHtml("zhihu")),
            ("文件", "复制到稀土掘金", string.Empty, () => menu.CopyHtml("juejin")),
            ("文件", "导入 HTML", string.Empty, () => menu.ImportFrom("HTML")),
            ("文件", "导入 PDF", string.Empty, () => menu.ImportFrom("PDF")),
            ("文件", "导入 Word", string.Empty, () => menu.ImportFrom("Word")),
            ("文件", "打印预览", "Ctrl+Shift+P", () => menu.Print()),
            ("文件", "刷新预览", string.Empty, () => menu.RefreshPreview()),
            ("文件", "打开文件位置", string.Empty, () => menu.OpenFileLocationAsync()),
            ("文件", "关闭文档", "Ctrl+W", () => menu.CloseDocument()),
            ("视图", "演示模式", "F6", () => menu.Workbench.StartPresentation()),
            ("视图", "专注模式", "F8", () => menu.Layout.ToggleFocusMode()),
            ("视图", "打字机模式", string.Empty, () => menu.Layout.ToggleTypewriterMode()),
            ("视图", "全屏", "F11", () => menu.Layout.ToggleFullScreen()),
            ("视图", "窗口置顶", string.Empty, () => menu.Layout.ToggleAlwaysOnTop()),
            ("帮助", "新手引导", string.Empty, () => menu.Help.OpenHelpTopic("guide")),
            ("帮助", "更新日志", string.Empty, () => menu.Help.OpenHelpTopic("changelog")),
            ("帮助", "鸣谢", string.Empty, () => menu.Help.OpenHelpTopic("thanks")),
            ("帮助", "检查更新", string.Empty, () => menu.Help.OpenHelpTopic("update")),
            ("帮助", "关于 Vex", string.Empty, () => menu.Help.OpenHelpTopic("about")),
        };
        foreach (var (group, label, gesture, execute) in fileCommands)
        {
            list.Add(new ShellCommandItem(group, label, gesture, execute));
        }

        _all = list;
        Results = new ObservableCollection<ShellCommandItem>(_all);
    }

    public string Query
    {
        get => _query;
        set
        {
            this.RaiseAndSetIfChanged(ref _query, value);
            ApplyFilter();
        }
    }

    public ObservableCollection<ShellCommandItem> Results { get; }

    public ShellCommandItem? Selected
    {
        get => _selected;
        set => this.RaiseAndSetIfChanged(ref _selected, value);
    }

    public void Reset()
    {
        Query = string.Empty;
        Selected = Results.FirstOrDefault();
    }

    public void Move(int delta)
    {
        if (Results.Count == 0) return;
        var index = Selected is null ? 0 : Results.IndexOf(Selected);
        if (index < 0) index = 0;
        Selected = Results[(index + delta + Results.Count) % Results.Count];
    }

    public bool ExecuteSelected()
    {
        var item = Selected;
        if (item is null) return false;
        item.Execute();
        return true;
    }

    private void ApplyFilter()
    {
        var needle = _query.Trim();
        var hits = needle.Length == 0
            ? _all
            : _all.Where(item => item.Label.Contains(needle, StringComparison.OrdinalIgnoreCase)
                                 || item.Group.Contains(needle, StringComparison.OrdinalIgnoreCase));
        Results.Clear();
        foreach (var hit in hits.Take(40)) Results.Add(hit);
        Selected = Results.FirstOrDefault();
    }
}
