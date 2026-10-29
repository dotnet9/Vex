using System.Linq;
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
            LayoutSettle.Run(); window.UpdateLayout();
        }
        window.Width = 1280; window.Height = 820; Render();
        Check(window.GetVisualDescendants().OfType<ShellFilesView>().Count() == 1, "Left file navigation resolves once");
        var files = window.FindControl<Border>("SidebarGuideTarget")!;
        var right = window.FindControl<Border>("OutlineGuideTarget")!;
        Check(!layout.IsOutlineVisible, "New workspace keeps the auxiliary outline closed by default");
        layout.ShowOutline(); Render();
        Check(window.GetVisualDescendants().OfType<ShellOutlineView>().Count() == 1, "Right outline resolves once");
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
        outline.OutlineItems[0].IsExpanded = false;
        Check(outline.FilteredOutlineItems.Count == 2, "Collapsing a section hides all descendants including skipped levels");
        outline.FilterText = "细节";
        Check(outline.FilteredOutlineItems.Count == 1, "Search reveals a heading under a collapsed section");
        outline.ClearFilter();
        Check(outline.FilteredOutlineItems.Count == 2, "Clearing search preserves the collapsed section");
        var restoredOutline = new ShellOutlineViewModel(app.Resolve<IShellStatusPublisher>(), app.Resolve<IAppLocalizer>(),
            app.Resolve<IAppSettingsStore>(), app.Resolve<IWorkspaceDocumentState>());
        restoredOutline.ApplyOutlineItemsChanged(new OutlineItemsChangedCommand([
            new(1, "第一章", 3), new(3, "细节", 6), new(3, "补充", 9), new(2, "第二节", 12), new(1, "下一章", 14)]));
        Check(restoredOutline.FilteredOutlineItems.Count == 2, "New outline instance restores collapse after source lines shift");
        var filesView = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        var productionFiles = filesView.DataContext;
        using var navigation = new ShellFilesViewModel(app.Resolve<IAppSettingsStore>());
        filesView.DataContext = navigation;
        var root = Path.Combine(output, "navigation");
        var documents = new[] {
            new DocumentFile(Path.Combine(root, "folder", "alpha.md"), "alpha.md", "folder", "", "first"),
            new DocumentFile(Path.Combine(root, "zeta.md"), "zeta.md", "navigation", "", "second") };
        navigation.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, workspaceRootPath: root)); Render();
        Check(navigation.CanCollapseAll && navigation.DocumentFileNodes[0].IsExpanded
            && navigation.DocumentFileNodes[0].Children.Where(node => node.IsFolder).All(node => !node.IsExpanded),
            "New file workspace expands only its root");
        Check(navigation.CanCollapseAll, "Expanded folders enable collapse action");
        navigation.CollapseAll(); Render();
        Check(!navigation.CanCollapseAll && !navigation.DocumentFileNodes[0].IsExpanded, "Collapse updates actual tree expansion state");
        navigation.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, workspaceRootPath: root));
        Check(!navigation.DocumentFileNodes[0].IsExpanded, "Folder refresh preserves the manually collapsed root state");
        using var restoredNavigation = new ShellFilesViewModel(app.Resolve<IAppSettingsStore>());
        restoredNavigation.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, workspaceRootPath: root));
        Check(restoredNavigation.DocumentFileNodes[0].IsExpanded, "New file navigation starts with the root expanded despite old collapse settings");
        navigation.ToggleListView(); Render();
        Check(navigation.IsListView && navigation.FlatDocumentFileNodes.Count == 2, "List view flattens nested files");
        Check(navigation.FlatDocumentFileNodes.First(n => n.Name == "alpha.md").FolderTag == "folder"
            && navigation.FlatDocumentFileNodes.First(n => n.Name == "zeta.md").FolderTag == "",
            "List rows carry a relative folder tag for nested files and none for root files");
        var fileList = filesView.FindControl<ListBox>("FileList")!;
        var listRows = fileList.GetVisualDescendants().OfType<ListBoxItem>().ToArray();
        TextBlock? NameOf(ListBoxItem row) => row.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("file-list-name"));
        TextBlock? TagOf(ListBoxItem row) => row.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Classes.Contains("file-folder-tag"));
        var alphaRow = listRows.FirstOrDefault(r => NameOf(r)?.Text == "alpha.md");
        var zetaRow = listRows.FirstOrDefault(r => NameOf(r)?.Text == "zeta.md");
        Check(listRows.Length == 2 && listRows.All(r => Math.Abs(r.Bounds.Height - 24) < 1)
            && alphaRow is not null && zetaRow is not null
            && TagOf(alphaRow) is { IsVisible: true, Text: "folder" } && TagOf(zetaRow) is { IsVisible: false },
            "List rows are 24px single lines; only the nested file shows its folder tag");
        navigation.SelectedListDocumentFileNode = navigation.FlatDocumentFileNodes.First(n => n.Name == "alpha.md"); Render();
        var selectedRow = fileList.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.IsSelected);
        var rowPresenter = selectedRow.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(c => c.Name == "PART_ContentPresenter");
        Application.Current!.TryGetResource("VexAccentSoftBrush", window.ActualThemeVariant, out var accentSoftResource);
        Check(rowPresenter.Background is Avalonia.Media.ISolidColorBrush rowBg
            && accentSoftResource is Avalonia.Media.ISolidColorBrush accentSoft && rowBg.Color == accentSoft.Color
            && Math.Abs(rowPresenter.Bounds.Width - selectedRow.Bounds.Width) < 1,
            "Selected list row paints a full-bleed accent-soft background");
        navigation.SetSortKey("natural-desc");
        var sortDeadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < sortDeadline
            && navigation.FlatDocumentFileNodes.Count > 0 && navigation.FlatDocumentFileNodes[0].Name != "zeta.md")
        {
            Render();
        }
        Check(navigation.FlatDocumentFileNodes.Count > 0 && navigation.FlatDocumentFileNodes[0].Name == "zeta.md", "Sort applies to flattened file list: got [" + string.Join(", ", navigation.FlatDocumentFileNodes.Select(n => n.Name)) + "]");
        navigation.FilterText = "first";
        Check(navigation.FlatDocumentFileNodes.Count == 0, "File search follows the prototype and only matches names");
        navigation.FilterText = "alpha";
        Check(navigation.FlatDocumentFileNodes.Count == 1 && navigation.FlatDocumentFileNodes[0].Name == "alpha.md", "File search keeps matching filenames and their ancestors");
        navigation.ClearFilter();
        // 搜索焦点单环（原型 .sidebar-search:focus-within）：聚焦内层输入 → 主框换 accent 实边、
        // 3px accent 18% 柔光环显形、悬停混边收起；失焦回到 control-border，环与混边都隐。
        var searchShell = filesView.GetVisualDescendants().OfType<Grid>().First(g => g.Classes.Contains("search-shell"));
        var searchFrame = searchShell.FindControl<Border>("FileSearchBox")!;
        var searchGlow = searchShell.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("search-glow"));
        var searchHover = searchShell.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("search-hover"));
        Application.Current!.TryGetResource("VexAccentBrush", window.ActualThemeVariant, out var accentResource);
        Application.Current!.TryGetResource("VexControlBorderBrush", window.ActualThemeVariant, out var controlBorderResource);
        var accentRef = (Avalonia.Media.ISolidColorBrush)accentResource!;
        var idleRef = (Avalonia.Media.ISolidColorBrush)controlBorderResource!;
        var searchText = searchFrame.GetVisualDescendants().OfType<TextBox>().First();
        Render();
        Check(searchFrame.CornerRadius.TopLeft == 16 && !searchGlow.IsVisible && !searchHover.IsVisible
            && searchFrame.BorderBrush is Avalonia.Media.ISolidColorBrush idleBorder && idleBorder.Color == idleRef.Color,
            "Idle search box is a pill on control-border with no glow or hover tint");
        searchText.Focus(); Render();
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "desktop-search-focus.png"));
        Check(searchFrame.BorderBrush is Avalonia.Media.ISolidColorBrush focusBorder && focusBorder.Color == accentRef.Color
            && searchGlow.IsVisible && !searchHover.IsVisible
            && searchGlow.BorderThickness == new Thickness(3) && searchGlow.Margin == new Thickness(-3)
            && searchGlow.BorderBrush is Avalonia.Media.ISolidColorBrush glowBrush
            && glowBrush.Color == accentRef.Color && Math.Abs(glowBrush.Opacity - 0.18) < 0.001,
            "Focused search box paints a single accent border plus a 3px accent-18% outset glow ring");
        filesView.FindControl<Button>("SortFolderButton")!.Focus(); Render();
        Check(!searchGlow.IsVisible && !searchHover.IsVisible
            && searchFrame.BorderBrush is Avalonia.Media.ISolidColorBrush restoredBorder && restoredBorder.Color == idleRef.Color,
            "Leaving the search box restores the idle control-border and hides the glow ring");
        // 面板头（原型 .panel-head/.panel-title）：两栏标题都在各自搜索框之上，12/14/9 内距、600 字重 12px。
        var localizer = app.Resolve<IAppLocalizer>();
        var filesHead = filesView.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("panel-title"));
        Check(filesHead.Text == localizer.Get(Vex.VexL.SidebarFiles) && filesHead.FontSize == 12
            && filesHead.FontWeight == Avalonia.Media.FontWeight.SemiBold
            && ((Grid)filesHead.Parent!).Margin == new Thickness(14, 12, 14, 9)
            && filesHead.TranslatePoint(default, filesView)!.Value.Y < searchShell.TranslatePoint(default, filesView)!.Value.Y,
            "Files panel head titles the pane above the search box at the prototype 12/14/9 inset");
        var auxPanel = window.GetVisualDescendants().OfType<ShellAuxiliaryPanelView>().Single();
        var structureHead = auxPanel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("panel-title"));
        var outlineShell = auxPanel.GetVisualDescendants().OfType<Grid>().First(g => g.Classes.Contains("search-shell"));
        Check(structureHead.Text == localizer.Get(Vex.VexL.StructureTitle)
            && ((Grid)structureHead.Parent!).Margin == new Thickness(14, 12, 14, 9)
            && structureHead.TranslatePoint(default, auxPanel)!.Value.Y < outlineShell.TranslatePoint(default, auxPanel)!.Value.Y,
            "Structure panel head shares the same title row metrics above its search box");
        navigation.SelectDocumentFileForContextMenu(navigation.DocumentFileNodes.First(node => node.IsFolder));
        Check(navigation.HasPathCommandTarget && !navigation.HasDocumentFileCommandTarget, "Folder context supports path actions without file mutation actions");
        var launch = ShellProcessLauncher.CreateStartInfo(Path.Combine(root, "a b.md"));
        Check(launch.ArgumentList[^1] == Path.Combine(root, "a b.md"), "New-window launch passes paths as a single argument");
        layout.IsOutlineVisible = true; Render();
        Check(layout.ShowOutlineSidebar && new ShellWindowLayoutViewModel(app.Resolve<IAppSettingsStore>(), app.Resolve<IShellStatusPublisher>()).IsOutlineVisible,
            "The outline sidebar is a single panel without tabs and its visibility persists");
        Render(); window.CaptureRenderedFrame()?.Save(Path.Combine(output, "desktop-outline.png"));
        filesView.DataContext = productionFiles;
    }
}
