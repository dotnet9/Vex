using System.Collections.ObjectModel;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;
using ReactiveUI;
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

/// <summary>变更列表行：状态描边字母 + 目录/文件名拆分 + 勾选态。</summary>
public sealed class GitChangeItem : ReactiveObject
{
    private bool _checked;
    private bool _isSelected;

    public GitChangeItem(string path, GitChangeKind kind, string statusText)
    {
        Path = path;
        Kind = kind;
        StatusText = statusText;
        var slash = path.LastIndexOf('/');
        Dir = slash >= 0 ? path[..(slash + 1)] : string.Empty;
        Name = slash >= 0 ? path[(slash + 1)..] : path;
        StatusLabel = kind switch
        {
            GitChangeKind.Added => "A",
            GitChangeKind.Deleted => "D",
            GitChangeKind.Modified => "M",
            GitChangeKind.Renamed => "R",
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

    public bool IsSelected { get => _isSelected; set => this.RaiseAndSetIfChanged(ref _isSelected, value); }

    /// <summary>状态描边标志的样式开关（黄点/绿加号/红减号/灰问号）。</summary>
    public bool IsModified => Kind == GitChangeKind.Modified || Kind == GitChangeKind.Renamed;
    public bool IsAdded => Kind == GitChangeKind.Added;
    public bool IsDeleted => Kind == GitChangeKind.Deleted;
    public bool IsUntracked => Kind == GitChangeKind.Untracked;

    /// <summary>状态说明（描边标志 tooltip，构造时按当前语言传入）。</summary>
    public string StatusText { get; }
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

    public ShellGitViewModel(IAppLocalizer localizer, IShellToastPublisher toasts, EditorPreferences preferences, ICredentialStore credentials)
    {
        _localizer = localizer;
        _toasts = toasts;
        _preferences = preferences;
        _credentials = credentials;
        SetDiffModeCommand = new RelayCommand<string?>(SetDiffMode);
        CreateTagCommand = new AsyncRelayCommand<string?>(CreateTagAsync);
        OpenChangeInExplorerCommand = new RelayCommand<GitChangeItem?>(RevealChange);
        OpenCommitFileInExplorerCommand = new RelayCommand<GitCommitFileItem?>(RevealCommitFile);
        _preferences.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorPreferences.GitAutoCommit) or nameof(EditorPreferences.GitAutoCommitInterval))
            {
                SyncAutoCommitTimer();
            }
        };
    }

    /// <summary>工作区根目录来源（MainWindowViewModel 注入；未打开文件夹时 Git 功能不可用）。</summary>
    public Func<string?>? WorkspaceFolderProvider { get; set; }

    /// <summary>PAT 输入弹层（宿主注入）：HTTPS 远程无已存凭据时调用，返回 null 表示取消。</summary>
    public Func<Task<string?>>? PatPrompt { get; set; }

    /// <summary>远程地址弹层（宿主注入）：返回 null 表示取消。</summary>
    public Func<Task<string?>>? RemoteDialog { get; set; }

    public ObservableCollection<GitChangeItem> Changes { get; } = [];
    public ObservableCollection<GitCommitItem> History { get; } = [];

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
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>在 HEAD 打轻量 tag（可选立即推送）：发布流程「升版提交后打发布 tag」的面板入口。</summary>
    public AsyncRelayCommand<string?> CreateTagCommand { get; }

    private string _newTagName = string.Empty;
    private bool _tagAlsoPush = true;

    /// <summary>tag 名称输入（Flyout 表单绑定）。</summary>
    public string NewTagName { get => _newTagName; set => this.RaiseAndSetIfChanged(ref _newTagName, value); }

    /// <summary>创建后同时推送（默认勾选：发布 tag 触发 CI）。</summary>
    public bool TagAlsoPush { get => _tagAlsoPush; set => this.RaiseAndSetIfChanged(ref _tagAlsoPush, value); }

    /// <summary>tag 名称规则：发布 tag（v 开头数字）或任意非空合法名。</summary>
    private async Task CreateTagAsync(string? name)
    {
        if (_service is null || string.IsNullOrWhiteSpace(name))
        {
            _toasts.Publish(_localizer.Get(VexL.GitTagNameInvalid), Avalonia.Controls.Notifications.NotificationType.Warning);
            return;
        }
        var clean = name.Trim();
        try
        {
            _service.CreateTag(clean);
            _toasts.Publish(_localizer.Format(VexL.GitTagCreatedFormat, clean));
            NewTagName = string.Empty;
            if (TagAlsoPush)
            {
                await PushTagAsync(clean);
            }
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
    }

    /// <summary>推送 tag 到 origin（发布 tag 触发 CI 的关键一步）。</summary>
    public async Task PushTagAsync(string? name)
    {
        if (_service is null || !HasRemote || string.IsNullOrWhiteSpace(name)) return;
        _isBusy = true;
        this.RaisePropertyChanged(nameof(CanPush));
        try
        {
            var tag = name.Trim();
            await PushWithTagAsync(tag);
            _toasts.Publish(_localizer.Format(VexL.GitTagPushedFormat, tag));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            _isBusy = false;
            this.RaisePropertyChanged(nameof(CanPush));
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

    public int Ahead { get => _ahead; private set { this.RaiseAndSetIfChanged(ref _ahead, value); this.RaisePropertyChanged(nameof(SyncChipText)); this.RaisePropertyChanged(nameof(HasSyncDelta)); } }

    public int Behind { get => _behind; private set { this.RaiseAndSetIfChanged(ref _behind, value); this.RaisePropertyChanged(nameof(SyncChipText)); this.RaisePropertyChanged(nameof(HasSyncDelta)); } }

    public bool HasSyncDelta => Ahead > 0 || Behind > 0;

    public string SyncChipText => (Ahead > 0 ? "↑" + Ahead + " " : string.Empty) + (Behind > 0 ? "↓" + Behind : string.Empty);

    /// <summary>上次同步时间（顶栏小字）；null 显示 —。</summary>
    public string? LastSyncText { get => _lastSyncText; private set => this.RaiseAndSetIfChanged(ref _lastSyncText, value); }

    public string LastSyncDisplay => LastSyncText ?? "—";

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

    public bool CanPush => HasRemote && Ahead > 0 && !_isBusy;

    public bool CanPull => HasRemote && !_isBusy;

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
        var folder = WorkspaceFolderProvider?.Invoke();
        _workspaceFolder = folder;
        _service = folder is null || !GitService.IsRepository(folder) ? null : new GitService(folder);
        RefreshInBackground();
    }

    /// <summary>退出 Git 模式：停掉自动提交定时器并释放常驻仓库句柄。</summary>
    public void Exit()
    {
        StopAutoCommitTimer();
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
        ReplaceAll(Changes, snapshot.Changes.Select(c => new GitChangeItem(c.Path, c.Kind, StatusTextOf(c.Kind))));
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
    internal string StatusTextOf(GitChangeKind kind) => kind switch
    {
        GitChangeKind.Added => _localizer.Get(VexL.GitStatusAdded),
        GitChangeKind.Deleted => _localizer.Get(VexL.GitStatusDeleted),
        GitChangeKind.Modified => _localizer.Get(VexL.GitStatusModified),
        GitChangeKind.Renamed => _localizer.Get(VexL.GitStatusModified),
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
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
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
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
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
    public async Task CommitAsync()
    {
        if (_service is null || !CanCommit) return;
        var paths = Changes.Where(item => item.Checked).Select(item => item.Path).ToArray();
        var message = Summary.Trim();
        var description = Description.Trim();
        if (description.Length > 0) message += "\n\n" + description;
        _isBusy = true;
        this.RaisePropertyChanged(nameof(CanCommit));
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
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            _isBusy = false;
            this.RaisePropertyChanged(nameof(CanCommit));
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
        _isBusy = true;
        this.RaisePropertyChanged(nameof(CanPush));
        try
        {
            var pat = await ResolvePatAsync();
            if (pat == null && IsHttpsRemote)
            {
                return; // 用户取消 PAT 输入
            }
            var branch = BranchName;
            _service.Push(pat ?? string.Empty, branch: branch);
            LastSyncText = NowText();
            _toasts.Publish(_localizer.Format(VexL.GitPushToastFormat, branch));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _toasts.Publish(_localizer.Format(VexL.GitPushFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            _isBusy = false;
            this.RaisePropertyChanged(nameof(CanPush));
        }
    }

    /// <summary>拉取：fetch + 仅快进合并；一次点击完成（原型模拟的两段式为演示口径）。</summary>
    public async Task PullAsync()
    {
        if (_service is null || !HasRemote) return;
        _isBusy = true;
        this.RaisePropertyChanged(nameof(CanPull));
        try
        {
            var pat = IsHttpsRemote ? await ResolvePatAsync() : null;
            if (pat == null && IsHttpsRemote)
            {
                return; // 用户取消 PAT 输入
            }
            var pulled = _service.PullFastForward(pat);
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
            _toasts.Publish(_localizer.Format(VexL.GitPullFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
        }
        finally
        {
            _isBusy = false;
            this.RaisePropertyChanged(nameof(CanPull));
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
            _toasts.Publish(_localizer.Format(VexL.GitCommitFailedFormat, ex.Message), Avalonia.Controls.Notifications.NotificationType.Warning);
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
