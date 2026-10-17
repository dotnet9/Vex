namespace Vex.Core.Models;

/// <summary>固定视图组合。源码和即时编辑共用一个编辑器席位。</summary>
public sealed record WorkbenchLayoutOption(string Id, string Label, string Hint, string Diagram, string? Editor, bool Render, bool Map)
{
    public static readonly IReadOnlyList<WorkbenchLayoutOption> All =
    [
        new("instant", "WSInstant", "WSInstantHint", "editor", "instant", false, false),
        new("source", "WSSource", "WSSourceHint", "editor", "source", false, false),
        new("read", "WSRead", "WSReadHint", "render", null, true, false),
        new("map", "WSMap", "WSMapHint", "map", null, false, true),
        new("source-render", "WSSourceRender", "WSCompareHint", "editor,render", "source", true, false),
        new("source-map", "WSSourceMap", "WSStructureHint", "editor,map", "source", false, true),
        new("instant-map", "WSInstantMap", "WSStructureHint", "editor,map", "instant", false, true),
        new("three", "WSThree", "WSThreeHint", "editor,render;map", "source", true, true)
    ];
    public string Name => WorkbenchText.Get(Label);
    public string Description => WorkbenchText.Get(Hint);
}

public static class WorkbenchText
{
    public static string Get(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL." + key) ?? key;
}
