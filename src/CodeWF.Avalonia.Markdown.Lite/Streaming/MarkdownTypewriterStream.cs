using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

using global::Avalonia.Threading;

namespace CodeWF.Avalonia.Markdown.Streaming;

/// <summary>
/// 面向 LLM 流式接入的打字机包装：宿主在任意线程把到达的 delta 喂给
/// <see cref="AppendDelta"/>，内部经 <see cref="MarkdownStreamPacer"/> 在 UI 帧
/// 上平滑释放进目标 <see cref="MarkdownStreamingDocument"/>；<see cref="Finish"/>
/// 后排空积压并自动终局（宿主不要再调用目标的 Complete）。
///
/// 典型接线：
/// <code>
/// var document = new MarkdownStreamingDocument();
/// viewer.StreamingSource = document;
/// var stream = MarkdownTypewriterStream.Attach(document);
/// // LLM 回调线程：stream.AppendDelta(delta)；流结束：stream.Finish();
/// </code>
/// Vex 的打字机诉求与防抖/突发输入均经此路径获得平滑观感；不需要时仍可
/// 直接使用 <see cref="MarkdownStreamingDocument"/>（零包装开销）。
/// </summary>
public sealed class MarkdownTypewriterStream : IDisposable
{
	private readonly MarkdownStreamingDocument _target;
	private readonly MarkdownStreamPacer _pacer;
	private readonly ConcurrentQueue<string> _pending = new();
	private readonly Stopwatch _clock = Stopwatch.StartNew();
	private readonly DispatcherTimer? _timer;
	private int _finishRequested;
	private int _completedFlag;
	private bool _disposed;

	private MarkdownTypewriterStream(MarkdownStreamingDocument target, MarkdownStreamPacer pacer, DispatcherTimer? timer)
	{
		_target = target;
		_pacer = pacer;
		_timer = timer;
	}

	/// <summary>挂接到目标流式文档并启动 UI 帧泵（须在 UI 线程调用）。</summary>
	public static MarkdownTypewriterStream Attach(
		MarkdownStreamingDocument target,
		MarkdownStreamPacer.PacingPreset preset = MarkdownStreamPacer.PacingPreset.Balanced)
	{
		ArgumentNullException.ThrowIfNull(target);
		var timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Background, static (sender, _) =>
		{
			if (sender is DispatcherTimer { Tag: MarkdownTypewriterStream stream }) stream.Tick();
		});
		var stream = new MarkdownTypewriterStream(target, new MarkdownStreamPacer(preset), timer);
		timer.Tag = stream;
		timer.Start();
		return stream;
	}

	/// <summary>以手动泵模式挂接（测试用：由调用方反复调 <see cref="Tick"/> 推进）。</summary>
	internal static MarkdownTypewriterStream AttachManual(MarkdownStreamingDocument target, TimeSpan targetLag)
	{
		ArgumentNullException.ThrowIfNull(target);
		return new MarkdownTypewriterStream(target, new MarkdownStreamPacer(targetLag), null);
	}

	/// <summary>目标流式文档（宿主绑定到 viewer 的实例）。</summary>
	public MarkdownStreamingDocument Target => _target;

	/// <summary>到达是否已结束（<see cref="Finish"/> 之后）。</summary>
	public bool IsFinished => Volatile.Read(ref _finishRequested) == 1;

	/// <summary>终局是否已发布（排空后自动调用目标的 Complete）。</summary>
	public bool IsCompleted => Volatile.Read(ref _completedFlag) == 1;

	/// <summary>
	/// 喂入一个到达分片（任意线程、任意大小）；<see cref="Finish"/> 之后到达的
	/// 分片被忽略（与节奏器语义一致）。
	/// </summary>
	public void AppendDelta(string delta)
	{
		ArgumentNullException.ThrowIfNull(delta);
		ObjectDisposedException.ThrowIf(_disposed, this);
		if (delta.Length == 0 || IsFinished)
		{
			return;
		}

		_pending.Enqueue(delta);
	}

	/// <summary>到达结束：不再接受新分片，剩余积压按排空速率继续释放，排空后自动终局。</summary>
	public void Finish() => Interlocked.Exchange(ref _finishRequested, 1);

	/// <summary>
	/// 放弃未释放的积压并立即终局（已释放进目标的内容保留，整篇重解析收敛）。
	/// </summary>
	public void Abort()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		Volatile.Write(ref _finishRequested, 1);
		while (_pending.TryDequeue(out _))
		{
		}

		CompleteTarget();
		_timer?.Stop();
	}

	/// <summary>推进一帧：入队分片 → 节奏器释放 → 追加目标；排空后自动终局。
	/// 内部计时器每帧调用；手动泵模式（测试）由调用方驱动。</summary>
	internal void Tick()
	{
		if (_disposed || Volatile.Read(ref _completedFlag) == 1)
		{
			return;
		}

		var now = _clock.Elapsed;
		while (_pending.TryDequeue(out var chunk))
		{
			_pacer.Push(chunk, now);
		}

		if (IsFinished)
		{
			_pacer.Finish();
		}

		var released = _pacer.Tick(now);
		if (released.Length > 0)
		{
			_target.Append(released);
		}

		if (_pacer.IsFinished && _pacer.IsDrained)
		{
			CompleteTarget();
			_timer?.Stop();
		}
	}

	private void CompleteTarget()
	{
		if (Interlocked.Exchange(ref _completedFlag, 1) == 1)
		{
			return;
		}

		_target.Complete();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_timer?.Stop();
	}
}
