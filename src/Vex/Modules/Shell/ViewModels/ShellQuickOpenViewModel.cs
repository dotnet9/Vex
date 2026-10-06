using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>快速打开候选项：文件路径 + 展示文本（文件名 - 文件夹）。</summary>
public sealed record QuickOpenItem(string Path, string DisplayName, string LocationText);

/// <summary>
/// Ctrl+P 快速打开浮层：对「最近文件 + 当前文件夹文档」做不区分大小写的包含匹配，
/// 上下键选择、回车打开、Esc 取消；结果为空时回车不动作。
/// </summary>
public sealed class ShellQuickOpenViewModel : ReactiveObject, Irihi.Avalonia.Shared.Contracts.IDialogContext
{
    private readonly List<QuickOpenItem> _allItems;
    private string _query = string.Empty;
    private int _selectedIndex;

    public ShellQuickOpenViewModel(IEnumerable<QuickOpenItem> items, string placeholder)
    {
        _allItems = items.ToList();
        Placeholder = placeholder;
        ApplyFilter();
    }

    public string Placeholder { get; }

    public ObservableCollection<QuickOpenItem> Items { get; } = [];

    public event EventHandler<object?>? RequestClose;

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value))
            {
                ApplyFilter();
            }
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var clamped = Items.Count == 0 ? -1 : Math.Clamp(value, 0, Items.Count - 1);
            if (SetProperty(ref _selectedIndex, clamped))
            {
                OnPropertyChanged(nameof(SelectedItem));
            }
        }
    }

    public QuickOpenItem? SelectedItem => SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : null;

    public bool HasResults => Items.Count > 0;

    public void MoveSelection(int delta)
    {
        if (Items.Count == 0)
        {
            return;
        }

        var next = SelectedIndex + delta;
        if (next < 0)
        {
            next = Items.Count - 1;
        }
        else if (next >= Items.Count)
        {
            next = 0;
        }

        SelectedIndex = next;
    }

    /// <summary>确认打开：把选中项作为结果关闭浮层；无结果时不关闭。</summary>
    public void Confirm()
    {
        if (SelectedItem is { } item)
        {
            RequestClose?.Invoke(this, item.Path);
        }
    }

    public void Cancel() => RequestClose?.Invoke(this, null);

    public void Close() => Cancel();

    private void ApplyFilter()
    {
        var query = _query?.Trim() ?? string.Empty;
        var matches = query.Length == 0
            ? _allItems
            : _allItems
                .Where(item => item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                               || item.LocationText.Contains(query, StringComparison.OrdinalIgnoreCase)
                               || item.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        Items.Clear();
        foreach (var item in matches.Take(200))
        {
            Items.Add(item);
        }

        _selectedIndex = Items.Count == 0 ? -1 : 0;
        OnPropertyChanged(nameof(SelectedIndex));
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(HasResults));
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

    private void OnPropertyChanged(string propertyName) => this.RaisePropertyChanged(propertyName);
}
