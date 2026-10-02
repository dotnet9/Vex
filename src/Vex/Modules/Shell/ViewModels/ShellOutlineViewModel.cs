using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.EventBus;
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

    public ShellOutlineViewModel(IShellStatusPublisher statusPublisher, IAppLocalizer localizer)
    {
        _statusPublisher = statusPublisher;
        _localizer = localizer;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    public string? TitleKey { get; } = VexL.SidebarOutline;

    public ObservableCollection<OutlineItem> OutlineItems { get; } = [];

    public bool HasOutlineItems => OutlineItems.Count > 0;

    public bool IsOutlineEmpty => !HasOutlineItems;

    public OutlineItem? SelectedOutlineItem
    {
        get => _selectedOutlineItem;
        set
        {
            if (SetProperty(ref _selectedOutlineItem, value) && value is not null)
            {
                CodeWF.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(value.Line));
                _statusPublisher.PublishResourceFormat(VexL.StatusNavigatedToOutlineFormat, value.Title);
            }
        }
    }

    [EventHandler]
    public void ApplyOutlineItemsChanged(OutlineItemsChangedCommand command)
    {
        // 大纲由 Markdown 派生生成，这里只更新展示状态，跳转仍通过事件总线发给编辑器。
        OutlineItems.Clear();
        foreach (var item in command.Items)
        {
            OutlineItems.Add(item);
        }

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

    private void NotifyOutlineChanged()
    {
        OnPropertyChanged(nameof(HasOutlineItems));
        OnPropertyChanged(nameof(IsOutlineEmpty));
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
