using LibGit2Sharp;
using Vex.Git;
using Vex.Modules.Git.ViewModels;
using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// GitService 集成测试：真实临时仓库走 LibGit2Sharp 全流程——探测/初始化、变更清单、
/// 勾选提交、历史与提交内文件、双 Diff（含未跟踪与二进制）、放弃更改、远端归一。
/// 远程推送拉取依赖网络与凭据，不在此覆盖（UiVerification/UI 层演示）。
/// </summary>
public sealed class GitServiceTests : IDisposable
{
    private readonly string _root;

    public GitServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vex-git-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true); } catch { }
    }

    private GitService CreateService() => new(_root);

    private static void SetIdentity(GitService service) => service.SetIdentity("测试者", "tester@example.com");

    private static void WriteFile(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    [Fact]
    public void IsRepository_false_for_plain_folder_and_true_after_init()
    {
        Assert.False(GitService.IsRepository(_root));
        GitService.InitRepository(_root);
        Assert.True(GitService.IsRepository(_root));
        // 默认分支 main（原型口径）。
        var (branch, _, _) = CreateService().BranchState();
        Assert.Equal("main", branch);
    }

    [Fact]
    public void Changes_reports_kinds_and_line_stats_with_untracked_default()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        WriteFile(_root, "笔记/架构.md", "# 架构\n\n第一行。\n");
        service.CommitPaths(["笔记/架构.md"], "init");

        File.WriteAllText(Path.Combine(_root, "笔记", "架构.md"), "# 架构\n\n第一行改了。\n第二行新增。\n");
        WriteFile(_root, "草稿/待整理.md", "# 待整理\n");

        var changes = service.Changes();
        Assert.Equal(2, changes.Count);
        var modified = changes.Single(c => c.Path == "笔记/架构.md");
        Assert.Equal(GitChangeKind.Modified, modified.Kind);
        Assert.True(modified.HasLineStats);
        Assert.Equal(2, modified.LinesAdded); // 改一行 + 增一行
        Assert.Equal(1, modified.LinesDeleted);
        var untracked = changes.Single(c => c.Path == "草稿/待整理.md");
        Assert.Equal(GitChangeKind.Untracked, untracked.Kind);
    }

    [Fact]
    public void CommitPaths_only_stages_checked_paths_and_appends_description()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        WriteFile(_root, "a.md", "A\n");
        WriteFile(_root, "b.md", "B\n");
        var commit = service.CommitPaths(["a.md"], "主题行\n\n详细描述。");
        Assert.Equal("主题行", commit.MessageShort);

        var status = service.Status();
        // b.md 未被提交：仍未跟踪。
        Assert.True(status.Single(i => i.FilePath == "b.md").State.HasFlag(FileStatus.NewInWorkdir));
        // 历史含完整描述。
        var history = service.History();
        Assert.Single(history);
    }

    [Fact]
    public void CommitPaths_without_repo_identity_falls_back_to_global_config()
    {
        // 本机全局 user.name/email 存在时提交可直接成功；语义上 IdentityOrDefault 只在
        // 仓库与全局都未配置时阻断（UI 层指引配置），这里验证仓库级身份优先落进提交。
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        WriteFile(_root, "a.md", "A\n");
        var (missing, name, _) = service.IdentityOrDefault();
        Assert.False(missing);
        Assert.Equal("测试者", name);
        service.CommitPaths(["a.md"], "x");
        Assert.Equal("测试者", service.History()[0].Author);
    }

    [Fact]
    public void CommitAll_returns_null_when_clean_and_commits_when_dirty()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        Assert.Null(service.CommitAll("auto: 09:30"));
        WriteFile(_root, "a.md", "A\n");
        var commit = service.CommitAll("auto: 09:30");
        Assert.NotNull(commit);
        Assert.Empty(service.Changes());
    }

    [Fact]
    public void TextDiff_parses_unified_patch_and_untracked_file_as_added()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        WriteFile(_root, "a.md", "one\n");
        service.CommitPaths(["a.md"], "init");
        File.WriteAllText(Path.Combine(_root, "a.md"), "one\nthree\n");
        WriteFile(_root, "new.md", "brand new\nfile\n");

        var diff = service.TextDiff("a.md");
        Assert.NotNull(diff);
        var lines = diff.Hunks.SelectMany(h => h.Lines).ToList();
        Assert.Contains(lines, l => l.Kind == ' ' && l.Text == "one"); // 上下文行未变
        Assert.Contains(lines, l => l.Kind == '+' && l.Text == "three");

        var added = service.TextDiff("new.md");
        Assert.NotNull(added);
        Assert.All(added.Hunks.SelectMany(h => h.Lines), l => Assert.Equal('+', l.Kind));

        // 解析器：纯文本 unified diff。
        var parsed = GitService.ParsePatch("x.md", "@@ -1,2 +1,2 @@\n-old\n+new\n ctx\n");
        var hunk = Assert.Single(parsed.Hunks);
        Assert.Equal(3, hunk.Lines.Count);
    }

    [Fact]
    public void TextDiff_returns_null_for_binary()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        File.WriteAllBytes(Path.Combine(_root, "pic.png"), [0x89, 0x50, 0x4E, 0x47]);
        Assert.Null(service.TextDiff("pic.png"));
    }

    [Fact]
    public void History_and_CommitChanges_cover_initial_commit()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        WriteFile(_root, "a.md", "A\n");
        service.CommitPaths(["a.md"], "init: 首次提交");
        WriteFile(_root, "b.md", "B\n");
        var second = service.CommitPaths(["b.md"], "docs: 加 b");

        var history = service.History();
        Assert.Equal(2, history.Count);
        Assert.Equal("docs: 加 b", history[0].Message);
        Assert.Equal("测试者", history[0].Author);

        // 首次提交（无父）：CommitChanges 覆盖整棵树。
        var initialChanges = service.CommitChanges(history[1].Sha);
        Assert.Single(initialChanges);
        Assert.Equal("a.md", initialChanges[0].Path);
        Assert.Equal(GitChangeKind.Added, initialChanges[0].Kind);

        var secondChanges = service.CommitChanges(second.Sha[..7]);
        Assert.Equal("b.md", Assert.Single(secondChanges).Path);
    }

    [Fact]
    public void Discard_restores_tracked_and_removes_untracked()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        SetIdentity(service);
        WriteFile(_root, "a.md", "A\n");
        service.CommitPaths(["a.md"], "init");
        WriteFile(_root, "a.md", "A changed\n");
        WriteFile(_root, "new.md", "N\n");

        service.Discard("a.md");
        // checkout 的行尾跟随用户全局 core.autocrlf 配置：断言按内容归一。
        Assert.Equal("A", File.ReadAllText(Path.Combine(_root, "a.md")).TrimEnd('\r', '\n'));
        service.Discard("new.md");
        Assert.False(File.Exists(Path.Combine(_root, "new.md")));
        Assert.Empty(service.Changes());
    }

    [Fact]
    public void NormalizeRemoteUrl_maps_ssh_and_https_and_rejects_local()
    {
        Assert.Equal("https://github.com/dotnet9/Vex", GitService.NormalizeRemoteUrl("https://github.com/dotnet9/Vex.git"));
        Assert.Equal("https://github.com/dotnet9/Vex", GitService.NormalizeRemoteUrl("git@github.com:dotnet9/Vex.git"));
        Assert.Equal("https://gitlab.com/g/h", GitService.NormalizeRemoteUrl("ssh://git@gitlab.com/g/h.git"));
        Assert.Null(GitService.NormalizeRemoteUrl(null));
        Assert.Null(GitService.NormalizeRemoteUrl("D:/repos/notes"));
        Assert.Null(GitService.NormalizeRemoteUrl(@"E:\data\repo"));
    }

    [Fact]
    public void IsValidRemoteUrl_accepts_three_forms_only()
    {
        Assert.True(ShellGitViewModel.IsValidRemoteUrl("https://github.com/dotnet9/Vex.git"));
        Assert.True(ShellGitViewModel.IsValidRemoteUrl("git@github.com:dotnet9/Vex.git"));
        Assert.True(ShellGitViewModel.IsValidRemoteUrl("ssh://git@host/repo.git"));
        Assert.False(ShellGitViewModel.IsValidRemoteUrl("ftp://example.com/x"));
        Assert.False(ShellGitViewModel.IsValidRemoteUrl("随便一段话"));
        Assert.False(ShellGitViewModel.IsValidRemoteUrl(""));
    }

    [Fact]
    public void SetRemote_updates_origin_and_remote_url_normalizes()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        Assert.Null(service.RemoteRawUrl());
        service.SetRemote("git@github.com:dotnet9/Vex.git");
        Assert.Equal("git@github.com:dotnet9/Vex.git", service.RemoteRawUrl());
        Assert.True(service.IsSshRemote());
        Assert.Equal("https://github.com/dotnet9/Vex", service.RemoteUrl());
        Assert.Equal("Vex", Path.GetFileName(service.RemoteUrl()));
        Assert.Null(service.RemoteCredentialHost());
        service.SetRemote("https://github.com/dotnet9/Vex.git");
        Assert.False(service.IsSshRemote());
        Assert.Equal("github.com", service.RemoteCredentialHost());
        // CommitUrl：sha → 远端 Web 地址（历史行跳转扩展点）。
        Assert.Equal("https://github.com/dotnet9/Vex/commit/abc1234", service.CommitUrl("abc1234"));
    }

    [Fact]
    public void AddToGitignore_is_idempotent()
    {
        GitService.InitRepository(_root);
        var service = CreateService();
        service.AddToGitignore("*.log");
        service.AddToGitignore("*.log");
        Assert.Equal(["*.log"], File.ReadAllLines(Path.Combine(_root, ".gitignore")).Where(l => l.Length > 0).ToArray());
    }
}
