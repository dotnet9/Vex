using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Ursa.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 格式条布局探针：导出工具条与各键的期望/实际宽度、坐标与溢出判定，
/// 用于回归验证 15 键（含 span 三键）在桌面窗口内显示或进入溢出菜单。
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

        window.Width = 1440;
        window.Height = 900;
        vm.Appearance.SelectThemeByKey("light");
        vm.Layout.IsSidebarVisible = true;
        vm.Workbench.SelectLayout("instant");
        Render();

        var options = window.Workspace.FindControl<ShellEditorOptionsView>("EditorOptions")!;
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
                $"  [{i++}] {child.GetType().Name} name={child.Name} desired={child.DesiredSize.Width:F1} {B(child)} origin={child.TranslatePoint(default, window)} margin={child.Margin} overflow={overflow}");
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

        vm.Workbench.SelectLayout("source-render");
        Render();
        Console.WriteLine($"=== source-render toolbar {B(toolbar)} options={B(options)} ===");
        foreach (var child in toolbar.GetLogicalChildren().OfType<Control>())
            Console.WriteLine($"  name={child.Name} desired={child.DesiredSize} actual={B(child)} origin={child.TranslatePoint(default, window)} overflow={getOverflow?.Invoke(null, new object?[] { child })}");
        if (grid is not null)
            foreach (var child in grid.Children) Console.WriteLine($"  grid {child.GetType().Name} {B(child)} margin={child.Margin}");
        foreach (var name in new[] { "SpanFontSize", "SpanForeground", "SpanBackground" })
        {
            var button = options.FindControl<Button>(name)!;
            if (button.Padding != default || getOverflow?.Invoke(null, new object?[] { button }) is true
                || button.Bounds.Size != new Size(28, 28)
                || button.Content is not Control content || content.Bounds.Width > button.Bounds.Width)
                throw new InvalidOperationException($"Split toolbar keeps {name} fully visible without clipping its glyph/arrow.");
            Console.WriteLine($"PASS source-render {name} fits its 28px button and remains outside overflow");
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
        var overflowToggle = toolbar.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name == "button");
        var point = overflowToggle.TranslatePoint(new Point(overflowToggle.Bounds.Width / 2, overflowToggle.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Render();
        var overflowPopup = toolbar.GetVisualDescendants().OfType<Popup>().Single();
        if (!overflowPopup.IsOpen || overflowPopup.Child is not Control { Bounds.Width: > 0, Bounds.Height: > 0 } overflowContent
            || !overflowContent.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "SpanBackground"))
            throw new InvalidOperationException("Narrow split toolbar overflow opens with the remaining color action reachable.");
        Console.WriteLine("PASS real pointer opens narrow toolbar overflow with all remaining style actions");
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Render();
        if (overflowPopup.IsOpen) throw new InvalidOperationException("Toolbar overflow closes on a second pointer click.");
        Console.WriteLine("PROBE DONE");
    }
}
