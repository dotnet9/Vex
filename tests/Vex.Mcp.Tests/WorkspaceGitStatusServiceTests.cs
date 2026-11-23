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
        var folder = new DocumentFileNode("ws", _root, isFolder: true, children: [_readme, _draft]);
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
}
