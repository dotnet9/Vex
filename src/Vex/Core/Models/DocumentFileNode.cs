using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Vex.Core.Models;

public sealed class DocumentFileNode : INotifyPropertyChanged
{
    public enum DirectoryLoadState { NotLoaded, Loading, Loaded, Failed }

    private DocumentFile? _documentFile;
    private string? _preview;
    private DirectoryLoadState _loadState;
    private bool _isInvalidated;

    public DocumentFileNode(
        string name,
        string path,
        bool isFolder,
        IReadOnlyList<DocumentFileNode>? children = null,
        DocumentFile? documentFile = null)
    {
        Name = name;
        Path = path;
        IsFolder = isFolder;
        Children = new ObservableCollection<DocumentFileNode>(children ?? []);
        UpdateDocumentFile(documentFile);
        _loadState = children is not null || !isFolder ? DirectoryLoadState.Loaded : DirectoryLoadState.NotLoaded;
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsFolder { get; }

    public bool IsDocument => DocumentFile is not null;

    public DocumentFile? DocumentFile => _documentFile;

    public ObservableCollection<DocumentFileNode> Children { get; }

    public DocumentFileNode? Parent { get; internal set; }

    public bool IsPlaceholder { get; private init; }

    public bool IsLoadingPlaceholder => IsPlaceholder && !IsErrorPlaceholder;

    public bool IsErrorPlaceholder { get; private init; }

    public DirectoryLoadState LoadState
    {
        get => _loadState;
        internal set { if (_loadState == value) return; _loadState = value; Notify(nameof(LoadState)); }
    }

    public bool IsInvalidated
    {
        get => _isInvalidated;
        internal set { if (_isInvalidated == value) return; _isInvalidated = value; Notify(nameof(IsInvalidated)); }
    }

    public string ModifiedText => DocumentFile?.ModifiedText ?? string.Empty;

    public string Preview => _preview ?? DocumentFile?.Preview ?? string.Empty;

    public string PreviewDisplay => string.IsNullOrEmpty(Preview) ? Path : Preview;

    internal void SetPreview(string? preview)
    {
        if (_preview == preview) return;
        _preview = preview;
        Notify(nameof(Preview));
        Notify(nameof(PreviewDisplay));
    }

    internal void UpdateDocumentFile(DocumentFile? file)
    {
        if (ReferenceEquals(_documentFile, file)) return;
        if (_documentFile is not null) _documentFile.PropertyChanged -= OnDocumentChanged;
        _documentFile = file;
        if (file is not null) file.PropertyChanged += OnDocumentChanged;
        Notify(nameof(DocumentFile));
        Notify(nameof(IsDocument));
        Notify(nameof(ModifiedText));
    }

    internal void DetachDocument()
    {
        if (_documentFile is not null) _documentFile.PropertyChanged -= OnDocumentChanged;
    }

    internal static DocumentFileNode CreatePlaceholder(bool failed = false) =>
        new(string.Empty, string.Empty, false) { IsPlaceholder = true, IsErrorPlaceholder = failed };

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs args) => Notify(args.PropertyName);

    private void Notify(string? name) => PropertyChanged?.Invoke(this, new(name));

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            Notify(nameof(IsExpanded));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
