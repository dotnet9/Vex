namespace CodeWF.Avalonia.Markdown.Streaming;

/// <summary>
/// 流式冻结边界切分器（规格见 docs/流式规则.md §3）。
///
/// 只扫描新到达文本的完整行，保守推进 <see cref="FrozenBoundary"/>：边界之前的前缀，
/// 无论后续到达什么文本，解析结果（块序列的 kind + 源码 span + 内容哈希）都不会再变
/// （不变量 I4）。所有识别从保守方向犯错：误冻结会破坏等价性，漏冻结只损失性能。
/// </summary>
/// <remarks>
/// 线程模型：实例状态只允许单个工作者线程访问（<see cref="MarkdownStreamingDocument"/>
/// 的后台工作者），<see cref="Scan"/> 之外无共享可变状态。
/// </remarks>
public sealed class MarkdownStreamSegmenter
{
	private enum TopLevelState
	{
		Normal,
		Fence,
		Math,
		HtmlRaw,
	}

	private TopLevelState _state = TopLevelState.Normal;
	private char _fenceChar;
	private int _fenceLength;
	private string _htmlCloseToken = string.Empty;

	// 文档起始视同"空行之后"，使首行允许开启数学块/原始 HTML 块。
	private bool _prevLineBlank = true;

	// 上一行是否为段落延续行（setext 下划线判定、决定性新块判定用）。
	private bool _prevParagraphish;

	// 自上个空行以来是否开启了引用/列表容器。lazy continuation 会把容器开启后
	// 的顶层样式行继续吞进容器（如 `> a\nb\n===` 的 setext 仍属于引用），
	// 容器开启期间必须抑制 setext 与决定性冻结，结构块（围栏/标题/分隔线）
	// 也只在零缩进时识别（缩进 1-3 的结构行可能是容器内容）。
	private bool _containerOpen;

	// HTML Type 6 原始块开启中（到空行才结束）：期间一切结构行都并入块内，
	// 必须抑制全部顶层冻结判定。
	private bool _htmlBlockOpen;

	// 待判定的空行结束偏移：该空行是否可冻结取决于下一行的形态。
	private int _pendingBlankEnd = -1;

	// 已扫描到的偏移（完整行的行尾）。
	private int _scanned;

	/// <summary>当前冻结边界（原始源码偏移），单调不减。</summary>
	public int FrozenBoundary { get; private set; }

	/// <summary>切分器是否停留在未闭合围栏内（开放围栏尾部快速路径的命中条件之一）。</summary>
	public bool IsScanningFence => _state == TopLevelState.Fence;

	/// <summary>当前开放围栏的字符与开围栏长度（<see cref="IsScanningFence"/> 为 true 时有效）。</summary>
	public (char Char, int Length) OpenFence => (_fenceChar, _fenceLength);

	/// <summary>
	/// 扫描 <paramref name="source"/> 中上次扫描位置之后的完整行，推进冻结边界。
	/// </summary>
	/// <param name="source">完整源码缓冲（只追加）。</param>
	/// <param name="sourceComplete">
	/// 源码是否已结束（<see cref="MarkdownStreamingDocument.Complete"/> 之后为 true）；
	/// 未结束时末尾没有换行的最后一行不参与扫描（它还可能继续生长）。
	/// </param>
	public void Scan(string source, bool sourceComplete)
	{
		ArgumentNullException.ThrowIfNull(source);

		while (_scanned < source.Length)
		{
			var newline = source.IndexOf('\n', _scanned);
			var isLastLine = newline < 0;
			if (isLastLine && !sourceComplete)
			{
				// 未完成行：前缀具备决定性时把边界推进到行首（规格 §3.4.1），行本体留在尾部。
				TryFreezeBeforeIncompleteLine(source, _scanned);
				break;
			}

			var lineEnd = isLastLine ? source.Length : newline + 1;
			ProcessLine(source, _scanned, lineEnd);
			_scanned = lineEnd;
		}
	}

	/// <summary>重置全部状态（<see cref="MarkdownStreamingDocument.Reset"/> 时调用）。</summary>
	public void Reset()
	{
		_state = TopLevelState.Normal;
		_fenceChar = default;
		_fenceLength = 0;
		_htmlCloseToken = string.Empty;
		_prevLineBlank = true;
		_prevParagraphish = false;
		_containerOpen = false;
		_htmlBlockOpen = false;
		_pendingBlankEnd = -1;
		_scanned = 0;
		FrozenBoundary = 0;
	}

