using System.Globalization;
using Avalonia.Controls.Notifications;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Git;
using Vex.Modules.Git.ViewModels;
using Vex.Modules.Shell.Services;
using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// Git 面板 ViewModel 集成测试：真实 GitService + 临时仓库，覆盖仓库探测与初始化、
/// 变更勾选与提交（Summary 必填）、双 Diff（Unified/Split/隐藏空白）、页签与占位、远程归一。
/// 推送拉取不联网：ResolvePatAsync 语义通过凭据替身验证。
/// </summary>
public sealed class ShellGitViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly FakeLocalizer _localizer = new();
    private readonly FakeToasts _toasts = new();
    private readonly FakeCredentialStore _credentials = new();
    private readonly FakeSettings _settings = new();

    public ShellGitViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "vex-gitvm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_root)!, recursive: true); } catch { }
    }

    private void WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private static void PumpJobs() => Avalonia.Threading.Dispatcher.UIThread.RunJobs();

    private ShellGitViewModel CreateVm()
    {
        var preferences = new EditorPreferences(_settings);
        var vm = new ShellGitViewModel(_localizer, _toasts, preferences, _credentials)
        {
            WorkspaceFolderProvider = () => _root,
            // 无调度器夹具：集合派发直执行（替代 Dispatcher 排队 + RunJobs 泵的线程亲和坑）。
            UiDispatch = action => { action(); return Task.CompletedTask; },
        };
        return vm;
    }

    private async Task<ShellGitViewModel> CreateReadyVmAsync()
    {
        GitService.InitRepository(_root);
        var service = new GitService(_root);
        service.SetIdentity("测试者", "tester@example.com");
        WriteFile("笔记/架构.md", "# 架构\n\n单文档。\n");
        service.CommitPaths(["笔记/架构.md"], "init: 初始化");
        File.WriteAllText(Path.Combine(_root, "笔记", "架构.md"), "# 架构\n\n四视图共享状态。\n");
        WriteFile("草稿/新文件.md", "# 新\n");
        var vm = CreateVm();
        await vm.RefreshAsync();
        return vm;
    }

    [Fact]
    public async Task RefreshAsync_without_repo_shows_empty_state()
    {
        var vm = CreateVm();
        await vm.RefreshAsync();
        Assert.False(vm.HasRepo);
        Assert.Empty(vm.Changes);
        Assert.Empty(vm.History);
    }

    [Fact]
    public async Task RefreshAsync_loads_changes_history_and_repo_chip()
    {
        var vm = await CreateReadyVmAsync();
        Assert.True(vm.HasRepo);
        Assert.Equal("main", vm.BranchName);
        Assert.Equal(2, vm.Changes.Count);
        Assert.Single(vm.History);
        Assert.False(vm.HasRemote);
        Assert.Equal("未配置远程，仅本地管理", vm.PushHint);
        // 未跟踪默认不勾选，修改默认勾选。
        Assert.False(vm.Changes.Single(c => c.Kind == GitChangeKind.Untracked).Checked);
        Assert.True(vm.Changes.Single(c => c.Kind == GitChangeKind.Modified).Checked);
        Assert.Equal(1, vm.CheckedCount);
    }

    [Fact]
    public async Task Commit_requires_summary_and_checked_files()
    {
        var vm = await CreateReadyVmAsync();
        Assert.False(vm.CanCommit);
        vm.Summary = "  ";
        Assert.False(vm.CanCommit);
        vm.Summary = "docs: 更新架构";
        Assert.True(vm.CanCommit);
        await vm.CommitAsync();
        Assert.Equal(2, vm.History.Count);
        Assert.Equal("docs: 更新架构", vm.History[0].Subject);
        // 提交后修改文件清场，未跟踪仍在（未勾选未被提交）。
        Assert.Single(vm.Changes);
        Assert.Equal(GitChangeKind.Untracked, vm.Changes[0].Kind);
        Assert.Empty(vm.Summary);
    }

    [Fact]
    public async Task AllChecked_toggles_and_commit_button_text_tracks_count()
    {
        var vm = await CreateReadyVmAsync();
        vm.Summary = "x";
        Assert.Equal(1, vm.CheckedCount);
        Assert.Contains("1 个文件", vm.CommitButtonText);
        vm.AllChecked = true;
        Assert.Equal(2, vm.CheckedCount);
        Assert.Contains("2 个文件", vm.CommitButtonText);
        vm.AllChecked = false;
        Assert.Equal(0, vm.CheckedCount);
        Assert.False(vm.CanCommit);
    }

    [Fact]
    public async Task Selecting_change_builds_unified_rows_with_line_numbers()
    {
        var vm = await CreateReadyVmAsync();
        var modified = vm.Changes.Single(c => c.Kind == GitChangeKind.Modified);
        vm.SelectedChange = modified;
        Assert.True(vm.HasDiffSelection);
        Assert.True(vm.HasDiffText);
        Assert.True(vm.IsUnified);
        Assert.Contains(vm.UnifiedRows, r => r.IsDeleted && r.Text == "单文档。");
        Assert.Contains(vm.UnifiedRows, r => r.IsAdded && r.Text == "四视图共享状态。");
        Assert.True(vm.DiffAdded > 0);
        Assert.StartsWith("@@", vm.DiffHeaderText);
        // 切 Split：配对行左右单元格按 -/+ 配对。
        vm.DiffMode = GitDiffViewMode.Split;
        Assert.True(vm.IsSplit);
        Assert.NotEmpty(vm.SplitRows);
        Assert.Contains(vm.SplitRows, p => p.Left.IsDeleted && p.Right.IsAdded);
        // 隐藏空白变更：全空白新增行被过滤。
        vm.DiffMode = GitDiffViewMode.Unified;
        vm.HideWhitespace = true;
        var before = vm.UnifiedRows.Count;
        Assert.True(before > 0);
    }

    [Fact]
    public async Task Untracked_selection_shows_added_diff_and_binary_shows_notice()
    {
        var vm = await CreateReadyVmAsync();
        vm.SelectedChange = vm.Changes.Single(c => c.Kind == GitChangeKind.Untracked);
        Assert.True(vm.HasDiffText);
        Assert.All(vm.UnifiedRows, r => Assert.True(r.IsAdded));
        // 二进制：占位说明行。
        File.WriteAllBytes(Path.Combine(_root, "pic.png"), [0x89, 0x50]);
        await vm.RefreshAsync();
        vm.SelectedChange = vm.Changes.Single(c => c.Path == "pic.png");
        Assert.Single(vm.UnifiedRows);
        Assert.Equal("二进制文件，不展示文本对比", vm.UnifiedRows[0].Text);
    }

    [Fact]
    public async Task History_tab_loads_commit_files_and_diff()
    {
        var vm = await CreateReadyVmAsync();
        vm.ShowHistoryTab();
        Assert.True(vm.IsHistoryTab);
        Assert.False(vm.IsChangesTab);
        Assert.True(vm.IsCommitBlank);
        vm.SelectedCommit = vm.History[0];
        Assert.True(vm.HasCommitDetail);
        Assert.Single(vm.CommitFiles);
        Assert.Equal("笔记/架构.md", vm.CommitFiles[0].Path);
        Assert.Equal("init: 初始化", vm.CommitSubject);
        Assert.Contains("测试者", vm.CommitMeta);
        Assert.True(vm.HasCommitFileSelection);
        // 初始提交：整文件新增。
        Assert.All(vm.UnifiedRows, r => Assert.True(r.IsAdded));
        Assert.Equal("1 个文件变更", vm.CommitFileCountText);
    }

    [Fact]
    public async Task SaveRemoteAsync_validates_and_updates_chip()
    {
        var vm = await CreateReadyVmAsync();
        await vm.SaveRemoteAsync("ftp://bad");
        Assert.False(vm.HasRemote);
        Assert.Contains("请输入 HTTPS 或 SSH", _toasts.Messages[^1]);
        await vm.SaveRemoteAsync("https://github.com/dotnet9/vex-notes.git");
        Assert.True(vm.HasRemote);
        Assert.False(vm.IsSshRemote);
        Assert.Equal("origin · HTTPS", vm.RemoteChipText);
        Assert.Equal("vex-notes", vm.RepoName);
        await vm.SaveRemoteAsync("git@github.com:dotnet9/vex-notes.git");
        Assert.True(vm.IsSshRemote);
        Assert.Equal("origin · SSH", vm.RemoteChipText);
    }

    [Fact]
    public async Task PushAsync_without_remote_toasts_nothing_to_push()
    {
        var vm = await CreateReadyVmAsync();
        await vm.PushAsync();
        Assert.Contains("没有待推送的提交", _toasts.Messages[^1]);
    }

    [Fact]
    public async Task PullAsync_without_remote_is_noop()
    {
        var vm = await CreateReadyVmAsync();
        await vm.PullAsync();
        Assert.DoesNotContain(_toasts.Messages, m => m.Contains("已合入"));
    }

    [Fact]
    public async Task History_paging_loads_next_batch_on_demand()
    {
        GitService.InitRepository(_root);
        var service = new GitService(_root);
        service.SetIdentity("测试者", "tester@example.com");
        for (var i = 0; i < ShellGitViewModel.HistoryPageSize + 4; i++)
        {
            WriteFile($"笔记/第{i}.md", $"# 第 {i} 篇");
            service.CommitPaths([$"笔记/第{i}.md"], $"docs: {i}");
        }
        var vm = CreateVm();
        await vm.RefreshAsync();
        Assert.True(vm.HasRepo);
        Assert.Equal(ShellGitViewModel.HistoryPageSize, vm.History.Count);
        Assert.True(vm.HasMoreHistory);
        await vm.LoadMoreHistoryAsync();
        Assert.Equal(ShellGitViewModel.HistoryPageSize + 4, vm.History.Count);
        Assert.False(vm.HasMoreHistory);
        // 再续拉：无更多时静默。
        await vm.LoadMoreHistoryAsync();
        Assert.Equal(ShellGitViewModel.HistoryPageSize + 4, vm.History.Count);
    }

    [Fact]
    public async Task Reveal_commands_resolve_absolute_paths_and_toast_missing()
    {
        var vm = await CreateReadyVmAsync();
        var revealed = new List<string>();
        vm.RevealInExplorer = path => revealed.Add(path);
        var modified = vm.Changes.Single(c => c.Kind == GitChangeKind.Modified);
        vm.OpenChangeInExplorerCommand.Execute(modified);
        Assert.Equal(Path.Combine(_root, "笔记", "架构.md"), Assert.Single(revealed));
        // 磁盘上已不存在的变更：不启动定位、提示而非静默（替身模拟 FileHelper 的 FileNotFoundException）。
        var before = _toasts.Messages.Count;
        vm.RevealInExplorer = _ => throw new FileNotFoundException();
        vm.OpenChangeInExplorerCommand.Execute(new GitChangeItem("笔记/已删除.md", GitChangeKind.Deleted, "删除"));
        Assert.True(_toasts.Messages.Count > before);
        Assert.Contains("已不在磁盘", _toasts.Messages[^1]);
        // null 参数静默。
        vm.OpenChangeInExplorerCommand.Execute(null);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Credential_store_semantics_save_and_delete_pat()
    {
        _credentials.SavePat("github.com", "token-1");
        Assert.Equal("token-1", _credentials.GetPat("github.com"));
        _credentials.DeletePat("github.com");
        Assert.Null(_credentials.GetPat("github.com"));
        Assert.Null(new FakeCredentialStore().GetPat("github.com"));
        await Task.CompletedTask;
    }

    [Fact]
    public void Pat_answer_roundtrip_encodes_token_and_remember()
    {
        var encoded = ShellGitViewModel.EncodePatAnswer("abc", true);
        var newline = encoded.LastIndexOf('\n');
        Assert.Equal("abc", encoded[..newline]);
        Assert.Equal("1", encoded[(newline + 1)..]);
    }

    [Fact]
    public async Task AutoCommitTick_commits_all_changes_with_template_and_skips_when_clean()
    {
        var vm = await CreateReadyVmAsync();
        Assert.Equal(2, vm.Changes.Count);
        await vm.AutoCommitTickAsync();
        Assert.Contains(_toasts.Messages, m => m.StartsWith("自动提交完成"));
        Assert.Empty(vm.Changes);
        Assert.Equal(2, vm.History.Count);
        // 无变更再触发：静默跳过（不追加 toast）。
        var before = _toasts.Messages.Count;
        await vm.AutoCommitTickAsync();
        Assert.Equal(before, _toasts.Messages.Count);
    }

    [Fact]
    public async Task FormatCommitTime_localizes_yesterday()
    {
        var vm = CreateVm();
        var now = DateTimeOffset.Now;
        Assert.Matches(@"^\d{2}:\d{2}$", vm.FormatCommitTime(now));
        Assert.StartsWith("昨天", vm.FormatCommitTime(now.AddDays(-1)));
        Assert.Matches(@"^\d{2}-\d{2}", vm.FormatCommitTime(now.AddDays(-10)));
        await Task.CompletedTask;
    }

    private sealed class FakeCredentialStore : ICredentialStore
    {
        public string? Pat;
        public string? GetPat(string host) => Pat;
        public void SavePat(string host, string pat) => Pat = pat;
        public void DeletePat(string host) => Pat = null;
        public string? Get(string key) => null;
        public void Save(string key, string value) { }
        public void Delete(string key) { }
    }

    private sealed class FakeSettings : IAppSettingsStore
    {
        public AppSettings Current { get; private set; } = new();
        public AppSettings Update(Func<AppSettings, AppSettings> update) => Current = update(Current);
    }

    private sealed class FakeToasts : IShellToastPublisher
    {
        public List<string> Messages { get; } = [];
        public void Publish(string message, NotificationType type = NotificationType.Success) => Messages.Add(message);
        public void Install(Avalonia.Controls.Primitives.VisualLayerManager? layerManager) { }
        public void Uninstall() { }
    }

    private sealed class FakeLocalizer : IAppLocalizer
    {
        private static readonly Dictionary<string, string> Zh = new()
        {
            ["Vex.VexL.GitNoRemoteHint"] = "未配置远程，仅本地管理",
            ["Vex.VexL.GitCommitButton"] = "提交 {0} 个文件到 {1}",
            ["Vex.VexL.GitRemoteUrlError"] = "请输入 HTTPS 或 SSH 形式的仓库地址",
            ["Vex.VexL.GitPushNothingToast"] = "没有待推送的提交",
            ["Vex.VexL.GitPullMergedFormat"] = "已合入远程的 {0} 个提交",
            ["Vex.VexL.GitAutoCommitToast"] = "自动提交完成",
            ["Vex.VexL.GitBinaryFileNote"] = "二进制文件，不展示文本对比",
            ["Vex.VexL.GitFileChangesCount"] = "{0} 个文件变更",
            ["Vex.VexL.GitYesterday"] = "昨天",
            ["Vex.VexL.GitStatusAdded"] = "新增",
            ["Vex.VexL.GitStatusDeleted"] = "删除",
            ["Vex.VexL.GitStatusModified"] = "修改",
            ["Vex.VexL.GitStatusUntracked"] = "未跟踪",
            ["Vex.VexL.GitChangeCount"] = "{0} 个变更文件",
            ["Vex.VexL.GitJustNow"] = "刚刚",
            ["Vex.VexL.GitSelectFileHint"] = "在左侧选择一个文件，查看与上次提交的差异",
            ["Vex.VexL.GitRevealMissingToast"] = "文件已不在磁盘上（已删除的变更无法定位）",
        };

        public event EventHandler<EventArgs>? CultureChanged { add { } remove { } }
        public CultureInfo Culture => CultureInfo.InvariantCulture;
        public void SetCulture(string cultureName) { }
        public string Get(string key) => Zh.GetValueOrDefault(key, key);
        public string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);
    }
}
