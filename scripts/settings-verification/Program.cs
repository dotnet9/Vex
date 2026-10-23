using Vex.Core.Services;
using CodeWF.Toolkit.Files.Helpers;
using System.Xml.Linq;

// 使用隔离配置验证真实存储，不读取用户设置。
var directory = Path.Combine(args.FirstOrDefault() ?? "E:/github/temp_files/vex-prototype/settings", Guid.NewGuid().ToString("N"));
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
    var defaults = new EditorPreferences(new AppSettingsStore());
    if (defaults.DefaultEncoding != "utf-8" || defaults.DefaultNewline != "system" || defaults.BodyFont != "system"
        || defaults.ExportFormat != "HTML" || defaults.PdfSize != "A4" || defaults.ImageFolder != "assets" || defaults.WatermarkText != "维刻 Vex")
        throw new InvalidOperationException("An unrelated settings update lost effective preference defaults.");
    Console.WriteLine("PASS: real storage preserves default choices and watermark after unrelated updates.");
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
        WorkbenchView = "mindmap", EditMode = "source", MindMapSummaryLength = 42,
        MindMapShowParagraphs = false, MindMapShowCode = true, MindMapShowTables = true, MindMapShowLists = false,
        SyntaxMath = false, SyntaxSoftBreaks = false, BodyFontSize = 18, ImageFolder = "images", AutoSave = true,
        DefaultEncoding = "utf-8-bom", DefaultNewline = "CRLF", BodyFont = "serif", ExportFormat = "Word",
        WatermarkPreview = true, WatermarkText = "test <&> 维刻", WatermarkOpacity = 18,
        PdfSize = "Letter", PdfMargin = 25, HtmlStyle = false,
        IsOutlineVisible = false, OutlineWidth = 286, FileListView = true, FileSortDescending = true,
        CollapsedFolders = "E%3A%5C笔记%5C目录\nE%3A%5C其他目录",
        CollapsedOutlineSections = "document/2:章节:0\ndocument/2:同名章节:1",
        EnableAutoPair = false, EnableMotion = false, IsFocusMode = true, IsTypewriterMode = true
    });
    if (new AppSettingsStore().Current != expected)
        throw new InvalidOperationException("Workbench, mind map or editing preferences did not survive a new store.");

    Console.WriteLine("PASS: batch settings preserve typed values, external keys and unrelated XML sections.");
    Console.WriteLine("PASS: all workbench, mind map, editing and motion preferences survive a new store.");
    AppConfigHelper.SetMany(path, new Dictionary<string, object?>
    {
        ["DefaultEncoding"] = "", ["DefaultNewline"] = "", ["BodyFont"] = "", ["ExportFormat"] = "", ["PdfSize"] = ""
    });
    var normalized = new EditorPreferences(new AppSettingsStore());
    if (normalized.DefaultEncoding != "utf-8" || normalized.DefaultNewline != "system" || normalized.BodyFont != "system"
        || normalized.ExportFormat != "HTML" || normalized.PdfSize != "A4")
        throw new InvalidOperationException("Empty legacy choice values were not normalized.");
    second.Update(settings => settings with { WatermarkText = "", ExportFolder = "", ImageFolder = "" });
    if (new AppSettingsStore().Current is not { WatermarkText: "", ExportFolder: "", ImageFolder: "" })
        throw new InvalidOperationException("An explicitly cleared text preference was replaced by a default.");
    Console.WriteLine("PASS: empty choice values recover defaults while explicitly cleared text remains empty.");
}
finally
{
    Directory.Delete(directory, recursive: true);
}
