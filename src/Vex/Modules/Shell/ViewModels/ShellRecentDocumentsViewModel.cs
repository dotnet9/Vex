using System.Collections.ObjectModel;
using ReactiveUI;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellRecentDocumentsViewModel : ReactiveObject
{
    private const int MaxRecentDocuments = 5;
    private static readonly string[] RecentDocumentPropertyNames =
    [
        nameof(HasRecentDocuments),
        nameof(HasRecentDocument1),
        nameof(HasRecentDocument2),
        nameof(HasRecentDocument3),
        nameof(HasRecentDocument4),
        nameof(HasRecentDocument5),
        nameof(RecentDocument1Text),
        nameof(RecentDocument2Text),
        nameof(RecentDocument3Text),
        nameof(RecentDocument4Text),
        nameof(RecentDocument5Text),
        nameof(RecentDocument1ToolTip),
        nameof(RecentDocument2ToolTip),
        nameof(RecentDocument3ToolTip),
        nameof(RecentDocument4ToolTip),
        nameof(RecentDocument5ToolTip)
    ];

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

    public string RecentDocument1ToolTip => GetRecentDocumentToolTip(0);

    public string RecentDocument2ToolTip => GetRecentDocumentToolTip(1);

    public string RecentDocument3ToolTip => GetRecentDocumentToolTip(2);

    public string RecentDocument4ToolTip => GetRecentDocumentToolTip(3);

    public string RecentDocument5ToolTip => GetRecentDocumentToolTip(4);

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
        foreach (var propertyName in RecentDocumentPropertyNames)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private string GetRecentDocumentText(int index)
    {
        return index >= 0 && index < RecentDocuments.Count
            ? RecentDocuments[index].DisplayText
            : _localizer.Get(VexL.RecentNoFiles);
    }

    private string GetRecentDocumentToolTip(int index)
    {
        return index >= 0 && index < RecentDocuments.Count
            ? RecentDocuments[index].Path
            : string.Empty;
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
