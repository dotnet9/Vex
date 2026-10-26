using System.Collections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;
using MenuPath = Avalonia.Controls.Shapes.Path;

internal static class MenuIconVerification
{
    private static readonly Dictionary<string, string> ExpectedPixels = new(StringComparer.Ordinal);

    public static bool HasIcon(MenuItem item, string key) => Data(item) == Expected(key);

    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var title = window.FindControl<ShellTitleMenuView>("TitleMenuView")!;
        var mermaid = title.FindControl<MenuItem>("MermaidMenuItem")!;
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["flowchart"] = "workflow", ["sequence"] = "arrow-down-up", ["state"] = "circle-dot",
            ["journey"] = "route", ["gantt"] = "chart-gantt", ["timeline"] = "history", ["git"] = "git-branch",
            ["kanban"] = "square-kanban", ["class"] = "panels-top-left", ["er"] = "network",
            ["requirement"] = "clipboard-list", ["mindmap"] = "list-tree", ["block"] = "blocks",
            ["c4"] = "boxes", ["architecture"] = "network", ["packet"] = "binary", ["pie"] = "chart-pie",
            ["quadrant"] = "grid-2x2", ["xy"] = "chart-column", ["sankey"] = "chart-network",
            ["radar"] = "pentagon", ["treemap"] = "layout-dashboard",
        };
        Check(mermaid.Items.OfType<MenuItem>().Count() == expected.Count, "Mermaid 覆盖全部 22 种图表");
        foreach (var item in mermaid.Items.OfType<MenuItem>())
        {
            var key = expected[(string)item.CommandParameter!];
            Check(Data(item) == Expected(key), $"Mermaid {item.Header} 使用 {key}");
        }

        // 遍历所有菜单宿主，区分真正的文件操作、帮助入口与意外回退。
        var menus = CollectMenus(window).ToArray();
        Check(menus.Distinct().Count() == menus.Length, "菜单审计不重复统计同一个控件");
        var formatKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Paragraph"] = "pilcrow", ["MathBlock"] = "sigma", ["HorizontalRule"] = "minus",
            ["footnote"] = "superscript", ["Strikethrough"] = "strike", ["Highlight"] = "highlight",
            ["Superscript"] = "superscript", ["Subscript"] = "subscript", ["ClearFormatting"] = "remove-formatting",
        };
        foreach (var (action, key) in formatKeys)
        {
            var matches = menus.Where(item => item.CommandParameter?.ToString() == action).ToArray();
            Check(matches.Length > 0 && matches.All(item => HasIcon(item, key)), $"{action} 使用正确语义图标 {key}");
        }
        var fileData = Expected("file");
        var helpData = Expected("help");
        foreach (var item in menus)
        {
            if (item.ToggleType != MenuItemToggleType.None)
            {
                Check(item.Icon is null, $"状态项不配置操作图标：{item.Header}");
                continue;
            }
            Check(Data(item) is { Length: > 0 }, $"菜单有矢量图标：{item.Header}");
            Check(Data(item) != fileData || MenuIconCatalog.GetKey(item) == "file", $"没有误用文件图标：{item.Header}");
            Check(Data(item) != helpData || MenuIconCatalog.GetKey(item) == "help", $"没有未匹配的菜单：{item.Header}");
        }
        File.WriteAllLines(Path.Combine(output, "menu-icon-audit.tsv"),
            menus.Select(item => $"{item.Header}\t{MenuIconCatalog.GetKey(item)}\t{item.CommandParameter}\t{item.ToggleType}\t{Data(item)}"));

        // 任意显示文字都不能影响参数或显式键；重复 Apply 不应重新分配或放大图标。
        var probe = new MenuItem { Header = "copy table file", CommandParameter = "flowchart" };
        MenuIconCatalog.Apply(probe);
        Check(Data(probe) == Expected("workflow"), "完整命令参数优先于显示文字");
        Check(!HasIcon(probe, "file"), "像素比较能识别流程图与文件图标的差别");
        var icon = probe.Icon;
        MenuIconCatalog.Apply(probe);
        Check(ReferenceEquals(icon, probe.Icon), "重复应用不替换已有图标控件");
        var explicitProbe = new MenuItem { Header = "file", CommandParameter = "flowchart" };
        MenuIconCatalog.SetKey(explicitProbe, "minus");
        MenuIconCatalog.Apply(explicitProbe);
        Check(Data(explicitProbe) == Expected("minus"), "显式图标键优先于命令参数");

        // 在每种语言下重新构造菜单，避免只验证切换前已经分配好的图标。
        foreach (var culture in new[] { "zh-CN", "en-US", "zh-Hant", "ja-JP" })
        {
            vm.Appearance.SelectLanguageByCulture(culture);
            Render(window);
            var localized = new ShellTitleMenuView();
            foreach (var item in localized.FindControl<MenuItem>("MermaidMenuItem")!.Items.OfType<MenuItem>())
            {
                Check(Data(item) == Expected(expected[(string)item.CommandParameter!]),
                    $"{culture} 新建 Mermaid 菜单图标正确：{item.Header}");
            }
            Check(Tree(((Menu)localized.Content!).Items).Where(item => item.ToggleType == MenuItemToggleType.None)
                .All(item => Data(item) != helpData || MenuIconCatalog.GetKey(item) == "help"), $"{culture} 顶部菜单无未匹配项");
            var instant = new MarkdownInstantView();
            Check(Tree(instant.Editor.ContextMenu!.Items).All(item => Data(item) != fileData && Data(item) != helpData),
                $"{culture} 即时菜单无文件或帮助回退图标");
        }
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        foreach (var theme in new[] { "light", "dark" })
        {
            vm.Appearance.SelectThemeByKey(theme);
            // 段落与格式已并入「编辑」，三级面板要先展开一级菜单才会实例化。
            var edit = title.FindControl<MenuItem>("EditMenuItem")!;
            edit.IsSubMenuOpen = true;
            Render(window);
            CapturePopup(edit, $"edit-{theme}");
            var paragraph = title.FindControl<MenuItem>("InsertMenuItem")!;
            paragraph.IsSubMenuOpen = true;
            Render(window);
            CapturePopup(paragraph, $"paragraph-{theme}");
            mermaid.IsSubMenuOpen = true;
            Render(window);
            CapturePopup(mermaid, $"mermaid-{theme}");
            mermaid.IsSubMenuOpen = false;
            paragraph.IsSubMenuOpen = false;
            var format = title.FindControl<MenuItem>("FormatMenuItem")!;
            format.IsSubMenuOpen = true;
            Render(window);
            CapturePopup(format, $"format-{theme}");
            format.IsSubMenuOpen = false;
            edit.IsSubMenuOpen = false;
            Render(window);
        }
        vm.Appearance.SelectThemeByKey("light");
        Render(window);

        void CapturePopup(MenuItem item, string name)
        {
            var panel = item.GetVisualDescendants().OfType<Popup>().First().Child!;
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
            bitmap.Render(panel);
            bitmap.Save(Path.Combine(output, name + ".png"), new PngBitmapEncoderOptions());
        }
    }

    private static IEnumerable<MenuItem> CollectMenus(MainWindow window)
    {
        var title = window.FindControl<ShellTitleMenuView>("TitleMenuView")!;
        foreach (var item in Tree(((Menu)title.Content!).Items)) yield return item;
        var files = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        foreach (var menu in new[] { files.FindControl<TreeView>("FileTree")!.ContextMenu!, files.FindControl<ListBox>("FileList")!.ContextMenu! })
            foreach (var item in Tree(menu.Items)) yield return item;
        var editor = window.FindControl<MarkdownEditorView>("EditorGuideTarget")!;
        foreach (var item in Tree(editor.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.ContextMenu!.Items)) yield return item;
        foreach (var item in Tree(window.FindControl<MarkdownInstantView>("InstantGuideTarget")!.Editor.ContextMenu!.Items)) yield return item;
        foreach (var item in Tree(window.LayoutTarget.ContextMenu!.Items)) yield return item;
        var options = window.GetVisualDescendants().OfType<ShellEditorOptionsView>().Single();
        foreach (var item in Tree(((MenuFlyout)options.FindControl<Button>("FormatHeading")!.Flyout!).Items)) yield return item;
    }

    private static IEnumerable<MenuItem> Tree(IEnumerable items)
    {
        foreach (var item in items.OfType<MenuItem>())
        {
            yield return item;
            foreach (var child in Tree(item.Items)) yield return child;
        }
    }

    private static string? Data(MenuItem item) => ((item.Icon as Viewbox)?.Child as MenuPath)?.Data is { } geometry
        ? Pixels(geometry) : null;

    private static string Expected(string key)
    {
        if (!ExpectedPixels.TryGetValue(key, out var pixels))
        {
            pixels = Pixels(MenuIconCatalog.Get(key));
            ExpectedPixels.Add(key, pixels);
        }
        return pixels;
    }

    // StreamGeometry.ToString() 只返回类型名；必须比较实际描边像素才能区分图形。
    private static string Pixels(Geometry geometry)
    {
        var path = new MenuPath
        {
            Width = 24, Height = 24, Data = geometry, Fill = Brushes.Transparent, Stroke = Brushes.Black,
            StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round,
        };
        path.Measure(new Size(24, 24));
        path.Arrange(new Rect(0, 0, 24, 24));
        using var bitmap = new RenderTargetBitmap(new PixelSize(24, 24));
        bitmap.Render(path);
        using var stream = new MemoryStream();
        bitmap.Save(stream, new PngBitmapEncoderOptions());
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream.ToArray()));
    }

    private static void Render(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
