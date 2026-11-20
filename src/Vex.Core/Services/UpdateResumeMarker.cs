using System.Globalization;

namespace Vex.Core.Services;

/// <summary>
/// 升级续接标记：更新器把安装包交接给 Inno Setup 且应用即将退出时写入；新版本首次
/// 无参启动读到新鲜标记（24 小时内）后恢复最近打开的文档并清除标记。取消安装或
/// 超时未启动时标记自然失效，普通启动（无标记）保持原有的空白文档行为。
/// </summary>
public sealed class UpdateResumeMarker
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private readonly string _directory;

    public UpdateResumeMarker(string? directory = null)
    {
        _directory = directory ?? DefaultDirectory();
    }

    /// <summary>安装交接成功即将退出时写入；内容只有时间戳，恢复对象由最近文档列表决定。</summary>
    public void Write(DateTimeOffset? now = null)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                MarkerPath,
                (now ?? DateTimeOffset.Now).UtcTicks.ToString(CultureInfo.InvariantCulture),
                System.Text.Encoding.ASCII);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // 标记写不进去只影响「升级后恢复」这一锦上添花行为，静默跳过。
        }
    }

    /// <summary>标记存在且新鲜则返回 true 并立即清除；过期/损坏的标记同样清除并返回 false。</summary>
    public bool TryConsume(DateTimeOffset? now = null)
    {
        try
        {
            if (!File.Exists(MarkerPath))
            {
                return false;
            }

            var text = File.ReadAllText(MarkerPath, System.Text.Encoding.ASCII).Trim();
            File.Delete(MarkerPath);
            return long.TryParse(text, CultureInfo.InvariantCulture, out var utcTicks)
                && new DateTimeOffset(utcTicks, TimeSpan.Zero) >= (now ?? DateTimeOffset.Now).Add(-Lifetime);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private string MarkerPath => Path.Combine(_directory, "update-resume.txt");

    private static string DefaultDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodeWF", "Vex");
}
