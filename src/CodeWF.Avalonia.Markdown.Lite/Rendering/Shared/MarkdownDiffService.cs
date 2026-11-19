namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

public readonly record struct MarkdownRenderDiff(
	bool RequiresFullRender,
	int ReplaceStartIndex,
	int ReplaceEndIndex,
	int NewStartIndex,
	int NewEndIndex)
{
	public int OldRemoveCount => Math.Max(0, ReplaceEndIndex - ReplaceStartIndex);
	public int NewInsertCount => Math.Max(0, NewEndIndex - NewStartIndex);

	public static MarkdownRenderDiff Full { get; } = new(true, 0, 0, 0, 0);
	public static MarkdownRenderDiff NoChange { get; } = new(false, 0, 0, 0, 0);
}

public static class MarkdownDiffService
{
	private const int LargeDocumentThreshold = 4096;

	public static MarkdownRenderDiff Compare(MarkdownDocumentModel oldModel, MarkdownDocumentModel newModel)
	{
		if (ReferenceEquals(oldModel, newModel) || oldModel.Source == newModel.Source)
		{
			return MarkdownRenderDiff.NoChange;
		}

		if (oldModel.Blocks.Count == 0 || newModel.Blocks.Count == 0)
		{
			return MarkdownRenderDiff.Full;
		}

		if (ShouldFullRenderByTextChange(oldModel.Source, newModel.Source))
		{
			return MarkdownRenderDiff.Full;
		}

		if (HasGlobalDependencyRisk(oldModel, newModel))
		{
			return MarkdownRenderDiff.Full;
		}

		var prefix = 0;
		while (prefix < oldModel.Blocks.Count
			   && prefix < newModel.Blocks.Count
			   && CanReuse(oldModel.Blocks[prefix], newModel.Blocks[prefix]))
		{
			prefix++;
		}

		var oldSuffix = oldModel.Blocks.Count - 1;
		var newSuffix = newModel.Blocks.Count - 1;
		while (oldSuffix >= prefix
			   && newSuffix >= prefix
			   && CanReuse(oldModel.Blocks[oldSuffix], newModel.Blocks[newSuffix]))
		{
			oldSuffix--;
			newSuffix--;
		}

		var oldChangedCount = oldSuffix - prefix + 1;
		var newChangedCount = newSuffix - prefix + 1;
		if (oldChangedCount < 0 && newChangedCount < 0)
		{
			return MarkdownRenderDiff.NoChange;
		}

		if (ShouldFullRenderByBlockChange(oldModel.Blocks.Count, newModel.Blocks.Count, oldChangedCount, newChangedCount))
		{
			return MarkdownRenderDiff.Full;
		}

		return new MarkdownRenderDiff(
			false,
			prefix,
			oldSuffix + 1,
			prefix,
			newSuffix + 1);
	}

	/// <summary>
	/// 叠加脏区间比较：变化集中在 <paramref name="dirtySpan"/> 内时，用窗口外块的
	/// 索引一致性（变化区间之外的文本逐字节相同，块索引与内容必然一致）证明可以
	/// 局部替换，避免长文一次局部编辑触发整篇全量渲染。
	/// 任一校验不通过（围栏块跨界、引用定义漂移、全局依赖变化等）返回 null，
	/// 由调用方降级到 <see cref="Compare"/> / 全量渲染。
	/// </summary>
	public static MarkdownRenderDiff? TryCompareWithinDirtySpan(
		MarkdownDocumentModel oldModel,
		MarkdownDocumentModel newModel,
		MarkdownTextSpan dirtySpan)
	{
		if (ReferenceEquals(oldModel, newModel) || oldModel.Source == newModel.Source)
		{
			return MarkdownRenderDiff.NoChange;
		}

		if (oldModel.Blocks.Count == 0 || newModel.Blocks.Count == 0 || dirtySpan.Length == 0)
		{
			return null;
		}

		if (HasGlobalDependencyRisk(oldModel, newModel))
		{
			return null;
		}

		var oldWindowStart = FindFirstBlockIndexAtOrAfter(oldModel, dirtySpan.Start);
		var oldWindowEnd = FindFirstBlockIndexAfter(oldModel, dirtySpan.End);
		if (oldWindowStart > oldWindowEnd)
		{
			return null;
		}

		var prefix = 0;
		while (prefix < oldWindowStart
			   && prefix < oldModel.Blocks.Count
			   && prefix < newModel.Blocks.Count
			   && CanReuse(oldModel.Blocks[prefix], newModel.Blocks[prefix]))
		{
			prefix++;
		}

		if (prefix < oldWindowStart)
		{
			return null;
		}

		var requiredSuffix = oldModel.Blocks.Count - oldWindowEnd;
		var suffix = 0;
		var maxSuffix = Math.Min(oldModel.Blocks.Count, newModel.Blocks.Count) - prefix;
		while (suffix < maxSuffix - requiredSuffix
			   && CanReuse(
				   oldModel.Blocks[oldModel.Blocks.Count - suffix - 1],
				   newModel.Blocks[newModel.Blocks.Count - suffix - 1]))
		{
			suffix++;
		}

		// 窗口之后的旧块必须整体被后缀复用，否则说明变化影响到了窗口之外，降级全量。
		if (suffix != requiredSuffix)
		{
			return null;
		}

		// 块首行哈希校验：窗口两侧的邻接块首行必须一致，否则复用键不可信。
		if (prefix > 0
			&& newModel.Blocks.Count > prefix
			&& GetFirstLineHash(oldModel.Blocks[prefix - 1]) != GetFirstLineHash(newModel.Blocks[prefix - 1]))
		{
			return null;
		}

		if (requiredSuffix > 0
			&& newModel.Blocks.Count - requiredSuffix - 1 >= prefix
			&& GetFirstLineHash(oldModel.Blocks[oldModel.Blocks.Count - requiredSuffix])
			   != GetFirstLineHash(newModel.Blocks[newModel.Blocks.Count - requiredSuffix]))
		{
			return null;
		}

		var oldReplaceEnd = oldModel.Blocks.Count - suffix;
		var newReplaceEnd = newModel.Blocks.Count - suffix;
		if (oldReplaceEnd < prefix || newReplaceEnd < prefix)
		{
			return null;
		}

		if (oldReplaceEnd == prefix && newReplaceEnd == prefix)
		{
			return MarkdownRenderDiff.NoChange;
		}

		return new MarkdownRenderDiff(false, prefix, oldReplaceEnd, prefix, newReplaceEnd);
	}

