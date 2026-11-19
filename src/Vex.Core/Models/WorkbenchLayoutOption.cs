namespace Vex.Core.Models;

/// <summary>工作区布局与编辑模式键的全仓唯一事实源：视图/布局/编辑模式的字面量一律引用此处常量。</summary>
public static class WorkbenchLayoutKeys
{
    public const string Instant = "instant";
    public const string Source = "source";
    public const string Read = "read";
    public const string Map = "map";
    public const string SourceRender = "source-render";
    public const string SourceMap = "source-map";
    public const string InstantMap = "instant-map";
    public const string Three = "three";

    /// <summary>编辑席位的形态键（WorkbenchView）：编辑=源码/即时，阅读与脑图无编辑席位。</summary>
    public const string ViewEdit = "edit";
    public const string ViewMindMap = "mindmap";
}

/// <summary>固定视图组合。源码和即时编辑共用一个编辑器席位。</summary>
public sealed record WorkbenchLayoutOption(string Id, string Label, string Hint, string Diagram, string? Editor, bool Render, bool Map)
{
    public static readonly IReadOnlyList<WorkbenchLayoutOption> All =
    [
        new(WorkbenchLayoutKeys.Instant, "WSInstant", "WSInstantHint", "editor", WorkbenchLayoutKeys.Instant, false, false),
        new(WorkbenchLayoutKeys.Source, "WSSource", "WSSourceHint", "editor", WorkbenchLayoutKeys.Source, false, false),
        new(WorkbenchLayoutKeys.Read, "WSRead", "WSReadHint", "render", null, true, false),
        new(WorkbenchLayoutKeys.Map, "WSMap", "WSMapHint", "map", null, false, true),
        new(WorkbenchLayoutKeys.SourceRender, "WSSourceRender", "WSCompareHint", "editor,render", WorkbenchLayoutKeys.Source, true, false),
        new(WorkbenchLayoutKeys.SourceMap, "WSSourceMap", "WSStructureHint", "editor,map", WorkbenchLayoutKeys.Source, false, true),
        new(WorkbenchLayoutKeys.InstantMap, "WSInstantMap", "WSInstantMapHint", "editor,map", WorkbenchLayoutKeys.Instant, false, true),
        new(WorkbenchLayoutKeys.Three, "WSThree", "WSThreeHint", "editor,render;map", WorkbenchLayoutKeys.Source, true, true)
    ];
    public string Name => WorkbenchText.Get(Label);
    public string Description => WorkbenchText.Get(Hint);
}

public static class WorkbenchText
{
    public static string Get(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL." + key) ?? key;
}
