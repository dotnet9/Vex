using System.Runtime.CompilerServices;
using Avalonia.Controls;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

// 管理 Shell 窗口布局状态，避免主 ViewModel 继续混入纯窗口显示逻辑。
public sealed class ShellWindowLayoutViewModel : ReactiveObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IShellStatusPublisher _statusPublisher;
    private bool _isSidebarVisible = true;
    private bool _isStatusBarVisible = true;
    private bool _isPreviewVisible = true;
    private bool _isAlwaysOnTop;
    private bool _isFullScreen;
    private bool _isSourceMode = true;

    public ShellWindowLayoutViewModel(
        IAppSettingsStore settingsStore,
        IShellStatusPublisher statusPublisher)
    {
        _settingsStore = settingsStore;
        _statusPublisher = statusPublisher;
        var settings = _settingsStore.Current;
        _isSidebarVisible = settings.IsSidebarVisible ?? true;
        _isStatusBarVisible = settings.IsStatusBarVisible ?? true;
        _isPreviewVisible = settings.IsPreviewVisible ?? true;
        _isSourceMode = settings.IsSourceMode ?? true;
        _isAlwaysOnTop = settings.IsAlwaysOnTop ?? false;
    }

    public bool IsSidebarVisible
    {
        get => _isSidebarVisible;
        set
        {
            if (SetProperty(ref _isSidebarVisible, value))
            {
                OnPropertyChanged(nameof(SidebarColumnWidth));
                OnPropertyChanged(nameof(SidebarSplitterWidth));
                PersistLayoutSettings();
            }
        }
    }

    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set
        {
            if (SetProperty(ref _isStatusBarVisible, value))
            {
                PersistLayoutSettings();
            }
        }
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (SetProperty(ref _isPreviewVisible, value))
            {
                OnPropertyChanged(nameof(PreviewColumnWidth));
                OnPropertyChanged(nameof(PreviewSplitterWidth));
                PersistLayoutSettings();
            }
        }
    }

    public GridLength SidebarColumnWidth => IsSidebarVisible ? new GridLength(320) : new GridLength(0);

    public GridLength SidebarSplitterWidth => IsSidebarVisible ? new GridLength(6) : new GridLength(0);

    public GridLength SourceColumnWidth => IsSourceMode ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength PreviewColumnWidth => IsPreviewVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength PreviewSplitterWidth => IsSourceMode && IsPreviewVisible ? new GridLength(6) : new GridLength(0);

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (SetProperty(ref _isAlwaysOnTop, value))
            {
                PersistLayoutSettings();
            }
        }
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (SetProperty(ref _isFullScreen, value))
            {
                OnPropertyChanged(nameof(CurrentWindowState));
            }
        }
    }

    public WindowState CurrentWindowState => IsFullScreen ? WindowState.FullScreen : WindowState.Normal;

    public bool IsSourceMode
    {
        get => _isSourceMode;
        set
        {
            if (SetProperty(ref _isSourceMode, value))
            {
                OnPropertyChanged(nameof(SourceColumnWidth));
                OnPropertyChanged(nameof(PreviewSplitterWidth));
                PersistLayoutSettings();
            }
        }
    }

    public void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    public void ShowOutline()
    {
        IsSidebarVisible = true;
        SelectSidebarTab(1);
    }

    public void ShowFiles()
    {
        IsSidebarVisible = true;
        SelectSidebarTab(0);
    }

    public void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;
    }

    public void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    public void ToggleSourceMode()
    {
        // 源代码模式现在只控制源码编辑面板，不再联动侧边栏或预览面板。
        IsSourceMode = !IsSourceMode;
        _statusPublisher.PublishResource(IsSourceMode ? VexL.StatusSourceModeEnabled : VexL.StatusSourceModeDisabled);
        if (IsSourceMode)
        {
            FocusEditor();
        }
    }

    public void ToggleAlwaysOnTop()
    {
        IsAlwaysOnTop = !IsAlwaysOnTop;
    }

    public void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
    }

    private void FocusEditor()
    {
        CodeWF.EventBus.EventBus.Default.Publish(new EditorActionCommand(EditorActionKind.FocusEditor));
    }

    private void SelectSidebarTab(int selectedIndex)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new ShellSidebarTabSelectedCommand(selectedIndex));
    }

    private void PersistLayoutSettings()
    {
        _settingsStore.Update(settings => settings with
        {
            IsSidebarVisible = IsSidebarVisible,
            IsStatusBarVisible = IsStatusBarVisible,
            IsPreviewVisible = IsPreviewVisible,
            IsSourceMode = IsSourceMode,
            IsAlwaysOnTop = IsAlwaysOnTop
        });
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

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
