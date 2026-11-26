using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

internal sealed record MarkdownSelectionBlock(
	Control Control,
	int BlockKey,
	MarkdownTextSpan PlainTextSpan,
	string PlainText);

/// <summary>
/// 选区坐标：只存值——块键（源码起始偏移）与块内纯文本偏移。文本始终按块模型
/// 取，不依赖控件实例，所以流式重建控件、虚拟化回收之后选区与复制都不受影响。
/// </summary>
internal readonly record struct MarkdownTextPoint(int BlockKey, int Offset);

internal sealed class MarkdownSelectionController
{
	private const string SelectedBlockClass = "MdSelectedBlock";
	private readonly List<MarkdownSelectionBlock> _blocks = [];
	private Visual? _coordinateRoot;
	private MarkdownTextPoint? _anchor;
	private MarkdownTextPoint? _active;
	private bool _hasVisualSelection;
	private readonly HashSet<MarkdownTextProjection> _selectedSegments = [];
	private readonly HashSet<IMarkdownTextSelectionVisual> _selectedVisuals = [];

	/// <summary>块控件子树内的自绘选区视觉（自绘文本段）：存在时接管命中与选区可视化。</summary>
	private static IMarkdownTextSelectionVisual? FindSelectionVisual(Control control)
	{
		if (control is IMarkdownTextSelectionVisual self)
		{
			return self;
		}

		foreach (var descendant in control.GetVisualDescendants())
		{
			if (descendant is IMarkdownTextSelectionVisual visual)
			{
				return visual;
			}
		}

		return null;
	}

	public bool HasSelection => _hasVisualSelection
								&& _anchor.HasValue
								&& _active.HasValue
								&& SelectedText.Length > 0;

	public string SelectedText
	{
		get
		{
			if (!_hasVisualSelection || !_anchor.HasValue || !_active.HasValue || _blocks.Count == 0)
			{
				return string.Empty;
			}

			var (start, end) = GetOrderedPoints();
			var startIndex = FindIndex(start.BlockKey);
			var endIndex = FindIndex(end.BlockKey);
			if (startIndex < 0 || endIndex < 0 || endIndex < startIndex)
			{
				return string.Empty;
			}

			var parts = new List<string>();
			for (var i = startIndex; i <= endIndex; i++)
			{
				var text = _blocks[i].PlainText;
				if (text.Length == 0)
				{
					continue;
				}

				var from = i == startIndex ? Math.Clamp(start.Offset, 0, text.Length) : 0;
				var to = i == endIndex ? Math.Clamp(end.Offset, 0, text.Length) : text.Length;
				if (to > from)
				{
					parts.Add(text[from..to]);
				}
			}

			return string.Join(Environment.NewLine + Environment.NewLine, parts);
		}
	}

	/// <summary>
	/// 重建块列表。选区按块键重新定位：键仍在（同一块的控件被替换/回收）就保留，
	/// 键消失（内容真的换了）就清空。控件引用只用于绘制选中底色与命中测试。
	/// </summary>
	public void SetBlocks(
		IEnumerable<MarkdownSelectionBlock> blocks,
		Visual? coordinateRoot = null,
		bool preserveSelection = false)
	{
		ClearVisualState();
		_blocks.Clear();
		_blocks.AddRange(blocks.Where(block => block.PlainTextSpan.Length > 0));
		_coordinateRoot = coordinateRoot;
		if (!preserveSelection)
		{
			_anchor = null;
			_active = null;
			_hasVisualSelection = false;
			return;
		}

		if (_anchor is { } anchor && FindIndex(anchor.BlockKey) < 0
			|| _active is { } active && FindIndex(active.BlockKey) < 0)
		{
			_anchor = null;
			_active = null;
			_hasVisualSelection = false;
			return;
		}

		ApplyVisualState();
	}

	public bool Begin(Point point, bool extend = false)
	{
		var index = HitTestBlock(point);
		if (index < 0)
		{
			Clear();
			return false;
		}

		var block = _blocks[index];
		var hit = new MarkdownTextPoint(block.BlockKey, HitTestOffset(block, point));
		if (!extend || _anchor is null) _anchor = hit;
		_active = hit;
		if (extend) ApplyVisualState(); else ClearVisualState();
		return true;
	}

