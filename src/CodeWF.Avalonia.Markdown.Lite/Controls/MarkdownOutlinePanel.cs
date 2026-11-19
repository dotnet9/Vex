using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 文档大纲侧栏面板：标题筛选框 + 分层行列表（序号可关）+ 空态提示。
/// 数据面吃 <see cref="MarkdownOutlineItem"/>（内部统一算序号），宿主通过
/// <see cref="NavigateRequested"/> 接自己的跳转动作、通过 <see cref="ExpandedChanged"/>
/// 决定折叠状态是否持久化；当前章高亮由宿主写 <see cref="CurrentRow"/>。
/// 视觉走 OutlinePanel* 资源键（Themes/Shared 提供兜底，宿主按各自令牌覆写）。
/// </summary>
[TemplatePart(FilterPartName, typeof(TextBox))]
[TemplatePart(ListPartName, typeof(ListBox))]
[TemplatePart(EmptyStatePartName, typeof(StackPanel))]
[TemplatePart(NoMatchStatePartName, typeof(StackPanel))]
public sealed class MarkdownOutlinePanel : TemplatedControl
{
	private const string FilterPartName = "PART_Filter";
	private const string ListPartName = "PART_List";
	private const string EmptyStatePartName = "PART_EmptyState";
	private const string NoMatchStatePartName = "PART_NoMatch";

	public static readonly StyledProperty<bool> ShowNumbersProperty =
		AvaloniaProperty.Register<MarkdownOutlinePanel, bool>(nameof(ShowNumbers));

	/// <summary>层级章节序号显隐（默认不显示，与正文标题原文一致）。</summary>
	public bool ShowNumbers
	{
		get => GetValue(ShowNumbersProperty);
		set => SetValue(ShowNumbersProperty, value);
	}

	public static readonly StyledProperty<string?> FilterWatermarkProperty =
		AvaloniaProperty.Register<MarkdownOutlinePanel, string?>(nameof(FilterWatermark));
	public string? FilterWatermark { get => GetValue(FilterWatermarkProperty); set => SetValue(FilterWatermarkProperty, value); }

	/// <summary>文档没有标题时的占位文案。</summary>
	public static readonly StyledProperty<string?> EmptyTextProperty =
		AvaloniaProperty.Register<MarkdownOutlinePanel, string?>(nameof(EmptyText));
	public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

	/// <summary>筛选无结果时的占位文案。</summary>
	public static readonly StyledProperty<string?> NoMatchTextProperty =
		AvaloniaProperty.Register<MarkdownOutlinePanel, string?>(nameof(NoMatchText));
	public string? NoMatchText { get => GetValue(NoMatchTextProperty); set => SetValue(NoMatchTextProperty, value); }

	public static readonly StyledProperty<OutlineRow?> SelectedRowProperty =
		AvaloniaProperty.Register<MarkdownOutlinePanel, OutlineRow?>(nameof(SelectedRow), defaultBindingMode: BindingMode.TwoWay);
	public OutlineRow? SelectedRow { get => GetValue(SelectedRowProperty); set => SetValue(SelectedRowProperty, value); }

	public static readonly StyledProperty<OutlineRow?> CurrentRowProperty =
		AvaloniaProperty.Register<MarkdownOutlinePanel, OutlineRow?>(nameof(CurrentRow));
	public OutlineRow? CurrentRow { get => GetValue(CurrentRowProperty); set => SetValue(CurrentRowProperty, value); }

	private TextBox? _filter;
	private ListBox? _list;
	private StackPanel? _emptyState;
	private StackPanel? _noMatchState;
	private readonly List<OutlineRow> _rows = [];
	// 评审修订（Vex UI 验证抓到的真实竞态）：可见集用 ObservableCollection 原地增删，
	// ItemsSource 只在首次挂上；此前每次 Refilter 都「置空再赋值」重置 ItemsSource，
	// 快速连续重建（逐字输入/多窗竞争）时 Avalonia 容器生成器在处理上一次变更途中
	// 又收到新 Reset，Remove(index) 直接越界崩溃。
	private readonly System.Collections.ObjectModel.ObservableCollection<OutlineRow> _visibleRows = [];
	private bool _applyingSelection;

