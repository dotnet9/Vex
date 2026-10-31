using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

// 实现侧几何探针：把 src/ 里每一块铬面量成和 design/ 原型同格式的表（位置、尺寸、内距、外距、圆角、
// 描边、底色、字号、字重、子元素间距），逐行比对。原型侧的同格式表由 design/tests/probe-chrome-metrics.cjs
// 在同一视口（1440×900，.app-window 实测 1390×850）下产出，两份输出按行 diff 就是待办清单。
internal static class ChromeMetricsProbe
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static MainWindow Shell = null!;

    static string Num(double value) => (Math.Round(value * 10) / 10).ToString("0.#", Inv);

    static string Box(Thickness box) => $"{Num(box.Top)} {Num(box.Right)} {Num(box.Bottom)} {Num(box.Left)}";

    static string Color(IBrush? brush) => brush is ISolidColorBrush solid
        ? $"#{solid.Color.R:x2}{solid.Color.G:x2}{solid.Color.B:x2}"
        : brush is null ? "none" : "gradient";

    static (Thickness pad, Thickness border, CornerRadius radius, IBrush? back, double font, string weight) Face(Control control)
    {
        var (pad, border, radius, back) = control switch
        {
            Border decorated => (decorated.Padding, decorated.BorderThickness, decorated.CornerRadius, decorated.Background),
            TemplatedControl templated => (templated.Padding, templated.BorderThickness, templated.CornerRadius, templated.Background),
            _ => (default, default, default, null),
        };
        var (font, weight) = control switch
        {
            TextBlock text => (text.FontSize, text.FontWeight.ToString()),
            TemplatedControl templated => (templated.FontSize, templated.FontWeight.ToString()),
            _ => (double.NaN, "n/a"),
        };
        return (pad, border, radius, back, font, weight);
    }

    static string SpacingOf(Control control) => control switch
    {
        StackPanel stack => Num(stack.Spacing),
        Grid grid => Num(Math.Max(grid.ColumnSpacing, grid.RowSpacing)),
        _ => "-",
    };

    // 与原型探针同口径：按主轴排好后取相邻子元素的空隙（重叠为负）。
    static string ChildGaps(Control control)
    {
        var kids = control.GetVisualChildren().OfType<Control>()
            .Where(k => k.IsEffectivelyVisible && k.Bounds.Width > 0.5 && k.Bounds.Height > 0.5)
            .ToArray();
        if (kids.Length < 2) return "";
        var horizontal = Math.Abs(kids[^1].Bounds.Right - kids[0].Bounds.Left) >=
                         Math.Abs(kids[^1].Bounds.Bottom - kids[0].Bounds.Top);
        var ordered = kids.OrderBy(k => horizontal ? k.Bounds.X : k.Bounds.Y).ToArray();
        return string.Join(" ", Enumerable.Range(1, ordered.Length - 1).Select(i => Num(
            horizontal ? ordered[i].Bounds.X - ordered[i - 1].Bounds.Right
                       : ordered[i].Bounds.Y - ordered[i - 1].Bounds.Bottom)));
    }

    // 位置一律折算到窗口左上角，和原型相对 .app-window 的 rect 同基准（原型另含 1px 窗框）。
    static void Row(string sel, Control? control)
    {
        if (control is null || !control.IsEffectivelyVisible) { Console.WriteLine($"MISS\t{sel}"); return; }
        var (pad, border, radius, back, font, weight) = Face(control);
        var point = control.TranslatePoint(default, Shell) ?? new Point(double.NaN, double.NaN);
        Console.WriteLine(string.Join("\t",
            sel, "vis",
            $"x{Num(point.X)}", $"y{Num(point.Y)}", $"w{Num(control.Bounds.Width)}", $"h{Num(control.Bounds.Height)}",
            $"r{Num(Shell.Bounds.Width - point.X - control.Bounds.Width)}", $"b{Num(Shell.Bounds.Height - point.Y - control.Bounds.Height)}",
            $"pad[{Box(pad)}]", $"marg[{Box(control.Margin)}]", $"rad[{Num(Math.Max(radius.TopLeft, radius.TopRight))}]",
            $"bor[{Box(border)}]", $"bg[{Color(back)}]", $"fs[{Num(font)}]", $"fw[{weight}",
            $"gap[{SpacingOf(control)}]", $"cg[{ChildGaps(control)}]"));
    }

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        Shell = window;
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        // UserControl 各自带 NameScope，window.FindControl 看不见里面的 x:Name；统一按视觉树找。
        Control? ByName(string name) => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);
        Control? ByClass(string cssClass) => window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c.Classes.Contains(cssClass) && c.IsEffectivelyVisible);
        Control? In_(Control? scope, Func<Control, bool> predicate) => scope?.GetVisualDescendants()
            .OfType<Control>().FirstOrDefault(c => predicate(c) && c.IsEffectivelyVisible && c.Bounds.Width > 0);

        var filesView = window.GetVisualDescendants().OfType<ShellFilesView>().FirstOrDefault();
        var outlineView = window.GetVisualDescendants().OfType<ShellOutlineView>().FirstOrDefault();
        var footer = In_(filesView, c => c is Border band && band.GetVisualChildren().OfType<Grid>()
            .Any(grid => grid.GetVisualChildren().OfType<Button>().Any(button => button.Name == "SortFolderButton")));

        // 原型页在 1440×900 视口内留 25px 页边，.app-window 实测 1390×850；比对就按这个尺寸量。
        window.Width = 1390; window.Height = 850;
        vm.Appearance.SelectThemeByKey("light");
        vm.Layout.IsSidebarVisible = true; LayoutSettle.Run(); Render();
        vm.Workbench.SelectLayout("read"); Render();

        void Dump(string state)
        {
            var strip = ByName("FindStrip");
            var gallery = ByName("TypographyGallery");
            Console.WriteLine($"### {state}");
            Row(".titlebar", ((Visual?)window.FindControl<ShellTitleMenuView>("TitleMenuView"))?.Parent as Control);
            Row(".brand", window.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p.Spacing == 7 && p.Bounds.Width > 0));
            Row(".tb-menu", ByName("TitleMenuView"));
            Row(".tb-menuitem", ByName("FileMenuItem"));
            Row(".nav-rail", ByName("NavRail"));
            Row(".rail-item", ByName("RailFilesToggle"));
            Row(".rail-item .ico", In_(ByName("RailFilesToggle"), c => c is Avalonia.Controls.Shapes.Path));
            Row(".sidebar", ByName("SidebarGuideTarget"));
            Row(".panel-head", In_(filesView, c => c.Classes.Contains("panel-head")));
            Row(".panel-title", In_(filesView, c => c.Classes.Contains("panel-title")));
            Row(".sidebar-search", In_(filesView, c => c.Classes.Contains("search-shell")));
            Row(".search-frame", In_(filesView, c => c.Classes.Contains("search-frame")));
            Row(".file-list", ByName("FileTree") is { IsEffectivelyVisible: true } tree ? tree : ByName("FileList"));
            // 原型 .file-row 只指文档行（夹行是另一档 .file-directory>summary），探针也按 leaf 档取首个。
            var fileRow = In_(filesView, c => c.Classes.Contains("file-card") && c.Classes.Contains("leaf"))
                          ?? In_(filesView, c => c.Classes.Contains("file-card"));
            Row(".file-row", fileRow);
            Row(".file-row .ico", In_(fileRow, c => c is Image || c is Avalonia.Controls.Shapes.Path));
            Row(".files-footer", footer);
            Row(".workspace-main", ByName("DocumentColumn"));
            Row(".find-strip", strip);
            Row(".find-strip .input", In_(strip, c => c.Classes.Contains("search-shell")));
            Row(".find-strip .search-frame", In_(strip, c => c.Classes.Contains("search-frame")));
            Row(".find-strip .btn.sm", In_(strip, c => c.Classes.Contains("find-action")));
            Row(".find-strip .icon-button", In_(strip, c => c.Classes.Contains("find-close")));
            Row(".workspace-tabrow", ByName("WorkspaceTabRow"));
            Row(".workspace-tabs", ((Visual?)window.FindControl<ToggleButton>("SourceSegment"))?.Parent as Control);
            Row(".workspace-tabs > button", ByName("SourceSegment"));
            Row(".typography-trigger", In_(ByName("WorkspaceTabRow"), c => c.Classes.Contains("typography-trigger")));
            Row(".workspace-viewswitch-more", ByName("EditorModeButton"));
            Row("#edit-options", ByName("EditorOptions"));
            Row(".format-button", In_(ByName("EditorOptions"), c => c.Classes.Contains("wb-format")));
            Row(".document-statusbar", ByClass("document-statusbar"));
            Row(".document-word-count", ByName("StatisticsButton"));
            Row(".statusbar-zoom", window.GetVisualDescendants().OfType<Vex.Controls.VexZoomDock>()
                .FirstOrDefault(d => d.IsEffectivelyVisible && d.Bounds.Width > 0));
            Row(".structure-sidebar", ByName("OutlineGuideTarget"));
            Row(".structure-search", In_(outlineView, c => c.Classes.Contains("search-shell")));
            Row(".structure-list", In_(outlineView, c => c is ListBox));
            Row(".structure-item", In_(outlineView, c => c.Classes.Contains("structure-item")));
            Row(".structure-item .marker", In_(outlineView, c => c.Classes.Contains("structure-marker")));
            Row(".structure-item .num", In_(outlineView, c => c.Classes.Contains("structure-num")));
            Row(".structure-item .text", In_(outlineView, c => c.Classes.Contains("structure-text")));
            Row(".workspace-layout-menu", ByName("LayoutGallery"));
            Row("#typography-panel", In_(gallery, c => c is Border panel && panel.Classes.Contains("wb-popover")));
            Row(".typography-grid", In_(gallery, c => c is WrapPanel));
            Row(".typography-card", In_(gallery, c => c.Classes.Contains("typography-card")));
            Row(".density-options", In_(gallery, c => c.Classes.Contains("segmented-track")));
            Row(".density-options > button", In_(gallery, c => c is Button pill && pill.Classes.Contains("render-density-pill")));
            Row("#map-settings", ByName("SettingsTarget"));
            Row("#node-inspector", ByName("NodeInspector"));
            Row(".inspector-head", In_(ByName("NodeInspector"), c => c is Border head && head.BorderThickness.Bottom > 0));
            Row(".inspector-foot", In_(ByName("NodeInspector"), c => c is Border foot && foot.BorderThickness.Top > 0));
            Console.WriteLine();
        }

        Dump("read layout, chrome closed");

        vm.FindBar.ShowFindPanel(); Render();
        Dump("find strip open");
        vm.FindBar.CloseFindPanel(); Render();

        vm.Workbench.ShowLayouts = true; Render();
        Dump("layout menu open");
        vm.Workbench.ShowLayouts = false; Render();

        vm.Workbench.ShowTypography = true; Render();
        Dump("typography panel open");
        vm.Workbench.ShowTypography = false; Render();

        vm.Layout.IsOutlineVisible = true; LayoutSettle.Run(); Render();
        Dump("outline open");
        vm.Layout.IsOutlineVisible = false; LayoutSettle.Run(); Render();

        vm.Workbench.SelectLayout("map"); Render();
        PumpUntil(() => vm.Workbench.Root is not null);
        vm.Workbench.ShowMapSettings = true; Render();
        Dump("map layout, settings open");
        vm.Workbench.ShowMapSettings = false; Render();
        if (vm.Workbench.Root?.Children.Count > 0) vm.Workbench.SelectedNode = vm.Workbench.Root.Children[0];
        Render();
        Dump("map layout, node selected");

        // 标题菜单弹出层：原型同一档由 .menu-flyout/.flyout 承担。
        var fileMenu = (MenuItem)ByName("FileMenuItem")!;
        fileMenu.IsSubMenuOpen = true; Render();
        var menuPanel = fileMenu.GetVisualDescendants().OfType<Popup>().FirstOrDefault()?.Child as Control;
        Console.WriteLine("### title menu flyout");
        Row(".flyout", menuPanel);
        Row(".menu-item", menuPanel?.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault(m => m.Bounds.Width > 0));
        foreach (var text in menuPanel?.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Bounds.Width > 0).Take(4) ?? [])
            Row("  .menu-item text", text);
        Row(".menu-sep", menuPanel?.GetVisualDescendants().OfType<Separator>().FirstOrDefault());
        fileMenu.IsSubMenuOpen = false; Render();
    }

    static void PumpUntil(Func<bool> condition)
    {
        for (var tick = 0; tick < 40 && !condition(); tick++)
        {
            Thread.Sleep(40);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
