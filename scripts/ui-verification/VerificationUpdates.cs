using CodeWF.Toolkit.Core.UpdateChecking;

public sealed class VerificationUpdates : IUpdateService
{
    private readonly TaskCompletionSource<UpdateDownloadResult> _download = new();
    // 与真实发布正文同形：`## V版本（日期）` 分节 + `- 表情[标签]-正文` 四类标签，另有一条没有标签的散行。
    // 夹具要是这份格式，解析回归才有人守着；换成「Update notes」这类散文本就什么都测不到。
    // 可变是为了让验证脚本中途换正文（例如跨多版升级要逐节列表头），不必为一次断言再做一个服务桩。
    public string Notes { get; set; } = """
        ## V9.0.0（2026-10-28）

        - ✨[新增]-自动保存开启时外部修改静默载入。
        - ⚡[优化]-分段控件收进共享库的 SegmentedPanel。
        - 🐛[修复]-阅读排版弹层二次点击可以收起。
        - ❌[删除]-更新日志的展开与折叠开关。
        - 附：安装包行尾归一化。
        """;
    public int Checks { get; private set; }
    public int Downloads { get; private set; }
    public UpdateServiceOptions Options { get; } = new() { Repository = "Vex" };
    public Version CurrentVersion => new(1, 6, 0);
    public string CurrentVersionText => "1.6.0";
    public Task<UpdateCheckResult> CheckAsync(Version? current = null, CancellationToken cancellationToken = default)
    {
        Checks++;
        return Task.FromResult(new UpdateCheckResult(new UpdateInfo(new Version(9, 0), "v9.0.0", "Vex", Notes,
            "https://example.test/releases", "https://example.test/Vex-setup.exe", "Vex-setup.exe", "https://example.test/Vex-setup.exe.sha256"), true, null));
    }
    public Task<UpdateDownloadResult> DownloadAsync(UpdateInfo update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        Downloads++;
        progress?.Report(new UpdateDownloadProgress(50, 100));
        return _download.Task.WaitAsync(cancellationToken);
    }
    public void CompleteDownload() => _download.SetResult(new UpdateDownloadResult(Path.Combine(Path.GetTempPath(), "Vex 更新", "Vex-setup.exe"), "Vex-setup.exe", 100));
    public Task<UpdateInstallResult> InstallAsync(UpdateDownloadResult download, CancellationToken cancellationToken = default)
        => Task.FromResult(new UpdateInstallResult(UpdateInstallKind.Manual, "Installer not ready / 安装向导尚未就绪") { ShouldExitApplication = false });
}
