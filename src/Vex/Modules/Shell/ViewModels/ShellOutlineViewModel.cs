using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Shared.Rendering;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Regions;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>
/// 大纲侧栏状态：条目来自命令（Markdown 派生，库条目类型），行 UI 与筛选/折叠渲染已下沉到
/// 库的 MarkdownOutlinePanel（View 薄壳 AttachPanel 接入）；这里只保留展开持久化、
/// 当前章跟随与导航下发。
/// </summary>
public sealed class ShellOutlineViewModel : ReactiveObject, IRegionTabItem
{
    private readonly IShellStatusPublisher _statusPublisher;
    private int _lastCaretLine = 1;
    private int _lastLineCount = 1;
    private readonly IAppSettingsStore? _settingsStore;
    private readonly IWorkspaceDocumentState? _document;
    private readonly ShellWindowLayoutViewModel _layout;
    private MarkdownOutlinePanel? _panel;
    private IReadOnlyList<MarkdownOutlineItem> _items = [];
    private IReadOnlyList<string> _expansionKeys = [];

    public ShellOutlineViewModel(IShellStatusPublisher statusPublisher,
        ShellWindowLayoutViewModel layout, IAppSettingsStore? settingsStore = null,
        IWorkspaceDocumentState? document = null)
    {
        _statusPublisher = statusPublisher;
        _layout = layout;
        _settingsStore = settingsStore;
        _document = document;
        _showOutlineNumbers = settingsStore?.Current.ShowOutlineNumbers ?? false;
        ToggleOutlineCommand = ReactiveCommand.Create(ToggleOutline);
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe(this);
    }

    public string? TitleKey { get; } = VexL.SidebarOutline;

    private IReadOnlyList<OutlineRow> _currentRows = [];

    /// <summary>验收脚本契约：当前文档的大纲行（与展开键同序）。</summary>
    public IReadOnlyList<OutlineRow> OutlineItems => _currentRows;

    private OutlineRow? _currentOutlineItem;

    /// <summary>光标/阅读位置所在章节行（验收脚本契约；面板同步高亮与滚动）。</summary>
    public OutlineRow? CurrentOutlineItem
    {
        get => _currentOutlineItem;
        private set
        {
            if (!SetProperty(ref _currentOutlineItem, value)) return;
            _panel?.CurrentRow = value;
        }
    }

    private bool _showOutlineNumbers;

    /// <summary>大纲条目前的层级章节序号显隐（默认不显示，见 AppSettings.ShowOutlineNumbers）。
    /// 序号始终已算好，这里只切面板样式，翻开关不需要重建大纲。</summary>
    public bool ShowOutlineNumbers
    {
        get => _showOutlineNumbers;
        set
        {
            if (_showOutlineNumbers == value) return;
            _showOutlineNumbers = value;
            _settingsStore?.Update(settings => settings with { ShowOutlineNumbers = value });
            OnPropertyChanged(nameof(ShowOutlineNumbers));
        }
    }

    /// <summary>面板头关闭钮：切换大纲停靠显隐（视图只负责回收焦点）。</summary>
    public ReactiveCommand<ReactiveUI.Primitives.RxVoid, ReactiveUI.Primitives.RxVoid> ToggleOutlineCommand { get; }

    /// <summary>面板头关闭钮：切换大纲停靠显隐（视图只负责回收焦点）。</summary>
    public void ToggleOutline() => _layout.ToggleOutline();

    /// <summary>View 挂载面板（区域视图可能随语言重建，单例 VM 只认最新挂载的面板）。</summary>
    public void AttachPanel(MarkdownOutlinePanel panel)
    {
        if (ReferenceEquals(_panel, panel)) return;
        if (_panel is { } old)
        {
            old.NavigateRequested -= OnPanelNavigateRequested;
            old.ExpandedChanged -= OnPanelExpandedChanged;
        }

        _panel = panel;
        panel.NavigateRequested += OnPanelNavigateRequested;
        panel.ExpandedChanged += OnPanelExpandedChanged;
        ReapplyRows();
    }

