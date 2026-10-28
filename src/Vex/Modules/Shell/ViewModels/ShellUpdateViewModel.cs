using System.Reactive;
using System.Reactive.Linq;
using CodeWF.Toolkit.Core.UpdateChecking;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellUpdateViewModel : ReactiveObject, IDialogContext, IDisposable
{
    private readonly IUpdateService _updates;
    private readonly IAppLocalizer _localizer;
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

    public ShellUpdateViewModel(IUpdateService updates, IAppLocalizer localizer,
        Action<string> openUrl, Action exit)
    {
        _updates = updates;
        _localizer = localizer;
        _openUrl = openUrl;
        _exit = exit;
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
    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
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
    private void RebuildReleaseSections()
    {
        var parsed = UpdateReleaseNotesParser.Parse(_update?.Notes, tag => tag switch
        {
            "新增" => _localizer.Get(VexL.UpdateTagNew),
            "优化" => _localizer.Get(VexL.UpdateTagOpt),
            "修复" => _localizer.Get(VexL.UpdateTagFix),
            "删除" => _localizer.Get(VexL.UpdateTagRemove),
            _ => tag,
        });
        // 原型表头是「2026-10-27 发布」，日期后带一个限定词；四语各自表述，空日期（无分节的自定义正文）不硬凑。
        ReleaseSections = parsed.Select(section => section with
        {
            ShowHeader = parsed.Count > 1,
            Date = section.Date.Length > 0 ? _localizer.Format(VexL.UpdateReleasedOn, section.Date) : section.Date,
        }).ToList();
    }
    /// <summary>标题里的版本号去掉 tag 的 v 前缀：那是发布命名约定，不是读者认知的版本号。</summary>
    private string LatestVersionText => _update!.Tag.TrimStart('v', 'V');
    private bool CanDownload => !string.IsNullOrWhiteSpace(_update?.AssetUrl) && !string.IsNullOrWhiteSpace(_update.ChecksumUrl);

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

    public Task CheckAsync() => RunAsync(UpdateState.Checking, async token =>
    {
        _update = null;
        _download = null;
        _install = null;
        var result = await _updates.CheckAsync(cancellationToken: token);
        token.ThrowIfCancellationRequested();
        if (!result.Succeeded) { _error = result.Error; SetState(UpdateState.Failed); }
        else { _update = result.Update; SetState(_update is null ? UpdateState.Current : UpdateState.Available); }
    });

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
                     nameof(ReleaseSections), nameof(ShowVersionHeaders) })
            this.RaisePropertyChanged(property);
    }

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
