namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 大纲章节序号（1、1.1、1.1.1）：按出现顺序逐级计数，进入浅层时清除更深层计数。
/// 跳级时中间级以 0 占位（如 H1 直接到 H3 得 1.0.1），与两宿主既有算法一致；
/// 序号只用于展示，正文未手写序号的文章由宿主决定是否显示。
/// </summary>
public static class MarkdownOutlineNumbering
{
	/// <summary>返回逐条带上 <see cref="MarkdownOutlineItem.Number"/> 的新列表；原列表元素不变。</summary>
	public static IReadOnlyList<MarkdownOutlineItem> WithNumbers(this IReadOnlyList<MarkdownOutlineItem> items)
	{
		if (items.Count == 0)
		{
			return items;
		}

		var counters = new int[6];
		var results = new List<MarkdownOutlineItem>(items.Count);
		foreach (var item in items)
		{
			var level = Math.Clamp(item.Level, 1, 6);
			counters[level - 1]++;
			Array.Clear(counters, level, 6 - level);
			results.Add(item with { Number = string.Join('.', counters.Take(level)) });
		}

		return results;
	}
}
