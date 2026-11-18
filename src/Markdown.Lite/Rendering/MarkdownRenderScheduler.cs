using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// 版本化文本快照：<see cref="Version"/> 单调递增，用于丢弃过期解析结果。
/// </summary>
public sealed record MarkdownTextSnapshot(int Version, string Text)
{
	public static MarkdownTextSnapshot Empty { get; } = new(0, string.Empty);
}

/// <summary>
/// 一次解析完成的结果。<see cref="DirtySpanValid"/> 为 true 时表示
/// <see cref="DirtySpan"/> 是可信的变更区间，可交给差异服务做局部替换。
/// </summary>
public sealed record MarkdownParseResult(
	MarkdownTextSnapshot Snapshot,
	MarkdownDocumentModel Model,
	MarkdownTextSpan DirtySpan,
	bool DirtySpanValid,
	TimeSpan Duration,
	bool IsFirstParse);

/// <summary>
/// 渲染调度器：Markdown 文本变化只登记「待处理快照」，合并窗口内的多次变更
/// （连续输入、粘贴）折叠成一次解析；解析在后台线程执行，完成后回到创建线程，
/// 结果版本落后于当前版本即丢弃。
/// </summary>
public sealed class MarkdownRenderScheduler
{
	private readonly Func<string, MarkdownDocumentModel> _parse;
	private readonly TimeSpan _coalesceWindow;
	private readonly SynchronizationContext? _synchronizationContext;

	private readonly object _gate = new();
	private string _text = string.Empty;
	private int _version;
	private MarkdownTextSpan _dirtySpan;
	private bool _hasDirtySpan;
	private int _parsedVersion = -1;
	private bool _isParsing;
	private bool _reparseRequested;
	private bool _isCoalescing;
	private CancellationTokenSource? _coalesceCts;

	public MarkdownRenderScheduler(
		Func<string, MarkdownDocumentModel> parse,
		TimeSpan? coalesceWindow = null)
	{
		_parse = parse ?? throw new ArgumentNullException(nameof(parse));
		_coalesceWindow = coalesceWindow ?? TimeSpan.FromMilliseconds(70);
		_synchronizationContext = SynchronizationContext.Current;
	}

	/// <summary>当前待处理快照（最后登记文本与其版本）。</summary>
	public MarkdownTextSnapshot Current => new(_version, _text);

	/// <summary>最近一次成功产出的结果版本；-1 表示尚未产出。</summary>
	public int ParsedVersion => _parsedVersion;

	/// <summary>解析完成（仅在版本为最新时触发）。</summary>
	public event EventHandler<MarkdownParseResult>? Completed;

	/// <summary>解析失败；版本不推进，下一次文本变更会重新尝试。</summary>
	public event EventHandler<Exception>? Failed;

	/// <summary>
	/// 登记一次文本变更；同一合并窗口内的多次调用只触发一次解析。
	/// </summary>
	public void Queue(string? markdown)
	{
		var newText = markdown ?? string.Empty;
		MarkdownTextSpan span;
		lock (_gate)
		{
			if (string.Equals(newText, _text, StringComparison.Ordinal))
			{
				return;
			}

			span = CalculateDirtySpan(_text, newText);
			_text = newText;
			_version++;
			_dirtySpan = _hasDirtySpan ? Union(_dirtySpan, span) : span;
			_hasDirtySpan = true;
			_isCoalescing = true;
		}

		RestartCoalesceWindow();
	}

	/// <summary>取消待处理的合并窗口与后续解析（宿主卸载时调用）。</summary>
	public void Cancel()
	{
		lock (_gate)
		{
			_coalesceCts?.Cancel();
			_coalesceCts = null;
			_isCoalescing = false;
			_reparseRequested = false;
		}
	}

	/// <summary>同步解析指定文本，用于首帧渲染与耗时统计，不参与版本门控。</summary>
	public MarkdownDocumentModel ParseNow(string? markdown, out TimeSpan duration)
	{
		var start = System.Diagnostics.Stopwatch.GetTimestamp();
		var model = _parse(markdown ?? string.Empty);
		duration = System.Diagnostics.Stopwatch.GetElapsedTime(start);
		if (markdown is not null)
		{
			lock (_gate)
			{
				_text = markdown;
				_version++;
				_parsedVersion = _version;
				_hasDirtySpan = false;
			}
		}

		return model;
	}