	public bool Extend(Point point)
	{
		if (!_anchor.HasValue)
		{
			return false;
		}

		var index = ResolveExtensionIndex(point);
		if (index < 0)
		{
			return false;
		}

		var block = _blocks[index];
		var next = new MarkdownTextPoint(block.BlockKey, HitTestOffset(block, point));
		if (_active == next)
		{
			return false;
		}

		_active = next;
		ApplyVisualState();
		return true;
	}

	public void CommitClickWithoutDrag()
	{
		Clear();
	}

	public void Clear()
	{
		ClearVisualState();
		_anchor = null;
		_active = null;
		_hasVisualSelection = false;
	}

	private int FindIndex(int blockKey)
	{
		for (var i = 0; i < _blocks.Count; i++)
		{
			if (_blocks[i].BlockKey == blockKey)
			{
				return i;
			}
		}

		return -1;
	}

	private int HitTestBlock(Point documentPoint)
	{
		for (var i = 0; i < _blocks.Count; i++)
		{
			// 虚拟化下未物化块已脱离坐标树：TranslatePoint 失败时旧 Bounds 是上一帧的残值，
			// 拿它命中会把锚点钉到错误块上（表现为点击后冒出莫名选区）。
			if (!IsBlockHittable(_blocks[i]))
			{
				continue;
			}

			var bounds = GetDocumentBounds(_blocks[i].Control);
			if (documentPoint.Y >= bounds.Top && documentPoint.Y <= bounds.Bottom)
			{
				return i;
			}
		}

		return -1;
	}

	private Rect GetDocumentBounds(Control control)
	{
		if (_coordinateRoot is not null
			&& control.TranslatePoint(new Point(0, 0), _coordinateRoot) is { } origin)
		{
			return new Rect(origin, control.Bounds.Size);
		}

		return control.Bounds;
	}

	/// <summary>控件挂在坐标树（即滚动宿主的可视树）上才算可命中；跨树 TranslatePoint 返回 null，
	/// 脱离树的块只保留纯文本选区语义（复制/跨块选区），不参与命中测试。</summary>
	private bool IsBlockHittable(MarkdownSelectionBlock block) =>
		_coordinateRoot is not null
		&& block.Control.TranslatePoint(new Point(0, 0), _coordinateRoot) is not null;

	private int ResolveExtensionIndex(Point documentPoint)
	{
		var hit = HitTestBlock(documentPoint);
		if (hit >= 0) return hit;
		return _blocks.Count == 0 ? -1 : Enumerable.Range(0, _blocks.Count)
			.Where(i => IsBlockHittable(_blocks[i]))
			.MinBy(i => Math.Abs(GetDocumentBounds(_blocks[i].Control).Center.Y - documentPoint.Y));
	}

	private int HitTestOffset(MarkdownSelectionBlock block, Point point)
	{
		// 自绘选区视觉接管：命中由段控件自己做 TextLayout 级命中测试。
		if (FindSelectionVisual(block.Control) is { } visual)
		{
			return visual.HitTestOffset(point);
		}

		var projected = MarkdownTextProjections.Create(block.Control, block.PlainText).ToArray();
		// 图表、图片等非文字块没有可投影的字形，端点沿用块模型的首尾。
		if (projected.Length == 0)
			return point.Y < GetDocumentBounds(block.Control).Center.Y ? 0 : block.PlainText.Length;
		var formulas = projected.Where(item => item.TextBlock is null)
			.Where(item => GetDocumentBounds(item.Control).Contains(point))
			.ToArray();
		if (formulas.Length > 0)
		{
			var formula = formulas.MinBy(item => GetDocumentBounds(item.Control).Width * GetDocumentBounds(item.Control).Height);
			var local = _coordinateRoot?.TranslatePoint(point, formula.Control) ?? point - GetDocumentBounds(formula.Control).Position;
			var formulaRatio = formula.Control.Bounds.Width <= 0 ? 0 : Math.Clamp(local.X / formula.Control.Bounds.Width, 0, 1);
			return formula.Start + (int)Math.Round(formulaRatio * formula.Length);
		}

		var nearest = projected.MinBy(item =>
		{
			var bounds = GetDocumentBounds(item.Control);
			return point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0;
		});
		var projectedPoint = _coordinateRoot?.TranslatePoint(point, nearest.Control) ?? point - GetDocumentBounds(nearest.Control).Position;
		if (projectedPoint.Y < 0) return nearest.Start;
		if (projectedPoint.Y > nearest.Control.Bounds.Height) return nearest.Start + nearest.Length;
		if (nearest.TextBlock is not null)
		{
			var hit = nearest.TextBlock.TextLayout.HitTestPoint(new Point(projectedPoint.X - nearest.TextBlock.Padding.Left, projectedPoint.Y - nearest.TextBlock.Padding.Top));
			var textPosition = hit.TextPosition;
			nearest = projected.Where(item => item.TextBlock == nearest.TextBlock).MinBy(item =>
			{
				var start = item.LocalStart;
				var end = start + item.Length;
				return textPosition < start ? start - textPosition : textPosition > end ? textPosition - end : 0;
			});
			return nearest.Start + Math.Clamp(textPosition - nearest.LocalStart, 0, nearest.Length);
		}

		var ratio = nearest.Control.Bounds.Width <= 0 ? 0 : Math.Clamp(projectedPoint.X / nearest.Control.Bounds.Width, 0, 1);
		return nearest.Start + (int)Math.Round(ratio * nearest.Length);
	}

