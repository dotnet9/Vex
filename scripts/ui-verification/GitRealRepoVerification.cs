using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Git.ViewModels;
using Vex.Git;
using Vex.Modules.Git.Views;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class GitRealRepoVerification
{
    /// <summary>
    /// 真实仓库回归（发布版崩溃复现场景）：用 Vex 仓库本身（含 tag、真实历史与大量变更）走
    /// Git 面板全量点击——逐条历史提交、每条的文件列表、右键菜单数据链路，任何 NRE 直接崩进程复现。
    /// </summary>
    public static void Run(VerificationVexApp app, MainWindow window, string output, string repoPath)
    {
        try
        {
            RunCore(app, window, output, repoPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("REAL-REPO CRASH STACK:");
            Console.WriteLine(ex.ToString());
            throw;
        }
    }

    private static void RunCore(VerificationVexApp app, MainWindow window, string output, string repoPath)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Pump(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(10); }
            task.GetAwaiter().GetResult(); Render();
        }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }

        window.Width = 1440; window.Height = 948;
        if (Environment.GetEnvironmentVariable("GIT_IDLE_ONLY") == "1")
        {
            // 二分开关：只打开文件夹长空闲（不进 Git 面板）——崩 = 工作区监控链，不崩 = Git 面板后台链。
            Pump(vm.OpenStartupDocumentAsync([repoPath]));
            Console.WriteLine("IDLE opened folder, idling 20s...");
            var idleDeadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < idleDeadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(100); }
            Console.WriteLine("IDLE survived");
            return;
        }
        Pump(vm.OpenStartupDocumentAsync([repoPath]));
        vm.EnterGitMode(); Render();
        var git = (ShellGitViewModel)window.GetVisualDescendants().OfType<ShellGitView>().Single().DataContext!;
        Console.WriteLine("STAGE refresh-real begin");
        Pump(git.RefreshAsync());
        Console.WriteLine("STAGE refresh-real done, history=" + git.History.Count + " changes=" + git.Changes.Count);
        if (Environment.GetEnvironmentVariable("GIT_IDLE_ONLY") == "2")
        {
            Console.WriteLine("IDLE2 git mode + refreshed, idling 20s...");
            var idleDeadline = DateTime.UtcNow.AddSeconds(20);
            while (DateTime.UtcNow < idleDeadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(100); }
            Console.WriteLine("IDLE2 survived");
            return;
        }
        Check(git.HasRepo && vm.IsGitMode, "Real repository opens in git mode: " + git.BranchName);
        Console.Error.WriteLine("changes=" + git.Changes.Count + " history=" + git.History.Count + " remote=" + git.RemoteChipText);

        // 变更列表逐项选中 + 右键菜单数据链路（含未跟踪）。
        foreach (var change in git.Changes.ToArray())
        {
            git.SelectedChange = change; Render();
            Check(git.HasDiffText || change.Kind == GitChangeKind.Deleted, "Change diff loads: " + change.Path);
        }

        // 历史逐条点击（用户崩溃点）：详情头/文件列表/文件 diff 全链路。
        var total = git.History.Count;
        for (var i = 0; i < total; i++)
        {
            git.SelectedCommit = git.History[i]; Render();
            Console.Error.WriteLine("commit[" + i + "] files=" + git.CommitFiles.Count + " subject=" + git.CommitSubject);
            foreach (var file in git.CommitFiles)
            {
                git.SelectedCommitFile = file; Render();
            }
        }
        Check(total > 0, "All " + total + " commits clicked without crash");

        // 右键定位命令（替身，不开资源管理器）。
        var revealed = new List<string>();
        git.RevealInExplorer = path => revealed.Add(path);
        git.OpenChangeInExplorerCommand.Execute(git.Changes.FirstOrDefault());
        Check(revealed.Count == 1, "Reveal command resolves path");

        Console.WriteLine("Git real-repo verification PASS");
    }
}
