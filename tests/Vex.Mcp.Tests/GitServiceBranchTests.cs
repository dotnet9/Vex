using LibGit2Sharp;

using Vex.Git;

using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// GitService 分支管理扩展（GitHub Desktop 分支下拉的落地端）：
/// 本地分支清单/切换/新建/重命名/删除/合并/指定提交打 tag，全部真实临时仓库驱动。
/// </summary>
public sealed class GitServiceBranchTests : IDisposable
{
    private readonly string _root;
    private readonly GitService _service;

    public GitServiceBranchTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vex-git-branch-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        GitService.InitRepository(_root);
        File.WriteAllText(Path.Combine(_root, "README.md"), "# init\n");
        _service = new GitService(_root);
        _service.CommitAll("init");
    }

    private string Abs(string relative) => Path.Combine(_root, relative);

    private void WriteAndCommit(string relative, string content, string message)
    {
        File.WriteAllText(Abs(relative), content);
        _service.CommitAll(message);
    }

    [Fact]
    public void LocalBranches_ListsCurrentFirstData_WithTipTime()
    {
        var branches = _service.LocalBranches();
        var current = branches.Single(b => b.IsCurrent);
        Assert.Equal("main", current.Name);
        Assert.True(current.LastCommitTime is not null);
    }

    [Fact]
    public void CheckoutBranch_MovesHead_KeepsUncommittedChanges()
    {
        _service.CreateBranch("feature/a");
        Assert.Equal("feature/a", _service.LocalBranches().Single(b => b.IsCurrent).Name);

        // 未提交变更跨检出保留（GHD 口径）
        File.WriteAllText(Abs("dirty.txt"), "wip\n");
        _service.CheckoutBranch("main");
        Assert.True(File.Exists(Abs("dirty.txt")));
    }

    [Fact]
    public void CreateBranch_ChecksOut_NewBranch()
    {
        _service.CreateBranch("feature/b", "main");
        var info = _service.LocalBranches().Single(b => b.IsCurrent);
        Assert.Equal("feature/b", info.Name);
    }

    [Fact]
    public void RenameBranch_MovesHead_KeepsTipTime()
    {
        _service.CreateBranch("old-name");
        var before = _service.LocalBranches().Single(b => b.IsCurrent).LastCommitTime;
        _service.RenameBranch("old-name", "new-name");
        var renamed = _service.LocalBranches().Single(b => b.Name == "new-name");
        Assert.Equal(before, renamed.LastCommitTime);
        Assert.Null(_service.LocalBranches().FirstOrDefault(b => b.Name == "old-name"));
    }

    [Fact]
    public void DeleteBranch_RejectsCurrent_AllowsOthers()
    {
        _service.CreateBranch("doomed");
        _service.CheckoutBranch("main");
        _service.DeleteBranch("doomed");
        Assert.Null(_service.LocalBranches().FirstOrDefault(b => b.Name == "doomed"));

        var ex = Assert.Throws<GitServiceException>(() => _service.DeleteBranch("main"));
        Assert.Equal(GitServiceError.BranchIsCurrent, ex.Code);
    }

    [Fact]
    public void MergeBranchInto_MergesFeatureIntoMain()
    {
        _service.CreateBranch("feature/m");
        WriteAndCommit("feature-file.md", "feature content\n", "feature work");
        _service.CheckoutBranch("main");

        var result = _service.MergeBranchInto("feature/m");
        Assert.NotEqual("up-to-date", result);
        Assert.True(File.Exists(Abs("feature-file.md")), "合并后目标文件应出现在 main");
    }

    [Fact]
    public void MergeBranchInto_UpToDate_WhenNothingToMerge()
    {
        var result = _service.MergeBranchInto("main");
        Assert.Equal("up-to-date", result);
    }

    [Fact]
    public void Fetch_WithoutRemote_ThrowsNoOriginRemote()
    {
        var ex = Assert.Throws<GitServiceException>(() => _service.Fetch(string.Empty));
        Assert.Equal(GitServiceError.NoOriginRemote, ex.Code);
    }

    [Fact]
    public void CreateTag_OnSpecificCommit_AttachesToThatCommit()
    {
        WriteAndCommit("a.md", "a\n", "first");
        string firstSha;
        using (var repo = new Repository(_root))
        {
            firstSha = repo.Head.Tip.Sha;
        }
        File.AppendAllText(Abs("a.md"), "more\n");
        var headSha = _service.CommitAll("second")!.Sha;
        _service.CreateTag("v9.9", firstSha);

        using var check = new Repository(_root);
        var tag = check.Tags["v9.9"];
        Assert.NotNull(tag);
        Assert.Equal(firstSha, tag.Target.Sha);
        Assert.NotEqual(headSha, tag.Target.Sha);
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
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try { Directory.Delete(_root, recursive: true); }
            catch
            {
                // 原生句柄延迟释放：临时目录留给系统清理。
            }
        }
    }
}
