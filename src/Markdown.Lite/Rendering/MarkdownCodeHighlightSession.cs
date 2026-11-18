namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 代码块高亮行级缓存会话：流式追加时代码只增长尾部，逐行缓存 token 与
/// 词法状态，文本未变的行直接复用上一快照的结果，只重算分叉点之后的行。
///
/// 与具体高亮器解耦：token 是高亮器自定义的不透明对象（<see cref="TokenizeLineHandler"/>
/// 的返回值），会话只负责行文本比对与状态链维护——第 i 行的 token 由第 i-1 行的
/// 结束状态派生，因此前缀行未变时整条状态链有效，后缀整体失效重算。
/// <see cref="Tag"/> 供高亮器挂缓存有效性键（如语言+主题），键变化时调用方应先
/// <see cref="Reset"/>。
///
/// 非线程安全：只在渲染线程使用。行拆分与 CRLF 归一化和默认高亮器一致
/// （CR/LF 归一为 LF，按 LF 拆行；空串视为一行）。
/// </summary>
public sealed class MarkdownCodeHighlightSession
{
	/// <summary>
	/// 词法分析单行委托：<paramref name="startState"/> 为上一行结束状态（首行为
	/// null），返回该行 token（不透明对象），<paramref name="endState"/> 输出该行
	/// 结束状态供下一行使用。
	/// </summary>
	public delegate object? TokenizeLineHandler(string line, object? startState, out object? endState);

	private sealed record LineEntry(string Text, object Tokens, object? EndState);

	private readonly List<LineEntry> _entries = [];

	/// <summary>缓存有效性键（高亮器自行赋值，如语言+主题组合）；变更后调用方须 Reset。</summary>
	public object? Tag { get; set; }

	/// <summary>当前缓存的行数。</summary>
	public int CachedLineCount => _entries.Count;

	/// <summary>
	/// 增量更新：返回 <paramref name="code"/> 每行的 token（与行序一一对应）。
	/// 与上次调用公共前缀内的行复用缓存，分叉行起调用 <paramref name="tokenizeLine"/>
	/// 重算；重算首行以上一保留行的结束状态为起点，保证跨行状态（块注释等）延续。
	/// </summary>
	public IReadOnlyList<object> Update(string code, TokenizeLineHandler tokenizeLine)
	{
		ArgumentNullException.ThrowIfNull(code);
		ArgumentNullException.ThrowIfNull(tokenizeLine);

		var lines = code.Replace("\r\n", "\n").Split('\n');
		var result = new object[lines.Length];

		var reuseCount = 0;
		var limit = Math.Min(_entries.Count, lines.Length);
		while (reuseCount < limit && _entries[reuseCount].Text == lines[reuseCount])
		{
			reuseCount++;
		}

		for (var i = 0; i < reuseCount; i++)
		{
			result[i] = _entries[i].Tokens;
		}

		if (_entries.Count > reuseCount)
		{
			_entries.RemoveRange(reuseCount, _entries.Count - reuseCount);
		}

		// 重算起点状态：有保留前缀时取其末行结束状态（跨行状态链延续），否则 null。
		var startState = reuseCount > 0 ? _entries[reuseCount - 1].EndState : null;
		for (var i = reuseCount; i < lines.Length; i++)
		{
			var tokens = tokenizeLine(lines[i], startState, out var endState)
				?? throw new InvalidOperationException(MarkdownLocalization.GetResource(MarkdownL.HighlightNullTokens));
			startState = endState;
			_entries.Add(new LineEntry(lines[i], tokens, endState));
			result[i] = tokens;
		}

		return result;
	}

	/// <summary>丢弃全部缓存行（语言/主题变更或复用语义失效时调用）。</summary>
	public void Reset() => _entries.Clear();
}
