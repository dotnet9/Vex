using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellNavigationViewModel : ReactiveObject
{
    private readonly IEventBus _eventBus;
    private DocumentFile? _selectedDocumentFile;
    private OutlineItem? _selectedOutlineItem;

    public ShellNavigationViewModel(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public ObservableCollection<DocumentFile> DocumentFiles { get; } = [];

    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];

    public int SelectedSideTabIndex
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool HasDocumentFiles => DocumentFiles.Count > 0;

    public bool IsDocumentFilesEmpty => !HasDocumentFiles;

    public bool HasOutlineItems => OutlineItems.Count > 0;

    public bool IsOutlineEmpty => !HasOutlineItems;

    public DocumentFile? SelectedDocumentFile
    {
        get => _selectedDocumentFile;
        set
        {
            var previousSelection = _selectedDocumentFile;
            if (SetProperty(ref _selectedDocumentFile, value) && value is not null)
            {
                _eventBus.Publish(new DocumentFileOpenRequestedCommand(value, previousSelection));
            }
        }
    }

    public OutlineItem? SelectedOutlineItem
    {
        get => _selectedOutlineItem;
        set
        {
            if (SetProperty(ref _selectedOutlineItem, value) && value is not null)
            {
                SelectedSideTabIndex = 1;
                _eventBus.Publish(new NavigateToLineCommand(value.Line));
                SetStatus($"Navigated to {value.Title}.");
            }
        }
    }

    public void SetDocumentFiles(IReadOnlyList<DocumentFile> files)
    {
        DocumentFiles.Clear();
        foreach (var file in files)
        {
            DocumentFiles.Add(file);
        }

        NotifyDocumentFilesChanged();
    }

    public void ClearDocumentFiles()
    {
        DocumentFiles.Clear();
        SelectDocumentFileSilently(null);
        SelectedOutlineItem = null;
        NotifyDocumentFilesChanged();
    }

    public void SelectDocumentFileSilently(DocumentFile? documentFile)
    {
        SetProperty(ref _selectedDocumentFile, documentFile, nameof(SelectedDocumentFile));
    }

    public void RestoreSelectedDocumentFile(DocumentFile? documentFile)
    {
        SelectDocumentFileSilently(documentFile);
    }

    public void SetOutlineItems(IEnumerable<OutlineItem> items)
    {
        OutlineItems.Clear();
        foreach (var item in items)
        {
            OutlineItems.Add(item);
        }

        NotifyOutlineChanged();
    }

    private void NotifyDocumentFilesChanged()
    {
        OnPropertyChanged(nameof(HasDocumentFiles));
        OnPropertyChanged(nameof(IsDocumentFilesEmpty));
    }

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutlineItems));
        OnPropertyChanged(nameof(IsOutlineEmpty));
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
