using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using CodeWF.Toolkit.Core.UpdateChecking;
using ReactiveUI;
using Vex;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

RxApp.MainThreadScheduler = CurrentThreadScheduler.Instance;
int passed = 0;
var service = new FakeUpdates();
var localizer = new FakeLocalizer();
var opened = new List<string>();
int exits = 0;
using (var model = new ShellAboutViewModel(service, localizer, opened.Add, () => exits++))
{
    Check(model.State == AboutUpdateState.Idle && !model.IsBusy, "initial state");
    Check(model.PlatformText.Contains(".NET") && model.PlatformText.Contains("Avalonia"), "runtime metadata");
    await model.CheckAsync();
    Check(model.State == AboutUpdateState.Current, "no update");
    service.Result = UpdateCheckResult.Failed("offline");
    await model.CheckAsync();
    Check(model.State == AboutUpdateState.Failed && model.StatusText.Contains("offline"), "visible failure");
    service.Result = Available(checksum: false);
    await model.CheckAsync();
    await model.AdvanceAsync();
    Check(opened.Single() == "https://example.test/releases/tag/v9.0.0" && service.Downloads == 0, "missing checksum opens release page");
    service.Result = Available(checksum: true) with { Update = Available(true).Update! with { AssetUrl = null } };
    await model.CheckAsync();
    await model.AdvanceAsync();
    Check(service.Downloads == 0 && opened.Count == 2, "missing platform asset opens release page");
    service.Result = Available(checksum: true);
    await model.CheckAsync();
    Check(model.State == AboutUpdateState.Available && model.ReleaseNotes == "Release notes", "new release and notes");
    await model.AdvanceAsync();
    Check(model.State == AboutUpdateState.Downloaded && service.Downloads == 1, "verified download");
    service.Install = new UpdateInstallResult(UpdateInstallKind.Launched, "Started") { ShouldExitApplication = false };
    await model.AdvanceAsync();
    Check(model.State == AboutUpdateState.Installed && exits == 0, "non-Windows installation does not exit");
    await model.CheckAsync();
    await model.AdvanceAsync();
    service.Install = new UpdateInstallResult(UpdateInstallKind.Launched, "Started") { ShouldExitApplication = true };
    await model.AdvanceAsync();
    Check(exits == 1, "exit only after installer requests it");
    service.Delay = new TaskCompletionSource<UpdateCheckResult>();
    var pending = model.CheckAsync();
    Check(model.IsBusy, "checking disables commands");
    var count = service.Checks;
    await model.CheckAsync();
    Check(service.Checks == count, "duplicate check ignored");
    model.CancelCommand.Execute().Subscribe();
    await pending;
    Check(model.State == AboutUpdateState.Canceled && !model.IsBusy, "cancel restores usable state");
    service.Delay = null;
    service.Result = Available(true);
    await model.CheckAsync();
    service.DownloadError = new IOException("checksum mismatch");
    await model.AdvanceAsync();
    Check(model.State == AboutUpdateState.Failed && model.StatusText.Contains("checksum mismatch"), "download failure does not enable installation");
    var oldText = model.ActionText;
    localizer.SetCulture("zh-CN");
    Check(model.ActionText != oldText, "language change updates action label");
}
Check(localizer.Subscribers == 0, "closing releases culture subscription");
Console.WriteLine($"Passed {passed} about/update UI scenarios. No real installer was launched.");

void Check(bool success, string scenario)
{
    if (!success) throw new InvalidOperationException("Failed: " + scenario);
    passed++;
}

static UpdateCheckResult Available(bool checksum) => new(new UpdateInfo(new Version(9, 0, 0),
    "v9.0.0", "Vex", "Release notes", "https://example.test/releases/tag/v9.0.0",
    "https://example.test/Vex-setup.exe", "Vex-setup.exe", checksum ? "https://example.test/Vex-setup.exe.sha256" : null), true, null);

sealed class FakeUpdates : IUpdateService
{
    public UpdateServiceOptions Options { get; } = new() { Repository = "Vex", ProductName = "Vex" };
    public Version CurrentVersion => new(1, 0, 0);
    public string CurrentVersionText => "1.0.0";
    public UpdateCheckResult Result { get; set; } = UpdateCheckResult.Latest();
    public UpdateInstallResult Install { get; set; } = new(UpdateInstallKind.Manual, "Manual");
    public TaskCompletionSource<UpdateCheckResult>? Delay { get; set; }
    public Exception? DownloadError { get; set; }
    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public async Task<UpdateCheckResult> CheckAsync(Version? current = null, CancellationToken cancellationToken = default)
    {
        Checks++;
        return Delay is { } delay ? await delay.Task.WaitAsync(cancellationToken) : Result;
    }
    public Task<UpdateDownloadResult> DownloadAsync(UpdateInfo update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Downloads++;
        if (DownloadError is { } error) throw error;
        progress?.Report(new UpdateDownloadProgress(50, 100));
        return Task.FromResult(new UpdateDownloadResult("Vex-setup.exe", "Vex-setup.exe", 100));
    }
    public Task<UpdateInstallResult> InstallAsync(UpdateDownloadResult download, CancellationToken cancellationToken = default) => Task.FromResult(Install);
}

sealed class FakeLocalizer : IAppLocalizer
{
    private EventHandler<EventArgs>? _changed;
    public int Subscribers { get; private set; }
    public event EventHandler<EventArgs>? CultureChanged
    {
        add { _changed += value; Subscribers++; }
        remove { _changed -= value; Subscribers--; }
    }
    public CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");
    public void SetCulture(string name) { Culture = CultureInfo.GetCultureInfo(name); _changed?.Invoke(this, EventArgs.Empty); }
    public string Get(string key) => $"{Culture.Name}:{key}";
    public string Format(string key, params object?[] args) => Get(key) + ":" + string.Join(",", args);
}
