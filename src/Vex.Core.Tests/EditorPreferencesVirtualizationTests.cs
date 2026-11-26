using Vex.Core.Models;
using Vex.Core.Services;

using Xunit;

namespace Vex.Core.Tests;

/// <summary>
/// 阅读视图虚拟化开关的持久化语义：非空、默认开启，关闭经 IAppSettingsStore.Update 落盘
/// 并广播属性变更（内存桩验证，不碰真实用户配置）。
/// </summary>
public sealed class EditorPreferencesVirtualizationTests
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

    [Fact]
    public void ReadingVirtualization_DefaultsToOn()
    {
        Assert.True(new AppSettings().ReadingVirtualization);
        Assert.True(new EditorPreferences(new MemoryStore()).ReadingVirtualization);
    }

    [Fact]
    public void ReadingVirtualization_Write_PersistsAndNotifies()
    {
        var store = new MemoryStore();
        var preferences = new EditorPreferences(store);
        var notifications = new List<string?>();
        preferences.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        preferences.ReadingVirtualization = false;
        Assert.False(preferences.ReadingVirtualization);
        Assert.False(store.Current.ReadingVirtualization);
        Assert.Contains(nameof(EditorPreferences.ReadingVirtualization), notifications);

        // 同值不重复落盘。
        preferences.ReadingVirtualization = false;
        Assert.Equal(1, store.UpdateCalls);

        preferences.ReadingVirtualization = true;
        Assert.True(store.Current.ReadingVirtualization);
    }
}
