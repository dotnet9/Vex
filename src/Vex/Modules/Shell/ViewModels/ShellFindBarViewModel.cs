using System.Runtime.CompilerServices;
using Avalonia.Threading;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellFindBarViewModel : ReactiveObject
{
    private static readonly TimeSpan SearchCountDebounce = TimeSpan.FromMilliseconds(180);
    private readonly IShellStatusPublisher _statusPublisher;
    private Timer? _searchCountTimer;
    private bool _isVisible;
    private bool _isReplaceVisible;
    private bool _isMatchCase;
    private bool _isWholeWord;
    private bool _isRegex;
    private string _searchText = string.Empty;
    private string _replacementText = string.Empty;
    private string _searchResultText = "0/0";

    public ShellFindBarViewModel(IShellStatusPublisher statusPublisher)
    {
        _statusPublisher = statusPublisher;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
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

    public bool IsMatchCase
    {
        get => _isMatchCase;
        set
        {
            if (SetProperty(ref _isMatchCase, value))
            {
                RefreshSearchResultCount();
            }
        }
    }

    public bool IsWholeWord
    {
        get => _isWholeWord;
        set
        {
            if (SetProperty(ref _isWholeWord, value))
            {
                RefreshSearchResultCount();
            }
        }
    }

    public bool IsRegex
    {
        get => _isRegex;
        set
        {
            if (SetProperty(ref _isRegex, value))
            {
                RefreshSearchResultCount();
            }
        }
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
        SeedSearchTextFromEditorSelection();
        RefreshSearchResultCount(immediate: true);
        SetStatusResource(VexL.StatusFindReady);
    }

    public void ShowReplacePanel()
    {
        IsVisible = true;
        IsReplaceVisible = true;
        SeedSearchTextFromEditorSelection();
        RefreshSearchResultCount(immediate: true);
        SetStatusResource(VexL.StatusReplaceReady);
    }

    public void CloseFindPanel()
    {
        IsVisible = false;
        CancelSearchResultCount();
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

        PublishSearch(EditorSearchAction.FindNext);
    }

    public void ReplaceNext()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatusResource(VexL.StatusEnterSearchTextFirst);
            return;
        }

        PublishSearch(EditorSearchAction.ReplaceNext, ReplacementText);
    }

    public void ReplaceAll()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatusResource(VexL.StatusEnterSearchTextFirst);
            return;
        }

        PublishSearch(EditorSearchAction.ReplaceAll, ReplacementText);
    }

    [EventHandler]
    public void ApplyEditorSearchResult(EditorSearchResultCommand command)
    {
        SearchResultText = command.TotalCount > 0
            ? $"{Math.Max(1, command.CurrentIndex)}/{command.TotalCount}"
            : "0/0";
        SetStatus(command.Message);
    }

    private void RefreshSearchResultCount(bool immediate = false)
    {
        if (!IsVisible)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            CancelSearchResultCount();
            SearchResultText = "0/0";
            return;
        }

        if (immediate)
        {
            CancelSearchResultCount();
            PublishSearch(EditorSearchAction.Count);
            return;
        }

        ScheduleSearchResultCount();
    }

    private void ScheduleSearchResultCount()
    {
        _searchCountTimer ??= new Timer(
            _ => Dispatcher.UIThread.Post(PublishSearchResultCount),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _searchCountTimer.Change(SearchCountDebounce, Timeout.InfiniteTimeSpan);
    }

    private void CancelSearchResultCount()
    {
        _searchCountTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private void PublishSearchResultCount()
    {
        if (IsVisible && !string.IsNullOrWhiteSpace(SearchText))
        {
            PublishSearch(EditorSearchAction.Count);
        }
    }

    private void PublishSearch(EditorSearchAction action, string? replacementText = null)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new EditorSearchCommand(
            action,
            SearchText,
            replacementText,
            IsMatchCase,
            IsWholeWord,
            IsRegex));
    }

    private void SeedSearchTextFromEditorSelection()
    {
        var selectedText = CodeWF.EventBus.EventBus.Default.Query(new EditorSelectedTextQuery());
        if (!string.IsNullOrEmpty(selectedText))
        {
            SearchText = selectedText;
        }
    }

    private void PublishEditorAction(EditorActionKind action)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
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