	public MarkdownOutlinePanel()
	{
		// 序号显隐走面板 Classes（列定义/间距/可见性全部由主题类选择器裁决）。
		PropertyChanged += (_, e) =>
		{
			if (e.Property.Name == nameof(ShowNumbers)) Classes.Set("op-numbers", ShowNumbers);
			if (e.Property.Name == nameof(CurrentRow)) ApplyCurrentRow();
		};
	}

	/// <summary>当前全量行（含被折叠/筛选隐藏的），宿主据此维护持久化键等派生状态。</summary>
	public IReadOnlyList<OutlineRow> Rows => _rows;

	/// <summary>当前可见行（折叠/筛选后），宿主的滚动联动等据此对齐。</summary>
	public IReadOnlyList<OutlineRow> VisibleRows => _visibleRows;

	/// <summary>
	/// 重建行。序号由 <see cref="MarkdownOutlineNumbering.WithNumbers"/> 统一计算；
	/// 折叠/当前/选中状态全部复位，宿主随后可按自身状态对返回行盖章。
	/// </summary>
	public IReadOnlyList<OutlineRow> SetItems(IEnumerable<MarkdownOutlineItem> items)
	{
		_rows.Clear();
		_selectedRowInternal = null;
		foreach (var row in MarkdownOutlineNumbering.WithNumbers(items.ToArray()))
		{
			var outlineRow = new OutlineRow(row, row.Number);
			outlineRow.PropertyChanged += RowPropertyChanged;
			_rows.Add(outlineRow);
		}

		// HasChildren：下一行层级更深即为父。
		for (var i = 0; i < _rows.Count; i++)
		{
			_rows[i].HasChildren = i + 1 < _rows.Count && _rows[i + 1].Level > _rows[i].Level;
		}

		Refilter();
		return _rows;
	}

	private OutlineRow? _selectedRowInternal;

	/// <summary>替换可见集（不改全量行），宿主跳转定位等场景可用。</summary>
	public void RefreshRows() => Refilter();

	/// <summary>用户触发导航（点击行释放 / 选中行上 Enter 或 Space）。</summary>
	public event EventHandler<OutlineRow>? NavigateRequested;

	/// <summary>用户以左右键折叠/展开行（宿主决定是否持久化；宿主自身盖章不触发）。</summary>
	public event EventHandler<OutlineRow>? ExpandedChanged;

	protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
		_filter = e.NameScope.Find<TextBox>(FilterPartName);
		_list = e.NameScope.Find<ListBox>(ListPartName);
		_emptyState = e.NameScope.Find<StackPanel>(EmptyStatePartName);
		_noMatchState = e.NameScope.Find<StackPanel>(NoMatchStatePartName);
		if (_filter is not null)
		{
			_filter.TextChanged += (_, _) => ApplyFilter(_filter.Text);
		}

		if (_list is not null)
		{
			_list.KeyDown += ListKeyDown;
			_list.SelectionChanged += OnListSelectionChanged;
			_list.AddHandler(PointerReleasedEvent, ListPointerReleased, RoutingStrategies.Bubble, true);
		}

		if (e.NameScope.Find<Button>("PART_ClearFilter") is { } clear)
		{
			clear.Click += (_, _) => ApplyFilter(string.Empty);
		}

