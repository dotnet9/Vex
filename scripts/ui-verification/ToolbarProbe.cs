using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 一次性诊断探针（临时，不随修复提交）：导出格式条可视化树各级宽度与
/// ToolBarPanel 的溢出判定输入，定位 1380 宽度下 span 四键被收编的原因。
/// </summary>
internal static class ToolbarProbe
{
    private static string B(Visual v) => v is Control c
        ? $"{c.Bounds.Width:F1}x{c.Bounds.Height:F1}@{c.Bounds.X:F0},{c.Bounds.Y:F0} vis={c.IsVisible}"
        : "-";

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        window.Width = 1380;
        window.Height = 850;
        vm.Appearance.SelectThemeByKey("dark");
        vm.Layout.IsSidebarVisible = true;
        vm.Workbench.SelectLayout("instant");
        Render();

        var options = window.FindControl<ShellEditorOptionsView>("EditorOptions")!;
        Console.WriteLine("=== ancestors of ShellEditorOptionsView ===");
        Visual v = options;
        int depth = 0;
        while (v is not null && depth < 24)
        {
            Console.WriteLine($"  [{depth}] {v.GetType().Name} {B(v)}");
            v = v.GetVisualParent();
            depth++;
        }

        var toolbar = options.FindControl<ToolBar>("FormattingToolbar")!;
        Console.WriteLine($"=== toolbar {B(toolbar)} margin={toolbar.Margin} hal={toolbar.HorizontalAlignment} maxw={toolbar.MaxWidth} ===");
        foreach (var d in new[] { "PART_BackgroundBorder", "PART_PopupButtonPanel", "button" })
        {
            var ctl = toolbar.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == d);
            Console.WriteLine($"  tpl {d}: {(ctl is null ? "null" : B(ctl))}");
        }
        var presenter = toolbar.GetVisualDescendants().OfType<ItemsPresenter>().FirstOrDefault();
        Console.WriteLine($"  ItemsPresenter: {(presenter is null ? "null" : B(presenter))}");
        var panel = toolbar.GetVisualDescendants().OfType<ToolBarPanel>().FirstOrDefault();
        Console.WriteLine($"  ToolBarPanel: {(panel is null ? "null" : B(panel))} children={panel?.Children.Count}");

        var getOverflow = typeof(ToolBar).GetMethod("GetIsOverflowItem", BindingFlags.NonPublic | BindingFlags.Static);
        Console.WriteLine("=== toolbar logical children ===");
        int i = 0;
        foreach (var child in toolbar.GetLogicalChildren().OfType<Control>())
        {
            var overflow = getOverflow?.Invoke(null, new object?[] { child });
            Console.WriteLine(
                $"  [{i++}] {child.GetType().Name} name={child.Name} desired={child.DesiredSize.Width:F1} {B(child)} overflow={overflow}");
        }

        var grid = options.GetVisualDescendants().OfType<Grid>().FirstOrDefault(g => g.ColumnDefinitions.Count == 2);
        if (grid is not null)
        {
            Console.WriteLine($"=== options grid {B(grid)} ===");
            foreach (var cd in grid.ColumnDefinitions)
                Console.WriteLine($"  col {cd.Width} actual={cd.ActualWidth:F1}");
            foreach (var child in grid.Children)
                Console.WriteLine($"  child {child.GetType().Name} {B(child)}");
        }

        window.Width = 980;
        Render();
        Console.WriteLine("=== after resize to 980 ===");
        Console.WriteLine($"  toolbar {B(toolbar)} panel={(panel is null ? "null" : B(panel))}");
        i = 0;
        foreach (var child in toolbar.GetLogicalChildren().OfType<Control>())
        {
            var overflow = getOverflow?.Invoke(null, new object?[] { child });
            Console.WriteLine($"  [{i++}] name={child.Name} overflow={overflow} vis={child.IsVisible}");
        }
        Console.WriteLine("PROBE DONE");
    }
}
