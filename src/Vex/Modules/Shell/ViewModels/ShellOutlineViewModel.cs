using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Regions;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellOutlineViewModel : ReactiveObject, IRegionTabItem
{
    private readonly IShellStatusPublisher _statusPublisher;
    private OutlineItem? _selectedOutlineItem;

    public ShellOutlineViewModel(IShellStatusPublisher statusPublisher)
    {
        _statusPublisher = statusPublisher;
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
