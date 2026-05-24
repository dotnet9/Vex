using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellNavigationViewModel : ReactiveObject
{
    private readonly IAppSettingsStore _settingsStore;

    public ShellNavigationViewModel(IAppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        SelectedSideTabIndex = Math.Clamp(_settingsStore.Current.SelectedSidebarTabIndex ?? 0, 0, 1);
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    public int SelectedSideTabIndex
    {
        get;
        set
        {
            var selectedIndex = Math.Clamp(value, 0, 1);
            if (SetProperty(ref field, selectedIndex))
            {
                _settingsStore.Update(settings => settings with
                {
                    SelectedSidebarTabIndex = selectedIndex
                });
            }
        }
    }

    [EventHandler]
    public void ApplyShellSidebarTabSelected(ShellSidebarTabSelectedCommand command)
    {
        // 侧边栏页签选择统一走事件总线，避免布局菜单直接引用文件/大纲 ViewModel。
        SelectedSideTabIndex = command.SelectedIndex;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }
}