    public void DetachPanel(MarkdownOutlinePanel panel)
    {
        if (!ReferenceEquals(_panel, panel)) return;
        panel.NavigateRequested -= OnPanelNavigateRequested;
        panel.ExpandedChanged -= OnPanelExpandedChanged;
        _panel = null;
    }

    [EventHandler]
    public void ApplyOutlineItemsChanged(OutlineItemsChangedCommand command)
    {
        // 评审修订（UI 验证抓到的真实竞态）：EventBus 的异步分发可能把处理器派到非 UI 线程，
        // 而大纲面板的集合重建只允许在 UI 线程进行（并发 SetItems/Refilter 会索引越界）。
        // 到不了 UI 线程时转投调度器串行执行。
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyOutlineItemsChanged(command));
            return;
        }
        // 大纲由 Markdown 派生生成，这里只更新展示状态，跳转仍通过事件总线发给编辑器。
        _items = command.Items;
        ReapplyRows();
        UpdateReadingState(_lastCaretLine, _lastLineCount);
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyMarkdownTextChanged(command));
            return;
        }
        UpdateReadingState(command.CaretLine, command.LineCount);
    }

    [EventHandler]
    public void ApplyReadingPositionChanged(MarkdownReadingPositionChangedCommand command)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyReadingPositionChanged(command));
            return;
        }
        UpdateReadingState(command.Line, _lastLineCount);
    }

    /// <summary>把命令条目灌进面板并按持久化状态盖章折叠（展开键由库生成，历史配置口径不变）。</summary>
    private void ReapplyRows()
    {
        var panel = _panel;
        if (panel is null) return;
        var rows = panel.SetItems(_items);
        var documentKey = OutlineExpansionState.PathKey(_document?.FilePath is { Length: > 0 } path
            ? path : "untitled");
        _expansionKeys = OutlineExpansionKey.BuildSequence(
            rows.Select(row => (row.Level, row.Title)), documentKey);
        var collapsed = OutlineExpansionState.Read(_settingsStore?.Current.CollapsedOutlineSections);
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].IsExpanded = !collapsed.Contains(_expansionKeys[i]);
        }

        _currentRows = rows;
        this.RaisePropertyChanged(nameof(OutlineItems));
        panel.RefreshRows();
        SyncCurrentToPanel();
    }

    private void OnPanelNavigateRequested(object? sender, OutlineRow row) => NavigateToOutlineItem(row);

    private void OnPanelExpandedChanged(object? sender, OutlineRow row)
    {
        // 用户折叠/展开即落盘；宿主自身的初始盖章不经过此事件。
        var index = IndexOf(_currentRows, row);
        if (index >= 0 && index < _expansionKeys.Count)
        {
            var key = _expansionKeys[index];
            _settingsStore?.Update(settings => settings with
            {
                CollapsedOutlineSections = OutlineExpansionState.Set(settings.CollapsedOutlineSections, key, !row.IsExpanded)
            });
        }
    }

    /// <summary>当前章换算：按行号找最后一条 ≤ 光标行，交面板盖章（滚动与"指针悬停不挪动"由面板负责）。</summary>
    private void UpdateReadingState(int caretLine, int lineCount)
    {
        _lastCaretLine = caretLine;
        _lastLineCount = lineCount;

        var panel = _panel;
        var rows = panel?.Rows ?? _currentRows;
        OutlineRow? current = null;
        foreach (var row in rows)
        {
            if (row.Line <= caretLine)
            {
                current = row;
            }
            else
            {
                break;
            }
        }

        CurrentOutlineItem = current;
    }

    private void SyncCurrentToPanel()
    {
        if (_panel is null) return;
        _panel.CurrentRow = CurrentOutlineItem;
    }

    /// <summary>面板行导航：换算回命令条目后统一走这里（验收脚本也直接调用条目重载）。</summary>
    public void NavigateToOutlineItem(OutlineRow row)
    {
        UpdateReadingState(row.Line, _lastLineCount);
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(row.Line));
        _statusPublisher.PublishResourceFormat(VexL.StatusNavigatedToOutlineFormat, row.Title);
    }

    private static int IndexOf(IReadOnlyList<OutlineRow> rows, OutlineRow? row)
    {
        if (row is null) return -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row)) return i;
        }

        return -1;
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
