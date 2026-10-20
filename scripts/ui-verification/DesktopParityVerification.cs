using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class DesktopParityVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var layout = vm.Layout;
        var outline = app.Resolve<ShellOutlineViewModel>();
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        window.Width = 1280; window.Height = 820; Render();
        Check(window.GetVisualDescendants().OfType<ShellFilesView>().Count() == 1, "Left file navigation resolves once");
        Check(window.GetVisualDescendants().OfType<ShellOutlineView>().Count() == 1, "Right outline resolves once");
        var files = window.FindControl<Border>("SidebarGuideTarget")!;
        var right = window.FindControl<Border>("OutlineGuideTarget")!;
        Check(files.Bounds.Width >= 200 && right.Bounds.Width >= 180, "Both independent sidebars are visible");
        var fileX = files.TranslatePoint(default, window)!.Value.X;
        Check(right.TranslatePoint(default, window)!.Value.X > fileX + 500, "Outline sits to the right of the document");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "desktop-wide.png"));
        window.Width = 800; window.Height = 480; Render();
        Check(!layout.ShowOutlineSidebar && layout.IsOutlineVisible, "Narrow window collapses outline without overwriting preference");
        layout.ToggleOutline(); Render();
        Check(layout.ShowOutlineSidebar, "Manual expansion overrides automatic collapse in a narrow window");
        layout.ToggleOutline(); Render();
        Check(!layout.ShowOutlineSidebar, "Outline can be closed from the narrow window");
        window.Width = 1280; Render(); layout.ShowOutline(); Render();
        layout.OutlineWidth = 280; Render();
        Check(Math.Abs(right.Bounds.Width - 280) < 1 && app.Resolve<IAppSettingsStore>().Current.OutlineWidth == 280, "Outline resizing persists independently");
        layout.IsFocusMode = true; Render();
        Check(!layout.ShowOutlineSidebar && !layout.ShowSidebar, "Focus hides both navigation panes");
        layout.IsFocusMode = false; Render();
        Check(layout.ShowOutlineSidebar && layout.ShowSidebar, "Focus exit restores both navigation panes");
        layout.IsPresentationMode = true; Render();
        Check(!layout.ShowOutlineSidebar, "Presentation hides the outline without changing preference");
        layout.IsPresentationMode = false;
        outline.ApplyOutlineItemsChanged(new OutlineItemsChangedCommand([
            new(1, "第一章", 1), new(3, "细节", 4), new(3, "补充", 7), new(2, "第二节", 10), new(1, "下一章", 12)]));
        Check(string.Join(',', outline.OutlineItems.Select(item => item.Number)) == "1,1.0.1,1.0.2,1.1,2", "Heading numbers preserve all source heading levels");
        outline.FilterText = "细节";
        Check(outline.FilteredOutlineItems.Count == 1 && outline.FilteredOutlineItems[0].Line == 4, "Outline filter keeps source positions");
        outline.ClearFilter();
        Check(outline.FilteredOutlineItems.Count == 5, "Clearing heading filter restores the outline");
        Render(); window.CaptureRenderedFrame()?.Save(Path.Combine(output, "desktop-outline.png"));
    }
}