	/// <summary>
	/// 块首行哈希：用于容忍纯插入场景下的块定位（首行未变即视为同一段落的锚点）。
	/// </summary>
	public static ulong GetFirstLineHash(MarkdownDocumentBlock block)
	{
		var source = block.SourceText;
		var length = source.Length;
		var lineEnd = source.IndexOf('\n');
		if (lineEnd >= 0)
		{
			length = lineEnd;
		}

		var hash = 14695981039346656037UL;
		for (var i = 0; i < length; i++)
		{
			hash = (hash ^ source[i]) * 1099511628211UL;
		}

		return hash;
	}

	public static bool CanReuse(MarkdownDocumentBlock oldBlock, MarkdownDocumentBlock newBlock)
	{
		// 流式快照的冻结块跨快照共享同一实例（规格 §2 对象共享），引用相同直接短路：
		// 未变更前缀的比较成本从 O(总块数) 次哈希比较降到 O(新增块数)。
		return ReferenceEquals(oldBlock, newBlock)
			   || (oldBlock.ContentHash == newBlock.ContentHash
				   && oldBlock.Kind == newBlock.Kind
				   && oldBlock.DependencyFlags == newBlock.DependencyFlags);
	}

	private static int FindFirstBlockIndexAtOrAfter(MarkdownDocumentModel model, int offset)
	{
		for (var i = 0; i < model.Blocks.Count; i++)
		{
			if (model.Blocks[i].SourceSpan.End >= offset)
			{
				return i;
			}
		}

		return model.Blocks.Count;
	}

	private static int FindFirstBlockIndexAfter(MarkdownDocumentModel model, int offset)
	{
		for (var i = 0; i < model.Blocks.Count; i++)
		{
			if (model.Blocks[i].SourceSpan.Start > offset)
			{
				return i;
			}
		}

		return model.Blocks.Count;
	}

	private static bool HasGlobalDependencyRisk(MarkdownDocumentModel oldModel, MarkdownDocumentModel newModel)
	{
		if ((oldModel.DependencyFlags & MarkdownDependencyFlags.Global)
			!= (newModel.DependencyFlags & MarkdownDependencyFlags.Global))
		{
			return true;
		}

		if ((oldModel.DependencyFlags & MarkdownDependencyFlags.Global) == 0)
		{
			return false;
		}

		var oldGlobalHash = CombineGlobalHash(oldModel);
		var newGlobalHash = CombineGlobalHash(newModel);
		return oldGlobalHash != newGlobalHash;
	}

	private static ulong CombineGlobalHash(MarkdownDocumentModel model)
	{
		var hash = 14695981039346656037UL;
		foreach (var block in model.Blocks.Where(block => block.HasGlobalDependency))
		{
			hash ^= block.ContentHash;
			hash *= 1099511628211UL;
		}

		return hash;
	}

	private static bool ShouldFullRenderByBlockChange(int oldCount, int newCount, int oldChangedCount, int newChangedCount)
	{
		var maxCount = Math.Max(oldCount, newCount);
		if (maxCount <= 0)
		{
			return true;
		}

		var changedCount = Math.Max(oldChangedCount, newChangedCount);
		return maxCount > 80 && changedCount > Math.Max(80, maxCount * 9 / 10);
	}

	private static bool ShouldFullRenderByTextChange(string oldText, string newText)
	{
		var change = CalculateTextChange(oldText, newText);
		var preservedLength = change.OldStart + oldText.Length - change.OldEnd;
		if (oldText.Length > 0 && preservedLength < oldText.Length / 2)
		{
			return true;
		}

		var newChangedLength = change.NewEnd - change.NewStart;
		return newText.Length > LargeDocumentThreshold
			   && newChangedLength > Math.Max(LargeDocumentThreshold, newText.Length * 9 / 10);
	}

	private static TextChange CalculateTextChange(string oldText, string newText)
	{
		var prefixLength = 0;
		var minLength = Math.Min(oldText.Length, newText.Length);
		while (prefixLength < minLength && oldText[prefixLength] == newText[prefixLength])
		{
			prefixLength++;
		}

		var suffixLength = 0;
		while (suffixLength < oldText.Length - prefixLength
			   && suffixLength < newText.Length - prefixLength
			   && oldText[oldText.Length - suffixLength - 1] == newText[newText.Length - suffixLength - 1])
		{
			suffixLength++;
		}

		return new TextChange(prefixLength, oldText.Length - suffixLength, prefixLength, newText.Length - suffixLength);
	}

	private readonly record struct TextChange(int OldStart, int OldEnd, int NewStart, int NewEnd);
}
