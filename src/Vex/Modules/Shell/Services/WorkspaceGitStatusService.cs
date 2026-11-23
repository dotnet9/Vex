using Avalonia.Threading;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Git;

namespace Vex.Modules.Shell.Services;

/// <summary>
/// 工作区 Git 状态徽章服务：常驻单例，独立于 Git 面板生命周期（面板仅在 Git 模式 Enter/Exit）。
/// 工作区根探测到仓库后启用；刷新把 RepositoryStatus 映射成「绝对路径 → 状态」字典，
/// 经 <see cref="WorkspaceGitStatusTree.Apply"/> 批量写入文件树（文件查表、文件夹聚合、差量 INPC）。
///
/// 触发源：工作区快照发布（DocumentFilesChangedCommand，树整体重建后补刷）、文件监控事件
/// （ShellFilesViewModel 转发）、文档落盘（DocumentSavedCommand）、Git 变更操作
/// （WorkspaceGitMutatedCommand：面板提交/推送与 MCP git 工具——提交不改工作区内容，监控收不到）。
///
/// 单工作者 + 置脏合并：刷新进行中的新触发只置脏，永远只算最新状态（渲染场景天然背压）；
/// 计算在后台线程（RetrieveStatus 是文件系统级 IO），应用经 marshal 回 UI 线程。
/// </summary>
public sealed class WorkspaceGitStatusService : IDisposable
{
    private readonly object _gate = new();
    private readonly UiMarshal _marshal;
    private GitService? _git;
    private readonly List<GitService> _retired = [];
    private string? _gitRoot;
    private string? _pendingRoot;
    private Func<IReadOnlyList<DocumentFileNode>>? _rootsProvider;
    private bool _dirty;
    private bool _running;
    private bool _disposed;

    /// <summary>测试注入口：默认回 UI 线程，测试传同步执行。</summary>
    public delegate void UiMarshal(Action action);

    public WorkspaceGitStatusService(UiMarshal? uiMarshal = null)
    {
        _marshal = uiMarshal ?? (action => Dispatcher.UIThread.Post(action));
        EventBus.Default.Subscribe(this);
    }

    /// <summary>文件树根提供者（ShellFilesViewModel 注册；工作区切换会整体重建节点实例）。</summary>
    public void Bind(Func<IReadOnlyList<DocumentFileNode>> rootsProvider)
    {
        lock (_gate)
        {
            _rootsProvider = rootsProvider;
            _dirty = true;
        }

        EnsureWorker();
    }

    [EventHandler]
    public void ApplyDocumentFilesChanged(DocumentFilesChangedCommand command) => SetWorkspace(command.WorkspaceRootPath);

    [EventHandler]
    public void ApplyDocumentSaved(DocumentSavedCommand command) => RequestRefresh();

    [EventHandler]
    public void ApplyWorkspaceGitMutated(WorkspaceGitMutatedCommand command) => RequestRefresh();

    /// <summary>设置期望工作区根：换根时重建/清空仓库句柄（非仓库 → 状态全清零，徽章静默禁用）。
    /// 退役句柄不在此处释放——工作者可能正拿它采集（LibGit2Sharp 并发用/释放原生层会崩），
    /// 移入退役列表由工作者线程在采集后统一释放。</summary>
    public void SetWorkspace(string? workspaceRoot)
    {
        lock (_gate)
        {
            _pendingRoot = workspaceRoot;
            var sameRoot = _gitRoot is not null && workspaceRoot is not null && SamePath(_gitRoot, workspaceRoot);
            if (!sameRoot)
            {
                if (_git is not null)
                {
                    _retired.Add(_git);
                }

                _git = null;
                _gitRoot = null;
                if (!string.IsNullOrWhiteSpace(workspaceRoot) && Directory.Exists(workspaceRoot)
                    && GitService.IsRepository(workspaceRoot))
                {
                    _git = new GitService(workspaceRoot);
                    _gitRoot = workspaceRoot;
                }
            }

            // 同根也置脏：树可能整体重建过，重刷一遍把状态补回新节点实例。
            _dirty = true;
        }

        EnsureWorker();
    }

    public void RequestRefresh()
    {
        lock (_gate)
        {
            _dirty = true;
        }

        EnsureWorker();
    }

    /// <summary>同步执行一次采集与应用（测试钩子；正常路径走后台置脏工作者，调用方无须等待）。</summary>
    public async Task RefreshNowAsync()
    {
        lock (_gate)
        {
            _dirty = false;
        }

        if (ComputeLatest() is { } snapshot)
        {
            ApplySnapshot(snapshot.RepoRoot, snapshot.Statuses);
        }

        await Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        EventBus.Default.Unsubscribe(this);
        _git?.Dispose();
        foreach (var service in _retired)
        {
            service.Dispose();
        }

        _retired.Clear();
    }

    private void EnsureWorker()
    {
        lock (_gate)
        {
            if (_running || _disposed)
            {
                return;
            }

            _running = true;
        }

        _ = Task.Run(ProcessLoop);
    }

