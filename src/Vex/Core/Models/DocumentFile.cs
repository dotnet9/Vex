using System.ComponentModel;

namespace Vex.Core.Models;

public sealed class DocumentFile : INotifyPropertyChanged
{
    private string _modifiedText;
    public DocumentFile(string path, string name, string folderName, string modifiedText, string preview)
    {
        Path = path;
        Name = name;
        FolderName = folderName;
        _modifiedText = modifiedText;
        Preview = preview;
    }

    public string Name { get; }

    public string Path { get; }

    public string FolderName { get; }

    public string ModifiedText
    {
        get => _modifiedText;
        internal set
        {
            if (_modifiedText == value) return;
            _modifiedText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModifiedText)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Preview { get; }
}
