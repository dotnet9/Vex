using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class GitBadgeVerification
{
    /// <summary>
    /// 文件树 Git 前导状态槽 UI 验证（design/Git-Badges.html REV2 的落地端到端）：
    /// 受管干净=灰锁 / 修改=红勾 / 新增=绿加号（树内）/ 重命名=红勾（VS 经典）/
    /// 未跟踪=空槽 / 干净文件夹无字形 / 差量清零 / 深浅两档换色 / 树与列表双模板一致。
    /// 服务采集半边（受管干净=索引命中）由 Vex.Mcp.Tests 集成测试覆盖。
    /// </summary>
    public static void Run(VerificationVexApp app, Window window, string output)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }

        var filesView = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        var productionFiles = filesView.DataContext;
        using var files = new ShellFilesViewModel();
        filesView.DataContext = files;

        var root = Path.Combine(output, "git-badge-workspace");
        Directory.CreateDirectory(Path.Combine(root, "docs"));
        var readme = new DocumentFile(Path.Combine(root, "README.md"), "README.md", "", "readme", "Badge");
        var draft = new DocumentFile(Path.Combine(root, "draft.md"), "draft.md", "", "", "Badge");
        var rule = new DocumentFile(Path.Combine(root, "docs", "rule.md"), "rule.md", "docs", "", "Badge");
        var keep = new DocumentFile(Path.Combine(root, "keep.md"), "keep.md", "", "", "Badge");
        DocumentFile[] samples = [readme, draft, rule, keep];
        files.ApplyDocumentFilesChanged(new Vex.Core.Messaging.DocumentFilesChangedCommand(samples, readme, root));
        files.DocumentFileNodes[0].IsExpanded = true;
        files.DocumentFileNodes[0].Children.Single(node => node.IsFolder).IsExpanded = true;
        Render();

        var tree = window.GetVisualDescendants().OfType<TreeView>().Single(control => control.Classes.Contains("side-tree"));

        static List<Avalonia.Controls.Shapes.Path> VisibleLeads(Control container) => container.GetVisualDescendants()
            .OfType<Avalonia.Controls.Shapes.Path>()
            .Where(path => path.Classes.Contains("git-lead") && path.IsEffectivelyVisible)
            .ToList();
        Avalonia.Controls.Shapes.Path? LeadOf(Control container, string name)
        {
            var item = container.GetVisualDescendants().OfType<TreeViewItem>()
                .FirstOrDefault(entry => entry.DataContext is DocumentFileNode node && node.Name == name)
                ?? (Control?)container.GetVisualDescendants().OfType<ListBoxItem>()
                    .FirstOrDefault(entry => entry.DataContext is DocumentFileNode node && node.Name == name);
            return item?.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
                .FirstOrDefault(path => path.Classes.Contains("git-lead"));
        }

        // ===== 基线：全 None（未管控/无状态）不渲染任何前导字形 =====
        Check(VisibleLeads(tree).Count == 0, "Tree renders no leading glyphs when every node is None");

        // ===== 状态应用：变更字形 + 挂锁 + 未跟踪空槽 + 文件夹只聚合变更 =====
        var statuses = new Dictionary<string, WorkspaceGitStatus>(StringComparer.OrdinalIgnoreCase)
        {
            [readme.Path] = WorkspaceGitStatus.Modified,
            [draft.Path] = WorkspaceGitStatus.Untracked,
            [rule.Path] = WorkspaceGitStatus.Renamed,
            [keep.Path] = WorkspaceGitStatus.TrackedClean,
        };
        WorkspaceGitStatusTree.Apply(files.DocumentFileNodes, statuses);
        Render();
        var readmeLead = LeadOf(tree, "README.md");
        var draftLead = LeadOf(tree, "draft.md");
        var ruleLead = LeadOf(tree, "rule.md");
        var keepLead = LeadOf(tree, "keep.md");
        var rootFolderLead = LeadOf(tree, "git-badge-workspace");
        var docsFolderLead = LeadOf(tree, "docs");
        Check(readmeLead is { } && ruleLead is { } && keepLead is { }
            && rootFolderLead is { } && docsFolderLead is { }, "Changed files, tracked-clean file and change folders render leading glyphs");
        Check(draftLead is { } && !draftLead.IsEffectivelyVisible,
            "Untracked row keeps an empty leading slot (not under git control)");
        Check(VisibleLeads(tree).Count == 5, "Exactly five leading glyphs render (four rows plus root folder)");

        // 聚合：根文件夹取最强变更（修改>重命名），docs 取重命名；挂锁与未跟踪不参与。
        var fileCheck = readmeLead!;
        var renamedCheck = ruleLead!;
        var lockGlyph = keepLead!;
        Check(ReferenceEquals(rootFolderLead!.Fill, fileCheck.Fill), "Root folder aggregates the strongest change (modified)");
        Check(ReferenceEquals(docsFolderLead!.Fill, renamedCheck.Fill), "Docs folder aggregates its renamed child");
        // 修改与重命名同走红勾（VS 经典口径）；锁与勾字形互异。
        Check(ReferenceEquals(fileCheck.Data, renamedCheck.Data), "Modified and renamed share the red check glyph in the tree");
        Check(!ReferenceEquals(fileCheck.Data, lockGlyph.Data), "Lock glyph differs from the check glyph");
        Check(fileCheck.Data is Geometry && lockGlyph.Data is Geometry && fileCheck.Fill is IBrush && lockGlyph.Fill is IBrush,
            "Leading glyphs bind resolved octicon geometries and themed brushes");
        Check(Math.Abs(fileCheck.Bounds.Width - 11) < .01 && Math.Abs(fileCheck.Bounds.Height - 11) < .01,
            "Leading glyphs render at the compact 11 px slot size");

        // ===== 差量清零：应用空字典后全部消失（含挂锁） =====
        WorkspaceGitStatusTree.Apply(files.DocumentFileNodes,
            new Dictionary<string, WorkspaceGitStatus>(StringComparer.OrdinalIgnoreCase));
        Render();
        Check(VisibleLeads(tree).Count == 0, "Reapplying an empty dictionary hides every leading glyph via INPC");
        WorkspaceGitStatusTree.Apply(files.DocumentFileNodes, statuses);
        Render();
        Check(VisibleLeads(tree).Count == 5, "Leading glyphs reappear when statuses are applied again");

        // ===== 深浅两档：同状态不同刷 =====
        var main = (MainWindowViewModel)window.DataContext!;
        var lightFill = fileCheck.Fill;
        main.Appearance.SelectThemeByKey("dark"); Render();
        var darkFill = LeadOf(tree, "README.md")!.Fill;
        Check(lightFill is IBrush && darkFill is IBrush && !ReferenceEquals(lightFill, darkFill),
            "Leading glyph fill follows the active theme (light and dark check brushes differ)");
        main.Appearance.SelectThemeByKey("light"); Render();

        // ===== 列表模式：同一套前导槽语义 =====
        files.ToggleListView(); Render();
        var list = filesView.FindControl<ListBox>("FileList")!;
        var listReadmeLead = LeadOf(list, "README.md");
        var listDraftLead = LeadOf(list, "draft.md");
        Check(listReadmeLead is { } && listReadmeLead.IsEffectivelyVisible && listReadmeLead.Data is Geometry,
            "List rows render the modified check in the leading slot");
        Check(listDraftLead is { } && !listDraftLead.IsEffectivelyVisible,
            "List rows keep an empty slot for untracked files");
        files.ToggleListView(); Render();

        Check(VisibleLeads(tree).Count == 5, "Tree view restores leading glyphs after list toggle");
        filesView.DataContext = productionFiles;
        Console.WriteLine("Git badge verification PASS");
    }
}
