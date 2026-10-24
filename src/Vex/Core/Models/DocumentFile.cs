using System.ComponentModel;

namespace Vex.Core.Models;

public sealed class DocumentFile : INotifyPropertyChanged
{
    private string _modifiedText;
    private string _preview;
    public DocumentFile(string path, string name, string folderName, string modifiedText, string preview)
    {
        Path = path;
        Name = name;
        FolderName = folderName;
        _modifiedText = modifiedText;
        _preview = preview;
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

    public string Preview
    {
        get => _preview;
        set
        {
            if (_preview == value) return;
            _preview = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Preview)));
        }
    }
}
