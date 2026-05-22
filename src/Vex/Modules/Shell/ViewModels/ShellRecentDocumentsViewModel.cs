using System.Collections.ObjectModel;
using ReactiveUI;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellRecentDocumentsViewModel : ReactiveObject
{
    private const int MaxRecentDocuments = 5;
    private readonly IAppLocalizer _localizer;
    private readonly IRecentDocumentStore _recentDocumentStore;
    private readonly IShellStatusPublisher _statusPublisher;

    public ShellRecentDocumentsViewModel(
        IAppLocalizer localizer,
        IRecentDocumentStore recentDocumentStore,
        IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
        _recentDocumentStore = recentDocumentStore;
        _statusPublisher = statusPublisher;
        LoadRecentDocuments();
    }

    public ObservableCollection<RecentDocument> RecentDocuments { get; } = [];

    public bool HasRecentDocuments => RecentDocuments.Count > 0;

    public bool HasRecentDocument1 => RecentDocuments.Count > 0;

    public bool HasRecentDocument2 => RecentDocuments.Count > 1;

    public bool HasRecentDocument3 => RecentDocuments.Count > 2;

    public bool HasRecentDocument4 => RecentDocuments.Count > 3;

    public bool HasRecentDocument5 => RecentDocuments.Count > 4;

    public string RecentDocument1Text => GetRecentDocumentText(0);

    public string RecentDocument2Text => GetRecentDocumentText(1);

    public string RecentDocument3Text => GetRecentDocumentText(2);

    public string RecentDocument4Text => GetRecentDocumentText(3);

    public string RecentDocument5Text => GetRecentDocumentText(4);

    public bool TryGetDocument(int index, out RecentDocument? document)
    {
        if (index < 0 || index >= RecentDocuments.Count)
        {
            document = null;
            return false;
        }

        document = RecentDocuments[index];
        return true;
    }

    public void ClearRecentDocuments()
    {
        RecentDocuments.Clear();
        SaveRecentDocuments();
        NotifyRecentDocumentsChanged();
        _statusPublisher.PublishResource(VexL.StatusRecentFilesCleared);
    }

    public void AddRecentDocument(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentDocuments.FirstOrDefault(item => item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentDocuments.Remove(existing);
        }

        RecentDocuments.Insert(0, new RecentDocument(fullPath));
        while (RecentDocuments.Count > MaxRecentDocuments)
        {
            RecentDocuments.RemoveAt(RecentDocuments.Count - 1);
        }

        SaveRecentDocuments();
        NotifyRecentDocumentsChanged();
    }

    public void RemoveRecentDocument(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var existing = RecentDocuments.FirstOrDefault(item => item.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            return;
        }

        RecentDocuments.Remove(existing);
        SaveRecentDocuments();
        NotifyRecentDocumentsChanged();
    }

    private void LoadRecentDocuments()
    {
        foreach (var document in _recentDocumentStore.Load(MaxRecentDocuments))
        {
            RecentDocuments.Add(document);
        }

        NotifyRecentDocumentsChanged();
    }

    private void SaveRecentDocuments()
    {
        _recentDocumentStore.Save(RecentDocuments);
    }

    private void NotifyRecentDocumentsChanged()
    {
        OnPropertyChanged(nameof(HasRecentDocuments));
        OnPropertyChanged(nameof(HasRecentDocument1));
        OnPropertyChanged(nameof(HasRecentDocument2));
        OnPropertyChanged(nameof(HasRecentDocument3));
        OnPropertyChanged(nameof(HasRecentDocument4));
        OnPropertyChanged(nameof(HasRecentDocument5));
        OnPropertyChanged(nameof(RecentDocument1Text));
        OnPropertyChanged(nameof(RecentDocument2Text));
        OnPropertyChanged(nameof(RecentDocument3Text));
        OnPropertyChanged(nameof(RecentDocument4Text));
        OnPropertyChanged(nameof(RecentDocument5Text));
    }

    private string GetRecentDocumentText(int index)
    {
        return index >= 0 && index < RecentDocuments.Count
            ? RecentDocuments[index].DisplayText
            : _localizer.Get(VexL.RecentNoFiles);
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
