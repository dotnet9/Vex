using System.Globalization;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using CodeWF.Toolkit.Core.UpdateChecking;
using ReactiveUI;
using Vex;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Help.Services;

RxApp.MainThreadScheduler = CurrentThreadScheduler.Instance;
int passed = 0;
var service = new FakeUpdates();
var localizer = new FakeLocalizer();
var opened = new List<string>();
int exits = 0;
using (var about = new ShellAboutViewModel(localizer, opened.Add))
{
    Check(about.PlatformText.Contains("Avalonia"), "runtime metadata");
    about.WebsiteCommand.Execute().Subscribe();
    Check(opened.Single() == "https://vex.codewf.com/", "About uses the configured website");
    opened.Clear();
    about.UpdateCommand.Execute().Subscribe();
    Check(about.WantsUpdates, "About requests the shared update view");
    Check(service.Checks == 0 && service.Downloads == 0, "About has no check or download path");
}
using (var model = new ShellUpdateViewModel(service, localizer, opened.Add, () => exits++))
{
    Check(model.State == UpdateState.Idle && !model.IsBusy, "initial state");
    await model.CheckAsync();
    Check(model.State == UpdateState.Current, "no update");
    service.Result = UpdateCheckResult.Failed("offline");
    await model.CheckAsync();
    Check(model.State == UpdateState.Failed && model.StatusText.Contains("offline"), "visible failure");
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
    Check(model.State == UpdateState.Available && model.ReleaseNotes == "Release notes", "new release and notes");
    await model.AdvanceAsync();
    Check(model.State == UpdateState.Downloaded && service.Downloads == 1, "verified download");
    var checksBeforeReopen = service.Checks;
    model.Close();
    await model.EnsureCheckedAsync();
    Check(model.HasDownload && service.Checks == checksBeforeReopen && service.Downloads == 1, "reopening retains verified download without recheck");
    service.Install = new UpdateInstallResult(UpdateInstallKind.Manual, "Not ready") { ShouldExitApplication = true };
    await model.AdvanceAsync();
    Check(model.State == UpdateState.Failed && exits == 0 && model.HasDownload, "manual result never exits and preserves download");
    var downloadsBeforeRetry = service.Downloads;
    service.Install = new UpdateInstallResult(UpdateInstallKind.Launched, "Started") { ShouldExitApplication = false };
    await model.AdvanceAsync();
    Check(model.State == UpdateState.Installed && exits == 0, "non-Windows installation does not exit");
    Check(service.Downloads == downloadsBeforeRetry, "install retry reuses verified download");
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
    Check(model.State == UpdateState.Canceled && !model.IsBusy, "cancel restores usable state");
    service.Delay = null;
    service.Result = Available(true);
    await model.CheckAsync();
    service.DownloadError = new IOException("checksum mismatch");
    await model.AdvanceAsync();
    Check(model.State == UpdateState.Failed && model.StatusText.Contains("checksum mismatch"), "download failure does not enable installation");
    var oldText = model.ActionText;
    using (var notes = new ShellUpdateViewModel(service, localizer, opened.Add, () => exits++))
    {
        // 更新日志类型化：分节靠版本标题、标签靠资源键判定，胶囊文案跟着界面语言现取。
        // 夹具的行内标签词由同一个 localizer 现取，正好复现「正文词形 = UpdateTagSource*」这条契约。
        const char Open = '\uFF08', Close = '\uFF09'; // 《更新日志》分节日期的全角括号
        string SectionNotes(string version, string date, string body) =>
            $"## V{version}{Open}{date}{Close}\n{body}\n";
        string TypedBody() =>
            $"- \u2728[{localizer.Get(VexL.UpdateTagSourceNew)}]-Typed first line\n" +
            $"- \U0001F41B[{localizer.Get(VexL.UpdateTagSourceFix)}]-Typed second line\n" +
            "- \U0001F604[Whatever]-Untagged fallback line\n";
        // 发布正文从 Windows 粘贴时常带 CRLF：分节式子按 \n 写，不归一化整段日志会退化成散行。
        service.Result = Available(true) with
        {
            Update = Available(true).Update! with
            {
                Notes = SectionNotes("9.0.0", "2026-10-27", TypedBody())
                    .Replace("\n", "\r\n")
                    + SectionNotes("8.9.0", "2026-10-20", "- plain line\n"),
            },
        };
        await notes.CheckAsync();
        Check(notes.ReleaseSections.Count == 2 && notes.ShowVersionHeaders, "two sections parsed from CRLF notes");
        var latest = notes.ReleaseSections[0];
        Check(latest.Version == "9.0.0" && latest.Entries.Count == 3, "newest section keeps every line");
        Check(latest.ShowHeader && latest.Date == localizer.Format(VexL.UpdateReleasedOn, "2026-10-27"), "section header localized once released-on resolves");
        var first = latest.Entries[0];
        Check(first.HasTag && first.IsNew && !first.IsFixed
              && first.TagKey == VexL.UpdateTagNew && first.TagText == localizer.Get(VexL.UpdateTagNew)
              && first.Text == "Typed first line", "tag resolves to the resource key, pill text to the current language");
        Check(latest.Entries[1] is { IsFixed: true, Text: "Typed second line" }, "fix tag uses the warning pill");
        Check(!latest.Entries[2].HasTag && latest.Entries[2].Text.StartsWith("\U0001F604"), "unknown tag falls back to the raw line");
        Check(notes.ReleaseSections[1].Entries.Single() is { HasTag: false, Text: "plain line" }, "older section parses independently");
        service.Result = Available(true) with
        {
            Update = Available(true).Update! with { Notes = SectionNotes("9.0.0", "2026-10-27", TypedBody()) },
        };
        await notes.CheckAsync();
        Check(!notes.ShowVersionHeaders && !notes.ReleaseSections[0].ShowHeader, "single version never repeats the title in a header");
        localizer.SetCulture("ja-JP");
        service.Result = Available(true) with
        {
            Update = Available(true).Update! with { Notes = SectionNotes("9.0.0", "2026-10-27", TypedBody()) },
        };
        await notes.CheckAsync();
        Check(notes.ReleaseSections[0].Entries[0].TagKey == VexL.UpdateTagNew, "language change never moves the tag key");
        Check(notes.ReleaseSections[0].Entries[0].TagText != first.TagText, "pill text follows the new language");
        localizer.SetCulture("en-US");
    }

    localizer.SetCulture("zh-CN");
    Check(model.ActionText != oldText, "language change updates action label");
}
Check(localizer.Subscribers == 0, "closing releases culture subscription");
if (OperatingSystem.IsWindows())
{
    var directory = Path.Combine(Path.GetTempPath(), "Vex handoff 测试 " + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    var package = Path.Combine(directory, "Vex setup.exe");
    await File.WriteAllTextAsync(package, "controlled fixture, never executed");
    try
    {
        foreach (var mode in new[] { "ready", "not-ready", "start-failed", "throws", "cancel" })
        {
            using var cancel = new CancellationTokenSource();
            var installer = new WindowsUpdateInstaller(localizer, info =>
            {
                Check(info.UseShellExecute && info.FileName == package && info.WorkingDirectory == directory, "safe launch path: " + mode);
                Check(info.ArgumentList.Any(arg => arg == "/LOG=" + package + ".install.log"), "quoted diagnostic log argument: " + mode);
                if (mode == "throws") throw new InvalidOperationException("start rejected");
                if (mode == "start-failed") return false;
                if (mode == "ready")
                {
                    var name = info.ArgumentList.Single(arg => arg.StartsWith("/VEXUPDATEREADY="))[16..];
                    using var signal = EventWaitHandle.OpenExisting(name);
                    signal.Set();
                }
                if (mode == "cancel") cancel.Cancel();
                return true;
            }, TimeSpan.FromMilliseconds(120));
            try
            {
                var result = await installer.InstallAsync(package, cancel.Token);
                Check(result.ShouldExitApplication == (mode == "ready"), "only acknowledged installer permits exit: " + mode);
                Check(result.Kind == (mode == "ready" ? UpdateInstallKind.Launched : UpdateInstallKind.Manual), "handoff result: " + mode);
            }
            catch (OperationCanceledException) when (mode == "cancel") { Check(true, "cancel does not return an exit request"); }
        }
    }
    finally { File.Delete(package); Directory.Delete(directory); }
}
Console.WriteLine($"Passed {passed} update and installer handoff scenarios. No real installer was launched.");

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
