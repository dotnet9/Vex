using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Git;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Git.ViewModels;

/// <summary>Git 面板页签（原型 Changes/History 双页签）。</summary>
public enum GitPanelTab
{
    Changes,
    History,
}

/// <summary>Diff 显示模式（原型右上齿轮弹层：Unified 单栏 / Split 左右对照）。</summary>
public enum GitDiffViewMode
{
    Unified,
    Split,
}

/// <summary>Unified 单行：左右行号 + 符号 + 文本。</summary>
public sealed record GitDiffRow(int? LeftNumber, int? RightNumber, char Sign, string Text, char Kind)
{
    public bool IsAdded => Kind == '+';
    public bool IsDeleted => Kind == '-';
    public bool IsContext => Kind == ' ';
    public string DisplaySign => Sign == '\0' ? string.Empty : Sign.ToString();
    public string LeftNumberText => LeftNumber?.ToString() ?? string.Empty;
    public string RightNumberText => RightNumber?.ToString() ?? string.Empty;
}

/// <summary>Split 单元格：行号 + 文本 + 类别（' '/'+'/'-'；'e' = 空侧）。</summary>
public sealed record GitDiffCell(int? Number, string Text, char Kind)
{
    public bool IsEmpty => Kind == 'e';
    public bool IsAdded => Kind == '+';
    public bool IsDeleted => Kind == '-';
    public bool IsContext => Kind == ' ';
    public string NumberText => Number?.ToString() ?? string.Empty;
}

/// <summary>Split 左右配对行：−块与其后的 + 块按行配对，余者单侧留空。</summary>
public sealed record GitDiffPairRow(GitDiffCell Left, GitDiffCell Right);

/// <summary>分支下拉行：分组头或分支行（GitHub Desktop 分支列表口径）。</summary>
public sealed record GitBranchRow(
    string Name,
    bool IsCurrent,
    string GroupName,
    string DateText,
    bool IsGroupHeader = false)
{
    public string Display => IsGroupHeader ? GroupName : Name;
}

/// <summary>已打开仓库下拉行（工作区主根 + 附加根中的 git 仓库）。</summary>
public sealed record GitRepoRow(string Path, string Name, bool IsCurrent);

/// <summary>分支名称弹层结果：新名称 + 基准分支（rename 时基准与旧分支相同）。</summary>
public sealed record GitBranchDialogResult(string Name, string BaseBranch);

/// <summary>分支名称弹层请求：create 模式带「基于…」二选一；rename 模式预填旧名。</summary>
public sealed record GitBranchDialogRequest(
    GitBranchDialogMode Mode,
    string InitialName,
    IReadOnlyList<string> Branches,
    string DefaultBranch,
    string CurrentBranch)
{
    public string? SelectedBase { get; set; }
}

public enum GitBranchDialogMode { Create, Rename }

/// <summary>同步按钮语义（GitHub Desktop push-pull-button 状态机）。</summary>
public enum GitSyncKind { Push, Pull, Fetch, Publish }

/// <summary>变更列表行：octicon 状态 + 目录/文件名拆分 + 勾选态 + 行级增删统计。</summary>
public sealed class GitChangeItem : ReactiveObject
{
    private bool _checked;
    private bool _isSelected;

    public GitChangeItem(string path, GitChangeKind kind, string statusText, int linesAdded = -1, int linesDeleted = -1)
    {
        Path = path;
        Kind = kind;
        StatusText = statusText;
        LinesAdded = linesAdded;
        LinesDeleted = linesDeleted;
        var slash = path.LastIndexOf('/');
        Dir = slash >= 0 ? path[..(slash + 1)] : string.Empty;
        Name = slash >= 0 ? path[(slash + 1)..] : path;
        StatusLabel = kind switch
        {
            GitChangeKind.Added => "A",
            GitChangeKind.Deleted => "D",
            GitChangeKind.Modified => "M",
            GitChangeKind.Renamed => "R",
            GitChangeKind.Conflicted => "C",
            _ => "?",
        };
        Checked = kind != GitChangeKind.Untracked;
    }

    public string Path { get; }
    public string Dir { get; }
    public string Name { get; }
    public GitChangeKind Kind { get; }
    public string StatusLabel { get; }

    /// <summary>未跟踪默认不勾选（原型 seed 口径），其余默认勾选。</summary>
    public bool Checked { get => _checked; set { this.RaiseAndSetIfChanged(ref _checked, value); } }

    public bool IsSelected { get => _isSelected; set { this.RaiseAndSetIfChanged(ref _isSelected, value); } }

    /// <summary>状态描边标志的样式开关（黄点/绿加号/红减号/灰问号）。</summary>
    public bool IsModified => Kind == GitChangeKind.Modified || Kind == GitChangeKind.Renamed;
    public bool IsAdded => Kind == GitChangeKind.Added;
    public bool IsDeleted => Kind == GitChangeKind.Deleted;
    public bool IsUntracked => Kind == GitChangeKind.Untracked;

    /// <summary>状态说明（描边标志 tooltip，构造时按当前语言传入）。</summary>
    public string StatusText { get; }

    /// <summary>行级增删统计（-1 = 未知/二进制，UI 不显示计数）。</summary>
    public int LinesAdded { get; }
    public int LinesDeleted { get; }
    public bool HasLineStats => LinesAdded >= 0 && LinesDeleted >= 0;
    public string LinesAddedText => LinesAdded >= 0 ? $"+{LinesAdded}" : string.Empty;
    public string LinesDeletedText => LinesDeleted >= 0 ? $"−{LinesDeleted}" : string.Empty;

    /// <summary>映射到文件树徽章同款状态枚举（octicon 字形与配色共用 GitBadgeConverters）。</summary>
    public WorkspaceGitStatus Status => Kind switch
    {
        GitChangeKind.Added => WorkspaceGitStatus.New,
        GitChangeKind.Untracked => WorkspaceGitStatus.Untracked,
        GitChangeKind.Modified => WorkspaceGitStatus.Modified,
        GitChangeKind.Deleted => WorkspaceGitStatus.Deleted,
        GitChangeKind.Renamed => WorkspaceGitStatus.Renamed,
        GitChangeKind.Conflicted => WorkspaceGitStatus.Conflicted,
        _ => WorkspaceGitStatus.None,
    };
}

/// <summary>历史文件行：状态描边 + 路径。</summary>
public sealed record GitCommitFileItem(string Path, string StatusLabel)
{
    public bool IsModified => StatusLabel is "M" or "R";
    public bool IsAdded => StatusLabel == "A";
    public bool IsDeleted => StatusLabel == "D";
    public bool IsUntracked => StatusLabel == "?";
}

/// <summary>历史提交行。</summary>
public sealed class GitCommitItem
{
    public GitCommitItem(CommitInfo info, string timeText)
    {
        Sha = info.Sha;
        Subject = info.Message;
        Author = info.Author;
        TimeText = timeText;
        Tag = info.Tag;
    }

    public string Sha { get; }
    public string Subject { get; }
    public string Author { get; }
    public string TimeText { get; }
    public string? Tag { get; }

    public bool HasTag => !string.IsNullOrEmpty(Tag);

    /// <summary>列表 meta（原型：作者 · 时间）。</summary>
    public string MetaText => Author + " · " + TimeText;
}

/// <summary>
/// Git 管理面板（原型 git-panel.js 的正式实现）：仓库探测 → 初始化/添加远程 → Changes/History 双页签
/// → 勾选提交（Summary 必填）→ HTTPS+PAT 推送拉取（SSH 交系统 git 并以警示芯片提示）→ Unified/Split 双 Diff。
/// </summary>
public sealed class ShellGitViewModel : ReactiveObject
{
    private readonly IAppLocalizer _localizer;
    private readonly IShellToastPublisher _toasts;
    private readonly EditorPreferences _preferences;
    private readonly ICredentialStore _credentials;

    private string? _workspaceFolder;
    private GitService? _service;
    private GitPanelTab _tab = GitPanelTab.Changes;
    private bool _hasRepo;
    private string _branchName = string.Empty;
    private string? _remoteRawUrl;
    private int _ahead;
    private int _behind;
    private string? _lastSyncText;
    private string _filterText = string.Empty;
    private GitChangeItem? _selectedChange;
    private string _summary = string.Empty;
    private string _description = string.Empty;
    private GitDiffViewMode _diffMode = GitDiffViewMode.Unified;
    private bool _hideWhitespace;
    private IReadOnlyList<DiffHunk> _diffHunks = [];
    private IReadOnlyList<GitDiffRow> _unifiedRows = [];
    private IReadOnlyList<GitDiffPairRow> _splitRows = [];
    private int _diffAdded;
    private int _diffDeleted;
    private bool _hasDiffSelection;
    private GitCommitItem? _selectedCommit;
    private IReadOnlyList<GitCommitFileItem> _commitFiles = [];
    private GitCommitFileItem? _selectedCommitFile;
    private bool _hasCommitFileSelection;
    private string _commitSubject = string.Empty;
    private string _commitMeta = string.Empty;
    private int _commitAdded;
    private int _commitDeleted;
    private bool _isBusy;

