using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>命令面板条目：标签/分类/快捷键与执行委托。</summary>
public sealed record ShellCommandItem(string Group, string Label, string? Gesture, Action Execute);

/// <summary>
/// 命令面板（F1）：全部编辑/插入/格式/视图/帮助命令的检索与执行。
/// 命令表编译期由 [VexCommand] 特性标注经 VexCommandCatalogGenerator 收集（AOT 安全），
/// 分组/标签/图表名全部是 I18n 键（Language.cs 常量 + localizer 四语言回退链），
/// CultureChanged 时整表重建，面板文案随语言即时切换。
/// </summary>
public sealed class ShellCommandPaletteViewModel : ReactiveObject
{
    /// <summary>Mermaid 片段键 → 图表名 I18n 键（与图鉴同源，键名见 Language.cs 的 Mermaid* 段）。</summary>
    private static readonly (string Snippet, string NameKey)[] MermaidCharts =
    [
        ("flowchart", VexL.MermaidFlowchart), ("sequence", VexL.MermaidSequence), ("state", VexL.MermaidState),
        ("journey", VexL.MermaidJourney), ("gantt", VexL.MermaidGantt), ("timeline", VexL.MermaidTimeline),
        ("git", VexL.MermaidGit), ("kanban", VexL.MermaidKanban), ("class", VexL.MermaidClass),
        ("er", VexL.MermaidEr), ("requirement", VexL.MermaidRequirement), (WorkbenchLayoutKeys.ViewMindMap, VexL.MermaidMindmap),
        ("block", VexL.MermaidBlock), ("c4", VexL.MermaidC4), ("architecture", VexL.MermaidArchitecture),
        ("packet", VexL.MermaidPacket), ("pie", VexL.MermaidPie), ("quadrant", VexL.MermaidQuadrant),
        ("xy", VexL.MermaidXy), ("sankey", VexL.MermaidSankey), ("radar", VexL.MermaidRadar),
        ("treemap", VexL.MermaidTreemap), ("venn", VexL.MermaidVenn), ("treeview", VexL.MermaidTreeView),
    ];

    /// <summary>
    /// 文件/视图/帮助命令的快捷键集中表（真值源是标题菜单 ShellTitleMenuView.axaml 的
    /// InputGesture；这里只读展示，菜单调整时同步本表）。
    /// </summary>
    private static class Gestures
    {
        public const string NewDocument = "Ctrl+N";
        public const string NewWindow = "Ctrl+Shift+N";
        public const string Open = "Ctrl+O";
        public const string QuickOpen = "Ctrl+P";
        public const string Save = "Ctrl+S";
        public const string SaveAs = "Ctrl+Shift+S";
        public const string Print = "Ctrl+Shift+P";
        public const string CloseDocument = "Ctrl+W";
        public const string Find = "Ctrl+F";
        public const string Replace = "Ctrl+H";
        public const string Presentation = "F6";
        public const string FocusMode = "F8";
        public const string FullScreen = "F11";
    }

    private readonly ShellTitleMenuViewModel _menu;
    private readonly IAppLocalizer _localizer;
    private List<ShellCommandItem> _all = [];
    private string _query = string.Empty;
    private ShellCommandItem? _selected;

    public ShellCommandPaletteViewModel(ShellTitleMenuViewModel menu, IAppLocalizer localizer)
    {
        _menu = menu;
        _localizer = localizer;
        RebuildCommands();
        localizer.CultureChanged += (_, _) => RebuildCommands();
    }

