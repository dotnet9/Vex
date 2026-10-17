using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using CodeWF.Toolkit.Core.Extensions;
using CodeWF.Toolkit.Core.UpdateChecking;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellAboutViewModel : ReactiveObject, IDialogContext, IDisposable
{
    private readonly IUpdateService _updates;
    private readonly IAppLocalizer _localizer;
    private readonly Action<string> _openUrl;
    private readonly Action _exit;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _operation;
    private UpdateInfo? _update;
    private UpdateDownloadResult? _download;
    private AboutUpdateState _state;
    private string? _error;
    private UpdateInstallResult? _install;
    private double? _progress;
    private bool _disposed;

    public ShellAboutViewModel(IUpdateService updates, IAppLocalizer localizer,
        Action<string> openUrl, Action exit, Assembly? assembly = null)
    {
        _updates = updates;
        _localizer = localizer;
        _openUrl = openUrl;
        _exit = exit;
        assembly ??= typeof(App).Assembly;
        VersionText = UpdateVersion.Display(assembly);
        PlatformText = ".NET 10，Avalonia";
        CompileTimeText = assembly.CompileTime()?.ToString("yyyy-MM-dd HH:mm:ss", localizer.Culture) ?? "—";
        CopyrightText = "Copyright © 2026 Vex(维刻)";
        var available = this.WhenAnyValue(model => model.IsBusy).Select(busy => !busy);
        UpdateCommand = ReactiveCommand.CreateFromTask(AdvanceAsync, available);
        CheckCommand = ReactiveCommand.CreateFromTask(CheckAsync, available);
        CancelCommand = ReactiveCommand.Create(() => _operation?.Cancel());
        RepositoryCommand = ReactiveCommand.Create(() => OpenUrl("https://github.com/dotnet9/Vex"));
        WebsiteCommand = ReactiveCommand.Create(() => OpenUrl("https://codewf.com"));
        ChangelogCommand = ReactiveCommand.Create(() => { WantsChangelog = true; Close(); });
        _localizer.CultureChanged += OnCultureChanged;
    }

    public bool WantsChangelog { get; private set; }
    public ReactiveCommand<Unit, Unit> ChangelogCommand { get; }
    public string VersionText { get; }
    public string PlatformText { get; }
    public string CompileTimeText { get; }
    public string CopyrightText { get; }
    public event EventHandler<object?>? RequestClose;
    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> RepositoryCommand { get; }
    public ReactiveCommand<Unit, Unit> WebsiteCommand { get; }
    public AboutUpdateState State => _state;
    public bool IsBusy => _state is AboutUpdateState.Checking or AboutUpdateState.Downloading or AboutUpdateState.Installing;
    public bool IsDownloading => _state == AboutUpdateState.Downloading;
    public bool CanRecheck => _state is AboutUpdateState.Available or AboutUpdateState.Downloaded;
    public bool IsIndeterminate => !_progress.HasValue;
    public double Progress => _progress ?? 0;
    public string ProgressText => _progress.HasValue ? (_progress.Value / 100).ToString("P0", _localizer.Culture) : string.Empty;
    public string ActionText => _localizer.Get(_state switch
    {
        AboutUpdateState.Checking => VexL.AboutCheckingUpdate,
        AboutUpdateState.Downloading => VexL.AboutDownloading,
        AboutUpdateState.Installing => VexL.AboutInstalling,
        AboutUpdateState.Available when !CanDownload => VexL.AboutOpenRelease,
        AboutUpdateState.Available => VexL.AboutDownload,
        AboutUpdateState.Downloaded when _download?.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true => VexL.AboutOpenFolder,
        AboutUpdateState.Downloaded => VexL.AboutInstall,
        _ => VexL.AboutCheckUpdate
    });
    public string StatusText => _state switch
    {
        AboutUpdateState.Checking => _localizer.Get(VexL.AboutCheckingUpdate),
        AboutUpdateState.Current => _localizer.Get(VexL.AboutUpToDate),
        AboutUpdateState.Available => _localizer.Format(VexL.AboutUpdateAvailable, _update!.Tag),
        AboutUpdateState.Downloading => _localizer.Format(VexL.AboutDownloadProgress, ProgressText),
        AboutUpdateState.Downloaded => _localizer.Get(VexL.AboutDownloaded),
        AboutUpdateState.Failed => _localizer.Format(VexL.AboutUpdateFailed, _error),
        AboutUpdateState.Canceled => _localizer.Get(VexL.AboutCanceled),
        AboutUpdateState.Installed => _install?.Message ?? string.Empty,
        _ => string.Empty
    };
    public string ReleaseNotes => _update?.Notes ?? string.Empty;
    private bool CanDownload => !string.IsNullOrWhiteSpace(_update?.AssetUrl) && !string.IsNullOrWhiteSpace(_update.ChecksumUrl);

    public async Task AdvanceAsync()
    {
        if (IsBusy || _disposed) return;
        if (_state == AboutUpdateState.Available)
        {
            if (CanDownload) await DownloadAsync();
            else OpenUrl(_update!.PageUrl);
        }
        else if (_state == AboutUpdateState.Downloaded) await InstallAsync();
        else await CheckAsync();
    }

    public Task CheckAsync() => RunAsync(AboutUpdateState.Checking, async token =>
    {
        _update = null;
        _download = null;
        _install = null;
        var result = await _updates.CheckAsync(cancellationToken: token);
        token.ThrowIfCancellationRequested();
        if (!result.Succeeded) { _error = result.Error; SetState(AboutUpdateState.Failed); }
        else { _update = result.Update; SetState(_update is null ? AboutUpdateState.Current : AboutUpdateState.Available); }
    });

    private Task DownloadAsync() => RunAsync(AboutUpdateState.Downloading, async token =>
    {
        _progress = null;
        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            if (_disposed || !IsDownloading) return;
            _progress = value.Percentage;
            Refresh();
        });
        _download = await _updates.DownloadAsync(_update!, progress, token);
        token.ThrowIfCancellationRequested();
        SetState(AboutUpdateState.Downloaded);
    });

    private Task InstallAsync() => RunAsync(AboutUpdateState.Installing, async token =>
    {
        _install = await _updates.InstallAsync(_download!, token);
        SetState(AboutUpdateState.Installed);
        if (_install.ShouldExitApplication) _exit();
    });

    private async Task RunAsync(AboutUpdateState state, Func<CancellationToken, Task> action)
    {
        if (IsBusy || _disposed) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _operation = operation;
        SetState(state);
        try { await action(operation.Token); }
        catch (OperationCanceledException) when (operation.IsCancellationRequested) { SetState(AboutUpdateState.Canceled); }
        catch (Exception error) { _error = error.Message; SetState(AboutUpdateState.Failed); }
        finally { _operation = null; }
    }

    private void OpenUrl(string url)
    {
        try { _openUrl(url); }
        catch (Exception error) { _error = error.Message; SetState(AboutUpdateState.Failed); }
    }

    private void SetState(AboutUpdateState state) { _state = state; Refresh(); }
    private void OnCultureChanged(object? sender, EventArgs args) => Refresh();
    private void Refresh()
    {
        foreach (var property in new[] { nameof(State), nameof(IsBusy), nameof(IsDownloading), nameof(CanRecheck), nameof(IsIndeterminate),
                     nameof(Progress), nameof(ProgressText), nameof(ActionText), nameof(StatusText), nameof(ReleaseNotes) })
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
        ChangelogCommand.Dispose();
        UpdateCommand.Dispose();
        CheckCommand.Dispose();
        CancelCommand.Dispose();
        RepositoryCommand.Dispose();
        WebsiteCommand.Dispose();
    }
}

public enum AboutUpdateState { Idle, Checking, Current, Available, Downloading, Downloaded, Installing, Installed, Failed, Canceled }