    /// <summary>网络操作进行中（根修：此前各操作只 Raise 自己的 CanXxx，其他按钮的可用性
    /// 状态滞留——「提交后推送按钮点不了」的根因）。</summary>
    private bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value) return;
            _isBusy = value;
            this.RaisePropertyChanged(nameof(CanPush));
            this.RaisePropertyChanged(nameof(CanPull));
            this.RaisePropertyChanged(nameof(CanCommit));
            this.RaisePropertyChanged(nameof(CanSync));
            this.RaisePropertyChanged(nameof(CanManageBranch));
        }
    }

    public ShellGitViewModel(IAppLocalizer localizer, IShellToastPublisher toasts, EditorPreferences preferences, ICredentialStore credentials)
    {
        _localizer = localizer;
        _toasts = toasts;
        _preferences = preferences;
        _credentials = credentials;
        SetDiffModeCommand = new RelayCommand<string?>(SetDiffMode);
        CreateTagCommand = new AsyncRelayCommand<string?>(CreateTagAsync);
        DiscardChangeCommand = new AsyncRelayCommand<GitChangeItem?>(async item => await DiscardChangeAsync(item));
        RenameBranchCommand = new AsyncRelayCommand<string?>(RenameBranchFromDialogAsync);
        DeleteBranchCommand = new AsyncRelayCommand<string?>(DeleteBranchFromDialogAsync);
        this.RaisePropertyChanged(nameof(DiscardChangeCommand));
        OpenChangeInExplorerCommand = new RelayCommand<GitChangeItem?>(RevealChange);
        OpenCommitFileInExplorerCommand = new RelayCommand<GitCommitFileItem?>(RevealCommitFile);
        _preferences.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorPreferences.GitAutoCommit) or nameof(EditorPreferences.GitAutoCommitInterval))
            {
                SyncAutoCommitTimer();
            }
        };
        // 工作区整体切换（打开/切换目录）时重建 Git 面板上下文。
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
    }

    /// <summary>工作区根目录来源（MainWindowViewModel 注入；未打开文件夹时 Git 功能不可用）。
    /// 已选中仓库根时优先生效（仓库下拉切换）。</summary>
    public Func<string?>? WorkspaceFolderProvider { get; set; }

    /// <summary>工作区根目录来源（主根 + 附加根；仓库下拉数据源）。</summary>
    public Func<IReadOnlyList<string>>? WorkspaceRootsProvider { get; set; }

    /// <summary>仓库下拉当前选中的根（null = 跟随主根；打开/切换目录时重置回主根）。</summary>
    public string? SelectedRepoRoot { get; private set; }

    /// <summary>Git 模式开关（标题栏 Git 菜单显隐的数据源；Enter/Exit 维护）。</summary>
    public bool IsGitMode { get; private set; }

    /// <summary>工作区整体切换：Git 面板跟随新主根重建上下文（打开目录 ≠ 添加附加根——附加根只进仓库下拉）。
    /// 路径未变（同目录刷新）时跳过；非仓库目录清空面板回英雄区。</summary>
    private void ApplyWorkspaceRootChanged(string? newRoot)
    {
        if (string.IsNullOrWhiteSpace(newRoot)) return;
        var sameRoot = _workspaceFolder is not null && string.Equals(
            _workspaceFolder, newRoot,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        if (sameRoot && _service is not null) return;

        SelectedRepoRoot = null;
        _workspaceFolder = newRoot;
        LastSyncText = null;
        SelectedCommit = null;
        BranchRows.Clear();
        if (!GitService.IsRepository(newRoot))
        {
            ResetState();
            return;
        }
        _ = RefreshAsync();
    }

    [EventHandler]
    public void ApplyDocumentFilesChanged(DocumentFilesChangedCommand command) =>
        ApplyWorkspaceRootChanged(command.WorkspaceRootPath);

    /// <summary>PAT 输入弹层（宿主注入）：HTTPS 远程无已存凭据时调用，返回 null 表示取消。</summary>
    public Func<Task<string?>>? PatPrompt { get; set; }

    /// <summary>放弃更改确认浮层（宿主注入）：返回 false 表示取消。</summary>
    public Func<string, Task<bool>>? DiscardConfirm { get; set; }

    /// <summary>远程地址弹层（宿主注入）：返回 null 表示取消。</summary>
    public Func<Task<string?>>? RemoteDialog { get; set; }

    public ObservableCollection<GitChangeItem> Changes { get; } = [];
    public ObservableCollection<GitCommitItem> History { get; } = [];

    /// <summary>分支下拉行（含分组头；GitHub Desktop 分支列表口径：默认/最近/其他）。</summary>
    public ObservableCollection<GitBranchRow> BranchRows { get; } = [];

    /// <summary>已打开仓库（工作区主根 + 附加根中的 git 仓库；GitHub Desktop Current repository ▾ 口径）。</summary>
    public ObservableCollection<GitRepoRow> OpenRepos { get; } = [];

    /// <summary>历史分页页大小（滚动近底部续拉下一批，原型滚动加载口径）。</summary>
    public const int HistoryPageSize = 30;

    private bool _isLoadingHistory;

    /// <summary>还有更多历史可加载（上一批满页即假定有更多）。</summary>
    public bool HasMoreHistory { get; private set; }

    /// <summary>续拉下一批历史（追加到列表尾部；正在加载或无更多时静默跳过）。</summary>
    public async Task LoadMoreHistoryAsync()
    {
        if (_service is null || !HasRepo || _isLoadingHistory || !HasMoreHistory) return;
        _isLoadingHistory = true;
        try
        {
            var skip = History.Count;
            var batch = _service.History(skip, HistoryPageSize);
            foreach (var info in batch) History.Add(new GitCommitItem(info, FormatCommitTime(info.When)));
            HasMoreHistory = batch.Count >= HistoryPageSize;
            this.RaisePropertyChanged(nameof(HasMoreHistory));
        }
        catch
        {
            HasMoreHistory = false;
            this.RaisePropertyChanged(nameof(HasMoreHistory));
        }
        finally
        {
            _isLoadingHistory = false;
        }
        await Task.CompletedTask;
    }
    public ObservableCollection<GitChangeItem> VisibleChanges { get; } = [];

    public GitPanelTab Tab
    {
        get => _tab;
        set
        {
            this.RaiseAndSetIfChanged(ref _tab, value);
            this.RaisePropertyChanged(nameof(IsChangesTab));
            this.RaisePropertyChanged(nameof(IsHistoryTab));
        }
    }

    public bool IsChangesTab => Tab == GitPanelTab.Changes;
    public bool IsHistoryTab => Tab == GitPanelTab.History;

    public void ShowChangesTab() => Tab = GitPanelTab.Changes;
    public void ShowHistoryTab() => Tab = GitPanelTab.History;

    /// <summary>资源管理器定位委托：默认 Toolkit 跨平台 FileHelper（explorer /select、open -R、桌面环境分发）；
    /// 验证工程换替身避免真启进程。</summary>
    public Action<string> RevealInExplorer { get; set; } = CodeWF.Toolkit.Core.FileExtensions.FileHelper.OpenFolderAndSelectFile;

    /// <summary>打开变更文件所在目录并选中（右键菜单）。</summary>
    public RelayCommand<GitChangeItem?> OpenChangeInExplorerCommand { get; }

    /// <summary>打开历史提交文件所在目录并选中（右键菜单）。</summary>
    public RelayCommand<GitCommitFileItem?> OpenCommitFileInExplorerCommand { get; }

    private void RevealChange(GitChangeItem? item)
    {
        if (item is null || _workspaceFolder is null) return;
        RevealPath(Path.Combine(_workspaceFolder, item.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }

    private void RevealCommitFile(GitCommitFileItem? item)
    {
        if (item is null || _workspaceFolder is null) return;
        RevealPath(Path.Combine(_workspaceFolder, item.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }

    private void RevealPath(string fullPath)
    {
        try
        {
            RevealInExplorer(fullPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // 已删除的变更磁盘上无文件：提示而非静默。
            _toasts.Publish(_localizer.Get(VexL.GitRevealMissingToast), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>在 HEAD 打轻量 tag（可选立即推送）：发布流程「升版提交后打发布 tag」的面板入口。</summary>
    public AsyncRelayCommand<string?> CreateTagCommand { get; }
    public AsyncRelayCommand<GitChangeItem?> DiscardChangeCommand { get; }
    public AsyncRelayCommand<string?> RenameBranchCommand { get; }
    public AsyncRelayCommand<string?> DeleteBranchCommand { get; }


    /// <summary>GitService 稳定错误码 → 本地化文案；未知异常回落原始消息。</summary>
    private string LocalizeGitError(Exception exception) => exception is GitServiceException service
        ? service.Code switch
        {
            GitServiceError.CommitIdentityMissing => _localizer.Get(VexL.GitErrorCommitIdentityMissing),
            GitServiceError.NoOriginRemote => _localizer.Get(VexL.GitErrorNoOriginRemote),
            GitServiceError.NonFastForward => _localizer.Get(VexL.GitErrorNonFastForward),
            GitServiceError.NoCommitsForTag => _localizer.Get(VexL.GitErrorNoCommitsForTag),
            GitServiceError.CommitNotFound => _localizer.Format(VexL.GitErrorCommitNotFoundFormat, service.Sha ?? string.Empty),
            _ => exception.Message
        }
        : exception.Message;

    private string _newTagName = string.Empty;
    private bool _tagAlsoPush = true;

    /// <summary>tag 名称输入（Flyout 表单绑定）。</summary>
    public string NewTagName { get => _newTagName; set => this.RaiseAndSetIfChanged(ref _newTagName, value); }

    /// <summary>创建后同时推送（默认勾选：发布 tag 触发 CI）。</summary>
    public bool TagAlsoPush { get => _tagAlsoPush; set => this.RaiseAndSetIfChanged(ref _tagAlsoPush, value); }

    /// <summary>tag 名称规则：发布 tag（v 开头数字）或任意非空合法名。</summary>
    /// <summary>分支管理（占位：桌面版后续提供分支列表/切换/创建）。</summary>
    public void BranchManage() => _toasts.Publish(_localizer.Get(VexL.GitBranchManagementToast));

    /// <summary>tag 表单预设置：alsoPush 决定「同时推送」初始勾选（分支下拉两个入口分别进入）。</summary>
    public void PrepareTagFlyout(bool alsoPush)
    {
        NewTagName = string.Empty;
        TagAlsoPush = alsoPush;
    }

    /// <summary>无头通道（--git-run tag）：等待完成的 tag 创建；不自动推送（推送走 push-tag 动作保持正交）。</summary>
    public Task CreateTagHeadlessAsync(string? name)
    {
        TagAlsoPush = false;
        return CreateTagAsync(name);
    }

    private async Task CreateTagAsync(string? name)
    {
        if (_service is null || string.IsNullOrWhiteSpace(name))
        {
            _toasts.Publish(_localizer.Get(VexL.GitTagNameInvalid), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        var clean = name.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(clean, "^[A-Za-z0-9._-]+$"))
        {
            _toasts.Publish(_localizer.Get(VexL.GitTagNameInvalid), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        if (!_service.HasCommits())
        {
            _toasts.Publish(_localizer.Get(VexL.GitTagNoCommitToast), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        try
        {
            if (TagTargetSha is { } sha) _service.CreateTag(clean, sha); else _service.CreateTag(clean);
            TagTargetSha = null;
            _toasts.Publish(_localizer.Format(VexL.GitTagCreatedFormat, clean));
            NewTagName = string.Empty;
            if (TagAlsoPush)
            {
                await PushTagAsync(clean);
            }
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>推送 tag 到 origin（发布 tag 触发 CI 的关键一步）。</summary>
    public async Task PushTagAsync(string? name)
    {
        if (_service is null || !HasRemote || string.IsNullOrWhiteSpace(name)) return;
        IsBusy = true;
        try
        {
            var tag = name.Trim();
            await PushWithTagAsync(tag);
            _toasts.Publish(_localizer.Format(VexL.GitTagPushedFormat, tag));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PushWithTagAsync(string tag)
    {
        var pat = IsHttpsRemote ? await ResolvePatAsync() : null;
        if (pat == null && IsHttpsRemote) return;
        var branch = BranchName;
        _service!.Push(pat ?? string.Empty, tag: tag);
        LastSyncText = DateTime.Now.ToString("HH:mm");
    }

    /// <summary>已存 PAT（HTTPS 远程；无弹层打扰的静默解析，供 MCP/自动化通道使用）。</summary>
    public string? ResolveStoredPat()
    {
        if (!IsHttpsRemote) return null;
        var host = _service?.RemoteCredentialHost();
        return string.IsNullOrEmpty(host) ? null : _credentials.GetPat(host);
    }

    /// <summary>Diff Options 弹层的模式切换（RadioButton 的 IsChecked 绑只读计算属性无法回写，走命令）。</summary>
    public RelayCommand<string?> SetDiffModeCommand { get; }

    /// <summary>命令入口：mode 取 "Unified" / "Split"。</summary>
    public void SetDiffMode(string? mode) => DiffMode = mode == "Split" ? GitDiffViewMode.Split : GitDiffViewMode.Unified;

    public bool HasRepo { get => _hasRepo; private set => this.RaiseAndSetIfChanged(ref _hasRepo, value); }

    public string BranchName { get => _branchName; private set => this.RaiseAndSetIfChanged(ref _branchName, value); }

    public string RepoName
    {
        get
        {
            if (RemoteUrl is { } url)
            {
                var match = new System.Text.RegularExpressions.Regex(@"/([^/]+?)(?:\.git)?$").Match(url.TrimEnd('/'));
                if (match.Success) return match.Groups[1].Value;
            }
            return _workspaceFolder is null ? string.Empty : System.IO.Path.GetFileName(_workspaceFolder.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        }
    }

    public string? RemoteRawUrl { get => _remoteRawUrl; private set { this.RaiseAndSetIfChanged(ref _remoteRawUrl, value); this.RaisePropertyChanged(nameof(RepoName)); } }

    public string? RemoteUrl => GitService.NormalizeRemoteUrl(RemoteRawUrl);

    public bool HasRemote => RemoteRawUrl is not null;

    public bool IsSshRemote { get => _isSshRemote; private set => this.RaiseAndSetIfChanged(ref _isSshRemote, value); }
    private bool _isSshRemote;

    /// <summary>远程协议芯片文案（origin · HTTPS / origin · SSH）。</summary>
    public string RemoteChipText => HasRemote ? "origin · " + (IsSshRemote ? "SSH" : "HTTPS") : string.Empty;

    public int Ahead { get => _ahead; private set { this.RaiseAndSetIfChanged(ref _ahead, value); this.RaisePropertyChanged(nameof(SyncChipText)); this.RaisePropertyChanged(nameof(HasSyncDelta)); RaiseSyncProps(); } }

    public int Behind { get => _behind; private set { this.RaiseAndSetIfChanged(ref _behind, value); this.RaisePropertyChanged(nameof(SyncChipText)); this.RaisePropertyChanged(nameof(HasSyncDelta)); RaiseSyncProps(); } }

    public bool HasSyncDelta => Ahead > 0 || Behind > 0;

    public string SyncChipText => (Ahead > 0 ? "↑" + Ahead + " " : string.Empty) + (Behind > 0 ? "↓" + Behind : string.Empty);

    /// <summary>上次同步时间（顶栏小字）；null 显示 —。</summary>
    public string? LastSyncText { get => _lastSyncText; private set => this.RaiseAndSetIfChanged(ref _lastSyncText, value); }

    public string LastSyncDisplay => LastSyncText ?? "—";

    private void RaiseLastSyncProps() => RaiseSyncProps();

    private double _leftColumnWidth = 300;

    /// <summary>左右面板分隔（原型 minmax(300px,38%) 的可拖拽化）：拖动即时生效，会话态。
    /// 列宽绑定的类型口径是 GridLength（sidebar-column 同款），双精度直接绑定不会转换。</summary>
    public Avalonia.Controls.GridLength LeftColumnWidth
    {
        get => new(_leftColumnWidth);
        set
        {
            if (!value.IsAbsolute || !double.IsFinite(value.Value)) return;
            var clamped = Math.Clamp(value.Value, 260, 640);
            if (clamped == _leftColumnWidth) return;
            _leftColumnWidth = clamped;
            this.RaisePropertyChanged();
        }
    }

    private double _commitFileListWidth = 220;

    /// <summary>History 右区 [文件列表 | 对比] 两栏的分隔（原型 minmax(220px,288px) 的可拖拽化），会话态。</summary>
    public Avalonia.Controls.GridLength CommitFileListWidth
    {
        get => new(_commitFileListWidth);
        set
        {
            if (!value.IsAbsolute || !double.IsFinite(value.Value)) return;
            var clamped = Math.Clamp(value.Value, 200, 440);
            if (clamped == _commitFileListWidth) return;
            _commitFileListWidth = clamped;
            this.RaisePropertyChanged();
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            this.RaiseAndSetIfChanged(ref _filterText, value);
            RefreshVisibleChanges();
        }
    }

    private void RefreshVisibleChanges()
    {
        var keyword = _filterText.Trim();
        VisibleChanges.Clear();
        foreach (var change in Changes)
        {
            if (keyword.Length == 0 || change.Path.Contains(keyword, StringComparison.OrdinalIgnoreCase)) VisibleChanges.Add(change);
        }
        RaiseViewFlags();
    }

    public GitChangeItem? SelectedChange
    {
        get => _selectedChange;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedChange, value);
            foreach (var item in Changes) item.IsSelected = ReferenceEquals(item, value);
            LoadWorkdirDiff();
        }
    }

    public string Summary
    {
        get => _summary;
        set
        {
            this.RaiseAndSetIfChanged(ref _summary, value);
            this.RaisePropertyChanged(nameof(CanCommit));
        }
    }

    public string Description { get => _description; set => this.RaiseAndSetIfChanged(ref _description, value); }

    public int CheckedCount => Changes.Count(item => item.Checked);

    public bool CanCommit => CheckedCount > 0 && !string.IsNullOrWhiteSpace(Summary) && !_isBusy;

    public string CommitButtonText => _localizer.Format(VexL.GitCommitButton, CheckedCount, BranchName.Length == 0 ? "main" : BranchName);

    /// <summary>全选框：全勾 = true；设置即批量勾选/取消（原型 git-stage-all）。</summary>
    public bool AllChecked
    {
        get => Changes.Count > 0 && Changes.All(item => item.Checked);
        set
        {
            foreach (var item in Changes) item.Checked = value;
            this.RaisePropertyChanged(nameof(CheckedCount));
            this.RaisePropertyChanged(nameof(CanCommit));
            this.RaisePropertyChanged(nameof(CommitButtonText));
        }
    }

    /// <summary>合成 hunk 头（原型 diffHtml 的 @@ -1,N +1,M @@ 口径，按显示行数计算）。</summary>
    public string DiffHeaderText
    {
        get
        {
            var rows = DiffMode == GitDiffViewMode.Unified ? UnifiedRows.Count : SplitRows.Count * 2;
            return "@@ -1," + (rows - DiffAdded) + " +1," + (rows - DiffDeleted) + " @@";
        }
    }

    public GitDiffViewMode DiffMode
    {
        get => _diffMode;
        set
        {
            this.RaiseAndSetIfChanged(ref _diffMode, value);
            this.RaisePropertyChanged(nameof(IsUnified));
            this.RaisePropertyChanged(nameof(IsSplit));
            RecomputeDiffView();
        }
    }

    public bool IsUnified => DiffMode == GitDiffViewMode.Unified;
    public bool IsSplit => DiffMode == GitDiffViewMode.Split;

    public bool HideWhitespace
    {
        get => _hideWhitespace;
        set
        {
            this.RaiseAndSetIfChanged(ref _hideWhitespace, value);
            RecomputeDiffView();
        }
    }

    /// <summary>Unified 行序列（右区主 diff 视图）。</summary>
    public IReadOnlyList<GitDiffRow> UnifiedRows { get => _unifiedRows; private set => this.RaiseAndSetIfChanged(ref _unifiedRows, value); }

    /// <summary>Split 配对行序列。</summary>
    public IReadOnlyList<GitDiffPairRow> SplitRows { get => _splitRows; private set => this.RaiseAndSetIfChanged(ref _splitRows, value); }

    public int DiffAdded { get => _diffAdded; private set => this.RaiseAndSetIfChanged(ref _diffAdded, value); }

    public int DiffDeleted { get => _diffDeleted; private set => this.RaiseAndSetIfChanged(ref _diffDeleted, value); }

    /// <summary>右区当前有可展示的文件（Changes 选中或 History 选中文件）。</summary>
    public bool HasDiffSelection
    {
        get => _hasDiffSelection;
        private set
        {
            this.RaiseAndSetIfChanged(ref _hasDiffSelection, value);
            this.RaisePropertyChanged(nameof(ShowChangeBlank));
            this.RaisePropertyChanged(nameof(ChangeBlankText));
        }
    }

    public GitCommitItem? SelectedCommit
    {
        get => _selectedCommit;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCommit, value);
            HasDiffSelection = false;
            SelectedCommitFile = null;
            LoadCommitDetail();
        }
    }

    /// <summary>历史详情头：提交主题。</summary>
    public string CommitSubject { get => _commitSubject; private set => this.RaiseAndSetIfChanged(ref _commitSubject, value); }

    /// <summary>历史详情头：作者 · 时间 · hash。</summary>
    public string CommitMeta { get => _commitMeta; private set => this.RaiseAndSetIfChanged(ref _commitMeta, value); }

    public int CommitAdded { get => _commitAdded; private set => this.RaiseAndSetIfChanged(ref _commitAdded, value); }

    public int CommitDeleted { get => _commitDeleted; private set => this.RaiseAndSetIfChanged(ref _commitDeleted, value); }

    public IReadOnlyList<GitCommitFileItem> CommitFiles { get => _commitFiles; private set => this.RaiseAndSetIfChanged(ref _commitFiles, value); }

    public GitCommitFileItem? SelectedCommitFile
    {
        get => _selectedCommitFile;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedCommitFile, value);
            LoadCommitFileDiff();
        }
    }

    public bool HasCommitFileSelection
    {
        get => _hasCommitFileSelection;
        private set
        {
            this.RaiseAndSetIfChanged(ref _hasCommitFileSelection, value);
            this.RaisePropertyChanged(nameof(ShowCommitFileBlank));
        }
    }

    public bool HasCommitDetail => SelectedCommit is not null;

    public bool CanPush => HasRemote && !_isBusy;
    public bool CanPull => HasRemote && !_isBusy;
    public bool CanSync => HasRepo && !_isBusy;
    public bool CanManageBranch => HasRepo && !_isBusy;

    /// <summary>同步按钮语义（GitHub Desktop push-pull-button 口径）：随状态换动作，永不禁用。</summary>
    public GitSyncKind SyncKind
    {
        get
        {
            if (!HasRemote) return GitSyncKind.Publish;
            if (Ahead > 0) return GitSyncKind.Push;
            if (Behind > 0) return GitSyncKind.Pull;
            return GitSyncKind.Fetch;
        }
    }

    public string SyncTitle => SyncKind switch
    {
        GitSyncKind.Push => _localizer.Get(VexL.GitSyncPushLabel) + " origin",
        GitSyncKind.Pull => _localizer.Get(VexL.GitSyncPullLabel) + " origin",
        GitSyncKind.Fetch => _localizer.Get(VexL.GitSyncFetchLabel) + " origin",
        _ => _localizer.Get(VexL.GitSyncPublishLabel),
    };

    /// <summary>副文案：上次拉取时间 / 从未拉取 / 未配置远程。</summary>
    public string SyncSubText => !HasRemote
        ? _localizer.Get(VexL.GitNoRemoteHint)
        : LastSyncText is null ? _localizer.Get(VexL.GitNeverFetched) : _localizer.Get(VexL.GitLastFetch) + " " + LastSyncText;

    public string SyncBadgeUp => Ahead > 0 ? "↑" + Ahead : string.Empty;
    public string SyncBadgeDown => Behind > 0 ? "↓" + Behind : string.Empty;
    public bool SyncIsPush => SyncKind == GitSyncKind.Push;
    public bool SyncIsPull => SyncKind == GitSyncKind.Pull;
    public bool SyncIsFetch => SyncKind == GitSyncKind.Fetch;
    public bool HasBadgeUp => SyncBadgeUp.Length > 0;
    public bool HasBadgeDown => SyncBadgeDown.Length > 0;

    /// <summary>同步按钮点击分发（GitHub Desktop：一个按钮随状态换动作）。</summary>
    public async Task SyncAsync()
    {
        switch (SyncKind)
        {
            case GitSyncKind.Push: await PushAsync(); break;
            case GitSyncKind.Pull: await PullAsync(); break;
            case GitSyncKind.Fetch: await FetchAsync(); break;
            case GitSyncKind.Publish: await OpenRemoteDialogAsync(); break;
        }
    }

    /// <summary>获取远端（顶栏同步按钮 Fetch 态；仅更新远端跟踪信息，不动工作区）。</summary>
    public async Task FetchAsync()
    {
        if (_service is null || !HasRemote || IsBusy) return;
        IsBusy = true;
        try
        {
            var pat = IsHttpsRemote ? await ResolvePatAsync() : null;
            // 网络 IO 放后台线程：网络操作在 UI 线程执行会冻结界面。
            await Task.Run(() => _service.Fetch(pat ?? string.Empty));
            LastSyncText = NowText();
            _toasts.Publish(_localizer.Get(VexL.GitFetchDoneToast));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>分支筛选词（分支下拉筛选框；空=全部）。</summary>
    private string _branchFilter = string.Empty;
    public string BranchFilter
    {
        get => _branchFilter;
        set
        {
            if (_branchFilter == value) return;
            _branchFilter = value;
            RefreshBranchRows();
        }
    }

    private void RaiseSyncProps()
    {
        this.RaisePropertyChanged(nameof(SyncKind));
        this.RaisePropertyChanged(nameof(SyncTitle));
        this.RaisePropertyChanged(nameof(SyncSubText));
        this.RaisePropertyChanged(nameof(SyncBadgeUp));
        this.RaisePropertyChanged(nameof(SyncBadgeDown));
        this.RaisePropertyChanged(nameof(SyncIsPush));
        this.RaisePropertyChanged(nameof(SyncIsPull));
        this.RaisePropertyChanged(nameof(SyncIsFetch));
        this.RaisePropertyChanged(nameof(HasBadgeUp));
        this.RaisePropertyChanged(nameof(HasBadgeDown));
    }

    public bool HasChanges => Changes.Count > 0;

    /// <summary>全选行文案（原型 {0} 个变更文件）。</summary>
    public string ChangeCountText => _localizer.Format(VexL.GitChangeCount, Changes.Count);

    public bool IsCleanWorkdir => HasRepo && IsChangesTab && Changes.Count == 0;

    public bool IsHistoryEmpty => History.Count == 0;

    public bool IsCommitBlank => IsHistoryTab && !HasCommitDetail;

    public bool ShowCommitFileBlank => IsHistoryTab && HasCommitDetail && SelectedCommitFile is null;

    /// <summary>Changes 右区占位仅在未选中任何文件时显示；二进制文件走 diff 区的占位说明行（原型 seed 同款）。</summary>
    public bool ShowChangeBlank => IsChangesTab && SelectedChange is null;

    public string ChangeBlankText => _localizer.Get(VexL.GitSelectFileHint);

    public string DiffAddedText => "+" + DiffAdded;
    public string DiffDeletedText => "−" + DiffDeleted;
    public string CommitAddedText => "+" + CommitAdded;
    public string CommitDeletedText => "−" + CommitDeleted;

    /// <summary>历史文件列表头（原型 {0} 个文件变更）。</summary>
    public string CommitFileCountText => _localizer.Format(VexL.GitFileChangesCount, CommitFiles.Count);

    public string PushHint => !HasRemote
        ? _localizer.Get(VexL.GitNoRemoteHint)
        : Ahead > 0 ? _localizer.Format(VexL.GitAheadHint, Ahead) : _localizer.Get(VexL.GitSyncedHint);

    /// <summary>右区有可展示内容：Unified 看 UnifiedRows，Split 看 SplitRows（此前只看 Unified，切 Split 整区消失）。</summary>
    public bool HasDiffText => HasDiffSelection && (IsUnified ? UnifiedRows.Count > 0 : SplitRows.Count > 0);

    public bool HasCommitFileDiffText => HasCommitFileSelection && (IsUnified ? UnifiedRows.Count > 0 : SplitRows.Count > 0);

    /// <summary>进入 Git 模式：探测工作区仓库并刷新全部状态。</summary>
    public void Enter()
    {
        IsGitMode = true;
        this.RaisePropertyChanged(nameof(IsGitMode));
        var folder = SelectedRepoRoot ?? WorkspaceFolderProvider?.Invoke();
        _workspaceFolder = folder;
        _service = folder is null || !GitService.IsRepository(folder) ? null : new GitService(folder);
        RefreshBranchRows();
        RefreshOpenRepos();
        RefreshInBackground();
    }

    /// <summary>退出 Git 模式：停掉自动提交定时器并释放常驻仓库句柄。</summary>
    public void Exit()
    {
        StopAutoCommitTimer();
        IsGitMode = false;
        this.RaisePropertyChanged(nameof(IsGitMode));
        _service?.Dispose();
        _service = null;
    }

    /// <summary>全量刷新：仓库状态、变更、历史、远程芯片（异步，完成后回 UI 线程）。</summary>
    private int _refreshEpoch;

    public async Task RefreshAsync()
    {
        // 自主探测工作区根（Enter 之外，提交/推送后的刷新也直接可用）；未打开文件夹才是真无仓库。
        _workspaceFolder ??= WorkspaceFolderProvider?.Invoke();
        if (_workspaceFolder is null)
        {
            HasRepo = false;
            ResetState();
            return;
        }
        // Enter 的自动刷新与显式刷新可能并发：旧快照晚到直接丢弃，集合重建只允许最新一次。
        var epoch = ++_refreshEpoch;
        var repoRoot = _workspaceFolder;
        GitSnapshot result;
        // 采集放后台线程（大仓库 RetrieveStatus/History/行统计是秒级 IO+计算，UI 线程跑会挂界面）；
        // 托管化后这里只剩值调用 API，线程安全；集合修改由 UiDispatch 保证回 UI 线程。
        result = await Task.Run(() =>
        {
            try
            {
                var service = new GitService(repoRoot);
                var (branch, ahead, behind) = service.BranchState();
                return new GitSnapshot(
                    true, branch, ahead ?? 0, behind ?? 0, service.RemoteRawUrl(),
                    service.Changes(), service.History());
            }
            catch (Exception)
            {
                return new GitSnapshot(false, string.Empty, 0, 0, null, [], []);
            }
        });
        if (epoch != _refreshEpoch) return;
        // 常驻服务复用（句柄创建/释放风暴是 AOT 无栈 NRE 源）：换仓库或关闭时释放旧句柄。
        if (result.HasRepo && repoRoot is not null)
        {
            if (ReferenceEquals(_service, null) || !_service.Owns(repoRoot))
            {
                _service?.Dispose();
                _service = new GitService(repoRoot);
            }
        }
        else
        {
            _service?.Dispose();
            _service = null;
        }
        // 集合与派生属性一律回 UI 线程更新：headless/验证夹具无 SyncContext，await 续体落在线程池，
        // 后台重建 ObservableCollection 会与绑定枚举交错成无栈 NRE（AOT 下直接 fail-fast）。
        await UiDispatch(() => ApplySnapshot(result));
        // 变更操作（提交/推送/拉取/放弃/自动提交）都经此刷新收尾：广播给文件树 Git 徽章重算
        // （提交不改工作区内容，文件监控收不到；Enter 的例行刷新只是多一次零差量重算，无副作用）。
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new WorkspaceGitMutatedCommand(_workspaceFolder));
    }

    private void ApplySnapshot(GitSnapshot snapshot)
    {
        HasRepo = snapshot.HasRepo;
        if (!snapshot.HasRepo)
        {
            ResetState();
            return;
        }
        BranchName = snapshot.Branch;
        Ahead = snapshot.Ahead;
        Behind = snapshot.Behind;
        RemoteRawUrl = snapshot.RemoteRawUrl;
        IsSshRemote = snapshot.RemoteRawUrl is not null &&
            (snapshot.RemoteRawUrl.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || snapshot.RemoteRawUrl.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase));
        ReplaceAll(Changes, snapshot.Changes.Select(c => new GitChangeItem(c.Path, c.Kind, StatusTextOf(c.Kind), c.LinesAdded, c.LinesDeleted)));
        RefreshBranchRows();
        foreach (var item in Changes) item.PropertyChanged += ChangeItemPropertyChanged;
        this.RaisePropertyChanged(nameof(CheckedCount));
        this.RaisePropertyChanged(nameof(CanCommit));
        this.RaisePropertyChanged(nameof(CommitButtonText));
        RefreshVisibleChanges();
        // 原型口径：右区默认空白，选中由用户点击驱动；仅当此前选中的文件仍存在时保持选中。
        SelectedChange = SelectedChange is { } current ? Changes.FirstOrDefault(item => item.Path == current.Path) : null;
        ReplaceAll(History, snapshot.History.Select(info => new GitCommitItem(info, FormatCommitTime(info.When))));
        // 首屏一批：满页即假定还有更早提交，滚动近底部时续拉。
        HasMoreHistory = snapshot.History.Count >= HistoryPageSize;
        this.RaisePropertyChanged(nameof(HasMoreHistory));
        this.RaisePropertyChanged(nameof(RepoName));
        if (SelectedCommit is { } commit && !History.Any(item => item.Sha == commit.Sha)) SelectedCommit = null;
        RaiseViewFlags();
        SyncAutoCommitTimer();
    }

    private void ResetState()
    {
        _service?.Dispose();
        _service = null;
        BranchName = string.Empty;
        Ahead = 0;
        Behind = 0;
        RemoteRawUrl = null;
        IsSshRemote = false;
        ReplaceAll(Changes, []);
        ReplaceAll(History, []);
        VisibleChanges.Clear();
        SelectedChange = null;
        SelectedCommit = null;
        SelectedCommitFile = null;
        HasDiffSelection = false;
        HasCommitFileSelection = false;
        StopAutoCommitTimer();
        this.RaisePropertyChanged(nameof(CheckedCount));
        this.RaisePropertyChanged(nameof(CanCommit));
        this.RaisePropertyChanged(nameof(CommitButtonText));
        this.RaisePropertyChanged(nameof(RepoName));
        this.RaisePropertyChanged(nameof(PushHint));
    }

    private static void ReplaceAll<T>(ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items) collection.Add(item);
    }

    private void ChangeItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName is not nameof(GitChangeItem.Checked)) return;
        this.RaisePropertyChanged(nameof(CheckedCount));
        this.RaisePropertyChanged(nameof(CanCommit));
        this.RaisePropertyChanged(nameof(CommitButtonText));
        this.RaisePropertyChanged(nameof(AllChecked));
    }

    /// <summary>状态描边标志的本地化说明（修改/新增/删除/未跟踪）。</summary>
    /// <summary>会话内最近检出的分支（本会话记录，不持久化；GHD「Recent branches」组的轻量实现）。</summary>
    private readonly List<string> _recentBranches = [];
    private string _defaultBranchName = string.Empty;

    /// <summary>按 GHD 分组重建分支行：默认分支 → 最近分支（去默认，会话内）→ 其他。</summary>
    private void RefreshBranchRows()
    {
        var rows = ReplaceBranchRows(_service?.LocalBranches() ?? []);
        this.RaisePropertyChanged(nameof(BranchName));
    }

    private List<GitBranchRow> ReplaceBranchRows(IReadOnlyList<Vex.Git.GitService.LocalBranchInfo> branches)
    {
        var current = _service is null ? string.Empty : CurrentBranchName();
        if (_defaultBranchName.Length == 0 || branches.All(b => b.Name != _defaultBranchName))
        {
            _defaultBranchName = branches.FirstOrDefault(b => b.Name is "main" or "master")?.Name
                ?? branches.FirstOrDefault()?.Name ?? string.Empty;
        }

        var rows = new List<GitBranchRow>();
        var filter = BranchFilter.Trim();
        void AddHeader(string title) => rows.Add(new GitBranchRow(string.Empty, false, title, string.Empty, IsGroupHeader: true));
        void AddBranch(Vex.Git.GitService.LocalBranchInfo b, string group)
        {
            // 筛选词：分支名包含即呈现（GHD SectionFilterList 的包含口径）。
            if (filter.Length > 0 && !b.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) return;
            rows.Add(new GitBranchRow(b.Name, b.IsCurrent, group,
                b.LastCommitTime is { } time ? FormatCommitTime(time) : string.Empty, IsGroupHeader: false));
        }

        var handled = new HashSet<string>(StringComparer.Ordinal);
        var def = branches.FirstOrDefault(b => b.Name == _defaultBranchName);
        if (def is not null) { AddHeader(_localizer.Get(VexL.GitBranchGroupDefault)); AddBranch(def, _defaultBranchName); handled.Add(def.Name); }
        var recent = _recentBranches.Where(n => n != _defaultBranchName && branches.Any(b => b.Name == n)).ToList();
        if (recent.Count > 0) AddHeader(_localizer.Get(VexL.GitBranchGroupRecent));
        foreach (var name in recent) { var b = branches.First(x => x.Name == name); AddBranch(b, "recent"); handled.Add(name); }
        var others = branches.Where(b => !handled.Contains(b.Name)).ToList();
        if (others.Count > 0) AddHeader(_localizer.Get(VexL.GitBranchGroupOther));
        foreach (var b in others) AddBranch(b, "other");

        ReplaceAll(BranchRows, rows);
        return rows;
    }

    /// <summary>当前分支名（切分器/检出后保持最新）。</summary>
    public string CurrentBranchName() => _service is null
        ? string.Empty
        : _service.LocalBranches().FirstOrDefault(b => b.IsCurrent)?.Name ?? string.Empty;

    /// <summary>重建已打开仓库行（工作区主根 + 附加根中的 git 仓库；当前置顶打勾）。</summary>
    private void RefreshOpenRepos()
    {
        var roots = WorkspaceRootsProvider?.Invoke() ?? [];
        var rows = roots.Where(GitService.IsRepository)
            .Select(r => new GitRepoRow(r, Path.GetFileName(r.TrimEnd(Path.DirectorySeparatorChar))!, string.Equals(r, _workspaceFolder, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (rows.Count == 0 && _workspaceFolder is not null && GitService.IsRepository(_workspaceFolder))
        {
            rows.Add(new GitRepoRow(_workspaceFolder, Path.GetFileName(_workspaceFolder.TrimEnd(Path.DirectorySeparatorChar))!, true));
        }
        ReplaceAll(OpenRepos, rows);
        this.RaisePropertyChanged(nameof(RepoChipName));
    }

    /// <summary>仓库芯片文案：当前选中的仓库名。</summary>
    public string RepoChipName => _workspaceFolder is null ? string.Empty : Path.GetFileName(_workspaceFolder.TrimEnd(Path.DirectorySeparatorChar));

    internal string StatusTextOf(GitChangeKind kind) => kind switch
    {
        GitChangeKind.Added => _localizer.Get(VexL.GitStatusAdded),
        GitChangeKind.Deleted => _localizer.Get(VexL.GitStatusDeleted),
        GitChangeKind.Modified => _localizer.Get(VexL.GitStatusModified),
        GitChangeKind.Renamed => _localizer.Get(VexL.GitBadgeRenamed),
        GitChangeKind.Conflicted => _localizer.Get(VexL.GitBadgeConflicted),
        _ => _localizer.Get(VexL.GitStatusUntracked),
    };

    /// <summary>初始化本地仓库（工作区根目录；未打开文件夹先提示）。</summary>
    public async Task InitRepositoryAsync()
    {
        var folder = WorkspaceFolderProvider?.Invoke();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            _toasts.Publish(_localizer.Get(VexL.GitNeedFolderToast), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        try
        {
            GitService.InitRepository(folder);
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        _workspaceFolder = folder;
        _service = new GitService(folder);
        _toasts.Publish(_localizer.Get(VexL.GitInitToast));
        await RefreshAsync();
    }

    /// <summary>远程地址弹层回调：校验 HTTPS/SSH 形态后保存 origin。</summary>
    public async Task SaveRemoteAsync(string? url)
    {
        if (_service is null) return;
        var value = url?.Trim() ?? string.Empty;
        if (!IsValidRemoteUrl(value))
        {
            _toasts.Publish(_localizer.Get(VexL.GitRemoteUrlError), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        try
        {
            _service.SetRemote(value);
            try { _service.TrackOrigin(); }
            catch { /* 无 HEAD 时尚不能跟踪，推送前再试 */ }
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        _toasts.Publish(_localizer.Format(VexL.GitRemoteSavedFormat, value.StartsWith("git@") || value.StartsWith("ssh://") ? "SSH" : "HTTPS"));
        await RefreshAsync();
    }

    /// <summary>原型校验：https://…、git@…、ssh://… 三种形态。</summary>
    public static bool IsValidRemoteUrl(string url) =>
        System.Text.RegularExpressions.Regex.IsMatch(url, @"^https?://\S+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        || System.Text.RegularExpressions.Regex.IsMatch(url, @"^git@\S+:\S+")
        || System.Text.RegularExpressions.Regex.IsMatch(url, @"^ssh://\S+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // ===== 分支下拉与 Git 菜单（GitHub Desktop 分支管理；原型 design/Git-Badges.html REV3+）=====

    /// <summary>分支名称弹层（宿主注入）：create 模式带「基于…」二选一；返回 null 表示取消。</summary>
    public Func<GitBranchDialogRequest, Task<GitBranchDialogResult?>>? BranchNameDialog { get; set; }

    /// <summary>合并分支弹层（宿主注入）：传入可选源分支，返回选中的源；null 取消。</summary>
    public Func<IReadOnlyList<string>, Task<string?>>? MergeDialog { get; set; }

    /// <summary>删除分支确认弹层（宿主注入）：返回 false 表示取消。</summary>
    public Func<string, Task<bool>>? BranchDeleteConfirm { get; set; }

    /// <summary>tag 表单请求挂在哪个提交上（null = HEAD）。</summary>
    public string? TagTargetSha { get; private set; }

    /// <summary>分支下拉当前选中行（行右键 重命名/复制/删除 的目标）。</summary>
    public GitBranchRow? SelectedBranchRow { get; set; }
    /// <summary>复制分支名的 toast（剪贴板在视图层写入）。</summary>
    public void CopyBranchNameToast(string name) => _toasts.Publish(_localizer.Get(VexL.GitBranchCopiedToast).Replace("{0}", name));


    /// <summary>tag 表单锚点请求（历史右键入口；ShellGitView 订阅后在分支芯片上展开表单）。</summary>
    public event EventHandler? TagFlyoutRequested;

    public void RequestTagFlyout(bool alsoPush, string? sha)
    {
        PrepareTagFlyout(alsoPush, sha);
        TagFlyoutRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>标题栏 Git 菜单：打 tag…（在分支芯片上展开 tag 表单）。</summary>
    public void RequestCreateTagFlyout() => RequestTagFlyout(false, null);

    /// <summary>标题栏 Git 菜单：推送 tag…（预勾选同时推送）。</summary>
    public void RequestPushTagFlyout() => RequestTagFlyout(true, null);

    /// <summary>tag 表单预设置（指定提交；历史右键入口）。</summary>
    public void PrepareTagFlyout(bool alsoPush, string? targetSha)
    {
        PrepareTagFlyout(alsoPush);
        TagTargetSha = targetSha;
    }

    /// <summary>切换检出分支（保留工作区未提交变更；冲突 toast）。</summary>
    public async Task SwitchBranchAsync(string? name)
    {
        if (_service is null || string.IsNullOrWhiteSpace(name) || name == BranchName) return;
        try
        {
            _service.CheckoutBranch(name);
            TrackRecentBranch(name);
            _toasts.Publish(_localizer.Get(VexL.GitBranchSwitchedFormat).Replace("{0}", name));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>新建分支（弹层收集名称与基准分支；创建后检出）。分支下拉与标题栏 Git 菜单共用。</summary>
    public async Task ShowNewBranchDialogAsync() => await CreateBranchFromDialogAsync();

    public async Task CreateBranchFromDialogAsync()
    {
        if (_service is null || BranchNameDialog is null) return;
        var sources = _service.LocalBranches().Select(b => b.Name).ToList();
        var request = new GitBranchDialogRequest(GitBranchDialogMode.Create, string.Empty, sources, _defaultBranchName, BranchName);
        var dialogResult = await BranchNameDialog.Invoke(request);
        if (dialogResult is null || string.IsNullOrWhiteSpace(dialogResult.Name)) return;
        var baseBranch = dialogResult.BaseBranch;
        try
        {
            _service.CreateBranch(dialogResult.Name, baseBranch);
            TrackRecentBranch(dialogResult.Name);
            _toasts.Publish(_localizer.Get(VexL.GitBranchCreatedFormat).Replace("{0}", dialogResult.Name));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>重命名分支（行右键；弹层预填旧名）。</summary>
    public async Task RenameBranchFromDialogAsync(string? oldName)
    {
        if (_service is null || string.IsNullOrWhiteSpace(oldName) || BranchNameDialog is null) return;
        var request = new GitBranchDialogRequest(GitBranchDialogMode.Rename, oldName, [], string.Empty, oldName);
        var dialogResult = await BranchNameDialog.Invoke(request);
        if (dialogResult is null || string.IsNullOrWhiteSpace(dialogResult.Name) || dialogResult.Name == oldName) return;
        try
        {
            _service.RenameBranch(oldName, dialogResult.Name);
            _toasts.Publish(_localizer.Get(VexL.GitBranchRenamedFormat).Replace("{0}", dialogResult.Name));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>删除分支（确认弹层；当前分支由服务端拒绝并 toast）。</summary>
    public async Task DeleteBranchFromDialogAsync(string? name)
    {
        if (_service is null || string.IsNullOrWhiteSpace(name)) return;
        if (BranchDeleteConfirm is not null && !await BranchDeleteConfirm.Invoke(name)) return;
        try
        {
            _service.DeleteBranch(name, force: false);
            _toasts.Publish(_localizer.Get(VexL.GitBranchDeletedFormat).Replace("{0}", name));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>合并分支（弹层选源；合并进当前分支，冲突 toast）。</summary>
    public async Task MergeFromDialogAsync()
    {
        if (_service is null || MergeDialog is null) return;
        var others = _service.LocalBranches().Where(b => !b.IsCurrent).Select(b => b.Name).ToList();
        if (others.Count == 0)
        {
            _toasts.Publish(_localizer.Get(VexL.GitMergeNoSourceToast), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        var source = await MergeDialog.Invoke(others);
        if (string.IsNullOrWhiteSpace(source)) return;
        try
        {
            var result = _service.MergeBranchInto(source);
            if (result == "up-to-date") _toasts.Publish(_localizer.Get(VexL.GitMergeUpToDateToast));
            else _toasts.Publish(_localizer.Format(VexL.GitMergeDoneFormat, source, CurrentBranchName()));
            await RefreshAsync();
        }
        catch (GitServiceException ex) when (ex.Code == GitServiceError.MergeConflicts)
        {
            _toasts.Publish(_localizer.Get(ex.Code), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>切换仓库（已打开仓库下拉；把 Git 面板重绑到该仓库根并刷新）。</summary>
    public async Task SwitchRepoAsync(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !GitService.IsRepository(rootPath)) return;
        if (_workspaceFolder is not null && string.Equals(_workspaceFolder, rootPath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        _workspaceFolder = rootPath;
        _service?.Dispose();
        _service = new GitService(rootPath);
        _toasts.Publish(_localizer.Get(VexL.GitRepoSwitchedFormat).Replace("{0}", Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar))));
        await RefreshAsync();
    }

    private void TrackRecentBranch(string name)
    {
        _recentBranches.Remove(name);
        _recentBranches.Insert(0, name);
        if (_recentBranches.Count > 5) _recentBranches.RemoveRange(5, _recentBranches.Count - 5);
    }

    /// <summary>分支按钮（原型占位：分支管理在桌面版中提供）。</summary>
    public void ShowBranchPlaceholder() => _toasts.Publish(_localizer.Get(VexL.GitBranchManagementToast));

    /// <summary>顶栏仓库按钮 / 空仓库英雄区：弹远程地址层（宿主注入），保存走 SaveRemoteAsync。</summary>
    public async Task OpenRemoteDialogAsync()
    {
        if (RemoteDialog is null) return;
        var url = await RemoteDialog.Invoke();
        if (string.IsNullOrWhiteSpace(url)) return;
        await SaveRemoteAsync(url);
    }

    /// <summary>勾选暂存后提交；Summary 必填，身份未配置/无勾选时阻断并提示。</summary>
    /// <summary>放弃更改：受跟踪文件还原、未跟踪文件移除；走确认浮层。</summary>
    public async Task DiscardChangeAsync(GitChangeItem? item)
    {
        if (_service is null || item is null) return;
        var path = item.Path;
        if (DiscardConfirm is not null && !await DiscardConfirm(path)) return;
        try
        {
            _service.Discard(path);
            _toasts.Publish(_localizer.Get(VexL.GitDiscardedToast));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    public async Task CommitAsync()
    {
        if (_service is null || !CanCommit) return;
        var paths = Changes.Where(item => item.Checked).Select(item => item.Path).ToArray();
        if (_service.HasIdentityGap && !string.IsNullOrWhiteSpace(_preferences.GitUserName) && !string.IsNullOrWhiteSpace(_preferences.GitUserEmail))
        {
            _service.EnsureIdentity(_preferences.GitUserName, _preferences.GitUserEmail);
        }
        var message = Summary.Trim();
        var description = Description.Trim();
        if (description.Length > 0) message += "\n\n" + description;
        IsBusy = true;
        try
        {
            var sha = _service.CommitPaths(paths, message).Sha[..7];
            _toasts.Publish(_localizer.Get(VexL.GitCommittedToast) + " · " + Summary.Trim());
            Summary = string.Empty;
            Description = string.Empty;
            Tab = GitPanelTab.Changes;
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>推送：HTTPS 走 PAT（无已存凭据先弹输入），SSH 交系统 git。</summary>
    public async Task PushAsync()
    {
        if (_service is null) return;
        if (!HasRemote || Ahead == 0)
        {
            _toasts.Publish(_localizer.Get(VexL.GitPushNothingToast));
            return;
        }
        IsBusy = true;
        try
        {
            var pat = await ResolvePatAsync();
            if (pat == null && IsHttpsRemote)
            {
                return; // 用户取消 PAT 输入
            }
            var branch = BranchName;
            // 网络 IO 放后台线程：推送传输可达秒级～分钟级，阻塞 UI 线程会冻结界面。
            await Task.Run(() => _service.Push(pat ?? string.Empty, branch: branch));
            LastSyncText = NowText();
            _toasts.Publish(_localizer.Format(VexL.GitPushToastFormat, branch));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>拉取：fetch + 仅快进合并；一次点击完成（原型模拟的两段式为演示口径）。</summary>
    public async Task PullAsync()
    {
        if (_service is null || !HasRemote) return;
        IsBusy = true;
        try
        {
            var pat = IsHttpsRemote ? await ResolvePatAsync() : null;
            if (pat == null && IsHttpsRemote)
            {
                return; // 用户取消 PAT 输入
            }
            var pulled = await Task.Run(() => _service.PullFastForward(pat));
            if (pulled > 0)
            {
                LastSyncText = NowText();
                _toasts.Publish(_localizer.Format(VexL.GitPullMergedFormat, pulled));
                await RefreshAsync();
            }
            else
            {
                LastSyncText = NowText();
                _toasts.Publish(_localizer.Get(VexL.GitPullUpToDate));
            }
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPullFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool IsHttpsRemote => HasRemote && !IsSshRemote;

    /// <summary>HTTPS 凭据解析：凭据库已有直接用；无记录弹 PAT 输入，勾选保存则写入凭据库。SSH 远端不需要（返回空串）。</summary>
    private async Task<string?> ResolvePatAsync()
    {
        if (IsSshRemote) return string.Empty;
        var host = _service?.RemoteCredentialHost();
        if (host is null) return string.Empty;
        if (_credentials.GetPat(host) is { } stored) return stored;
        if (PatPrompt is null) return null;
        var answer = await PatPrompt.Invoke();
        if (string.IsNullOrWhiteSpace(answer)) return null;
        var (token, remember) = DecodePatAnswer(answer);
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (remember)
        {
            try { _credentials.SavePat(host, token); }
            catch { /* 保存失败不阻断本次操作 */ }
        }
        return token;
    }

    /// <summary>PAT 弹层答案打包：token + "\n" + (remember ? "1" : "0")。</summary>
    public static string EncodePatAnswer(string token, bool remember) => token + "\n" + (remember ? "1" : "0");

    private static (string Token, bool Remember) DecodePatAnswer(string combined)
    {
        var newline = combined.LastIndexOf('\n');
        if (newline < 0) return (combined, false);
        return (combined[..newline], combined[(newline + 1)..] == "1");
    }

    private void LoadWorkdirDiff()
    {
        if (_service is null || SelectedChange is not { } change)
        {
            _diffHunks = [];
            HasDiffSelection = false;
            RecomputeDiffView();
            return;
        }
        HasDiffSelection = true;
        _diffHunks = _service.TextDiff(change.Path)?.Hunks ?? BinaryNoticeHunks();
        RecomputeDiffView();
    }

    private void LoadCommitDetail()
    {
        if (_service is null || SelectedCommit is not { } commit)
        {
            CommitFiles = [];
            CommitSubject = string.Empty;
            CommitMeta = string.Empty;
            CommitAdded = 0;
            CommitDeleted = 0;
            return;
        }
        var sha = commit.Sha;
        var changes = _service.CommitChanges(sha);
        CommitFiles = changes.Select(c => new GitCommitFileItem(c.Path, c.Kind switch
        {
            GitChangeKind.Added => "A",
            GitChangeKind.Deleted => "D",
            GitChangeKind.Renamed => "R",
            _ => "M",
        })).ToList();
        this.RaisePropertyChanged(nameof(CommitFileCountText));
        CommitSubject = commit.Subject + (commit.Tag is { } tag ? "  " + tag : string.Empty);
        CommitMeta = commit.Author + " · " + commit.TimeText + " · " + commit.Sha;
        var (added, deleted) = CommitStats(sha, changes);
        CommitAdded = added;
        CommitDeleted = deleted;
        this.RaisePropertyChanged(nameof(CommitAddedText));
        this.RaisePropertyChanged(nameof(CommitDeletedText));
        if (CommitFiles.Count > 0) SelectedCommitFile = CommitFiles[0];
        this.RaisePropertyChanged(nameof(HasCommitDetail));
    }

    /// <summary>整次提交的 +/- 行合计（逐文件文本 diff 累加；二进制计 0）。</summary>
    private (int Added, int Deleted) CommitStats(string sha, IReadOnlyList<CommitFileChange> changes)
    {
        int added = 0, deleted = 0;
        foreach (var change in changes)
        {
            var diff = _service?.CommitTextDiff(sha, change.Path);
            if (diff is null) continue;
            foreach (var hunk in diff.Hunks)
            {
                foreach (var line in hunk.Lines)
                {
                    if (line.Kind == '+') added++;
                    else if (line.Kind == '-') deleted++;
                }
            }
        }
        return (added, deleted);
    }

    private void LoadCommitFileDiff()
    {
        if (_service is null || SelectedCommit is not { } commit || SelectedCommitFile is not { } file)
        {
            _diffHunks = [];
            HasCommitFileSelection = false;
            HasDiffSelection = false;
            RecomputeDiffView();
            return;
        }
        HasCommitFileSelection = true;
        HasDiffSelection = true;
        _diffHunks = _service.CommitTextDiff(commit.Sha, file.Path)?.Hunks ?? BinaryNoticeHunks();
        RecomputeDiffView();
    }

    /// <summary>二进制文件（无文本 diff）的占位行（原型 seed：二进制文件展示图片对比说明行）。</summary>
    private IReadOnlyList<DiffHunk> BinaryNoticeHunks() =>
        [new DiffHunk(string.Empty, [new DiffLine('+', _localizer.Get(VexL.GitBinaryFileNote))])];

    /// <summary>刷新派生的视图开关（列表计数/空态/占位等跨属性联动）。</summary>
    private void RaiseViewFlags()
    {
        this.RaisePropertyChanged(nameof(HasChanges));
        this.RaisePropertyChanged(nameof(ChangeCountText));
        this.RaisePropertyChanged(nameof(IsCleanWorkdir));
        this.RaisePropertyChanged(nameof(IsHistoryEmpty));
        this.RaisePropertyChanged(nameof(IsCommitBlank));
        this.RaisePropertyChanged(nameof(ShowCommitFileBlank));
        this.RaisePropertyChanged(nameof(ShowChangeBlank));
        this.RaisePropertyChanged(nameof(ChangeBlankText));
        this.RaisePropertyChanged(nameof(PushHint));
        this.RaisePropertyChanged(nameof(CanPush));
        this.RaisePropertyChanged(nameof(CanPull));
        this.RaisePropertyChanged(nameof(AllChecked));
    }

    /// <summary>按 DiffMode/HideWhitespace 重建 Unified/Split 视图与增删统计（原型 effectiveDiff + splitPairs）。</summary>
    private void RecomputeDiffView()
    {
        var rows = new List<(char Kind, string Text)>();
        foreach (var hunk in _diffHunks)
        {
            foreach (var line in hunk.Lines)
            {
                if (_hideWhitespace && line.Kind != ' ' && string.IsNullOrWhiteSpace(line.Text)) continue;
                rows.Add((line.Kind, line.Text));
            }
        }
        int added = 0, deleted = 0;
        foreach (var (kind, _) in rows)
        {
            if (kind == '+') added++;
            else if (kind == '-') deleted++;
        }
        DiffAdded = added;
        DiffDeleted = deleted;

        if (DiffMode == GitDiffViewMode.Unified)
        {
            var unified = new List<GitDiffRow>();
            int left = 1, right = 1;
            foreach (var (kind, text) in rows)
            {
                int? lno = null, rno = null;
                if (kind == '+') rno = right++;
                else if (kind == '-') lno = left++;
                else { lno = left++; rno = right++; }
                unified.Add(new GitDiffRow(lno, rno, kind == ' ' ? '\0' : kind, text, kind));
            }
            UnifiedRows = unified;
            SplitRows = [];
        }
        else
        {
            var split = new List<GitDiffPairRow>();
            var pairs = SplitPairs(rows);
            int left = 1, right = 1;
            foreach (var (del, add, ctx) in pairs)
            {
                if (ctx is { } context)
                {
                    var cell = new GitDiffCell(left++, context, ' ');
                    split.Add(new GitDiffPairRow(cell, new GitDiffCell(right++, context, ' ')));
                }
                else
                {
                    var leftCell = del is { } d ? new GitDiffCell(left++, d, '-') : new GitDiffCell(null, string.Empty, 'e');
                    var rightCell = add is { } a ? new GitDiffCell(right++, a, '+') : new GitDiffCell(null, string.Empty, 'e');
                    split.Add(new GitDiffPairRow(leftCell, rightCell));
                }
            }
            SplitRows = split;
            UnifiedRows = [];
        }
        this.RaisePropertyChanged(nameof(DiffHeaderText));
        this.RaisePropertyChanged(nameof(HasDiffText));
        this.RaisePropertyChanged(nameof(HasCommitFileDiffText));
        // HasDiffText 是计算属性：其变化必须连带刷新占位开关，否则占位与 diff 并存。
        this.RaisePropertyChanged(nameof(ShowChangeBlank));
        this.RaisePropertyChanged(nameof(ChangeBlankText));
        this.RaisePropertyChanged(nameof(ShowCommitFileBlank));
        this.RaisePropertyChanged(nameof(DiffAddedText));
        this.RaisePropertyChanged(nameof(DiffDeletedText));
    }

    /// <summary>Unified 行序列 → Split 左右配对：−块与其后的 + 块按行配对，余者单侧留空（原型 splitPairs）。</summary>
    private static IReadOnlyList<(string? Del, string? Add, string? Ctx)> SplitPairs(IReadOnlyList<(char Kind, string Text)> rows)
    {
        var pairs = new List<(string?, string?, string?)>();
        var i = 0;
        while (i < rows.Count)
        {
            if (rows[i].Kind == '-')
            {
                var dels = new List<string>();
                var adds = new List<string>();
                while (i < rows.Count && rows[i].Kind == '-') dels.Add(rows[i++].Text);
                while (i < rows.Count && rows[i].Kind == '+') adds.Add(rows[i++].Text);
                var n = Math.Max(dels.Count, adds.Count);
                for (var k = 0; k < n; k++)
                {
                    pairs.Add((k < dels.Count ? dels[k] : null, k < adds.Count ? adds[k] : null, null));
                }
            }
            else if (rows[i].Kind == '+')
            {
                pairs.Add((null, rows[i++].Text, null));
            }
            else
            {
                pairs.Add((null, null, rows[i++].Text));
            }
        }
        return pairs;
    }

    /// <summary>提交时间文案：今天 HH:mm / 昨天 HH:mm / MM-dd HH:mm（原型 commit-meta 口径）。</summary>
    public string FormatCommitTime(DateTimeOffset when)
    {
        var local = when.ToLocalTime();
        var today = DateTimeOffset.Now;
        if (local.Date == today.Date) return local.ToString("HH:mm");
        if (local.Date == today.AddDays(-1).Date) return _localizer.Get(VexL.GitYesterday) + " " + local.ToString("HH:mm");
        return local.ToString("MM-dd HH:mm");
    }

    private static string NowText() => DateTime.Now.ToString("HH:mm");

    /* ---------- 定时自动提交（偏好开关默认关；界面不显示状态，与原型一致） ---------- */

    private DispatcherTimer? _autoCommitTimer;

    private void SyncAutoCommitTimer()
    {
        if (!_hasRepo || !_preferences.GitAutoCommit)
        {
            StopAutoCommitTimer();
            return;
        }
        StartAutoCommitTimer();
    }

    /// <summary>启动自动提交定时器（间隔来自偏好；重复调用先复位，避免计时器叠加）。</summary>
    private void StartAutoCommitTimer()
    {
        StopAutoCommitTimer();
        _autoCommitTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Math.Max(1, _preferences.GitAutoCommitInterval)) };
        _autoCommitTimer.Tick += async (_, _) => await AutoCommitTickAsync();
        _autoCommitTimer.Start();
    }

    private void StopAutoCommitTimer()
    {
        _autoCommitTimer?.Stop();
        _autoCommitTimer = null;
    }

    /// <summary>定时器到点：有变更才提交（模板 {date} 占位 HH:mm），无变更自动跳过；只提交本地，不推送。</summary>
    public async Task AutoCommitTickAsync()
    {
        if (_service is null || !HasRepo || Changes.Count == 0) return;
        var template = _preferences.GitAutoCommitMessage;
        var message = template.Replace("{date}", DateTime.Now.ToString("HH:mm"), StringComparison.Ordinal);
        try
        {
            var commit = _service.CommitAll(message);
            if (commit is null) return;
            _toasts.Publish(_localizer.Get(VexL.GitAutoCommitToast) + " · " + commit.MessageShort);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, LocalizeGitError(ex)), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>Enter 的 fire-and-forget 刷新：异常吞掉（快照内部已兜底）。</summary>
    private void RefreshInBackground() => RefreshAsync().ContinueWith(t => _ = t.Exception, TaskScheduler.Default);

    /// <summary>UI 线程派发器：默认走 Avalonia Dispatcher；验证夹具注入直执行替身，
    /// 避免 xunit 无调度线程时 RunJobs/线程亲和的连环坑。</summary>
    public Func<Action, Task> UiDispatch { get; set; } =
        action => Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(action).GetTask();
}

/// <summary>RefreshAsync 的一次快照（线程池采集，回 UI 线程后套用）。</summary>
internal sealed record GitSnapshot(
    bool HasRepo,
    string Branch,
    int Ahead,
    int Behind,
    string? RemoteRawUrl,
    IReadOnlyList<ChangeEntry> Changes,
    IReadOnlyList<CommitInfo> History);
