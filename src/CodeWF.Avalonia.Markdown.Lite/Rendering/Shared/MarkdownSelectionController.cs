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
	private readonly HashSet<SelectableTextBlock> _selectedTexts = [];

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

	private int ResolveExtensionIndex(Point documentPoint)
	{
		var hit = HitTestBlock(documentPoint);
		if (hit >= 0) return hit;
		return _blocks.Count == 0 ? -1 : Enumerable.Range(0, _blocks.Count)
			.MinBy(i => Math.Abs(GetDocumentBounds(_blocks[i].Control).Center.Y - documentPoint.Y));
	}

	private int HitTestOffset(MarkdownSelectionBlock block, Point point)
	{
		var projected = TextSegments(block).ToArray();
		// 图表、图片等非文字块没有可投影的字形，端点沿用块模型的首尾。
		if (projected.Length == 0)
			return point.Y < GetDocumentBounds(block.Control).Center.Y ? 0 : block.PlainText.Length;
		var nearest = projected.MinBy(item =>
		{
			var bounds = GetDocumentBounds(item.Text);
			return point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0;
		});
		var local = _coordinateRoot?.TranslatePoint(point, nearest.Text) ?? point - GetDocumentBounds(nearest.Text).Position;
		if (local.Y < 0) return nearest.Start;
		if (local.Y > nearest.Text.Bounds.Height) return nearest.Start + nearest.Length;
		var hit = nearest.Text.TextLayout.HitTestPoint(new Point(local.X - nearest.Text.Padding.Left, local.Y - nearest.Text.Padding.Top));
		return nearest.Start + Math.Clamp(hit.TextPosition, 0, nearest.Length);
	}

	private static IEnumerable<(SelectableTextBlock Text, int Start, int Length)> TextSegments(MarkdownSelectionBlock block)
	{
		var root = block.Control;
		var texts = root is SelectableTextBlock text ? new[] { text }.Concat(root.GetVisualDescendants().OfType<SelectableTextBlock>())
			: root.GetVisualDescendants().OfType<SelectableTextBlock>();
		var cursor = 0;
		foreach (var item in texts.Where(item => item.Tag as string != "CodeLineNumbers"))
		{
			var buffer = item.Inlines?.Text ?? item.Text ?? string.Empty;
			if (buffer.Length == 0) continue;
			var start = block.PlainText.IndexOf(buffer, cursor, StringComparison.Ordinal);
			if (start < 0) continue;
			cursor = start + buffer.Length;
			yield return (item, start, buffer.Length);
		}
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
			var segments = TextSegments(block).ToArray();
			if (segments.Length == 0) block.Control.Classes.Add(SelectedBlockClass);
			foreach (var segment in segments)
			{
				_selectedTexts.Add(segment.Text);
				segment.Text.SelectionStart = Math.Clamp((i == start ? from.Offset : 0) - segment.Start, 0, segment.Length);
				segment.Text.SelectionEnd = Math.Clamp((i == end ? to.Offset : block.PlainText.Length) - segment.Start, 0, segment.Length);
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
		foreach (var text in _selectedTexts) text.ClearSelection();
		_selectedTexts.Clear();

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
