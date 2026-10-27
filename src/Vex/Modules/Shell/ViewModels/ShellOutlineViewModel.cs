using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Regions;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellOutlineViewModel : ReactiveObject, IRegionTabItem
{
    private readonly IShellStatusPublisher _statusPublisher;
    private readonly IAppLocalizer _localizer;
    private OutlineItem? _selectedOutlineItem;
    private OutlineItem? _currentOutlineItem;
    private double _readingPercent;
    private int _lastCaretLine = 1;
    private int _lastLineCount = 1;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly IWorkspaceDocumentState? _document;
    public ShellWindowLayoutViewModel? Layout { get; }

    public ShellOutlineViewModel(IShellStatusPublisher statusPublisher, IAppLocalizer localizer,
        IAppSettingsStore? settingsStore = null, IWorkspaceDocumentState? document = null, ShellWindowLayoutViewModel? layout = null)
    {
        _statusPublisher = statusPublisher;
        _localizer = localizer;
        _settingsStore = settingsStore;
        _document = document;
        Layout = layout;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
    }

    public string? TitleKey { get; } = VexL.SidebarOutline;

    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];
    public ObservableCollection<OutlineItem> FilteredOutlineItems { get; } = [];
    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set { if (SetProperty(ref _filterText, value)) FilterOutline(); }
    }

    public void ClearFilter() => FilterText = string.Empty;

    private void FilterOutline()
    {
        FilteredOutlineItems.Clear();
        var hiddenBelow = 7;
        foreach (var item in OutlineItems)
        {
            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                if (item.Title.Contains(FilterText.Trim(), StringComparison.CurrentCultureIgnoreCase))
                    FilteredOutlineItems.Add(item);
                continue;
            }
            if (item.Level > hiddenBelow) continue;
            hiddenBelow = item.IsExpanded ? 7 : item.Level;
            FilteredOutlineItems.Add(item);
        }
        OnPropertyChanged(nameof(IsFilterEmpty));
        OnPropertyChanged(nameof(IsFilterNoMatch));
    }

    public bool IsFilterEmpty => FilteredOutlineItems.Count == 0;

    /// <summary>原型区分两种空态：文档没有标题给引导文案，筛选无结果给无匹配文案。</summary>
    public bool IsFilterNoMatch => HasOutlineItems && !string.IsNullOrWhiteSpace(FilterText) && IsFilterEmpty;

    public bool HasOutlineItems => OutlineItems.Count > 0;

    public bool IsOutlineEmpty => !HasOutlineItems;

    public OutlineItem? SelectedOutlineItem
    {
        get => _selectedOutlineItem;
        set
        {
            SetProperty(ref _selectedOutlineItem, value);
            if (value is not null)
            {
                NavigateToOutlineItem(value);
            }
        }
    }

    [EventHandler]
    public void ApplyOutlineItemsChanged(OutlineItemsChangedCommand command)
    {
        // 大纲由 Markdown 派生生成，这里只更新展示状态，跳转仍通过事件总线发给编辑器。
        OutlineItems.Clear();
        var counters = new int[6];
        var collapsed = NavigationExpansionState.Read(_settingsStore?.Current.CollapsedOutlineSections);
        var documentKey = NavigationExpansionState.Encode(_document?.FilePath is { Length: > 0 } path
            ? NavigationExpansionState.PathKey(path) : "untitled");
        var ancestors = new Stack<OutlineItem>();
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in command.Items)
        {
            var level = Math.Clamp(item.Level, 1, 6);
            counters[level - 1]++;
            Array.Clear(counters, level, 6 - level);
            item.Number = string.Join('.', counters.Take(level));
            while (ancestors.Count > 0 && ancestors.Peek().Level >= item.Level) ancestors.Pop();
            var parentKey = ancestors.Count > 0 ? ancestors.Peek().ExpansionKey : documentKey;
            var key = parentKey + "/" + level + ":" + NavigationExpansionState.Encode(item.Title);
            occurrences.TryGetValue(key, out var occurrence);
            occurrences[key] = occurrence + 1;
            item.ExpansionKey = key + ":" + occurrence;
            item.IsExpanded = !collapsed.Contains(item.ExpansionKey);
            if (ancestors.Count > 0) ancestors.Peek().HasChildren = true;
            ancestors.Push(item);
            item.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName != nameof(OutlineItem.IsExpanded)) return;
                _settingsStore?.Update(settings => settings with
                {
                    CollapsedOutlineSections = NavigationExpansionState.Set(settings.CollapsedOutlineSections,
                        item.ExpansionKey, !item.IsExpanded)
                });
                FilterOutline();
            };
            OutlineItems.Add(item);
        }

        FilterOutline();
        SelectOutlineItemSilently(null);
        NotifyOutlineChanged();
        UpdateReadingState(_lastCaretLine, _lastLineCount);
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        UpdateReadingState(command.CaretLine, command.LineCount);
    }

    // 光标所在章节高亮 + 阅读进度（对应原型「当前章节 · N%」与底部进度条）
    public OutlineItem? CurrentOutlineItem
    {
        get => _currentOutlineItem;
        private set
        {
            if (ReferenceEquals(_currentOutlineItem, value))
            {
                return;
            }

            if (_currentOutlineItem is not null)
            {
                _currentOutlineItem.IsCurrent = false;
            }

            _currentOutlineItem = value;
            if (_currentOutlineItem is not null)
            {
                _currentOutlineItem.IsCurrent = true;
            }

            OnPropertyChanged(nameof(CurrentOutlineItem));
            OnPropertyChanged(nameof(CurrentChapterText));
        }
    }

    public double ReadingPercent
    {
        get => _readingPercent;
        private set
        {
            if (SetProperty(ref _readingPercent, value))
            {
                OnPropertyChanged(nameof(CurrentChapterText));
            }
        }
    }

    public string CurrentChapterText
    {
        get
        {
            var title = _currentOutlineItem?.Title ?? _localizer.Get(VexL.OutlineNoCurrentChapter);
            return _localizer.Format(VexL.OutlineCurrentFormat, title, (int)Math.Round(_readingPercent));
        }
    }

    private void UpdateReadingState(int caretLine, int lineCount)
    {
        _lastCaretLine = caretLine;
        _lastLineCount = lineCount;

        OutlineItem? current = null;
        foreach (var item in OutlineItems)
        {
            if (item.Line <= caretLine)
            {
                current = item;
            }
            else
            {
                break;
            }
        }

        CurrentOutlineItem = current;
        ReadingPercent = Math.Clamp(caretLine * 100.0 / Math.Max(1, lineCount), 0, 100);
    }

    private void SelectOutlineItemSilently(OutlineItem? outlineItem)
    {
        SetProperty(ref _selectedOutlineItem, outlineItem, nameof(SelectedOutlineItem));
    }

    /// <summary>
    /// 点击大纲跳转。重复点击同一项时选中值不变，因此不能只靠选中值变化触发，
    /// 必须每次都下发一次跳转指令（指令带序号，接收端按序号强制重新定位）。
    /// </summary>
    private void NavigateToOutlineItem(OutlineItem outlineItem)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(outlineItem.Line));
        _statusPublisher.PublishResourceFormat(VexL.StatusNavigatedToOutlineFormat, outlineItem.Title);
    }

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutlineItems));
        OnPropertyChanged(nameof(IsOutlineEmpty));
        OnPropertyChanged(nameof(IsFilterNoMatch));
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
