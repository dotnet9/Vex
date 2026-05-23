using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Regions;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellFilesViewModel : ReactiveObject, IRegionTabItem
{
    private readonly IEventBus _eventBus;
    private DocumentFile? _selectedDocumentFile;
    private DocumentFile? _contextDocumentFile;

    public ShellFilesViewModel(IEventBus eventBus)
    {
        _eventBus = eventBus;
        eventBus.Subscribe(this);
    }

    public string? TitleKey { get; } = VexL.SidebarFiles;

    public ObservableCollection<DocumentFile> DocumentFiles { get; } = [];

    public bool HasDocumentFiles => DocumentFiles.Count > 0;

    public bool IsDocumentFilesEmpty => !HasDocumentFiles;

    public DocumentFile? SelectedDocumentFile
    {
        get => _selectedDocumentFile;
        set
        {
            var previousSelection = _selectedDocumentFile;
            if (SetProperty(ref _selectedDocumentFile, value))
            {
                OnPropertyChanged(nameof(HasSelectedDocumentFile));
                OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
                if (value is not null)
                {
                    _eventBus.Publish(new DocumentFileOpenRequestedCommand(value, previousSelection));
                }
            }
        }
    }

    public bool HasSelectedDocumentFile => SelectedDocumentFile is not null;

    public bool HasDocumentFileCommandTarget => GetDocumentFileCommandTarget() is not null;

    public void OpenSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        var previousSelection = SelectedDocumentFile;
        ClearContextDocumentFile();
        _eventBus.Publish(new DocumentFileOpenRequestedCommand(file, previousSelection));
    }

    public void RenameSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        _eventBus.Publish(new DocumentFileRenameRequestedCommand(file));
    }

    public void OpenSelectedFileLocation()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        _eventBus.Publish(new DocumentFileOpenLocationRequestedCommand(file));
    }

    public void DeleteSelectedFile()
    {
        if (GetDocumentFileCommandTarget() is not { } file)
        {
            return;
        }

        ClearContextDocumentFile();
        _eventBus.Publish(new DocumentFileDeleteRequestedCommand(file));
    }

    public void SelectDocumentFileForContextMenu(DocumentFile documentFile)
    {
        _contextDocumentFile = documentFile;
        OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
    }

    [EventHandler]
    public void ApplyDocumentFilesChanged(DocumentFilesChangedCommand command)
    {
        // 文件列表只接收 Shell 发布的快照，不直接调用主 ViewModel 的文件打开流程。
        DocumentFiles.Clear();
        foreach (var file in command.Files)
        {
            DocumentFiles.Add(file);
        }

        SelectDocumentFileSilently(command.SelectedFile);
        NotifyDocumentFilesChanged();
    }

    [EventHandler]
    public void ApplyDocumentFileSelectionChanged(DocumentFileSelectionChangedCommand command)
    {
        // 未保存确认被取消时静默恢复旧选择，避免再次触发打开文件请求。
        SelectDocumentFileSilently(command.SelectedFile);
    }

    private void SelectDocumentFileSilently(DocumentFile? documentFile)
    {
        if (SetProperty(ref _selectedDocumentFile, documentFile, nameof(SelectedDocumentFile)))
        {
            OnPropertyChanged(nameof(HasSelectedDocumentFile));
            OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
        }
    }

    private DocumentFile? GetDocumentFileCommandTarget() => _contextDocumentFile ?? SelectedDocumentFile;

    private void ClearContextDocumentFile()
    {
        if (_contextDocumentFile is null)
        {
            return;
        }

        _contextDocumentFile = null;
        OnPropertyChanged(nameof(HasDocumentFileCommandTarget));
    }

    private void NotifyDocumentFilesChanged()
    {
        OnPropertyChanged(nameof(HasDocumentFiles));
        OnPropertyChanged(nameof(IsDocumentFilesEmpty));
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
