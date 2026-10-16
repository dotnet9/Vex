using Vex.Core.Services;
using CodeWF.Toolkit.Files.Helpers;
using System.Xml.Linq;

// 使用隔离配置验证真实存储，不读取用户设置。
var directory = Path.Combine(Path.GetTempPath(), "Vex.SettingsVerification", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var path = Path.Combine(directory, "verification.config");
AppContext.SetData("APP_CONFIG_FILE", path);
try
{
    File.WriteAllText(path, """
        <configuration><appSettings><add key="ExternalSetting" value="keep" /></appSettings><custom value="keep" /></configuration>
        """);
    var first = new AppSettingsStore();
    first.Update(settings => settings with { ThemeKey = "system", PaletteKey = "aurora" });
    var second = new AppSettingsStore();
    if (second.Current.ThemeKey != "system" || second.Current.PaletteKey != "aurora")
        throw new InvalidOperationException("Theme mode and palette did not survive a new settings store.");

    // 来自另一实例的部分更新必须保留磁盘上的配色。
    second.Update(settings => settings with { ThemeKey = "dark" });
    first.Update(settings => settings with { ShowLineNumbers = true });
    var restored = new AppSettingsStore().Current;
    if (restored.ThemeKey != "dark" || restored.PaletteKey != "aurora" || restored.ShowLineNumbers != true)
        throw new InvalidOperationException("A partial update discarded persisted appearance settings.");

    AppConfigHelper.Set(path, "ExternalSetting", "modified externally");
    second.Update(settings => settings with { EditorZoom = 1.25, McpRequireConfirmation = false });
    restored = new AppSettingsStore().Current;
    if (restored.EditorZoom != 1.25 || restored.McpRequireConfirmation != false || restored.ThemeKey != "dark")
        throw new InvalidOperationException("Batch persistence changed typed values or discarded another instance's settings.");
    var document = XDocument.Load(path);
    if (AppConfigHelper.ReadAll(path)["ExternalSetting"] != "modified externally" || document.Root?.Element("custom")?.Attribute("value")?.Value != "keep")
        throw new InvalidOperationException("Saving settings discarded unrelated keys or configuration sections.");

    Console.WriteLine("PASS: real settings persist independent mode and palette across instances and partial updates.");
    var expected = second.Update(settings => settings with
    {
        SourcePreviewEnabled = false, WorkbenchView = "mindmap", EditMode = "source", MindMapSummaryLength = 42,
        MindMapShowParagraphs = false, MindMapShowCode = true, MindMapShowTables = true, MindMapShowLists = false,
        EnableAutoPair = false, EnableMotion = false, IsFocusMode = true, IsTypewriterMode = true
    });
    if (new AppSettingsStore().Current != expected)
        throw new InvalidOperationException("Workbench, mind map or editing preferences did not survive a new store.");

    Console.WriteLine("PASS: batch settings preserve typed values, external keys and unrelated XML sections.");
    Console.WriteLine("PASS: all workbench, mind map, editing and motion preferences survive a new store.");
}
finally
{
    Directory.Delete(directory, recursive: true);
}
