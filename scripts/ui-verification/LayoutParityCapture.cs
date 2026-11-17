using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Vex.Modules.Shell.Views;

/// <summary>
/// 布局锚点 parity（native 侧）：与 design/tests/capture-parity-layout.cjs 的锚点表一一对应，
/// 输出相对主窗左上角的元素几何 JSON（±2px/边由 layout-score 比对）。
/// 段落结构锚点两端形态不同（原型 select / 原生按钮+Flyout），取各自形态几何，语义对齐。
/// </summary>
public static class LayoutParityCapture
{
    private static readonly string[] AnchorNames =
    [
        "FileMenuItem", "ViewMenuItem", "HelpMenuItem",
        "TitleDocumentLabel", "UpdatePill",
        "FormatUndo", "FormatHeading", "FormatMermaidGallery", "SpanFontSize", "TypographyTarget",
        "FilesPanelTools", "FilesFooter", "DocumentWordCount",
    ];

    private static readonly Dictionary<string, string> NativeToProto =
        new()
        {
            ["FileMenuItem"] = "menu-file",
            ["ViewMenuItem"] = "menu-view",
            ["HelpMenuItem"] = "menu-help",
            ["TitleDocumentLabel"] = "document-title",
            ["UpdatePill"] = "update-pill",
            ["FormatUndo"] = "band-undo",
            ["FormatHeading"] = "band-heading",
            ["FormatMermaidGallery"] = "band-mermaid",
            ["SpanFontSize"] = "band-span-size",
            ["TypographyTarget"] = "typography-trigger",
            ["FilesPanelTools"] = "sidebar-search",
            ["FilesFooter"] = "files-footer",
            ["DocumentWordCount"] = "word-count",
        };

    public static void Capture(MainWindow window, string output)
    {
        var controls = window.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Name is not null && NativeToProto.ContainsKey(control.Name))
            .GroupBy(control => control.Name!)
            .ToDictionary(group => group.Key, group => group.First());

        var scene = new Dictionary<string, object?>();
        foreach (var (nativeName, protoName) in NativeToProto)
        {
            if (!controls.TryGetValue(nativeName, out var control) || !control.IsVisible)
            {
                scene[protoName] = null;
                continue;
            }
            var topLeft = control.TranslatePoint(new Point(0, 0), window);
            if (topLeft is null) { scene[protoName] = null; continue; }
            scene[protoName] = new
            {
                x = Math.Round(topLeft.Value.X, 1),
                y = Math.Round(topLeft.Value.Y, 1),
                w = Math.Round(control.Bounds.Width, 1),
                h = Math.Round(control.Bounds.Height, 1),
            };
        }

        var report = new Dictionary<string, object?> { ["instant"] = scene };
        Directory.CreateDirectory(output);
        var path = Path.Combine(output, "layout-anchors.json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        Console.WriteLine("LAYOUT ANCHORS WRITTEN: " + path);
    }
}
