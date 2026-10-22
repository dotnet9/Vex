using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class PrototypeChromeVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        Check(!vm.Layout.IsOutlineVisible, "Auxiliary panel starts closed for a new workspace");
        app.Resolve<IWorkspaceDocumentState>().UpdateDocument("# 在文字之间，理清思路\n\n一篇文章，从一个想法开始。这里保留完整的原文。\n\n## 写作，让想法有迹可循\n\n先记下观察，再整理线索。\n\n> 好的工具，让注意力回到内容本身。\n\n| API | 说明 |\n| --- | --- |\n| Read | 读取文档 |", null);
        var original = vm.Markdown; var history = vm.Workbench.History.Capture();
        var layoutButton = window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")!.FindControl<Button>("LayoutButton")!;
        var formats = window.FindControl<ShellEditorOptionsView>("EditorOptions")!;
        foreach (var size in new[] { new Size(800,480), new Size(1024,768), new Size(1440,1000) })
        {
            window.Width = size.Width; window.Height = size.Height; Render();
            foreach (var layout in WorkbenchLayoutOption.All)
            {
                vm.Workbench.SelectLayout(layout.Id); Render();
                Check(vm.Workbench.LayoutName == layout.Name && vm.Markdown == original, "Current layout and document stay synchronized: " + size.Width + "/" + layout.Id);
            }
            vm.Workbench.SelectLayout("instant"); Render();
            var statusChrome = window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")!;
            var viewSwitch = statusChrome.FindControl<Border>("ViewSwitch")!;
            var sourceSegment = statusChrome.FindControl<Button>("SourceSegment")!;
            var readSegment = statusChrome.FindControl<Button>("ReadSegment")!;
            var mapSegment = statusChrome.FindControl<Button>("MapSegment")!;
            var switchCenter = viewSwitch.TranslatePoint(new Point(viewSwitch.Bounds.Width / 2, 0), statusChrome)!.Value.X;
            Check(Math.Abs(switchCenter - statusChrome.Bounds.Width / 2) < 2,
                $"View switch group stays centered at {size.Width}: center={switchCenter}, status={statusChrome.Bounds.Width}");
            Check(!sourceSegment.Classes.Contains("active") && !readSegment.Classes.Contains("active") && !mapSegment.Classes.Contains("active"),
                "Combined layouts keep every status segment inactive");
            sourceSegment.Command!.Execute(null); Render();
            Check(vm.Workbench.WorkspaceLayout == "source" && sourceSegment.Classes.Contains("active") && !readSegment.Classes.Contains("active") && !mapSegment.Classes.Contains("active"),
                "Source segment selects the pure source view and highlights itself");
            mapSegment.Command!.Execute(null); Render();
            Check(vm.Workbench.WorkspaceLayout == "map" && mapSegment.Classes.Contains("active"), "Map segment selects the mind map view");
            readSegment.Command!.Execute(null); Render();
            Check(vm.Workbench.WorkspaceLayout == "read" && readSegment.Classes.Contains("active"), "Read segment selects the reading view");
            vm.Workbench.SelectLayout("instant"); Render();
            var formatting = formats.FindControl<Ursa.Controls.ToolBar>("FormattingToolbar")!;
            var origin = formatting.TranslatePoint(default, formats);
            Check(origin is not null && origin.Value.X >= 0 && formatting.Bounds.Width <= formats.Bounds.Width,
                $"Formatting toolbar stays inside the document at {size.Width}: origin={origin?.ToString() ?? "null"} toolbar={formatting.Bounds} host={formats.Bounds}");
            var status = window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")!;
            Check(status.TranslatePoint(default, window)!.Value.X >= 200 && Math.Abs(status.Bounds.Height - 34) < 1,
                $"Document corner controls leave file navigation full height: {status.TranslatePoint(default, window)} / {status.Bounds}");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "workspace-" + size.Width + ".png"));
            vm.Workbench.ShowLayouts = true; Render();
            var gallery = window.FindControl<WorkspaceLayoutGallery>("LayoutGallery")!;
            var choices = gallery.GetVisualDescendants().OfType<Button>().Where(b => b.Tag is string id && WorkbenchLayoutOption.All.Any(o => o.Id == id)).ToArray();
            Check(choices.Length == 8 && gallery.Bounds.Height <= window.ClientSize.Height - 120, "All eight layouts fit the available panel at " + size.Width);
            choices[0].Focus(); window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
            Check(vm.Workbench.WorkspaceLayout == "instant", "Browsing layouts does not apply a choice");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Render();
            Check(!vm.Workbench.ShowLayouts && layoutButton.IsFocused, "Escape closes layout panel and returns focus");
        }
        foreach (var culture in vm.Appearance.LanguageOptions)
        {
            vm.Appearance.SelectedLanguage = culture; Render();
            foreach (var name in new[] { "FileMenuItem", "InsertMenuItem", "HelpMenuItem" })
            {
                var item = window.FindControl<ShellTitleMenuView>("TitleMenuView")!.FindControl<MenuItem>(name)!;
                item.IsSubMenuOpen = true; Render();
                var popup = item.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
                Check(popup?.Child is { Bounds.Width: 248 }, "Title menu has a 248px panel: " + culture.CultureName + "/" + name);
                item.IsSubMenuOpen = false; Render();
            }
        }
        Check(vm.Markdown == original && vm.Workbench.History.Capture().Undo.SequenceEqual(history.Undo), "Layout and language changes preserve text and undo history");
    }
}
