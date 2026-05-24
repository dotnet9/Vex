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
    private bool _isSourceMode;
    private bool _isApplyingSourceMode;
    private bool _sidebarBeforeSourceMode = true;
    private bool _previewBeforeSourceMode = true;

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
        _isAlwaysOnTop = settings.IsAlwaysOnTop ?? false;
        _sidebarBeforeSourceMode = _isSidebarVisible;
        _previewBeforeSourceMode = _isPreviewVisible;
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

    public GridLength PreviewColumnWidth => IsPreviewVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength PreviewSplitterWidth => IsPreviewVisible ? new GridLength(6) : new GridLength(0);

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
        set => SetProperty(ref _isSourceMode, value);
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
        if (!IsSourceMode)
        {
            // 源码模式会临时隐藏侧栏和预览，退出时必须恢复用户原先的布局选择。
            _sidebarBeforeSourceMode = IsSidebarVisible;
            _previewBeforeSourceMode = IsPreviewVisible;
            _isApplyingSourceMode = true;
            IsSidebarVisible = false;
            IsPreviewVisible = false;
            _isApplyingSourceMode = false;
            IsSourceMode = true;
            _statusPublisher.PublishResource(VexL.StatusSourceModeEnabled);
            FocusEditor();
            return;
        }

        _isApplyingSourceMode = true;
        IsSidebarVisible = _sidebarBeforeSourceMode;
        IsPreviewVisible = _previewBeforeSourceMode;
        _isApplyingSourceMode = false;
        PersistLayoutSettings();
        IsSourceMode = false;
        _statusPublisher.PublishResource(VexL.StatusSourceModeDisabled);
        FocusEditor();
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
        if (_isApplyingSourceMode)
        {
            return;
        }

        _settingsStore.Update(settings => settings with
        {
            IsSidebarVisible = IsSidebarVisible,
            IsStatusBarVisible = IsStatusBarVisible,
            IsPreviewVisible = IsPreviewVisible,
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
