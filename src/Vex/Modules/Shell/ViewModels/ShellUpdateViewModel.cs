using ReactiveUI.Primitives;
using CodeWF.Toolkit.Core.UpdateChecking;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>更新会话的单一数据源：标题栏药丸、日志气泡与更新窗共用这一个 ViewModel 实例。
/// 对应原型 about-update.js——静默启动检查、下载可后台（关窗/最小化不终止）、失败可重试的状态机。</summary>
public sealed class ShellUpdateViewModel : ReactiveObject, IDialogContext, IDisposable
{
    private readonly IUpdateService _updates;
    private readonly IAppLocalizer _localizer;
    private readonly IShellToastPublisher? _toast;
    private readonly Action<string> _openUrl;
    private readonly Action _exit;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private UpdateInfo? _update;
    private UpdateDownloadResult? _download;
    private UpdateState _state;
    private string? _error;
    private UpdateInstallResult? _install;
    private double? _progress;
    private bool _disposed;
    private bool _windowPresented;
    private bool _startupWatched;
    private string _latestReleaseDate = string.Empty;

    public ShellUpdateViewModel(IUpdateService updates, IAppLocalizer localizer,
        Action<string> openUrl, Action exit, IShellToastPublisher? toast = null)
    {
        _updates = updates;
        _localizer = localizer;
        _openUrl = openUrl;
        _exit = exit;
        _toast = toast;
        VersionText = updates.CurrentVersionText;
        var available = this.WhenAnyValue(model => model.IsBusy).Select(busy => !busy);
        UpdateCommand = ReactiveCommand.CreateFromTask(AdvanceAsync, available);
        CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync, available);
        CancelCommand = ReactiveCommand.Create(() => _operation?.Cancel());
        OpenFolderCommand = ReactiveCommand.Create(() => OpenUrl(Path.GetDirectoryName(_download!.FilePath)!));
        _localizer.CultureChanged += OnCultureChanged;
    }

    public string VersionText { get; }
    public string DownloadPath => _download?.FilePath ?? string.Empty;
    public bool HasDownload => _download is not null;
    public Task EnsureCheckedAsync() => _state == UpdateState.Idle ? CheckAsync() : Task.CompletedTask;
    public event EventHandler<object?>? RequestClose;
    /// <summary>药丸点击「恢复更新窗」或气泡「立即更新」时请求宿主打开/置前更新窗（后台态恢复现场，不重新检查）。</summary>
    public event EventHandler? RestoreWindowRequested;
    /// <summary>气泡「完整日志」请求打开内置《更新日志》文档。</summary>
    public event EventHandler? OpenChangelogRequested;
    public ReactiveCommand<RxVoid, RxVoid> UpdateCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> CheckCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> CancelCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenFolderCommand { get; }
    public UpdateState State => _state;
    public bool IsBusy => _state is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing;
    public bool IsDownloading => _state == UpdateState.Downloading;
    public bool ShowAction => !IsBusy;
    public bool ShowLater => !IsBusy && _update is not null;
    public bool IsPrimaryAction => _state is not (UpdateState.Idle or UpdateState.Current or UpdateState.Canceled or UpdateState.Installed);
    public bool HasReleaseNotes => !string.IsNullOrWhiteSpace(ReleaseNotes);
    public string PhaseTitle => _state switch
    {
        UpdateState.Idle or UpdateState.Checking => _localizer.Get(VexL.AboutCheckingUpdate),
        UpdateState.Available => _localizer.Format(VexL.UpdateAvailableTitle, LatestVersionText),
        UpdateState.Downloading => _localizer.Get(VexL.AboutDownloading),
        UpdateState.Downloaded => _localizer.Get("Vex.VexL.UpdateDownloadComplete"),
        UpdateState.Installing => _localizer.Get(VexL.AboutInstalling),
        UpdateState.Installed => _localizer.Get("Vex.VexL.UpdateFinished"),
        _ => StatusText
    };
    public string PhaseDetail => _state switch
    {
        UpdateState.Idle or UpdateState.Checking => _localizer.Get("Vex.VexL.UpdateConnecting"),
        UpdateState.Available => _localizer.Format(VexL.UpdateCurrentVersionNote, VersionText),
        UpdateState.Downloading => ProgressText,
        UpdateState.Downloaded => _localizer.Get(VexL.AboutDownloaded),
        UpdateState.Installing => _localizer.Get("Vex.VexL.UpdateInstallingHint"),
        UpdateState.Installed => _install?.Message ?? string.Empty,
        _ => _localizer.Format("Vex.VexL.UpdateCurrentVersion", VersionText)
    };
    public bool CanRecheck => !IsBusy && _update is not null;
    public bool IsIndeterminate => !_progress.HasValue;
    public double Progress => _progress ?? 0;
    public string ProgressText => _progress.HasValue ? (_progress.Value / 100).ToString("P0", _localizer.Culture) : string.Empty;
    public string ActionText => _localizer.Get(_state switch
    {
        UpdateState.Checking => VexL.AboutCheckingUpdate,
        UpdateState.Downloading => VexL.AboutDownloading,
        UpdateState.Installing => VexL.AboutInstalling,
        UpdateState.Available when !CanDownload => VexL.AboutOpenRelease,
        UpdateState.Available => VexL.AboutDownload,
        UpdateState.Downloaded => VexL.AboutInstall,
        UpdateState.Failed or UpdateState.Canceled when _download is not null => VexL.AboutInstall,
        UpdateState.Failed or UpdateState.Canceled when CanDownload => VexL.AboutDownload,
        _ => VexL.AboutCheckUpdate
    });
    public string StatusText => _state switch
    {
        UpdateState.Checking => _localizer.Get(VexL.AboutCheckingUpdate),
        UpdateState.Current => _localizer.Get(VexL.AboutUpToDate),
        UpdateState.Available => _localizer.Format(VexL.AboutUpdateAvailable, _update!.Tag),
        UpdateState.Downloading => _localizer.Format(VexL.AboutDownloadProgress, ProgressText),
        UpdateState.Downloaded => _localizer.Get(VexL.AboutDownloaded),
        UpdateState.Failed => _localizer.Format(VexL.AboutUpdateFailed, _error),
        UpdateState.Canceled => _localizer.Get(VexL.AboutCanceled),
        UpdateState.Installed => _install?.Message ?? string.Empty,
        _ => string.Empty
    };
    public string ReleaseNotes => _update?.Notes ?? string.Empty;
    /// <summary>按原型分节 + 类型化条目渲染的更新日志。四语切换后重建，标签跟着界面语言走。</summary>
    public IReadOnlyList<UpdateNoteVersion> ReleaseSections { get; private set; } = [];
    /// <summary>单版本升级不重复标题里已有的版本号，跨多版才逐节列出「V 版本 · 发布日期」。</summary>
    public bool ShowVersionHeaders => ReleaseSections.Count > 1;
    /// <summary>更新窗当前是否可见：下载完成 toast 只在窗体不可见时发（原型 layer-update.hidden 判定）。</summary>
    public bool IsWindowPresented { get => _windowPresented; set => _windowPresented = value; }

    /* ---------- 标题栏药丸（原型 pillSync）：随状态变形，下载中显进度、就绪后提示重启 ---------- */

    public bool PillVisible => _state is UpdateState.Available or UpdateState.Downloading or UpdateState.Downloaded
        or UpdateState.Installing or UpdateState.Failed or UpdateState.Canceled;
    /// <summary>下载完成/安装中亮脉冲环，提示回到更新窗确认。</summary>
    public bool PillReady => _state is UpdateState.Downloaded or UpdateState.Installing;
    public string PillText => _state switch
    {
        UpdateState.Downloading => _localizer.Format(VexL.UpdatePillDownloadingFormat, (int)Math.Round(Progress)),
        UpdateState.Downloaded or UpdateState.Installing => _localizer.Get(VexL.UpdatePillReady),
        _ => _localizer.Get(VexL.AboutCheckUpdateLabel)
    };
    /// <summary>药丸进度（0-100）：供「accent 底 + 左段混色」的星号分栏使用。</summary>
    public double PillProgress => IsDownloading ? Progress : 0;
    public string PillTip => _state switch
    {
        UpdateState.Downloading => _localizer.Get(VexL.UpdatePillDownloadingTip),
        UpdateState.Downloaded or UpdateState.Installing => _localizer.Get(VexL.UpdatePillReadyTip),
        _ => _update is null
            ? _localizer.Get(VexL.AboutCheckUpdate)
            : _localizer.Format(VexL.UpdatePillFoundTipFormat, LatestVersionText)
    };

    /* ---------- 日志气泡（原型 renderPop）：版本+日期、[系统要求]/[注意] 引用块、按标签分组 ---------- */

    public string BubbleTitle => HasBubble ? _localizer.Format(VexL.UpdateBubbleTitleFormat, LatestVersionText) : string.Empty;
    public string BubbleDate => _latestReleaseDate;
    public bool HasBubble => _update is not null && ReleaseSections.Count > 0;
    /// <summary>[系统要求]/[注意] 行原文升引用块；正文里出现才会渲染，当前《更新日志》暂无此类行。</summary>
    public IReadOnlyList<string> BubbleQuotes { get; private set; } = [];
    public IReadOnlyList<UpdateBubbleGroup> BubbleGroups { get; private set; } = [];
    public bool HasBubbleQuotes => BubbleQuotes.Count > 0;

    /// <summary>启动后台静默检查（原型 1.4s 后自动检查）：发现新版只亮药丸不打扰写作；
    /// 离线/请求失败回到待机态，不弹错误也不阻塞主界面。</summary>
    public async Task RunStartupCheckAsync()
    {
        if (_disposed || _startupWatched || _state != UpdateState.Idle) return;
        _startupWatched = true;
        try
        {
            await CheckCoreAsync(silent: true);
            if (_state == UpdateState.Available && !_windowPresented)
                _toast?.Publish(_localizer.Format(VexL.UpdateFoundToastFormat, LatestVersionText));
        }
        catch
        {
            // 静默检查的兜底：CheckCoreAsync 内部已归零状态，这里不再向用户暴露任何失败细节。
        }
    }

    /// <summary>药丸在后台态点击：恢复更新窗现场（不重新检查）。</summary>
    public void RestoreWindow() => RestoreWindowRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>气泡「完整日志」：打开内置《更新日志》文档。</summary>
    public void OpenFullChangelog() => OpenChangelogRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>气泡「立即更新」：日志已在气泡看过，直接进入下载（原型同时打开更新窗显示进度）；
    /// 无包时退回更新窗走「打开发布页」链路。</summary>
    public Task StartDownloadFromBubbleAsync()
    {
        RestoreWindow();
        if (_state == UpdateState.Available && CanDownload) return DownloadAsync();
        return Task.CompletedTask;
    }

    public async Task AdvanceAsync()
    {
        if (IsBusy || _disposed) return;
        if (_download is not null && _state is UpdateState.Downloaded or UpdateState.Failed or UpdateState.Canceled) await InstallAsync();
        else if (CanDownload && _state is UpdateState.Failed or UpdateState.Canceled) await DownloadAsync();
        else if (_state == UpdateState.Available)
        {
            if (CanDownload) await DownloadAsync();
            else OpenUrl(_update!.PageUrl);
        }
        else await CheckAsync();
    }

    public Task CheckAsync() => CheckCoreAsync(silent: false);

    private async Task CheckCoreAsync(bool silent)
    {
        if (IsBusy || _disposed) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _operation = operation;
        _error = null;
        SetState(UpdateState.Checking);
        try
        {
            _update = null;
            _download = null;
            _install = null;
            var result = await _updates.CheckAsync(cancellationToken: operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (!result.Succeeded)
            {
                // 静默检查失败不进失败态：保持待机，窗口里再次打开时还会重试。
                if (silent) SetState(UpdateState.Idle);
                else { _error = result.Error; SetState(UpdateState.Failed); }
            }
            else { _update = result.Update; SetState(_update is null ? UpdateState.Current : UpdateState.Available); }
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { SetState(silent ? UpdateState.Idle : UpdateState.Canceled); }
        catch (Exception error)
        {
            if (silent) SetState(UpdateState.Idle);
            else { _error = error.Message; SetState(UpdateState.Failed); }
        }
        finally { _operation = null; }
    }

    private Task DownloadAsync() => RunAsync(UpdateState.Downloading, async token =>
    {
        _progress = null;
        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            if (_disposed || token.IsCancellationRequested || !IsDownloading) return;
            _progress = value.Percentage;
            Refresh();
        });
        _download = await _updates.DownloadAsync(_update!, progress, token);
        token.ThrowIfCancellationRequested();
        SetState(UpdateState.Downloaded);
        // 下载中关窗/最小化不再终止下载（原型：后台完成只 toast，不打断写作）。
        if (!_windowPresented) _toast?.Publish(_localizer.Get(VexL.UpdateReadyToast));
    });

    private Task InstallAsync() => RunAsync(UpdateState.Installing, async token =>
    {
        _install = await _updates.InstallAsync(_download!, token);
        token.ThrowIfCancellationRequested();
        if (_install.Kind is UpdateInstallKind.Manual or UpdateInstallKind.Unsupported)
        {
            _error = _install.Message;
            SetState(UpdateState.Failed);
            return;
        }
        SetState(UpdateState.Installed);
        if (_install.Kind == UpdateInstallKind.Launched && _install.ShouldExitApplication) _exit();
    });

    private async Task RunAsync(UpdateState state, Func<CancellationToken, Task> action)
    {
        if (IsBusy || _disposed) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _operation = operation;
        _error = null;
        SetState(state);
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { SetState(UpdateState.Canceled); }
        catch (Exception error) { _error = error.Message; SetState(UpdateState.Failed); }
        finally { _operation = null; }
    }

    private void OpenUrl(string url)
    {
        try { _openUrl(url); }
        catch (Exception error) { _error = error.Message; SetState(UpdateState.Failed); }
    }

    private void SetState(UpdateState state) { _state = state; Refresh(); }
    private void OnCultureChanged(object? sender, EventArgs args) => Refresh();
    private void Refresh()
    {
        RebuildReleaseSections();
        foreach (var property in new[] { nameof(State), nameof(IsBusy), nameof(IsDownloading), nameof(CanRecheck), nameof(IsIndeterminate),
                     nameof(Progress), nameof(ProgressText), nameof(ActionText), nameof(StatusText), nameof(ReleaseNotes), nameof(DownloadPath), nameof(HasDownload),
                     nameof(ShowAction), nameof(ShowLater), nameof(IsPrimaryAction), nameof(HasReleaseNotes), nameof(PhaseTitle), nameof(PhaseDetail),
                     nameof(ReleaseSections), nameof(ShowVersionHeaders),
                     nameof(PillVisible), nameof(PillReady), nameof(PillText), nameof(PillProgress), nameof(PillTip),
                     nameof(BubbleTitle), nameof(BubbleDate), nameof(HasBubble), nameof(BubbleQuotes), nameof(BubbleGroups), nameof(HasBubbleQuotes) })
            this.RaisePropertyChanged(property);
    }

    private void RebuildReleaseSections()
    {
        // 标签词表与资源键的对照由 I18n/zh-CN.json 生成（Vex.VexReleaseTag），这里只把键翻成当前界面的文案。
        var parsed = UpdateReleaseNotesParser.Parse(_update?.Notes, _localizer.Get);
        // 原型表头是「2026-10-27 发布」，日期后带一个限定词；四语各自表述，空日期（无分节的自定义正文）不硬凑。
        _latestReleaseDate = parsed.Count > 0 ? parsed[0].Date : string.Empty;
        ReleaseSections = parsed.Select(section => section with
        {
            ShowHeader = parsed.Count > 1,
            Date = section.Date.Length > 0 ? _localizer.Format(VexL.UpdateReleasedOn, section.Date) : section.Date,
        }).ToList();
        RebuildBubbleGroups();
    }

    /// <summary>气泡条目（原型 renderPop）：[系统要求]/[注意] 行升为引用块，其余按新增/优化/修复/删除分组，
    /// 分组顺序按首次出现排列，词表外条目归入「其他」组（无色点语义，随 muted 走）。</summary>
    private void RebuildBubbleGroups()
    {
        if (!HasBubble) { BubbleQuotes = []; BubbleGroups = []; return; }
        var quotes = new List<string>();
        var groups = new List<(string? Key, List<string> Items)>();
        foreach (var section in ReleaseSections)
        {
            foreach (var entry in section.Entries)
            {
                if (QuotePrefixes.Any(prefix => entry.Text.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    quotes.Add(entry.Text);
                    continue;
                }
                var key = entry.TagKey;
                var group = groups.FirstOrDefault(item => item.Key == key);
                if (group.Items is null)
                {
                    group = (key, []);
                    groups.Add(group);
                }
                group.Items.Add(entry.Text);
            }
        }
        BubbleQuotes = quotes;
        BubbleGroups = groups.Select(group => new UpdateBubbleGroup(group.Key,
            group.Key is null ? _localizer.Get("Vex.VexL.UpdateBubbleOthers") : _localizer.Get(group.Key),
            group.Items)).ToList();
    }

    /// <summary>气泡引用块只认正文里的这两个数据词（原型同款字面量）：与 UpdateTagSource* 一样按数据对待，
    /// 不随界面语言变化；《更新日志》换词需回这里同步。</summary>
    private static readonly string[] QuotePrefixes = ["[系统要求]", "[注意]"];

    /// <summary>标题里的版本号去掉 tag 的 v 前缀：那是发布命名约定，不是读者认知的版本号。</summary>
    private string LatestVersionText => _update!.Tag.TrimStart('v', 'V');
    private bool CanDownload => !string.IsNullOrWhiteSpace(_update?.AssetUrl) && !string.IsNullOrWhiteSpace(_update.ChecksumUrl);

    public void Close() => RequestClose?.Invoke(this, null);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _localizer.CultureChanged -= OnCultureChanged;
        _lifetime.Dispose();
        UpdateCommand.Dispose();
        CheckCommand.Dispose();
        CancelCommand.Dispose();
        OpenFolderCommand.Dispose();
    }
}

public enum UpdateState { Idle, Checking, Current, Available, Downloading, Downloaded, Installing, Installed, Failed, Canceled }