    private void ProcessLoop()
    {
        while (true)
        {
            lock (_gate)
            {
                if (_disposed || !_dirty)
                {
                    _running = false;
                    return;
                }

                _dirty = false;
            }

            var snapshot = ComputeLatest();
            if (snapshot is null)
            {
                continue;
            }

            _marshal(() => ApplySnapshot(snapshot.Value.RepoRoot, snapshot.Value.Statuses));
        }
    }

    /// <summary>后台线程采集：仓库状态 → 绝对路径状态字典（LibGit2Sharp 异常吞掉保留上一份，下次触发重算）。
    /// 前导状态槽语义（REV2）：字典含全部受管文件——变更查 RetrieveStatus（未跟踪不入字典=树内空槽），
    /// 受管干净查索引命中（TrackedClean=挂锁）；未跟踪文件缺席字典，Apply 按.None 清零。</summary>
    private (string RepoRoot, Dictionary<string, WorkspaceGitStatus> Statuses)? ComputeLatest()
    {
        string? repoRoot;
        GitService? git;
        lock (_gate)
        {
            repoRoot = _gitRoot;
            git = _git;
        }

        if (repoRoot is null || git is null)
        {
            return (string.Empty, new Dictionary<string, WorkspaceGitStatus>(StringComparer.OrdinalIgnoreCase));
        }

        var statuses = new Dictionary<string, WorkspaceGitStatus>(StringComparer.OrdinalIgnoreCase);
        GitService[] retired;
        lock (_gate)
        {
            retired = [.. _retired];
            _retired.Clear();
        }

        try
        {
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in git.Status())
            {
                var relative = item.FilePath.Replace('\\', '/');
                changed.Add(relative);
                if (item.State.HasFlag(LibGit2Sharp.FileStatus.NewInWorkdir))
                {
                    // 未跟踪 = 不受管控 = 树内空槽：不入字典。
                    continue;
                }

                var absolute = Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                statuses[absolute] = MapKind(item.State);
            }

            foreach (var tracked in git.TrackedIndexPaths())
            {
                if (changed.Contains(tracked))
                {
                    continue;
                }

                statuses[Path.Combine(repoRoot, tracked.Replace('/', Path.DirectorySeparatorChar))] =
                    WorkspaceGitStatus.TrackedClean;
            }
        }
        catch (Exception exception)
        {
            DisposeRetired(retired);
            System.Diagnostics.Debug.WriteLine($"Git status refresh failed: {exception.Message}");
            return null;
        }

        // 采集已离开旧实例：此刻在工作者线程释放退役句柄是安全的（UI 线程 SetWorkspace 不再碰它们）。
        DisposeRetired(retired);
        return (repoRoot, statuses);
    }

    private void DisposeRetired(GitService[] retired)
    {
        foreach (var service in retired)
        {
            service.Dispose();
        }
    }

    private void ApplySnapshot(string repoRoot, IReadOnlyDictionary<string, WorkspaceGitStatus> statuses)
    {
        string? expected;
        Func<IReadOnlyList<DocumentFileNode>>? provider;
        lock (_gate)
        {
            expected = _pendingRoot;
            provider = _rootsProvider;
        }

        if (provider is null)
        {
            return;
        }

        // 工作区已切走（期望根与服务根不符）：把树清零而不是抹上别的仓库的状态。
        var effective = SamePath(expected, repoRoot)
            ? statuses
            : new Dictionary<string, WorkspaceGitStatus>(StringComparer.OrdinalIgnoreCase);
        WorkspaceGitStatusTree.Apply(provider(), effective);
    }

    private static WorkspaceGitStatus MapKind(LibGit2Sharp.FileStatus state)
    {
        if (state.HasFlag(LibGit2Sharp.FileStatus.Conflicted)) return WorkspaceGitStatus.Conflicted;
        if (state.HasFlag(LibGit2Sharp.FileStatus.NewInWorkdir)) return WorkspaceGitStatus.Untracked;
        if (state.HasFlag(LibGit2Sharp.FileStatus.NewInIndex)) return WorkspaceGitStatus.New;
        if (state.HasFlag(LibGit2Sharp.FileStatus.DeletedFromWorkdir)
            || state.HasFlag(LibGit2Sharp.FileStatus.DeletedFromIndex)) return WorkspaceGitStatus.Deleted;
        if (state.HasFlag(LibGit2Sharp.FileStatus.RenamedInWorkdir)
            || state.HasFlag(LibGit2Sharp.FileStatus.RenamedInIndex)) return WorkspaceGitStatus.Renamed;
        if (state.HasFlag(LibGit2Sharp.FileStatus.ModifiedInWorkdir)
            || state.HasFlag(LibGit2Sharp.FileStatus.ModifiedInIndex)) return WorkspaceGitStatus.Modified;
        return WorkspaceGitStatus.None;
    }

    private static bool SamePath(string? left, string? right) =>
        string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