	private void ProcessLine(string source, int start, int end)
	{
		var line = source.Substring(start, end - start);

		// 行内状态优先：围栏/数学/原始 HTML 内部的空行与语法不产生任何冻结机会。
		switch (_state)
		{
			case TopLevelState.Fence:
				if (IsFenceClose(line))
				{
					_state = TopLevelState.Normal;
					Freeze(end);
				}

				return;
			case TopLevelState.Math:
				if (line.Contains("$$", StringComparison.Ordinal))
				{
					_state = TopLevelState.Normal;
					Freeze(end);
				}

				return;
			case TopLevelState.HtmlRaw:
				if (line.Contains(_htmlCloseToken, StringComparison.OrdinalIgnoreCase))
				{
					_state = TopLevelState.Normal;
					Freeze(end);
				}

				return;
		}

		var indent = CountIndent(line, out var rest);
		var trimmed = rest.TrimEnd();

		// 空行：冻结与否取决于下一行形态，挂起等待；空行同时关闭容器上下文。
		if (trimmed.Length == 0)
		{
			_pendingBlankEnd = end;
			_prevLineBlank = true;
			_prevParagraphish = false;
			_containerOpen = false;
			_htmlBlockOpen = false;
			return;
		}

		// 本行非空：先裁决挂起的空行能否冻结（边界 = 本行行首）。
		if (_pendingBlankEnd >= 0)
		{
			_pendingBlankEnd = -1;
			if (indent == 0 && IsSafeAfterBlank(trimmed))
			{
				Freeze(start);
			}
		}

		// 本行是否紧跟空行/文档起始（数学块与原始 HTML 块只在此时开启）。
		// 注意先捕获再清理：分类阶段之后 _prevLineBlank 服务于下一行。
		var followsBlank = _prevLineBlank;
		_prevLineBlank = false;

		// HTML Type 6 原始块开启期间：一切结构行（围栏/标题/分隔线/setext）
		// 都并入块内，禁止任何顶层冻结判定，空行规则兜底收尾。
		if (_htmlBlockOpen)
		{
			_prevParagraphish = true;
			return;
		}

		// 容器开启期间，顶层结构识别只认零缩进行（缩进 1-3 的行可能是容器内容）。
		var structuralIndentLimit = _containerOpen ? 0 : 3;
		var isStructuralIndent = indent <= structuralIndentLimit;

		// 围栏开启（围栏可打断段落与容器，行首必为新块起点）。
		if (isStructuralIndent && TryMatchFenceOpen(trimmed, out var fenceChar, out var fenceLength))
		{
			Freeze(start);
			_state = TopLevelState.Fence;
			_fenceChar = fenceChar;
			_fenceLength = fenceLength;
			_prevParagraphish = false;
			_containerOpen = false;
			return;
		}

		// ATX 标题不允许延续且可打断容器，行尾即可冻结。
		if (isStructuralIndent && IsAtxHeading(trimmed))
		{
			Freeze(end);
			_prevParagraphish = false;
			_containerOpen = false;
			return;
		}

		// Setext 下划线（段落延续时 =/- 拼成的行）：标题在此行完成。
		// 必须先于主题分隔线判定——`---` 在段落延续时是 setext 而非分隔线。
		// 容器开启期间禁止：lazy continuation 会把 setext 吞进容器
		// （`> a\nb\n===` 的 h2 仍属于引用块）。
		if (_prevParagraphish && !_containerOpen && indent <= 3 && IsSetextUnderline(trimmed))
		{
			Freeze(end);
			_prevParagraphish = false;
			return;
		}

		// 主题分隔线（可打断容器）。
		if (isStructuralIndent && IsThematicBreak(trimmed))
		{
			Freeze(end);
			_prevParagraphish = false;
			_containerOpen = false;
			return;
		}

		// 决定性新块起始（引用/列表打断段落）：行首之前的所有内容就此定型。
		// 仅在"上一行是段落延续且无容器"时适用——容器内的 lazy continuation
		// 仍属于容器（`> a\nb\n> c` 是同一个引用），松散列表同理。
		if (_prevParagraphish && !_containerOpen && indent <= 3 && IsDecisiveBlockStart(trimmed))
		{
			Freeze(start);
			_containerOpen = true;
			_prevParagraphish = false;
			return;
		}

		// 引用/列表行的非打断出现（完整块之后或容器延续）：开启容器上下文，不冻结。
		if (indent <= 3 && IsQuoteLine(trimmed))
		{
			_containerOpen = true;
			_prevParagraphish = false;
			return;
		}

		if (indent <= 3 && IsListItemLine(trimmed))
		{
			_containerOpen = true;
			_prevParagraphish = false;
			return;
		}

		// 数学块与原始 HTML 块不能打断段落，只在空行/文档起始后识别
		// （空行已关闭容器，此处 containerOpen 恒为 false）。
		if (followsBlank && indent <= structuralIndentLimit && IsMathOpen(trimmed, out var mathClosedInline))
		{
			if (mathClosedInline)
			{
				// 单行闭合的数学块，行尾即冻结。
				Freeze(end);
			}
			else
			{
				_state = TopLevelState.Math;
			}

			_prevParagraphish = false;
			return;
		}

		if (followsBlank && indent <= structuralIndentLimit && IsHtmlRawOpen(trimmed, out var htmlCloseToken, out var htmlClosedInline))
		{
			if (htmlClosedInline)
			{
				Freeze(end);
			}
			else
			{
				_state = TopLevelState.HtmlRaw;
				_htmlCloseToken = htmlCloseToken;
			}

			_prevParagraphish = false;
			return;
		}

		if (followsBlank && indent <= structuralIndentLimit && IsHtmlBlockStart(trimmed))
		{
			// HTML 原始块 Type 6：到空行结束，期间抑制全部顶层冻结判定。
			_htmlBlockOpen = true;
			_prevParagraphish = false;
			return;
		}

		// 其余都是段落文本（含表格行、定义行、lazy continuation）。
		_prevParagraphish = true;
	}

