using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
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
        var navigation = app.Resolve<ShellFilesViewModel>();
        var root = Path.Combine(output, "navigation");
        var documents = new[] {
            new DocumentFile(Path.Combine(root, "folder", "alpha.md"), "alpha.md", "folder", "", "first"),
            new DocumentFile(Path.Combine(root, "zeta.md"), "zeta.md", "navigation", "", "second") };
        navigation.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, workspaceRootPath: root)); Render();
        Check(navigation.CanCollapseAll, "Expanded folders enable collapse action");
        navigation.CollapseAll(); Render();
        Check(!navigation.CanCollapseAll && !navigation.DocumentFileNodes[0].IsExpanded, "Collapse updates actual tree expansion state");
        navigation.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, workspaceRootPath: root));
        Check(!navigation.DocumentFileNodes[0].IsExpanded, "Folder refresh preserves collapsed state");
        navigation.ToggleListView(); Render();
        Check(navigation.IsListView && navigation.FlatDocumentFileNodes.Count == 2, "List view flattens nested files");
        navigation.ToggleSort();
        Check(navigation.FlatDocumentFileNodes[0].Name == "zeta.md", "Sort applies to flattened file list");
        navigation.FilterText = "first";
        Check(navigation.FlatDocumentFileNodes.Count == 1 && navigation.FlatDocumentFileNodes[0].Name == "alpha.md", "File search includes document summaries in both views");
        navigation.ClearFilter();
        navigation.SelectDocumentFileForContextMenu(navigation.DocumentFileNodes.First(node => node.IsFolder));
        Check(navigation.HasPathCommandTarget && !navigation.HasDocumentFileCommandTarget, "Folder context supports path actions without file mutation actions");
        var launch = ShellProcessLauncher.CreateStartInfo(Path.Combine(root, "a b.md"));
        Check(launch.ArgumentList[^1] == Path.Combine(root, "a b.md"), "New-window launch passes paths as a single argument");
        Render(); window.CaptureRenderedFrame()?.Save(Path.Combine(output, "desktop-outline.png"));
    }
}