	/// <summary>
	/// 计算两段文本之间发生变化的区间（新文本坐标）。相同则返回空区间。
	/// </summary>
	public static MarkdownTextSpan CalculateDirtySpan(string? oldText, string? newText)
	{
		var oldValue = oldText ?? string.Empty;
		var newValue = newText ?? string.Empty;
		if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
		{
			return new MarkdownTextSpan(0, 0);
		}

		var prefix = 0;
		var minLength = Math.Min(oldValue.Length, newValue.Length);
		while (prefix < minLength && oldValue[prefix] == newValue[prefix])
		{
			prefix++;
		}

		var suffix = 0;
		while (suffix < oldValue.Length - prefix
			   && suffix < newValue.Length - prefix
			   && oldValue[oldValue.Length - suffix - 1] == newValue[newValue.Length - suffix - 1])
		{
			suffix++;
		}

		var end = newValue.Length - suffix;
		return new MarkdownTextSpan(prefix, Math.Max(prefix, end));
	}

	private void RestartCoalesceWindow()
	{
		CancellationTokenSource cts;
		lock (_gate)
		{
			_coalesceCts?.Cancel();
			_coalesceCts?.Dispose();
			cts = new CancellationTokenSource();
			_coalesceCts = cts;
		}

		_ = CoalesceAsync(cts);
	}

	private async Task CoalesceAsync(CancellationTokenSource cts)
	{
		try
		{
			if (_coalesceWindow > TimeSpan.Zero)
			{
				await Task.Delay(_coalesceWindow, cts.Token).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException)
		{
			return;
		}
		finally
		{
			lock (_gate)
			{
				if (ReferenceEquals(_coalesceCts, cts))
				{
					_coalesceCts = null;
					_isCoalescing = false;
				}
			}

			cts.Dispose();
		}

		lock (_gate)
		{
			if (_isCoalescing)
			{
				return;
			}
		}

		await ParsePendingAsync().ConfigureAwait(false);
	}

	private async Task ParsePendingAsync()
	{
		string text;
		int version;
		MarkdownTextSpan dirtySpan;
		bool hasDirtySpan;
		bool isFirstParse;
		lock (_gate)
		{
			if (_isParsing || _version == _parsedVersion)
			{
				return;
			}

			text = _text;
			version = _version;
			dirtySpan = _dirtySpan;
			hasDirtySpan = _hasDirtySpan;
			isFirstParse = _parsedVersion < 0;
			_dirtySpan = default;
			_hasDirtySpan = false;
			_isParsing = true;
		}

		MarkdownDocumentModel? model = null;
		TimeSpan duration = TimeSpan.Zero;
		Exception? failure = null;
		try
		{
			var start = System.Diagnostics.Stopwatch.GetTimestamp();
			model = await Task.Run(() => _parse(text)).ConfigureAwait(false);
			duration = System.Diagnostics.Stopwatch.GetElapsedTime(start);
		}
		catch (Exception ex)
		{
			failure = ex;
		}

		var isCurrent = false;
		lock (_gate)
		{
			_isParsing = false;
			if (model is not null && version == _version)
			{
				_parsedVersion = version;
				isCurrent = true;
			}
			else if (version != _version)
			{
				_reparseRequested = true;
			}
		}

		if (failure is not null)
		{
			// 解析失败不更新版本，下一次文本变更会重新触发；异常交给宿主处理。
			Post(() => Failed?.Invoke(this, failure));
			return;
		}

		if (isCurrent && model is not null)
		{
			var result = new MarkdownParseResult(
				new MarkdownTextSnapshot(version, text),
				model,
				dirtySpan,
				hasDirtySpan,
				duration,
				isFirstParse);
			Post(() => Completed?.Invoke(this, result));
		}

		var shouldRestart = false;
		lock (_gate)
		{
			if (_reparseRequested)
			{
				_reparseRequested = false;
				shouldRestart = true;
			}
		}

		if (shouldRestart)
		{
			await ParsePendingAsync().ConfigureAwait(false);
		}
	}

	private void Post(Action action)
	{
		if (_synchronizationContext is { } context)
		{
			context.Post(static state => ((Action)state!).Invoke(), action);
			return;
		}

		action();
	}

	private static MarkdownTextSpan Union(MarkdownTextSpan first, MarkdownTextSpan second)
	{
		return new MarkdownTextSpan(
			Math.Min(first.Start, second.Start),
			Math.Max(first.End, second.End));
	}
}
