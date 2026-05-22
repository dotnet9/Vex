using System.Runtime.CompilerServices;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellEditorDisplayViewModel : ReactiveObject
{
    private double _editorZoom = 1.0;

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
