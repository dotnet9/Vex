using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

// 管理编辑器显示偏好，Workspace 视图只绑定这些状态，不直接保存用户显示选择。
public sealed class ShellEditorDisplayViewModel : ReactiveObject
{
    private readonly IEventBus _eventBus;
    private double _editorZoom = 1.0;
    private bool _showLineNumbers;

    public ShellEditorDisplayViewModel(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public double EditorZoom
    {
        get => _editorZoom;
        set
        {
            if (SetProperty(ref _editorZoom, value))
            {
                OnPropertyChanged(nameof(EditorFontSize));
                OnPropertyChanged(nameof(ZoomText));
            }
        }
    }

    public double EditorFontSize => Math.Round(15 * EditorZoom, 1);

    public string ZoomText => $"{EditorZoom:P0}";

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set => SetProperty(ref _showLineNumbers, value);
    }

    public void ActualSize()
    {
        EditorZoom = 1.0;
    }

    public void ZoomIn()
    {
        EditorZoom = Math.Min(1.8, EditorZoom + 0.1);
    }

    public void ZoomOut()
    {
        EditorZoom = Math.Max(0.7, EditorZoom - 0.1);
    }

    public void ToggleLineNumbers()
    {
        ShowLineNumbers = !ShowLineNumbers;
        SetStatus(ShowLineNumbers ? "Line numbers shown." : "Line numbers hidden.");
    }

    private void SetStatus(string message)
    {
        _eventBus.Publish(new WorkspaceStatusChangedCommand(message));
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