	private (MarkdownTextPoint Start, MarkdownTextPoint End) GetOrderedPoints()
	{
		var anchor = _anchor.GetValueOrDefault();
		var active = _active.GetValueOrDefault();
		var anchorIndex = FindIndex(anchor.BlockKey);
		var activeIndex = FindIndex(active.BlockKey);
		if (anchorIndex > activeIndex
			|| anchorIndex == activeIndex && anchor.Offset > active.Offset)
		{
			return (active, anchor);
		}

		return (anchor, active);
	}

	private void ApplyVisualState()
	{
		ClearVisualState();
		if (!_anchor.HasValue || !_active.HasValue)
		{
			return;
		}

		var (from, to) = GetOrderedPoints();
		var start = FindIndex(from.BlockKey);
		var end = FindIndex(to.BlockKey);
		if (start < 0 || end < 0)
		{
			return;
		}

		if (start > end)
		{
			(start, end) = (end, start);
		}

		for (var i = start; i <= end; i++)
		{
			var block = _blocks[i];
			var fromOffset = i == start ? from.Offset : 0;
			var toOffset = i == end ? to.Offset : block.PlainText.Length;
			// 自绘选区视觉接管：按块内纯文本区间绘制，不投影、不加整块选中类。
			if (FindSelectionVisual(block.Control) is { } visual)
			{
				_selectedVisuals.Add(visual);
				visual.ApplySelection(fromOffset, toOffset);
				continue;
			}

			var segments = MarkdownTextProjections.Create(block.Control, block.PlainText).ToArray();
			if (segments.Length == 0) block.Control.Classes.Add(SelectedBlockClass);
			foreach (var textGroup in segments.Where(segment => segment.TextBlock is not null).GroupBy(segment => segment.TextBlock))
			{
				var text = textGroup.Key!;
				_selectedSegments.Add(textGroup.First());
				text.SelectionStart = textGroup.Min(segment => segment.LocalStart
					+ Math.Clamp(fromOffset - segment.Start, 0, segment.Length));
				text.SelectionEnd = textGroup.Max(segment => segment.LocalStart
					+ Math.Clamp(toOffset - segment.Start, 0, segment.Length));
			}

			foreach (var segment in segments.Where(segment => segment.TextBlock is null))
			{
				_selectedSegments.Add(segment);
				MarkdownFormulaState.SetSelectionStart(segment.Control, Math.Clamp(fromOffset - segment.Start, 0, segment.Length));
				MarkdownFormulaState.SetSelectionEnd(segment.Control, Math.Clamp(toOffset - segment.Start, 0, segment.Length));
			}
		}

		_hasVisualSelection = true;
	}

	private void ClearVisualState()
	{
		foreach (var block in _blocks)
		{
			block.Control.Classes.Remove(SelectedBlockClass);
		}
		foreach (var segment in _selectedSegments)
		{
			if (segment.TextBlock is { } text) text.ClearSelection();
			else MarkdownFormulaState.ClearSelection(segment.Control);
		}
		_selectedSegments.Clear();
		foreach (var visual in _selectedVisuals)
		{
			visual.ClearSelection();
		}
		_selectedVisuals.Clear();

		_hasVisualSelection = false;
	}
}

internal readonly record struct MarkdownPointerSelectionState(Point StartPoint)
{
	public bool IsDragging(Point currentPoint)
	{
		return Math.Abs(currentPoint.X - StartPoint.X) > 4
			   || Math.Abs(currentPoint.Y - StartPoint.Y) > 4;
	}
}