		Classes.Set("op-numbers", ShowNumbers);
		Refilter();
	}

	/// <summary>宿主驱动的导航入口（验收脚本/宿主菜单调用）：置选中并发出 <see cref="NavigateRequested"/>。</summary>
	public void Navigate(OutlineRow row)
	{
		if (row is null || !_rows.Contains(row)) return;
		_applyingSelection = true;
		SetCurrentValue(SelectedRowProperty, row);
		if (_list is not null) _list.SelectedItem = row;
		_applyingSelection = false;
		NavigateRequested?.Invoke(this, row);
	}

	private void RowPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not OutlineRow row) return;
		if (e.PropertyName == nameof(OutlineRow.IsExpanded))
		{
			Refilter();
			ExpandedChanged?.Invoke(this, row);
		}
	}

	private void ListPointerReleased(object? sender, PointerReleasedEventArgs e)
	{
		if (e.InitialPressMouseButton != MouseButton.Left) return;
		var container = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
		if (container?.DataContext is OutlineRow row)
		{
			NavigateRequested?.Invoke(this, row);
		}
	}

	private void ListKeyDown(object? sender, KeyEventArgs e)
	{
		if (SelectedRow is not { } row) return;
		if (e.Key is Key.Enter or Key.Space)
		{
			NavigateRequested?.Invoke(this, row);
			e.Handled = true;
			return;
		}

		if (e.Key is not (Key.Left or Key.Right) || !row.HasChildren) return;
		row.IsExpanded = e.Key == Key.Right; // 触发 RowPropertyChanged → Refilter + ExpandedChanged
		e.Handled = true;
	}

	/// <summary>宿主或模板内筛选框统一走这里；空串即清筛选。</summary>
	public void ApplyFilter(string? text)
	{
		_filterText = text ?? string.Empty;
		if (_filter is not null && _filter.Text != _filterText)
		{
			_filter.Text = _filterText; // 触发同一管道，不必重复 Refilter
			return;
		}

		Refilter();
	}

	private string _filterText = string.Empty;

	/// <summary>
	/// 统一两侧语义：筛选时按「标题匹配 + 父链」显示（无视折叠）；
	/// 无筛选时按折叠层级收敛。选中行仍在可见集时保持选中。
	/// </summary>
	private void Refilter()
	{
		var query = _filterText.Trim();
		_visibleRows.Clear();
		HashSet<OutlineRow>? matches = null;
		if (query.Length > 0)
		{
			matches = [];
			var ancestors = new List<OutlineRow>();
			foreach (var row in _rows)
			{
				while (ancestors.Count > 0 && ancestors[^1].Level >= row.Level) ancestors.RemoveAt(ancestors.Count - 1);
				if (row.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase))
				{
					matches.Add(row);
					foreach (var parent in ancestors) matches.Add(parent);
				}

				ancestors.Add(row);
			}
		}

		var hiddenBelowLevel = int.MaxValue;
		foreach (var row in _rows)
		{
			if (matches is not null)
			{
				if (matches.Contains(row)) _visibleRows.Add(row);
				continue;
			}

			if (row.Level > hiddenBelowLevel) continue;
			hiddenBelowLevel = row.IsExpanded ? int.MaxValue : row.Level;
			_visibleRows.Add(row);
		}

		if (_list is not null && !ReferenceEquals(_list.ItemsSource, _visibleRows))
		{
			// 只在首次（或模板重建后）挂源；此后集合原地变更，避免高频重置 ItemsSource。
			_applyingSelection = true;
			_list.ItemsSource = _visibleRows;
			_applyingSelection = false;
		}

		SyncSelection();
		UpdateStates();
	}

	private void SyncSelection()
	{
		_selectedRowInternal = SelectedRow is { } selected && _visibleRows.Contains(selected) ? selected : null;
		_applyingSelection = true;
		SetCurrentValue(SelectedRowProperty, _selectedRowInternal);
		_applyingSelection = false;
		if (_list is not null && _selectedRowInternal is not null)
		{
			_list.SelectedItem = _selectedRowInternal;
		}
	}

	private void UpdateStates()
	{
		var hasVisible = _visibleRows.Count > 0;
		if (_list is not null) _list.IsVisible = hasVisible;
		if (_emptyState is not null) _emptyState.IsVisible = _rows.Count == 0;
		if (_noMatchState is not null) _noMatchState.IsVisible = _rows.Count > 0 && !hasVisible;
	}

	/// <summary>宿主写当前章：盖章并滚动到可见行（指针悬停/键盘聚焦列表时不挪动）。</summary>
	private void ApplyCurrentRow()
	{
		var current = CurrentRow;
		foreach (var row in _rows)
		{
			row.IsCurrent = ReferenceEquals(row, current);
		}

		if (current is null || _list is null) return;
		if (_list.IsPointerOver || _list.IsKeyboardFocusWithin) return;
		var visible = _visibleRows.LastOrDefault(row => row.Line <= current.Line);
		if (visible is not null) _list.ScrollIntoView(visible);
	}

	private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (_applyingSelection) return;
		SetCurrentValue(SelectedRowProperty, _list?.SelectedItem as OutlineRow);
	}
}
