using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellFindBarViewModel : ReactiveObject
{
    private readonly IEventBus _eventBus;
    private readonly IShellStatusPublisher _statusPublisher;
    private bool _isVisible;
    private bool _isReplaceVisible;
    private string _searchText = string.Empty;
    private string _replacementText = string.Empty;
    private string _searchResultText = "0/0";

    public ShellFindBarViewModel(IEventBus eventBus, IShellStatusPublisher statusPublisher)
    {
        _eventBus = eventBus;
        _statusPublisher = statusPublisher;
        _eventBus.Subscribe(this);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsReplaceVisible
    {
        get => _isReplaceVisible;
        set => SetProperty(ref _isReplaceVisible, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                RefreshSearchResultCount();
            }
        }
    }

    public string ReplacementText
    {
        get => _replacementText;
        set => SetProperty(ref _replacementText, value ?? string.Empty);
    }

    public string SearchResultText
    {
        get => _searchResultText;
        set => SetProperty(ref _searchResultText, value);
    }

    public void ShowFindPanel()
    {
        IsVisible = true;
        IsReplaceVisible = false;
        RefreshSearchResultCount();
        SetStatusResource(VexL.StatusFindReady);
    }

    public void ShowReplacePanel()
    {
        IsVisible = true;
        IsReplaceVisible = true;
        RefreshSearchResultCount();
        SetStatusResource(VexL.StatusReplaceReady);
    }

    public void CloseFindPanel()
    {
        IsVisible = false;
        SetStatusResource(VexL.StatusFindClosed);
        PublishEditorAction(EditorActionKind.FocusEditor);
    }

    public void FindNext()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatusResource(VexL.StatusEnterSearchTextFirst);
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.FindNext, SearchText));
    }

    public void ReplaceNext()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatusResource(VexL.StatusEnterSearchTextFirst);
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.ReplaceNext, SearchText, ReplacementText));
    }

    public void ReplaceAll()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatusResource(VexL.StatusEnterSearchTextFirst);
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.ReplaceAll, SearchText, ReplacementText));
    }

    [EventHandler]
    public void ApplyEditorSearchResult(EditorSearchResultCommand command)
    {
        SearchResultText = command.TotalCount > 0
            ? $"{Math.Max(1, command.CurrentIndex)}/{command.TotalCount}"
            : "0/0";
        SetStatus(command.Message);
    }

    private void RefreshSearchResultCount()
    {
        if (!IsVisible)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SearchResultText = "0/0";
            return;
        }

        _eventBus.Publish(new EditorSearchCommand(EditorSearchAction.Count, SearchText));
    }

    private void PublishEditorAction(EditorActionKind action)
    {
        _eventBus.Publish(new EditorActionCommand(action));
    }

    private void SetStatus(string message)
    {
        _statusPublisher.Publish(message);
    }

    private void SetStatusResource(string key)
    {
        _statusPublisher.PublishResource(key);
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
}
