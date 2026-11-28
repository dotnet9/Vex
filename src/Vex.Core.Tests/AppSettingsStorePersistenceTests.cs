using Vex.Core.Models;
using Vex.Core.Services;

using Xunit;

namespace Vex.Core.Tests;

/// <summary>
/// 设置持久化迁移语义：配置存于用户数据目录（%LOCALAPPDATA%/Vex/settings.json），
/// 原子写、固定文化小数点、损坏文件回落全默认值。测试经 ConfigFilePathOverride
/// 重定向到临时目录，不碰真实用户配置；不做旧 Vex.dll.config 迁移（产品决定）。
/// </summary>
public sealed class AppSettingsStorePersistenceTests
{
    private sealed class Redirected : IDisposable
    {
        public string Directory { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "vex-settings-tests", Guid.NewGuid().ToString("N"));
        public string FilePath => System.IO.Path.Combine(Directory, "settings.json");

        public Redirected() => AppSettingsStore.ConfigFilePathOverride = FilePath;
        public void Dispose() => AppSettingsStore.ConfigFilePathOverride = string.Empty;
    }

    [Fact]
    public void MissingFile_FallsBackToDefaults()
    {
        using var redirect = new Redirected();
        var store = new AppSettingsStore();
        Assert.False(File.Exists(redirect.FilePath), "load must not create the file");
    }

    [Fact]
    public void Update_WritesUserDirectoryJson_AndRoundTrips()
    {
        using var redirect = new Redirected();
        var store = new AppSettingsStore();
        store.Update(settings => settings with
        {
            SidebarWidth = 271.5,
            ContentZoom = 130,
            BodyFont = "serif",
        });

        Assert.True(File.Exists(redirect.FilePath));
        var payload = File.ReadAllText(redirect.FilePath);
        // 已退役的虚拟化偏好键不再写回（旧配置文件中的该键读取时被静默忽略）。
        Assert.DoesNotContain("\"ReadingVirtualization\"", payload);
        Assert.DoesNotContain("271,5", payload, StringComparison.Ordinal);

        // 新实例从用户目录文件恢复（默认路径未动：真实用户配置不受测试影响）。
        var reopened = new AppSettingsStore();
        Assert.Equal(271.5, reopened.Current.SidebarWidth);
        Assert.Equal(130, reopened.Current.ContentZoom);
        Assert.Equal("serif", reopened.Current.BodyFont);
    }

    [Fact]
    public void CorruptedFile_FallsBackToDefaults_InsteadOfThrowing()
    {
        using var redirect = new Redirected();
        Directory.CreateDirectory(redirect.Directory);
        File.WriteAllText(redirect.FilePath, "{ not valid json !!");

        var store = new AppSettingsStore();
        Assert.NotNull(store.Current);
    }

    [Fact]
    public void CorruptedFile_IsReplacedByNextSave()
    {
        using var redirect = new Redirected();
        Directory.CreateDirectory(redirect.Directory);
        File.WriteAllText(redirect.FilePath, "{ not valid json !!");

        var store = new AppSettingsStore();
        store.Update(settings => settings with { RecoveryDrafts = false });

        Assert.False(new AppSettingsStore().Current.RecoveryDrafts);
    }
}
