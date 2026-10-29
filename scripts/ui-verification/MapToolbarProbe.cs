using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

/// <summary>
/// 一次性探针：导图工具行对照原型 workspace-chrome.css:494 —— 单行 36px、溢出横滚且滚动条隐藏，
/// 不再换行成两档高度；宽时右组贴右（flex margin-left:auto 等效），窄时整行可滚到最后一个按钮。
/// </summary>
internal static class MapToolbarProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1280; window.Height = 800;
        app.Resolve<IWorkspaceDocumentState>().UpdateDocument("# 导图工具行\n\n## 甲\n\n正文一。\n\n## 乙\n\n正文二。\n\n### 丙\n\n正文三。", null);
        vm.Workbench.SelectLayout("map"); Render();
        var host = window.GetVisualDescendants().OfType<MarkdownMindMapHost>().First(h => h.IsEffectivelyVisible);
        var bar = host.FindControl<Border>("MapEditingToolbar")!;
        var scroll = host.FindControl<ScrollViewer>("MapToolsScroll")!;
        var right = host.FindControl<StackPanel>("MapToolsRight")!;
        var pin = host.FindControl<Control>("PinMap")!;
        var tools = bar.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && (c.Classes.Contains("map-tool") || c.Classes.Contains("toolbar-pin"))).ToArray();
        Check(Math.Abs(bar.Bounds.Height - 36) < 1 && tools.Length >= 6,
            $"Map toolbar is the prototype single 36px row with every tool on it: height={bar.Bounds.Height:0.#}, tools={tools.Length}");
        Check(scroll.GetVisualDescendants().OfType<ScrollBar>().All(b => !b.IsEffectivelyVisible),
            "The overflow scrollbar stays hidden like the prototype scrollbar-width:none");
        void CheckSingleRow(string label)
        {
            // 原型 align-items:center：按钮高度不一（钉钮更矮），单行的判据是中心共线而不是顶边对齐。
            var tops = tools.Select(t =>
            {
                var origin = t.TranslatePoint(default, bar)!.Value;
                return origin.Y + t.Bounds.Height / 2;
            }).ToArray();
            Check(tops.Max() - tops.Min() < 1 && Math.Abs(bar.Bounds.Height - 36) < 1,
                $"Every tool stays on one 36px row at {label}: spread={tops.Max() - tops.Min():0.#}, height={bar.Bounds.Height:0.#}");
        }
        CheckSingleRow("1280px");
        Check(scroll.Extent.Width <= scroll.Viewport.Width + 1
            && Math.Abs(right.Bounds.Right - scroll.Viewport.Width) < 1.5,
            $"Wide window right-aligns the tool group without overflow: extent={scroll.Extent.Width:0.#}, viewport={scroll.Viewport.Width:0.#}, right={right.Bounds.Right:0.#}");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "map-toolbar-wide.png"));
        foreach (var width in new[] { 800, 640 })
        {
            window.Width = width; Render();
            CheckSingleRow(width + "px");
            Check(scroll.Extent.Width > scroll.Viewport.Width + 1,
                $"Narrow window overflows into horizontal scroll instead of a second row: extent={scroll.Extent.Width:0.#}, viewport={scroll.Viewport.Width:0.#}");
            scroll.Offset = new Vector(scroll.Extent.Width - scroll.Viewport.Width, 0); Render();
            var pinRight = pin.TranslatePoint(new Point(pin.Bounds.Width, 0), scroll)!.Value.X;
            Check(pinRight <= scroll.Viewport.Width + 1,
                $"Scrolling to the end brings the last tool fully into view at {width}px: pinRight={pinRight:0.#}, viewport={scroll.Viewport.Width:0.#}");
        }
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "map-toolbar-narrow.png"));
        scroll.Offset = new Vector(0, 0);
        window.Width = 1280; Render();
        Check(Math.Abs(right.Bounds.Right - scroll.Viewport.Width) < 1.5,
            "Restoring the wide window right-aligns the tool group again");
        Console.WriteLine("PASS map toolbar probe complete");
    }
}