    /// <summary>整表重建：键 → 文案的翻译只发生在这一处，语言切换后面板文案即时跟随。</summary>
    private void RebuildCommands()
    {
        var menu = _menu;
        var actions = menu.EditorActions;
        var list = new List<ShellCommandItem>();

        // ---- 编辑/插入/格式动作（VexCommandCatalog 编译期目录驱动，与工具带同一命令源）----
        foreach (var entry in VexCommandCatalog.Items)
        {
            if (entry.Action is EditorActionKind kind)
            {
                var kindCaptured = kind;
                list.Add(new ShellCommandItem(
                    _localizer.Get(entry.Group), _localizer.Get(entry.Label), entry.Gesture,
                    () => actions.InsertAction(kindCaptured)));
            }
        }

        // 查找/替换不走 EditorActionKind（开关查找条而非插入动作）。
        list.Add(new ShellCommandItem(_localizer.Get(VexL.CommandGroupEdit), _localizer.Get(VexL.FindBarFind), Gestures.Find, menu.ShowFindPanel));
        list.Add(new ShellCommandItem(_localizer.Get(VexL.CommandGroupEdit), _localizer.Get(VexL.FindBarReplace), Gestures.Replace, menu.ShowReplacePanel));

        // ---- Mermaid 图表（片段键表驱动，图表名与图鉴同源）----
        foreach (var (snippet, nameKey) in MermaidCharts)
        {
            var snippetCaptured = snippet;
            list.Add(new ShellCommandItem(
                _localizer.Get(VexL.CommandGroupCharts), _localizer.Get(nameKey), string.Empty,
                () => actions.InsertSnippet(snippetCaptured)));
        }

        // ---- 文件（title menu VM 命令源，标签键与菜单项同源）----
        var fileCommands = new (string LabelKey, string? Gesture, Action Execute)[]
        {
            (VexL.NewDocument, Gestures.NewDocument, () => menu.NewDocument()),
            (VexL.TemplateCenterTitle, string.Empty, () => menu.NewFromTemplate()),
            (VexL.NewWindow, Gestures.NewWindow, () => menu.NewWindow()),
            (VexL.OpenFile, Gestures.Open, () => menu.OpenAsync()),
            (VexL.DialogOpenFolderTitle, string.Empty, () => menu.OpenFolderAsync()),
            (VexL.MenuSamples, string.Empty, () => menu.Help.OpenHelpTopic("samples")),
            (VexL.QuickOpen, Gestures.QuickOpen, () => menu.QuickOpenAsync()),
            (VexL.Save, Gestures.Save, () => menu.SaveAsync()),
            (VexL.SaveAs, Gestures.SaveAs, () => menu.SaveAsAsync()),
            (VexL.TemplateSaveTitle, string.Empty, () => menu.SaveAsTemplate()),
            (VexL.Delete, string.Empty, () => menu.DeleteAsync()),
            (VexL.DialogExportHtmlTitle, string.Empty, () => menu.Export("HTML")),
            (VexL.ExportPdf, string.Empty, () => menu.Export("PDF")),
            (VexL.MenuExportPng, string.Empty, () => menu.Export("PNG")),
            (VexL.MenuExportWord, string.Empty, () => menu.Export("Word")),
            (VexL.CopyToWechat, string.Empty, () => menu.CopyHtml("wechat")),
            (VexL.CopyToZhihu, string.Empty, () => menu.CopyHtml("zhihu")),
            (VexL.CopyToJuejin, string.Empty, () => menu.CopyHtml("juejin")),
            (VexL.ImportHtml, string.Empty, () => menu.ImportFrom("HTML")),
            (VexL.ImportPdf, string.Empty, () => menu.ImportFrom("PDF")),
            (VexL.ImportWord, string.Empty, () => menu.ImportFrom("Word")),
            (VexL.Print, Gestures.Print, () => menu.Print()),
            (VexL.RefreshPreview, string.Empty, () => menu.RefreshPreview()),
            (VexL.OpenFileLocation, string.Empty, () => menu.OpenFileLocationAsync()),
            (VexL.MenuCloseDocument, Gestures.CloseDocument, () => menu.CloseDocument()),
        };
        foreach (var (labelKey, gesture, execute) in fileCommands)
        {
            list.Add(new ShellCommandItem(_localizer.Get(VexL.CommandGroupFile), _localizer.Get(labelKey), gesture, execute));
        }

        // ---- 视图 ----
        var viewCommands = new (string LabelKey, string? Gesture, Action Execute)[]
        {
            (VexL.WBPresentation, Gestures.Presentation, () => menu.Workbench.StartPresentation()),
            (VexL.MenuFocusMode, Gestures.FocusMode, () => menu.Layout.ToggleFocusMode()),
            (VexL.MenuTypewriterMode, string.Empty, () => menu.Layout.ToggleTypewriterMode()),
            (VexL.ToggleFullScreen, Gestures.FullScreen, () => menu.Layout.ToggleFullScreen()),
            (VexL.AlwaysOnTop, string.Empty, () => menu.Layout.ToggleAlwaysOnTop()),
        };
        foreach (var (labelKey, gesture, execute) in viewCommands)
        {
            list.Add(new ShellCommandItem(_localizer.Get(VexL.CommandGroupView), _localizer.Get(labelKey), gesture, execute));
        }

        // ---- 帮助（帮助主题跳转键与帮助菜单同源）----
        var helpCommands = new (string LabelKey, string Topic)[]
        {
            (VexL.OnboardingGuide, "guide"),
            (VexL.Changelog, "changelog"),
            (VexL.Thanks, "thanks"),
            (VexL.AboutCheckUpdate, "update"),
            (VexL.About, "about"),
        };
        foreach (var (labelKey, topic) in helpCommands)
        {
            var topicCaptured = topic;
            list.Add(new ShellCommandItem(
                _localizer.Get(VexL.CommandGroupHelp), _localizer.Get(labelKey), string.Empty,
                () => _ = menu.Help.OpenHelpTopic(topicCaptured)));
        }

        _all = list;
        ApplyFilter();
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

    public ObservableCollection<ShellCommandItem> Results { get; } = [];

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
