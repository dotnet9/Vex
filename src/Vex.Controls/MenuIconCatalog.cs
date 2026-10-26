using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using MenuPath = Avalonia.Controls.Shapes.Path;

namespace Vex.Controls;

/// <summary>
/// Provides the shared Lucide menu icon geometries and applies them to menu trees.
/// </summary>
public static class MenuIconCatalog
{
    /// <summary>为没有动作参数的菜单声明稳定图标键，避免依赖翻译后的文字。</summary>
    public static readonly AttachedProperty<string?> KeyProperty =
        AvaloniaProperty.RegisterAttached<MenuItem, string?>("Key", typeof(MenuIconCatalog));

    public static string? GetKey(MenuItem item) => item.GetValue(KeyProperty);

    public static void SetKey(MenuItem item, string? value) => item.SetValue(KeyProperty, value);

    private static readonly IReadOnlyDictionary<string, string> CommandIconKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Paragraph"] = "pilcrow",
        ["Heading1"] = "heading", ["Heading2"] = "heading", ["Heading3"] = "heading",
        ["Heading4"] = "heading", ["Heading5"] = "heading", ["Heading6"] = "heading",
        ["UnorderedList"] = "list", ["OrderedList"] = "list-ordered", ["TaskList"] = "list-check",
        ["Quote"] = "quote", ["CodeFence"] = "code", ["InlineCode"] = "code", ["Table"] = "table",
        ["MathBlock"] = "sigma", ["HorizontalRule"] = "minus", ["footnote"] = "superscript",
        ["Bold"] = "bold", ["Italic"] = "italic", ["Strikethrough"] = "strike", ["Highlight"] = "highlight",
        ["Superscript"] = "superscript", ["Subscript"] = "subscript", ["ClearFormatting"] = "remove-formatting",
        ["Link"] = "link", ["Image"] = "image",
        ["RowAbove"] = "arrow-up-to-line", ["RowBelow"] = "arrow-down-to-line",
        ["ColBefore"] = "arrow-left", ["ColAfter"] = "arrow-right",
        ["RowUp"] = "arrow-up", ["RowDown"] = "arrow-down", ["ColLeft"] = "arrow-left", ["ColRight"] = "arrow-right",
        ["AlignLeft"] = "align-left", ["AlignCenter"] = "align-center", ["AlignRight"] = "align-right",
        ["RowDelete"] = "table-rows-split", ["ColDelete"] = "table-columns-split",
        ["flowchart"] = "workflow", ["sequence"] = "arrow-down-up", ["state"] = "circle-dot",
        ["journey"] = "route", ["gantt"] = "chart-gantt", ["timeline"] = "history", ["git"] = "git-branch",
        ["kanban"] = "square-kanban", ["class"] = "panels-top-left", ["er"] = "network",
        ["requirement"] = "clipboard-list", ["mindmap"] = "list-tree", ["block"] = "blocks",
        ["c4"] = "boxes", ["architecture"] = "network", ["packet"] = "binary", ["pie"] = "chart-pie",
        ["quadrant"] = "grid-2x2", ["xy"] = "chart-column", ["sankey"] = "chart-network",
        ["radar"] = "pentagon", ["treemap"] = "layout-dashboard",
        ["HTML"] = "code", ["PDF"] = "file-text", ["PNG"] = "image", ["Word"] = "file-text",
        ["utf-8"] = "binary", ["utf-8-bom"] = "binary", ["GB18030"] = "binary", ["Big5"] = "binary",
        ["samples"] = "book-open", ["changelog"] = "history", ["thanks"] = "heart",
        ["website"] = "external", ["feedback"] = "message-circle", ["update"] = "refresh", ["about"] = "info",
        ["wechat"] = "copy", ["zhihu"] = "copy", ["juejin"] = "copy",
    };

    private static readonly IReadOnlyDictionary<string, string> GeometryData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["file"] = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6",
        ["file-plus"] = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M12 18v-6 M9 15h6",
        ["folder"] = "M3 7a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z",
        ["folder-open"] = "M3 7a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v1H7a2 2 0 0 0-1.9 1.37L3 19z M3 19l2.1-7.63A2 2 0 0 1 7 10h14l-2.1 7.63A2 2 0 0 1 17 19z",
        ["history"] = "M3 12a9 9 0 1 0 3-6.7 M3 4v5h5 M12 7v5l3 2",
        ["save"] = "M3 3h15l3 3v15H3z M7 3v6h9V3 M7 21v-7h10v7",
        ["download"] = "M12 3v12 M7 10l5 5 5-5 M5 21h14",
        ["upload"] = "M12 15V3 M7 8l5-5 5 5 M5 21h14",
        ["printer"] = "M6 9V3h12v6 M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2 M6 14h12v7H6z",
        ["info"] = "M12 16v-4 M12 8h.01 M21 12a9 9 0 1 1-18 0a9 9 0 0 1 18 0z",
        ["map-pin"] = "M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0z M12 13a3 3 0 1 0 0-6a3 3 0 0 0 0 6z",
        ["trash"] = "M3 6h18 M8 6V4h8v2 M19 6l-1 15H6L5 6 M10 11v6 M14 11v6",
        ["close"] = "M6 6l12 12 M18 6L6 18",
        ["undo"] = "M9 14L4 9l5-5 M4 9h10a6 6 0 0 1 6 6v1",
        ["redo"] = "M15 14l5-5-5-5 M20 9H10a6 6 0 0 0-6 6v1",
        ["cut"] = "m6 9 12 6 M6 15l12-6 M6 9a3 3 0 1 0 0-6a3 3 0 0 0 0 6z M6 21a3 3 0 1 0 0-6a3 3 0 0 0 0 6z",
        ["copy"] = "M8 8V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2h-4 M4 8h9a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-9a2 2 0 0 1 2-2z",
        ["clipboard"] = "M9 5h6 M9 3h6v4H9z M5 5H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-1 M8 12h8 M8 16h6",
        ["search"] = "M11 19a8 8 0 1 0 0-16a8 8 0 0 0 0 16z M21 21l-4.3-4.3",
        ["palette"] = "M13 6.5a.5.5 0 1 0 1 0a.5.5 0 1 0-1 0 M17 10.5a.5.5 0 1 0 1 0a.5.5 0 1 0-1 0 M8 7.5a.5.5 0 1 0 1 0a.5.5 0 1 0-1 0 M6 12.5a.5.5 0 1 0 1 0a.5.5 0 1 0-1 0 M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z",
        ["replace"] = "M3 6h18 M7 3v6 M17 18h4 M19 15v6 M3 18h10 M5 15l-2 3 2 3 M13 15l2 3-2 3",
        ["heading"] = "M6 4v16 M18 4v16 M6 12h12 M6 4H4 M6 20H4 M18 4h2 M18 20h2",
        // 列表四项使用轮廓差异明显的几何，避免在 14px 槽位中退化为相同的点线图标。
        ["list"] = "M3 4h4v4H3z M3 10h4v4H3z M3 16h4v4H3z M11 6h10 M11 12h10 M11 18h10",
        ["list-ordered"] = "M3 4h2v4H3 M3 4h3 M3 8h3 M3 10h3 M3 10l-3 4h3 M3 16h3 M3 16l-3 4h3 M11 6h10 M11 12h10 M11 18h10",
        ["list-check"] = "M3 3h6v6H3z M3 12h6v6H3z M13 6h8 M13 15h8 M4 15l2 2 4-4",
        ["quote"] = "M3 4h7v8H3z M14 4h7v8h-7z M6 14c0 3-1 5-3 6 M17 14c0 3-1 5-3 6",
        ["code"] = "m8 9-4 3 4 3 M16 9l4 3-4 3 M14 5l-4 14",
        ["table"] = "M3 3h18v18H3z M3 9h18 M3 15h18 M9 3v18 M15 3v18",
        ["bold"] = "M6 4h7a4 4 0 0 1 0 8H6z M6 12h8a4 4 0 0 1 0 8H6z",
        ["italic"] = "M10 4h8 M6 20h8 M14 4l-4 16",
        ["strike"] = "M5 12h14 M7 7c1-3 9-3 10 0 M7 17c1 3 9 3 10 0",
        ["highlight"] = "m9 11 4 4 M4 20h16 M14 4l6 6-7 7H7v-6z",
        ["link"] = "M10 13a5 5 0 0 0 7 0l2-2a5 5 0 0 0-7-7l-1 1 M14 11a5 5 0 0 0-7 0l-2 2a5 5 0 0 0 7 7l1-1",
        ["image"] = "M3 5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M8.5 9a1.5 1.5 0 1 0 0-3a1.5 1.5 0 0 0 0 3z M21 15l-5-5L5 21",
        ["eye"] = "M2 12s3-7 10-7s10 7 10 7s-3 7-10 7S2 12 2 12z M12 15a3 3 0 1 0 0-6a3 3 0 0 0 0 6z",
        ["wrench"] = "M14.7 6.3a4.9 4.9 0 0 0-6.4 6.4L3 18a2.1 2.1 0 1 0 3 3l5.3-5.3a4.9 4.9 0 0 0 6.4-6.4L14 12l-2-2z",
        ["help"] = "M9.1 9a3 3 0 1 1 5.8 1c0 2-3 2-3 4 M12 18h.01 M21 12a9 9 0 1 1-18 0a9 9 0 0 1 18 0z",
        ["refresh"] = "M20 7V3h-4 M20 3a9 9 0 1 0 1 9",
        ["external"] = "M14 3h7v7 M10 14L21 3 M21 14v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5",
        ["book-open"] = "M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z",
        ["heart"] = "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78z",
        ["message-circle"] = "M21 11.5a8.38 8.38 0 0 1-.9 3.8a8.5 8.5 0 0 1-7.6 4.7a8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8a8.5 8.5 0 0 1 4.7-7.6a8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8z",
        ["pencil"] = "M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L8 18l-4 1 1-4z",
        ["panel"] = "M3 3h18v18H3z M9 3v18",
        ["settings"] = "M12 15.5a3.5 3.5 0 1 0 0-7a3.5 3.5 0 0 0 0 7z M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1-2.8 2.8-.1-.1a1.7 1.7 0 0 0-1.9-.3a1.7 1.7 0 0 0-1 1.6v.1h-4v-.1a1.7 1.7 0 0 0-1-1.6a1.7 1.7 0 0 0-1.9.3l-.1.1-2.8-2.8.1-.1a1.7 1.7 0 0 0 .3-1.9a1.7 1.7 0 0 0-1.6-1H3v-4h.1a1.7 1.7 0 0 0 1.6-1a1.7 1.7 0 0 0-.3-1.9l-.1-.1L7.1 4l.1.1a1.7 1.7 0 0 0 1.9.3a1.7 1.7 0 0 0 1-1.6v-.1h4v.1a1.7 1.7 0 0 0 1 1.6a1.7 1.7 0 0 0 1.9-.3l.1-.1 2.8 2.8-.1.1a1.7 1.7 0 0 0-.3 1.9a1.7 1.7 0 0 0 1.6 1h.1v4h-.1a1.7 1.7 0 0 0-1.6 1z",
        ["file-text"] = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M8 13h8 M8 17h6",
    };

    public static Geometry Get(string key)
    {
        var data = MenuIconGeometry.Data.TryGetValue(key, out var lucideData) ? lucideData : GeometryData[key];
        return Geometry.Parse(data);
    }

    public static void Apply(Menu menu)
    {
        foreach (var item in menu.Items.OfType<MenuItem>()) Apply(item, topLevel: true);
    }

    public static void Apply(ContextMenu menu)
    {
        foreach (var item in menu.Items.OfType<MenuItem>()) Apply(item);
    }

    public static void Apply(MenuItem item)
    {
        Apply(item, topLevel: false);
    }

    private static void Apply(MenuItem item, bool topLevel)
    {
        item.FontSize = 13;
        if (item.ToggleType == MenuItemToggleType.None && item.Icon is null)
        {
            var path = new MenuPath
            {
                Width = 24,
                Height = 24,
                Data = Get(ResolveKey(item)),
                Fill = Brushes.Transparent,
                StrokeThickness = 2,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Classes = { "menu-icon" },
            };
            path.Bind(MenuPath.StrokeProperty, item.GetObservable(MenuItem.ForegroundProperty));
            item.Icon = new Viewbox
            {
                Width = topLevel ? 12 : 14,
                Height = topLevel ? 12 : 14,
                Stretch = Stretch.Uniform,
                Child = path,
                Classes = { "menu-icon-host" },
            };
        }

        foreach (var child in GetChildren(item)) Apply(child, topLevel: false);
    }

    private static IEnumerable<MenuItem> GetChildren(MenuItem item)
    {
        // Items 会映射 ItemsSource，选择一个集合即可，避免动态子菜单被重复递归。
        var source = item.ItemsSource ?? item.Items;
        return source.OfType<MenuItem>();
    }

    private static string ResolveKey(MenuItem item)
    {
        // 显式语义和完整动作参数优先，不能让翻译文案或子串碰撞把图表误配成文件。
        if (GetKey(item) is { Length: > 0 } key) return key;
        if (item.CommandParameter?.ToString() is { } parameter && CommandIconKeys.TryGetValue(parameter, out var commandKey))
        {
            return commandKey;
        }
        return "help";
    }
}
