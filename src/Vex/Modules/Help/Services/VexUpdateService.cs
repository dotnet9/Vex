using CodeWF.Toolkit.Core.UpdateChecking;
using Vex.Core.Services;

namespace Vex.Modules.Help.Services;

/// <summary>检查、下载和校验复用 Toolkit；Windows 使用 Vex 安装器的就绪握手。</summary>
public sealed class VexUpdateService : IUpdateService, IDisposable
{
    private readonly UpdateService _updates = new(new UpdateServiceOptions
    {
        Owner = "dotnet9", Repository = "Vex", ProductName = "Vex"
    });
    private readonly WindowsUpdateInstaller _windows;

    public VexUpdateService(IAppLocalizer localizer) => _windows = new WindowsUpdateInstaller(localizer);
    public UpdateServiceOptions Options => _updates.Options;
    public Version CurrentVersion => _updates.CurrentVersion;
    public string CurrentVersionText => _updates.CurrentVersionText;
    public Task<UpdateCheckResult> CheckAsync(Version? current = null, CancellationToken cancellationToken = default)
        => _updates.CheckAsync(current, cancellationToken);
    public Task<UpdateDownloadResult> DownloadAsync(UpdateInfo update, IProgress<UpdateDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        => _updates.DownloadAsync(update, progress, cancellationToken);
    public Task<UpdateInstallResult> InstallAsync(UpdateDownloadResult download, CancellationToken cancellationToken = default)
        => OperatingSystem.IsWindows() ? _windows.InstallAsync(download.FilePath, cancellationToken) : _updates.InstallAsync(download, cancellationToken);
    public void Dispose() => _updates.Dispose();
}
