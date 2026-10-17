using System.Diagnostics;
using CodeWF.Toolkit.Core.UpdateChecking;
using Vex.Core.Services;

namespace Vex.Modules.Help.Services;

/// <summary>Inno 的引导进程可能先退出，必须等待真正的安装窗口确认，不能只检查 Process.Start。</summary>
public sealed class WindowsUpdateInstaller
{
    private readonly IAppLocalizer _localizer;
    private readonly Func<ProcessStartInfo, bool> _start;
    private readonly TimeSpan _timeout;

    public WindowsUpdateInstaller(IAppLocalizer localizer) : this(localizer, Start, TimeSpan.FromSeconds(60)) { }

    internal WindowsUpdateInstaller(IAppLocalizer localizer, Func<ProcessStartInfo, bool> start, TimeSpan timeout)
    {
        _localizer = localizer;
        _start = start;
        _timeout = timeout;
    }

    public async Task<UpdateInstallResult> InstallAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(filePath) || !Path.GetExtension(filePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            return Failed(filePath);

        var eventName = @"Local\Vex.Update." + Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        var start = new ProcessStartInfo(Path.GetFullPath(filePath))
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath))!
        };
        start.ArgumentList.Add("/VEXUPDATEREADY=" + eventName);
        // 同目录日志便于在安装向导未能显示时定位；Arguments 由运行库引用，支持中文和空格路径。
        start.ArgumentList.Add("/LOG=" + Path.GetFullPath(filePath) + ".install.log");
        try
        {
            if (!_start(start)) return Failed(filePath);
            var timer = Stopwatch.StartNew();
            while (timer.Elapsed < _timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ready.WaitOne(0))
                    return new UpdateInstallResult(UpdateInstallKind.Launched, _localizer.Get(VexL.UpdateInstallerReady)) { ShouldExitApplication = true };
                await Task.Delay(100, cancellationToken);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { return Failed(filePath, error.Message); }
        return Failed(filePath);
    }

    private UpdateInstallResult Failed(string filePath, string? details = null) => new(UpdateInstallKind.Manual,
        _localizer.Format(VexL.UpdateInstallerNotReady, filePath) + (details is null ? string.Empty : Environment.NewLine + details)) { ShouldExitApplication = false };

    private static bool Start(ProcessStartInfo info)
    {
        using var process = Process.Start(info);
        return process is not null;
    }
}
