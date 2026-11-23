using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Messaging;
using Vex.Git;
using Vex.Modules.Git.ViewModels;
using Vex.Modules.Git.Views;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class GitPanelVerification
{
    /// <summary>
    /// Git 管理界面端到端验证（原型 git-panel.js 的 UIA 夹具路径）：
    /// rail 文件/Git 双模式、无仓库英雄区与初始化、Changes/History 双页签、勾选提交（Summary 必填）、
    /// Unified/Split 双 Diff、文件状态描边标志、远程芯片与 SSH 警示、偏好 Git 分类。
    /// 推送拉取不联网：仅验证按钮可用性与占位提示。
    /// </summary>
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Pump(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(10); }
            Check(task.IsCompleted, "Async operation completes"); task.GetAwaiter().GetResult(); Render();
        }
        T Control<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
        IReadOnlyList<T> Controls<T>() where T : Control => window.GetVisualDescendants().OfType<T>().ToList();

        window.Width = 1280; window.Height = 820;
        var folder = Path.Combine(Path.GetTempPath(), "vex-uia-git", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        // 打开临时文件夹为工作区（文件模式），再切 Git 模式。
        Pump(vm.OpenStartupDocumentAsync([folder]));
        Render();
        Check(!vm.IsGitMode, "Startup stays in files mode");
        Check(!Control<ShellGitView>("GitPanel").IsVisible, "Git panel hidden in files mode");

        // ===== rail：文件/Git 双模式 =====
        var railFiles = Control<ToggleButton>("RailFilesToggle");
        var railGit = Control<ToggleButton>("RailGitToggle");
        Check(railFiles.IsChecked == true && railGit.IsChecked == false, "Rail files mode checked by default");
        railGit.Command!.Execute(null); Render();
        Check(vm.IsGitMode && railGit.IsChecked == true && !railFiles.IsChecked!.Value, "Rail switches to git mode");
        Check(Control<ShellGitView>("GitPanel").IsVisible, "Git panel visible in git mode");
        // 父级隐藏不改变子级 IsVisible 属性值：按有效可见性（视觉树链）判定。
        Check(!Controls<ShellFilesView>().Any(v => v.IsEffectivelyVisible), "Files sidebar leaves the stage in git mode");
        // 标题栏双折叠钮在 Git 模式隐藏（原型 body.git-mode）。
        Check(!Control<ToggleButton>("TitlebarSidebarToggle").IsVisible, "Titlebar sidebar toggle hidden in git mode");
        Check(!Control<ToggleButton>("TitlebarOutlineToggle").IsVisible, "Titlebar outline toggle hidden in git mode");
        // 查找面板从 Git 模式先回文件模式（原型 git-panel.js 口径）。
        vm.ShowFindPanel(); Render();
        Check(!vm.IsGitMode, "Find action returns to files mode first");

        // ===== 无仓库英雄区 + 初始化 =====
        vm.EnterGitMode(); Render();
        var initButton = Control<Button>("InitRepoButton");
        Check(initButton.IsVisible, "Empty hero shows initialize button");
        Pump(((ShellGitViewModel)Control<ShellGitView>("GitPanel").DataContext!).InitRepositoryAsync()); Render(); Render();
        Check(GitService.IsRepository(folder), "Initialize creates a local repository");
        Check(!initButton.IsEffectivelyVisible, "Hero leaves the stage after initialization");
        var git = (ShellGitViewModel)Control<ShellGitView>("GitPanel").DataContext!;
        Check(git.BranchName == "main", "Default branch is main (prototype wording)");
        Check(git.IsCleanWorkdir, "Clean workdir shows blank state");
        // 崩点定位：该窗口主线程只跑 Dispatcher 队列回调——泵内逐回调抓栈。
        Console.Error.WriteLine("STAGE queue-pump begin");
        var pumpDeadline = DateTime.UtcNow.AddSeconds(4);
        while (DateTime.UtcNow < pumpDeadline)
        {
            try { Dispatcher.UIThread.RunJobs(); }
            catch (Exception ex)
            {
                Console.Error.WriteLine("QUEUE CRASH STACK:");
                Console.WriteLine(ex.ToString());
                throw;
            }
            Thread.Sleep(50);
        }
        Console.Error.WriteLine("STAGE queue-pump survived");
        // AOT 栈能力探针：同步 NRE 是否有栈（决定后续诊断路线）。
        try { object? nothing = null; _ = nothing!.ToString(); }
        catch (NullReferenceException probe)
        {
            Console.Error.WriteLine("PROBE STACK: " + (string.IsNullOrWhiteSpace(probe.StackTrace) ? "<EMPTY>" : probe.StackTrace));
        }
        Console.Error.WriteLine("STAGE write-baseline");

        // ===== Changes：勾选 + Summary 必填 + 提交 =====
        // 先提交一份基线（建立 HEAD），再制造「修改 + 未跟踪」组合。
        File.WriteAllText(Path.Combine(folder, "笔记.md"), "# 笔记\n\n第一稿。\n");
        Pump(git.RefreshAsync());
        git.AllChecked = true; git.Summary = "docs: 初始化笔记";
        Console.Error.WriteLine("STAGE commit-1 begin");
        Pump(git.CommitAsync());
        Console.Error.WriteLine("STAGE commit-1 done");
        Pump(git.RefreshAsync());
        Console.Error.WriteLine("STAGE refresh-2 done");
        Check(git.Changes.Count == 0, "Commit clears the working tree");
        File.WriteAllText(Path.Combine(folder, "笔记.md"), "# 笔记\n\n第一稿改。\n");
        File.WriteAllText(Path.Combine(folder, "草稿箱.md"), "# 草稿\n");
        Pump(git.RefreshAsync());
        Check(git.Changes.Count == 2, "Changes lists tracked modification and untracked file");
        var modified = git.Changes.Single(c => c.Kind == GitChangeKind.Modified);
        var untracked = git.Changes.Single(c => c.Kind == GitChangeKind.Untracked);
        Check(modified.Checked && !untracked.Checked, "Tracked pre-checked, untracked unchecked (prototype seed)");
        Check(git.AllChecked == false, "Stage-all checkbox reflects partial selection");
        git.AllChecked = true; Render();
        Check(git.CheckedCount == 2, "Stage-all checks every change");
        Check(!git.CanCommit, "Commit disabled without summary");
        git.Summary = "docs: 加入草稿箱"; Render();
        Check(git.CanCommit, "Commit enabled with summary and checked files");
        Thread.Sleep(1100);
        Pump(git.CommitAsync()); Pump(git.RefreshAsync());
        Check(git.Changes.Count == 0, "Commit clears the working tree");
        Check(git.IsCleanWorkdir, "Clean workdir blank returns after commit");

        // ===== History：提交行与文件列 =====
        File.WriteAllText(Path.Combine(folder, "笔记.md"), "# 笔记\n\n第二稿。\n");
        Pump(git.RefreshAsync());
        git.Summary = "docs: 第二稿"; Pump(git.CommitAsync()); Pump(git.RefreshAsync());
        git.ShowHistoryTab(); Render();
        Check(git.IsHistoryTab, "History tab activates");
        Check(git.History.Count == 3, "History lists all three commits");
        Check(git.History[0].Subject.StartsWith("docs: 第二稿"), "Newest commit first");
        git.SelectedCommit = git.History[0]; Render();
        Check(git.HasCommitDetail, "Commit detail header renders");
        Check(git.CommitFiles.Count == 1 && git.CommitFiles[0].Path == "笔记.md", "Commit file list shows changed path");
        // 最新提交是修改：diff 含删除与新增成对。
        Check(git.UnifiedRows.Any(r => r.IsDeleted) && git.UnifiedRows.Any(r => r.IsAdded), "Modification diff pairs deletion and addition");
        // 最早的初始化提交：整文件新增（无父提交覆盖整棵树）。
        git.SelectedCommit = git.History[^1]; Render();
        Check(git.History[^1].Subject == "docs: 初始化笔记" && git.CommitFiles.All(f => f.Path == "笔记.md"), "Initial commit lists only the first file");
        Check(git.UnifiedRows.All(r => r.IsAdded), "Initial-state diff renders added lines for the file");
        // [文件列表 | 对比] 两栏分隔：文件列表列宽绑定 VM（可拖拽），初始 220。
        // ColumnDefinition 不在视觉树：从 [文件列表|diff] 两栏 Grid（3 列：绝对 220 + Auto + *）取列。
        var commitFilesGrid = window.GetVisualDescendants().OfType<Grid>()
            .FirstOrDefault(g => g.ColumnDefinitions.Count == 3 && g.ColumnDefinitions[1].Width.IsAuto
                && g.ColumnDefinitions[0].Width.IsAbsolute && Math.Abs(g.ColumnDefinitions[0].Width.Value - git.CommitFileListWidth.Value) < .1);
        Check(commitFilesGrid is not null && git.CommitFileListWidth.Value == 220, "Commit file list column binds the draggable width");
        git.CommitFileListWidth = new Avalonia.Controls.GridLength(300); Render();
        Check(Math.Abs(commitFilesGrid!.ColumnDefinitions[0].Width.Value - 300) < .1, "Commit file list width is draggable via the view model");
        git.CommitFileListWidth = new Avalonia.Controls.GridLength(220); Render();
        git.SelectedCommit = git.History[0]; Render();

        // ===== Diff 视图：Unified/Split + 行号 + 状态描边 =====
        git.ShowChangesTab();
        File.WriteAllText(Path.Combine(folder, "笔记.md"), "# 笔记\n\n第三稿：四视图共享同一份文档状态。\n");
        Pump(git.RefreshAsync());
        var changeItem = git.Changes.Single(c => c.Kind == GitChangeKind.Modified);
        git.SelectedChange = changeItem; Render();
        Check(git.HasDiffSelection && git.HasDiffText, "Selecting a change renders its diff");
        Check(git.IsUnified && git.UnifiedRows.Any(r => r.IsDeleted) && git.UnifiedRows.Any(r => r.IsAdded),
            "Unified diff pairs deletion and addition");
        Check(git.UnifiedRows.All(r => r.LeftNumberText.Length == 0 || int.TryParse(r.LeftNumberText, out _)),
            "Unified rows carry numeric line numbers");
        git.DiffMode = GitDiffViewMode.Split; Render();
        Check(git.IsSplit && git.SplitRows.Any(p => p.Left.IsDeleted && p.Right.IsAdded), "Split diff pairs sides row-by-row");
        // Split 模式下右区必须仍显示 hunk 头与 Split 内容（此前 HasDiffText 只看 UnifiedRows，切 Split 整区消失）。
        Check(git.HasDiffText, "Split mode keeps the diff area visible");
        Check(Control<ItemsControl>("SplitDiff").IsVisible, "Split diff ItemsControl is bound in split mode");
        git.DiffMode = GitDiffViewMode.Unified; Render();
        Check(git.HasDiffText && Control<ItemsControl>("UnifiedDiff").IsVisible, "Unified diff ItemsControl is bound");

        // ===== 远程芯片：HTTPS 与 SSH 警示 =====
        Check(!git.HasRemote && git.RemoteChipText.Length == 0, "No remote chip before configuration");
        Pump(git.SaveRemoteAsync("git@github.com:dotnet9/vex-notes.git"));
        Check(git.IsSshRemote && git.RemoteChipText == "origin · SSH", "SSH remote shows warn chip wording");
        Pump(git.SaveRemoteAsync("https://github.com/dotnet9/vex-notes.git"));
        Check(!git.IsSshRemote && git.RemoteChipText == "origin · HTTPS", "HTTPS remote switches chip wording");
        Check(git.RepoName == "vex-notes", "Repo name derives from remote url");
        Check(!git.CanPush, "Push disabled while in sync");
        Check(git.PushHint.Contains("与远程同步"), "Sync hint wording matches prototype");

        // ===== 偏好：Git 分类（原型第 6 类） =====
        var preferencesView = OpenPreferences(window);
        Check(preferencesView.GetVisualDescendants().OfType<ListBox>().Single().ItemCount == 7,
            "Preferences has seven categories with Git");
        preferencesView.SelectCategory("git"); Render();
        var toggles = preferencesView.GetVisualDescendants().OfType<ToggleButton>().Where(b => b.Classes.Contains("pref-switch")).ToList();
        Check(toggles.Any(t => t.Name == "GitAutoCommit"), "Git page hosts auto-commit switch");
        // 原型 gitAutoCommitInterval 是 range 滑条（5~120）：偏好页用共享 Range 行呈现，页内含一枚 Slider。
        var gitPref = preferencesView.GetVisualDescendants().OfType<Slider>().FirstOrDefault();
        Check(gitPref is not null && gitPref.Minimum == 5 && gitPref.Maximum == 120, "Git page hosts commit interval slider 5 to 120");
        var messageBox = preferencesView.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(b => b.Name == "GitAutoCommitMessage");
        Check(messageBox is not null && messageBox.Text == "auto: {date}", "Git page message template defaults to auto: {date}");
        ClosePreferences(preferencesView);

        Console.WriteLine("Git panel verification PASS");
    }

    private static ShellPreferencesOverlayView OpenPreferences(MainWindow window)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.ShowPreferences));
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            var view = window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().FirstOrDefault();
            if (view is not null) return view;
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Preferences did not open");
            Thread.Sleep(20);
        }
    }

    private static void ClosePreferences(ShellPreferencesOverlayView view) =>
        ((ShellPreferencesViewModel)view.DataContext!).Close();
}
