using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellFindBarViewModel : ReactiveObject
{
    private const int SearchTextLimit = 200;
    private static readonly TimeSpan SearchCountDebounce = TimeSpan.FromMilliseconds(180);
    private readonly IAppLocalizer _localizer;
    private readonly IShellStatusPublisher _statusPublisher;
    private Timer? _searchCountTimer;
    private bool _isVisible;
    private bool _isReplaceVisible;
    private bool _isMatchCase;
    private bool _isWholeWord;
    private bool _isRegex;
    private string _searchText = string.Empty;
    private string _replacementText = string.Empty;
    private int _currentMatch;
    private int _matchTotal;

    public ShellFindBarViewModel(IAppLocalizer localizer, IShellStatusPublisher statusPublisher)
    {
        _localizer = localizer;
        _statusPublisher = statusPublisher;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public bool IsReplaceVisible
    {
        get => _isReplaceVisible;
        set
        {
            if (SetProperty(ref _isReplaceVisible, value))
            {
                this.RaisePropertyChanged(nameof(ReplacePillColumnWidth));
            }
        }
    }

    /// <summary>原型隐藏替换框是让整行重新排，不是留一个空槽：星列换成 0 宽，后面的控件才收得回来。</summary>
    public GridLength ReplacePillColumnWidth => IsReplaceVisible ? new(1, GridUnitType.Star) : new(0);

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
            if (SetProperty(ref _searchText, NormalizeSearchInput(value)))
            {
                this.RaisePropertyChanged(nameof(HasFindQuery));
                this.RaisePropertyChanged(nameof(FindCountText));
                RefreshSearchResultCount();
            }
        }
    }

    /// <summary>原型 #find-count 没词时整枚 hidden，不占行里的位置。</summary>
    public bool HasFindQuery => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>原型徽标只报命中数（「N 处匹配」/「当前正文无匹配」），当前位置另有读数。</summary>
    public string FindCountText => _matchTotal > 0
        ? _localizer.Format(VexL.FindCountMatches, _matchTotal)
        : _localizer.Get(VexL.FindCountNone);

    /// <summary>步进落在第几处命中：查找流程用它判断光标真的动了。</summary>
    public int CurrentMatch => _currentMatch;

    public string ReplacementText
    {
        get => _replacementText;
        set => SetProperty(ref _replacementText, NormalizeSearchInput(value));
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

    public void FindPrev()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            SetStatusResource(VexL.StatusEnterSearchTextFirst);
            return;
        }

        PublishSearch(EditorSearchAction.FindPrev);
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
        _matchTotal = command.TotalCount;
        _currentMatch = command.TotalCount > 0 ? Math.Max(1, command.CurrentIndex) : 0;
        this.RaisePropertyChanged(nameof(CurrentMatch));
        this.RaisePropertyChanged(nameof(FindCountText));
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
            _matchTotal = 0;
            _currentMatch = 0;
            this.RaisePropertyChanged(nameof(CurrentMatch));
            this.RaisePropertyChanged(nameof(FindCountText));
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
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorSearchCommand(
            action,
            SearchText,
            replacementText,
            IsMatchCase,
            IsWholeWord,
            IsRegex));
    }

    private void SeedSearchTextFromEditorSelection()
    {
        var selectedText = CodeWF.Toolkit.EventBus.EventBus.Default.Query(new EditorSelectedTextQuery());
        if (!string.IsNullOrEmpty(selectedText))
        {
            SearchText = selectedText;
        }
    }

    private static string NormalizeSearchInput(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ");
        return normalized.Length <= SearchTextLimit
            ? normalized
            : normalized[..SearchTextLimit];
    }

    private void PublishEditorAction(EditorActionKind action)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
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
