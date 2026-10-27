using System.Runtime.CompilerServices;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

// 管理编辑器显示偏好，Workspace 视图只绑定这些状态，不直接保存用户显示选择。
public sealed class ShellEditorDisplayViewModel : ReactiveObject
{
    private readonly IEditorDisplayState _editorDisplayState;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IShellStatusPublisher _statusPublisher;
    private readonly EditorPreferences _preferences;
    private bool _showLineNumbers;
    private bool _enableAutoPair = true;

    public ShellEditorDisplayViewModel(
        IEditorDisplayState editorDisplayState,
        IAppSettingsStore settingsStore,
        EditorPreferences preferences,
        IShellStatusPublisher statusPublisher)
    {
        _editorDisplayState = editorDisplayState;
        _settingsStore = settingsStore;
        _preferences = preferences;
        preferences.PreferencesChanged += (_, _) => { this.RaisePropertyChanged(nameof(EditorFontSize)); PublishDisplayState(); };
        _statusPublisher = statusPublisher;
        var settings = _settingsStore.Current;
        _showLineNumbers = settings.ShowLineNumbers ?? true;
        _enableAutoPair = settings.EnableAutoPair ?? true;
        PublishDisplayState();
    }

    // 原型 --vex-content-zoom 只缩放渲染正文，源码编辑区不随内容缩放变化。
    public double EditorFontSize => _preferences.CodeFontSize;

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            if (SetProperty(ref _showLineNumbers, value))
            {
                PublishDisplayState();
                PersistDisplaySettings();
            }
        }
    }

    public bool EnableAutoPair
    {
        get => _enableAutoPair;
        set
        {
            if (SetProperty(ref _enableAutoPair, value))
            {
                PublishDisplayState();
                PersistDisplaySettings();
            }
        }
    }

    public void ToggleAutoPair()
    {
        EnableAutoPair = !EnableAutoPair;
        _statusPublisher.PublishResource(EnableAutoPair ? VexL.StatusAutoPairEnabled : VexL.StatusAutoPairDisabled);
    }

    public void ToggleLineNumbers()
    {
        ShowLineNumbers = !ShowLineNumbers;
        _statusPublisher.PublishResource(ShowLineNumbers ? VexL.StatusLineNumbersShown : VexL.StatusLineNumbersHidden);
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

    private void PublishDisplayState()
    {
        _editorDisplayState.Update(EditorFontSize, ShowLineNumbers, EnableAutoPair);
    }

    private void PersistDisplaySettings()
    {
        _settingsStore.Update(settings => settings with
        {
            ShowLineNumbers = ShowLineNumbers,
            EnableAutoPair = EnableAutoPair
        });
    }
}
