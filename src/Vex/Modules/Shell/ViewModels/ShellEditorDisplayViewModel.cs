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
    private bool _showLineNumbers;

    public ShellEditorDisplayViewModel(
        IEditorDisplayState editorDisplayState,
        IAppSettingsStore settingsStore,
        IShellStatusPublisher statusPublisher)
    {
        _editorDisplayState = editorDisplayState;
        _settingsStore = settingsStore;
        _statusPublisher = statusPublisher;
        var settings = _settingsStore.Current;
        _showLineNumbers = settings.ShowLineNumbers ?? true;
        PublishDisplayState();
    }

    public double EditorFontSize => 15d;

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
        _editorDisplayState.Update(EditorFontSize, ShowLineNumbers);
    }

    private void PersistDisplaySettings()
    {
        _settingsStore.Update(settings => settings with
        {
            ShowLineNumbers = ShowLineNumbers
        });
    }
}
