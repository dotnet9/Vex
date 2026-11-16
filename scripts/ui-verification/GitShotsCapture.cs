using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Git;
using Vex.Modules.Git.ViewModels;
using Vex.Modules.Git.Views;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class GitShotsCapture
{
    /// <summary>原生 Git 面板与原型四态同框截图：空仓库 / Changes+Diff / History / Split（保真核对用）。</summary>
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Pump(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(10); }
            task.GetAwaiter().GetResult(); Render();
        }
        void Shot(string name)
        {
            Render();
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, name));
            Console.WriteLine("SHOT " + name);
        }

        window.Width = 1440; window.Height = 948;
        var folder = Path.Combine(Path.GetTempPath(), "vex-git-shots", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Pump(vm.OpenStartupDocumentAsync([folder]));
        Render();

        var gitPanel = window.GetVisualDescendants().OfType<ShellGitView>().Single();
        var git = (ShellGitViewModel)gitPanel.DataContext!;
        // 空仓库英雄区。
        vm.EnterGitMode();
        Pump(git.RefreshAsync());
        Shot("native-git-empty.png");

        // 初始化 + 基线提交 + 修改与未跟踪 → Changes + Unified diff。
        GitService.InitRepository(folder);
        var service = new GitService(folder);
        service.SetIdentity("Dotnet9(dotnet9.com)", "632871194@qq.com");
        Directory.CreateDirectory(Path.Combine(folder, "笔记"));
        File.WriteAllText(Path.Combine(folder, "笔记", "架构设计.md"), "# 架构总览\n\n编辑器采用单文档模型，视图共享状态。\n切换布局时保留当前章节位置与撤销栈。\n");
        File.WriteAllText(Path.Combine(folder, "笔记", "模板中心调研.md"), "### 模板格式\n\n模板携带 YAML 元数据。\n");
        // VM 面板先吃到仓库与变更清单，勾选与提交才走真实绑定链路。
        Pump(git.RefreshAsync());
        git.AllChecked = true; git.Summary = "init: 初始化本地仓库";
        Pump(git.CommitAsync());
        Thread.Sleep(1100);
        File.WriteAllText(Path.Combine(folder, "笔记", "架构设计.md"), "# 架构总览\n\n编辑器采用单文档模型，四视图共享同一份文档状态。\n视图间通过 IWorkspaceDocumentState 同步光标与选区。\n切换布局时保留当前章节位置与撤销栈。\n");
        Directory.CreateDirectory(Path.Combine(folder, "草稿"));
        File.WriteAllText(Path.Combine(folder, "草稿", "待整理.md"), "# 待整理\n\n- 阅读视图的断行规则\n");
        Pump(git.RefreshAsync());
        git.SelectedChange = git.Changes.FirstOrDefault(c => c.Kind == GitChangeKind.Modified);
        Shot("native-git-changes.png");

        // History + 提交详情。
        git.Summary = "docs: 四视图共享状态"; Pump(git.CommitAsync());
        Thread.Sleep(1100);
        Pump(git.RefreshAsync());
        git.ShowHistoryTab();
        git.SelectedCommit = git.History.FirstOrDefault(); Render();
        Shot("native-git-history.png");

        // Split 模式。
        git.ShowChangesTab();
        Pump(git.RefreshAsync());
        File.WriteAllText(Path.Combine(folder, "笔记", "架构设计.md"), "# 架构总览\n\n编辑器采用单文档模型，Split 对照。\n视图间通过 IWorkspaceDocumentState 同步光标与选区。\n");
        Pump(git.RefreshAsync());
        git.SelectedChange = git.Changes.FirstOrDefault(c => c.Kind == GitChangeKind.Modified);
        git.DiffMode = GitDiffViewMode.Split; Render();
        Shot("native-git-split.png");
        // 全新增文件（GHD 口径：左栏全空灰格、右栏全内容，等分不横穿）。
        File.WriteAllText(Path.Combine(folder, "草稿", "新特性.md"), "# 新特性\n\n- Split 两栏等分\n- 长行自动换行不横穿\n");
        Pump(git.RefreshAsync());
        git.SelectedChange = git.Changes.FirstOrDefault(c => c.Kind == GitChangeKind.Untracked);
        Render();
        Shot("native-git-split-added.png");

        vm.ExitGitMode();
        Console.WriteLine("Git shots done");
    }
}
