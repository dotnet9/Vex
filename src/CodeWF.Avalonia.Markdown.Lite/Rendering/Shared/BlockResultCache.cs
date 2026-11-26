namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

/// <summary>
/// 块视图重结果的指纹 LRU 缓存：按内容键（块指纹 + 影响结果的环境，如语言/主题）索引，
/// 跨文档共享、线程安全。块视图实例随文档切换重建后，同样内容的块直接命中而不必重算
/// （代码高亮 token、图表渲染、表格解析等）。容量默认 64，键未命中按序入列，超容量淘汰最旧。
/// 思路来自 StreamMark 的 BlockResultCache（MIT，© StreamMark contributors）。
/// </summary>
public sealed class BlockResultCache<TKey, TValue> where TKey : notnull
{
	private readonly int _capacity;
	private readonly Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>> _items;
	private readonly LinkedList<KeyValuePair<TKey, TValue>> _order = new();
	private readonly object _lock = new();

	public BlockResultCache(int capacity = 64)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
		_capacity = capacity;
		_items = new Dictionary<TKey, LinkedListNode<KeyValuePair<TKey, TValue>>>(capacity);
	}

	public int Count
	{
		get
		{
			lock (_lock)
			{
				return _items.Count;
			}
		}
	}

	/// <summary>取结果；命中时把它记为最近使用。</summary>
	public bool TryGet(TKey key, out TValue value)
	{
		lock (_lock)
		{
			if (_items.TryGetValue(key, out var node))
			{
				_order.Remove(node);
				_order.AddLast(node);
				value = node.Value.Value;
				return true;
			}
		}
		value = default!;
		return false;
	}

	/// <summary>写入或刷新结果（记为最近使用；超容量淘汰最旧）。</summary>
	public void Set(TKey key, TValue value)
	{
		lock (_lock)
		{
			if (_items.TryGetValue(key, out var node))
			{
				node.Value = new KeyValuePair<TKey, TValue>(key, value);
				_order.Remove(node);
				_order.AddLast(node);
				return;
			}
			var fresh = new LinkedListNode<KeyValuePair<TKey, TValue>>(new KeyValuePair<TKey, TValue>(key, value));
			if (_items.Count >= _capacity)
			{
				var oldest = _order.First!;
				_items.Remove(oldest.Value.Key);
				_order.RemoveFirst();
			}
			_items.Add(key, fresh);
			_order.AddLast(fresh);
		}
	}
}