	private void Freeze(int offset)
	{
		if (offset > FrozenBoundary)
		{
			FrozenBoundary = offset;
		}
	}

	/// <summary>
	/// 未完成行的提前冻结（规格 §3.4.1）：前缀已具备决定性时把边界推进到该行行首。
	/// 判定只用已到达字符且追加文本不可能推翻；不满足时不动边界（少冻结无损害）。
	/// </summary>
	private void TryFreezeBeforeIncompleteLine(string source, int lineStart)
	{
		if (_state != TopLevelState.Normal || _htmlBlockOpen)
		{
			return;
		}

		// 全空白前缀缩进未知（可能是缩进代码续行），等首个非空白字符。
		var indent = 0;
		var index = lineStart;
		while (index < source.Length)
		{
			var c = source[index];
			if (c == ' ')
			{
				indent++;
				index++;
			}
			else if (c == '\t')
			{
				indent += 4 - (indent % 4);
				index++;
			}
			else
			{
				break;
			}
		}

		if (index >= source.Length)
		{
			return;
		}

		var prefix = source[index..];

		// E1：挂起空行 + 零缩进 + 安全新块前缀。
		if (_pendingBlankEnd >= 0)
		{
			if (indent == 0 && IsSafeAfterBlankPrefix(prefix))
			{
				Freeze(lineStart);
			}

			return;
		}

		// E2：段落延续中的决定性新块起始（容器抑制与完整行规则一致）。
		if (_prevParagraphish && !_containerOpen && indent <= 3 && IsDecisiveBlockStartPrefix(prefix))
		{
			Freeze(lineStart);
		}
	}

