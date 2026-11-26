namespace Vex.Core.Services;

/// <summary>
/// 日志目录定时清理：只保留最近 N 天（默认 7）。按文件最后写入时间判定，
/// 删除失败（文件被占用/权限）忽略并跳过，不影响其余文件与应用运行。
/// </summary>
public static class LogCleanup
{
    public static readonly TimeSpan DefaultRetention = TimeSpan.FromDays(7);

    /// <summary>删除目录下超过保留期的 *.log 文件，返回删除数量。目录不存在返回 0。</summary>
    public static int DeleteOldLogs(string directory, TimeSpan retention, DateTimeOffset now)
    {
        if (!Directory.Exists(directory)) return 0;
        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*.log"))
        {
            try
            {
                var lastWrite = new DateTimeOffset(File.GetLastWriteTimeUtc(file), DateTimeOffset.UtcNow.Offset);
                if (now - lastWrite > retention)
                {
                    File.Delete(file);
                    removed++;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // 单个文件删除失败不终止清理（当前会话日志正被占用属常态）。
            }
        }

        return removed;
    }
}
