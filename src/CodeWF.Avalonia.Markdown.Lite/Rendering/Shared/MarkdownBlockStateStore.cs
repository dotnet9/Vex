namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 按「块键（源码起始偏移）+ 名称」存放与块绑定的视图状态：折叠、换行、高亮会话等。
/// 状态归属 Viewer 而非控件，因此虚拟化回收、块重建、主题切换都不丢；
/// 整篇刷新时经 <see cref="Remap" /> 把键搬到新偏移，内容真变了的状态随之丢弃。
/// </summary>
internal sealed class MarkdownBlockStateStore
{
	public const string CodeBlockFolded = "CodeBlock.Folded";
	public const string CodeBlockWrapped = "CodeBlock.Wrapped";
	public const string CodeBlockHighlight = "CodeBlock.Highlight";

	private readonly Dictionary<StateKey, object?> _states = [];

	public int Count => _states.Count;

	public bool TryGet<T>(int blockKey, string name, out T value)
	{
		if (_states.TryGetValue(new StateKey(blockKey, name), out var stored) && stored is T typed)
		{
			value = typed;
			return true;
		}

		value = default!;
		return false;
	}

	public void Set<T>(int blockKey, string name, T value) =>
		_states[new StateKey(blockKey, name)] = value;

	/// <summary>取已有状态，没有时用工厂建一个并存入（高亮会话等可变对象）。</summary>
	public T GetOrAdd<T>(int blockKey, string name, Func<T> factory) where T : class
	{
		var key = new StateKey(blockKey, name);
		if (_states.TryGetValue(key, out var stored) && stored is T existing)
		{
			return existing;
		}

		var created = factory();
		_states[key] = created;
		return created;
	}

	public bool Remove(int blockKey, string name) => _states.Remove(new StateKey(blockKey, name));

	/// <summary>删除某个块的全部状态（块被真正替换时调用）。</summary>
	public void RemoveBlock(int blockKey)
	{
		var keys = _states.Keys.Where(key => key.BlockKey == blockKey).ToArray();
		foreach (var key in keys)
		{
			_states.Remove(key);
		}
	}

	public void Clear() => _states.Clear();

	/// <summary>
	/// 把每个键交给映射函数求新位置；映射不到（内容已变、块已删）的状态丢弃。
	/// 多个旧键落到同一新键时保留先到者，与折叠状态合并语义一致。
	/// </summary>
	public void Remap(Func<int, int?> map)
	{
		if (_states.Count == 0)
		{
			return;
		}

		var remapped = new Dictionary<StateKey, object?>(_states.Count);
		foreach (var (key, value) in _states)
		{
			if (map(key.BlockKey) is not { } newOffset)
			{
				continue;
			}

			remapped.TryAdd(new StateKey(newOffset, key.Name), value);
		}

		_states.Clear();
		foreach (var (key, value) in remapped)
		{
			_states[key] = value;
		}
	}

	private readonly record struct StateKey(int BlockKey, string Name);
}
