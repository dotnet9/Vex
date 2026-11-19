using Vex.Core.Models;
using Vex.Core.Services;

using Xunit;

namespace Vex.Core.Tests;

/// <summary>
/// 缩放类偏好（阅读 ContentZoom / 导图 MapZoom / 演示 PresentationScale）的持久化语义：
/// 写入经 Clamp 收敛后通过 IAppSettingsStore.Update 落盘（内存桩验证，不碰真实用户配置）。
/// 配置不一致（类型/键不匹配）由 AppSettingsStore.Get 回落 default 覆盖——无需兼容迁移。
/// </summary>
public sealed class EditorPreferencesZoomTests
{
    private sealed class MemoryStore : IAppSettingsStore
    {
        public AppSettings Current { get; private set; } = new();
        public int UpdateCalls { get; private set; }
        public AppSettings Update(Func<AppSettings, AppSettings> update)
        {
            UpdateCalls++;
            Current = update(Current);
            return Current;
        }
    }

    private static (EditorPreferences Preferences, MemoryStore Store) Create()
    {
        var store = new MemoryStore();
        return (new EditorPreferences(store), store);
    }

    [Theory]
    [InlineData(150, 150)]
    [InlineData(50, 50)]
    [InlineData(300, 200)]
    [InlineData(20, 50)]
    public void ContentZoom_Write_ClampsToRange(double written, double expected)
    {
        var (preferences, store) = Create();
        preferences.ContentZoom = written;
        Assert.Equal(expected, preferences.ContentZoom);
        Assert.Equal(expected, store.Current.ContentZoom);
    }

    [Theory]
    [InlineData(150, 150)]
    [InlineData(500, 200)]
    [InlineData(-5, 1)]
    public void MapZoom_Write_ClampsToRange(double written, double expected)
    {
        var (preferences, store) = Create();
        preferences.MapZoom = written;
        Assert.Equal(expected, preferences.MapZoom);
        Assert.Equal(expected, store.Current.MapZoom);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(4, 2.5)]
    [InlineData(0.5, 1)]
    public void PresentationScale_Write_ClampsToRange(double written, double expected)
    {
        var (preferences, store) = Create();
        preferences.PresentationScale = written;
        Assert.Equal(expected, preferences.PresentationScale);
        Assert.Equal(expected, store.Current.PresentationScale);
    }

    [Fact]
    public void ZoomWrites_PersistThroughStoreUpdate()
    {
        // 持久化语义：每次写都触发 Store.Update 落盘（读-改-写合并），跨会话可恢复。
        var store = new MemoryStore();
        var preferences = new EditorPreferences(store);
        preferences.ContentZoom = 150;
        preferences.MapZoom = 120;
        preferences.PresentationScale = 1.8;
        Assert.Equal(150, store.Current.ContentZoom);
        Assert.Equal(120, store.Current.MapZoom);
        Assert.Equal(1.8, store.Current.PresentationScale);
        Assert.True(store.UpdateCalls >= 3, "each zoom write must persist through the store");
    }
}
