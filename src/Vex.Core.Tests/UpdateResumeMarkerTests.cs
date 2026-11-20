using Vex.Core.Services;
using Xunit;

namespace Vex.Core.Tests;

/// <summary>升级续接标记的写读清往返与时效语义。</summary>
public sealed class UpdateResumeMarkerTests
{
    [Fact]
    public void WrittenMarkerConsumesOnceWithinLifetime()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vex-marker-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var marker = new UpdateResumeMarker(directory);
            var now = DateTimeOffset.Now;
            marker.Write(now);

            Assert.True(File.Exists(Path.Combine(directory, "update-resume.txt")));
            Assert.True(marker.TryConsume(now));
            Assert.False(marker.TryConsume(now)); // 第二次消费即无：标记读取即清。
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void StaleMarkerIsConsumedAsFalseAndRemoved()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vex-marker-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var marker = new UpdateResumeMarker(directory);
            marker.Write(DateTimeOffset.Now.AddHours(-25));

            Assert.False(marker.TryConsume(DateTimeOffset.Now));
            Assert.False(File.Exists(Path.Combine(directory, "update-resume.txt")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingMarkerConsumesAsFalse()
    {
        var marker = new UpdateResumeMarker(Path.Combine(Path.GetTempPath(), "vex-marker-tests", Guid.NewGuid().ToString("N")));
        Assert.False(marker.TryConsume());
    }
}
