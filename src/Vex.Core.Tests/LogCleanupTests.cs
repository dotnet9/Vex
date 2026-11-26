using Vex.Core.Services;

using Xunit;

namespace Vex.Core.Tests;

/// <summary>日志清理：只删超期 *.log，保留新日志与其它扩展名；占用/权限失败静默跳过。</summary>
public sealed class LogCleanupTests
{
    private static string NewDirectory()
    {
        var directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "vex-logcleanup-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        return directory;
    }

    private static string Touch(string directory, string name, DateTimeOffset lastWrite)
    {
        var path = System.IO.Path.Combine(directory, name);
        File.WriteAllText(path, string.Empty);
        File.SetLastWriteTimeUtc(path, lastWrite.UtcDateTime);
        return path;
    }

    [Fact]
    public void DeletesOnlyExpiredLogs_KeepsRecentAndOtherExtensions()
    {
        var directory = NewDirectory();
        var now = DateTimeOffset.UtcNow;
        var expiredLog = Touch(directory, "Log_1_2026_01_01.log", now - TimeSpan.FromDays(8));
        var freshLog = Touch(directory, "Log_2_2026_11_26.log", now - TimeSpan.FromDays(6));
        var freshOther = Touch(directory, "audit.jsonl", now - TimeSpan.FromDays(30));

        var removed = LogCleanup.DeleteOldLogs(directory, TimeSpan.FromDays(7), now);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(expiredLog));
        Assert.True(File.Exists(freshLog));
        Assert.True(File.Exists(freshOther), "non-log files must never be touched");
    }

    [Fact]
    public void ExactlyRetentionBoundary_IsKept_And_MissingDirectoryIsNoop()
    {
        var directory = NewDirectory();
        var now = DateTimeOffset.UtcNow;
        var boundaryLog = Touch(directory, "Log_boundary.log", now - TimeSpan.FromDays(7));

        Assert.Equal(0, LogCleanup.DeleteOldLogs(directory, TimeSpan.FromDays(7), now));
        Assert.True(File.Exists(boundaryLog));
        Assert.Equal(0, LogCleanup.DeleteOldLogs(
            System.IO.Path.Combine(directory, "missing"), TimeSpan.FromDays(7), now));
    }

    [Fact]
    public void LockedFile_IsSkippedWithoutBreakingTheSweep()
    {
        var directory = NewDirectory();
        var now = DateTimeOffset.UtcNow;
        Touch(directory, "Log_old.log", now - TimeSpan.FromDays(9));
        using var locked = File.Open(
            System.IO.Path.Combine(directory, "Log_locked.log"), FileMode.Create,
            FileAccess.ReadWrite, FileShare.None);
        File.SetLastWriteTimeUtc(locked.Name, (now - TimeSpan.FromDays(9)).UtcDateTime);

        var removed = LogCleanup.DeleteOldLogs(directory, TimeSpan.FromDays(7), now);

        Assert.Equal(1, removed);
        Assert.True(File.Exists(locked.Name), "locked file stays for its owner to release");
    }
}