	/// <summary>
	/// 空行规则的前缀版（对照 <see cref="IsSafeAfterBlank"/>）：数字序列未见到
	/// 非数字字符前不判定（可能是任意段落文本或更长的标记前缀）。
	/// </summary>
	private static bool IsSafeAfterBlankPrefix(string prefix)
	{
		var first = prefix[0];
		if (first is '>' or '[' or ':')
		{
			return false;
		}

		if (first is '-' or '*' or '+')
		{
			return prefix.Length > 1 && prefix[1] is not (' ' or '\t');
		}

		if (first is >= '0' and <= '9')
		{
			var digits = 0;
			while (digits < prefix.Length && prefix[digits] is >= '0' and <= '9')
			{
				digits++;
			}

			if (digits == prefix.Length)
			{
				return false;
			}

			if (prefix[digits] is '.' or ')'
				&& (digits + 1 == prefix.Length || prefix[digits + 1] is ' ' or '\t'))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	/// 决定性新块起始的前缀版（对照 <see cref="IsDecisiveBlockStart"/>）：
	/// 不完整的 `-`/`--`/`===` 可能是 setext 下划线（改写上一段落为标题），
	/// 不满足完整起始形态的 `*`/`` ` ``/`~`/`#` 可能只是段落延续，一律不冻结。
	/// </summary>
	private static bool IsDecisiveBlockStartPrefix(string prefix)
	{
		switch (prefix[0])
		{
			case '>':
				// `>` 行无论后续是什么都是引用起始，不可能回退为段落延续。
				return prefix.Length == 1 || prefix[1] is ' ' or '\t' or '>';
			case '-' or '*' or '+':
				// 需要已见空白（列表标记）：`-` 全横线可能是 setext，`*x` 可能是强调延续。
				return prefix.Length >= 2 && prefix[1] is ' ' or '\t';
			case '1':
				return prefix.Length >= 3 && prefix[1] is '.' or ')' && prefix[2] is ' ' or '\t';
			case '`' or '~':
				return TryMatchFenceOpen(prefix, out _, out _);
			case '#':
			{
				var level = 0;
				while (level < prefix.Length && prefix[level] == '#')
				{
					level++;
				}

				return level is >= 1 and <= 6
					&& level < prefix.Length
					&& prefix[level] is ' ' or '\t';
			}

			default:
				return false;
		}
	}

	/// <summary>
	/// 空行之后跟本行（零缩进）是否可冻结：本行必须开启一个全新顶层块。
	/// 调用方保证 <paramref name="trimmed"/> 无前导空白（缩进行不可冻结）。
	/// </summary>
	private static bool IsSafeAfterBlank(string trimmed)
	{
		var first = trimmed[0];

		// 引用（`> a\n\n> b` 是同一个引用块）、链接引用定义/脚注定义（`[`）、
		// 定义列表（`:`）都不能跨越空行拆分。
		if (first is '>' or '[' or ':')
		{
			return false;
		}

		// 列表项（含松散列表与任意序号的有序列表）：空行后的列表项可能
		// 仍属于前一个列表，保守不冻结。
		if (first is '-' or '*' or '+')
		{
			return trimmed.Length > 1 && trimmed[1] is not (' ' or '\t');
		}

		if (first is >= '0' and <= '9')
		{
			var digits = 0;
			while (digits < trimmed.Length && trimmed[digits] is >= '0' and <= '9')
			{
				digits++;
			}

			if (digits < trimmed.Length
				&& trimmed[digits] is '.' or ')'
				&& (digits + 1 == trimmed.Length || trimmed[digits + 1] is ' ' or '\t'))
			{
				return false;
			}
		}

		return true;
	}

	private static int CountIndent(string line, out string rest)
	{
		var indent = 0;
		var index = 0;
		while (index < line.Length)
		{
			var c = line[index];
			if (c == ' ')
			{
				indent++;
				index++;
			}
			else if (c == '\t')
			{
				indent += 4 - (indent % 4);
				index++;
			}
			else
			{
				break;
			}
		}

		rest = line[index..];
		return indent;
	}

	private static bool TryMatchFenceOpen(string trimmed, out char fenceChar, out int fenceLength)
	{
		fenceChar = default;
		fenceLength = 0;
		if (trimmed.Length < 3)
		{
			return false;
		}

		var candidate = trimmed[0];
		if (candidate is not ('`' or '~'))
		{
			return false;
		}

		var run = 1;
		while (run < trimmed.Length && trimmed[run] == candidate)
		{
			run++;
		}

		if (run < 3)
		{
			return false;
		}

		// 反引号围栏的 info string 不允许再出现反引号。
		if (candidate == '`' && trimmed[run..].Contains('`'))
		{
			return false;
		}

		fenceChar = candidate;
		fenceLength = run;
		return true;
	}

	private bool IsFenceClose(string line)
	{
		var indent = CountIndent(line, out var rest);
		if (indent > 3)
		{
			return false;
		}

		var run = 0;
		while (run < rest.Length && rest[run] == _fenceChar)
		{
			run++;
		}

		return run >= _fenceLength && rest[run..].Trim().Length == 0;
	}

	private static bool IsAtxHeading(string trimmed)
	{
		var level = 0;
		while (level < trimmed.Length && trimmed[level] == '#')
		{
			level++;
		}

		return level is >= 1 and <= 6
			&& (level == trimmed.Length || trimmed[level] is ' ' or '\t');
	}

	private static bool IsSetextUnderline(string trimmed)
	{
		if (trimmed.Length == 0)
		{
			return false;
		}

		var c = trimmed[0];
		if (c is not ('=' or '-'))
		{
			return false;
		}

		return trimmed.AsSpan().IndexOfAnyExcept(c) < 0;
	}

	private static bool IsThematicBreak(string trimmed)
	{
		var candidate = default(char);
		var count = 0;
		foreach (var c in trimmed)
		{
			if (c is ' ' or '\t')
			{
				continue;
			}

			if (c is not ('-' or '*' or '_'))
			{
				return false;
			}

			if (candidate == default)
			{
				candidate = c;
			}
			else if (c != candidate)
			{
				return false;
			}

			count++;
		}

		return count >= 3;
	}

	private static bool IsDecisiveBlockStart(string trimmed)
	{
		switch (trimmed[0])
		{
			case '>':
				// `>`、`> `、`>>` 都是引用行起始。
				return trimmed.Length == 1 || trimmed[1] is ' ' or '\t' or '>';
			case '-' or '*' or '+':
				return trimmed.Length == 1 || trimmed[1] is ' ' or '\t';
			case '1':
				return trimmed.Length >= 2
					&& trimmed[1] is '.' or ')'
					&& (trimmed.Length == 2 || trimmed[2] is ' ' or '\t');
			default:
				return false;
		}
	}

	private static bool IsQuoteLine(string trimmed)
	{
		return trimmed[0] == '>';
	}

	private static bool IsListItemLine(string trimmed)
	{
		switch (trimmed[0])
		{
			case '-' or '*' or '+':
				return trimmed.Length == 1 || trimmed[1] is ' ' or '\t';
			case >= '0' and <= '9':
			{
				var digits = 0;
				while (digits < trimmed.Length && trimmed[digits] is >= '0' and <= '9')
				{
					digits++;
				}

				return digits < trimmed.Length
					&& trimmed[digits] is '.' or ')'
					&& (digits + 1 == trimmed.Length || trimmed[digits + 1] is ' ' or '\t');
			}

			default:
				return false;
		}
	}

	private static bool IsMathOpen(string trimmed, out bool closedInline)
	{
		closedInline = false;
		if (!trimmed.StartsWith("$$", StringComparison.Ordinal))
		{
			return false;
		}

		closedInline = trimmed.IndexOf("$$", 2, StringComparison.Ordinal) >= 0;
		return true;
	}

	private static bool IsHtmlRawOpen(string trimmed, out string closeToken, out bool closedInline)
	{
		closeToken = string.Empty;
		closedInline = false;

		// 只在行首识别（调用方保证空行之后）；注释/处理指令/Type1 标签。
		if (trimmed.StartsWith("<!--", StringComparison.Ordinal))
		{
			closeToken = "-->";
		}
		else if (trimmed.StartsWith("<?", StringComparison.Ordinal))
		{
			closeToken = "?>";
		}
		else
		{
			foreach (var tag in (ReadOnlySpan<string>)["<script", "<style", "<pre", "<textarea"])
			{
				if (trimmed.StartsWith(tag, StringComparison.OrdinalIgnoreCase)
					&& (trimmed.Length == tag.Length || trimmed[tag.Length] is ' ' or '\t' or '>'))
				{
					closeToken = "</" + tag[1..] + ">";
					break;
				}
			}

			if (closeToken.Length == 0)
			{
				return false;
			}
		}

		closedInline = trimmed.Contains(closeToken, StringComparison.OrdinalIgnoreCase);
		return true;
	}

	private static bool IsHtmlBlockStart(string trimmed)
	{
		// HTML 块 Type 6 的粗略识别：`<` + 字母。保守方向正确——
		// 误判成"HTML 块"只是少冻结（该行不计为段落文本），不会误冻结。
		return trimmed[0] == '<' && trimmed.Length > 1 && char.IsAsciiLetter(trimmed[1]);
	}
}
