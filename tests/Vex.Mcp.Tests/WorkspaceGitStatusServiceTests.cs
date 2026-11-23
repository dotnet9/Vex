using Vex.Core.Models;
using Vex.Git;
using Vex.Modules.Shell.Services;

using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// WorkspaceGitStatusService 集成：真实临时仓库（LibGit2Sharp Init/提交）驱动，
/// 同步 marshal 下验证状态映射（未跟踪/修改/干净）、非仓库禁用清零与换根重建。
/// 工作者路径的置脏合并由实现保证，测试走 RefreshNowAsync 同步钩子。
/// </summary>
public sealed class WorkspaceGitStatusServiceTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceGitStatusService _service;
    private readonly List<DocumentFileNode> _roots;
    private readonly DocumentFileNode _readme;
    private readonly DocumentFileNode _draft;
    private readonly DocumentFileNode _ignored;

    public WorkspaceGitStatusServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vex-git-badge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        GitService.InitRepository(_root);
        File.WriteAllText(Path.Combine(_root, "README.md"), "# hello\n");
        using var git = new GitService(_root);
        git.CommitAll("init");

        _readme = new DocumentFileNode("README.md", Path.Combine(_root, "README.md"), isFolder: false);
        _draft = new DocumentFileNode("draft.md", Path.Combine(_root, "draft.md"), isFolder: false);
        _ignored = new DocumentFileNode("ignored.log", Path.Combine(_root, "ignored.log"), isFolder: false);
        var folder = new DocumentFileNode("ws", _root, isFolder: true, children: [_readme, _draft, _ignored]);
        _roots = [folder];
        _service = new WorkspaceGitStatusService(uiMarshal: action => action());
        _service.Bind(() => _roots);
    }

    [Fact]
    public async Task Refresh_MapsWorkingTreeStates()
    {
        // 前导状态槽语义：README 已修改（红勾），draft 未跟踪（空槽=None），
        // 受管干净文件挂锁——README 在修改前是干净态，改后翻转为修改。
        File.WriteAllText(Path.Combine(_root, "README.md"), "# hello\n\ntail\n");
        File.WriteAllText(Path.Combine(_root, "draft.md"), "new\n");
        _service.SetWorkspace(_root);
        await _service.RefreshNowAsync();

        Assert.Equal(WorkspaceGitStatus.Modified, _readme.GitStatus);
        Assert.Equal(WorkspaceGitStatus.None, _draft.GitStatus); // 未跟踪=空槽
        Assert.Equal(WorkspaceGitStatus.Modified, _roots[0].GitStatus); // 聚合：只取变更
    }

    [Fact]
    public async Task Refresh_CommittedFilesBecomeClean()
    {
        File.WriteAllText(Path.Combine(_root, "draft.md"), "new\n");
        _service.SetWorkspace(_root);
        await _service.RefreshNowAsync();
        Assert.Equal(WorkspaceGitStatus.None, _draft.GitStatus); // 未跟踪=空槽

        using (var git = new GitService(_root))
        {
            git.CommitAll("add draft");
        }

        // 提交后进入管控：受管干净=挂锁（TrackedClean）。
        await _service.RefreshNowAsync();
        Assert.Equal(WorkspaceGitStatus.TrackedClean, _draft.GitStatus);
        Assert.Equal(WorkspaceGitStatus.TrackedClean, _readme.GitStatus);
        Assert.Equal(WorkspaceGitStatus.None, _roots[0].GitStatus); // 干净文件夹不挂锁
    }

    [Fact]
    public async Task NonRepositoryWorkspace_ClearsAllBadges()
    {
        File.WriteAllText(Path.Combine(_root, "draft.md"), "new\n");
        _service.SetWorkspace(_root);
        await _service.RefreshNowAsync();
        Assert.Equal(WorkspaceGitStatus.None, _draft.GitStatus); // 未跟踪=空槽

        var plain = Path.Combine(Path.GetTempPath(), "vex-git-badge-tests", "plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(plain);
        try
        {
            _service.SetWorkspace(plain);
            await _service.RefreshNowAsync();
            Assert.Equal(WorkspaceGitStatus.None, _draft.GitStatus);
            Assert.Equal(WorkspaceGitStatus.None, _roots[0].GitStatus);
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }

    public void Dispose()
    {
        _service.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            // LibGit2Sharp 原生句柄延迟释放：GC 收敛后重试一次，仍占用则留给系统临时目录清理。
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { Directory.Delete(_root, recursive: true); }
            catch
            {
                // 句柄仍未释放：临时目录留给系统清理。
            }
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, string message, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), message + "（超时未收敛）");
    }

    // ===== 后台置脏工作者路径（真实 ProcessLoop，不经 RefreshNowAsync 同步钩子）=====
    // 置脏合并由置脏标志实现：并发触发只留最新，收敛断言验证最终态正确。

    [Fact]
    public async Task RequestRefresh_WorkerPath_LockToModifiedToLock()
    {
        // 初始：受管干净 → 工作者路径挂锁。
        _service.SetWorkspace(_root);
        await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.TrackedClean, "初始挂锁");

        // 改受管文件 → 红勾（Modified）。
        File.AppendAllText(Path.Combine(_root, "README.md"), "more\n");
        _service.RequestRefresh();
        await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.Modified, "修改态");
        Assert.Equal(WorkspaceGitStatus.Modified, _roots[0].GitStatus);

        // 提交 → 回到挂锁（提交经事件通知路径之外的直接 RequestRefresh 等价）。
        using (var git = new GitService(_root))
        {
            git.CommitAll("more");
        }

        _service.RequestRefresh();
        await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.TrackedClean, "提交后回到挂锁");
        Assert.Equal(WorkspaceGitStatus.None, _roots[0].GitStatus);
    }

    [Fact]
    public async Task RequestRefresh_BurstTriggers_ConvergeToLatest()
    {
        _service.SetWorkspace(_root);
        await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.TrackedClean, "基线挂锁");

        // 突发连发触发（穿插真实变更）：置脏合并后必须收敛到最新状态。
        for (var round = 0; round < 5; round++)
        {
            File.AppendAllText(Path.Combine(_root, "README.md"), "round " + round + "\n");
            for (var burst = 0; burst < 8; burst++)
            {
                _service.RequestRefresh();
            }

            await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.Modified, "第 " + round + " 轮修改态");

            using (var git = new GitService(_root))
            {
                git.CommitAll("round " + round);
            }

            _service.RequestRefresh();
            await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.TrackedClean, "第 " + round + " 轮回锁");
        }
    }

    [Fact]
    public async Task SetWorkspace_SwitchWhileWorkerActive_ClearsStaleStatesAndRebinds()
    {
        // 先在工作区 A 得到修改态，再切到非仓库目录：旧状态必须清零（退役句柄由工作者释放，不崩）。
        File.AppendAllText(Path.Combine(_root, "README.md"), "dirty\n");
        _service.SetWorkspace(_root);
        await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.Modified, "工作区 A 修改态");

        var plain = Path.Combine(Path.GetTempPath(), "vex-git-badge-tests", "switch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(plain);
        try
        {
            _service.SetWorkspace(plain);
            await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.None, "切走后清零");

            // 再切回：徽章按当前状态重算。
            _service.SetWorkspace(_root);
            await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.Modified, "切回恢复修改态");
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }

    [Fact]
    public async Task RequestRefresh_IgnoredFilesStayNone()
    {
        // RetrieveStatus 默认含 Ignored 条目：忽略文件不得进入前导槽（保持空槽）。
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "ignored.log\n");
        File.WriteAllText(Path.Combine(_root, "ignored.log"), "noise\n");
        _service.SetWorkspace(_root);
        await WaitForAsync(() => _readme.GitStatus == WorkspaceGitStatus.TrackedClean, "基线挂锁");

        // .gitignore 本身是未跟踪变更（空槽）；忽略文件必须保持 None。
        Assert.Equal(WorkspaceGitStatus.None, _ignored.GitStatus);
        await _service.RefreshNowAsync();
        Assert.Equal(WorkspaceGitStatus.None, _ignored.GitStatus);
    }
}
