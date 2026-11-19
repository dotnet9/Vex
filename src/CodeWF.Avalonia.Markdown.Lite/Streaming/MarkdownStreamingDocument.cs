using System.Diagnostics;
using System.Text;

using CodeWF.Avalonia.Markdown.Shared.Rendering;

using Markdig;
using Markdig.Helpers;
using Markdig.Syntax;
using FencedCodeBlockParser = Markdig.Parsers.FencedCodeBlockParser;

namespace CodeWF.Avalonia.Markdown.Streaming;

/// <summary>
/// 流式快照：某一时刻源码缓冲的完整块模型。
///
/// <see cref="FrozenBlocks"/> 中的块实例跨快照共享（冻结后只增不改），
/// <see cref="Model"/> 是冻结块 + 尾部块组装出的完整文档模型，可直接交给
/// 渲染层的块级 diff 复用。
/// </summary>
public sealed record MarkdownStreamingSnapshot(
	int Generation,
	long Version,
	string Source,
	int FrozenOffset,
	bool IsComplete,
	IReadOnlyList<MarkdownDocumentBlock> FrozenBlocks,
	MarkdownDocumentModel Model,
	TimeSpan ParseDuration)
{
	public IReadOnlyList<MarkdownDocumentBlock> Blocks => Model.Blocks;

	public static MarkdownStreamingSnapshot Empty { get; } =
		new(0, 0, string.Empty, 0, false, [], MarkdownDocumentModel.Empty, TimeSpan.Zero);
}

/// <summary>
/// 流式文档：以只追加的源码缓冲为唯一真相，把连续到达的文本切片
/// 增量变成块模型（架构见 docs/流式规则.md §2）。
///
/// <see cref="Append"/> 可在任意线程调用；解析在单个后台工作者上串行执行，
/// 进行中再来的文本只置脏标记、中间版本直接跳过；解析异常吞掉并保留
/// 上一份快照（界面不崩、停更），异常经 <see cref="Failed"/> 上抛。
/// </summary>
public sealed class MarkdownStreamingDocument : IDisposable
{
	private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
		.UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter()
		.Build();

	private readonly object _gate = new();
	private readonly MarkdownStreamSegmenter _segmenter = new();

	private string _source = string.Empty;
	private long _version;
	private int _generation;
	private bool _dirty;
	private bool _running;
	private bool _completed;
	private bool _disposed;

	// 冻结区状态：只增不改，跨快照共享。
	private int _frozenEnd;
	private readonly List<MarkdownDocumentBlock> _frozenBlocks = [];
	private readonly StringBuilder _frozenPlain = new();
	private MarkdownDependencyFlags _frozenFlags = MarkdownDependencyFlags.None;

	private MarkdownStreamingSnapshot _latest = MarkdownStreamingSnapshot.Empty;

	private readonly SynchronizationContext? _synchronizationContext;

	public MarkdownStreamingDocument()
	{
		_synchronizationContext = SynchronizationContext.Current;
	}

	/// <summary>快照就绪（在创建文档的同步上下文上回调）。</summary>
	public event EventHandler<MarkdownStreamingSnapshot>? SnapshotReady;

	/// <summary>解析失败；保留上一份快照，下一次文本到达会重新尝试。</summary>
	public event EventHandler<Exception>? Failed;

	/// <summary>流结束（<see cref="Complete"/> 之后的最终整篇解析已发布）。</summary>
	public event EventHandler? Completed;

	/// <summary>最新快照（尚未产出时为 <see cref="MarkdownStreamingSnapshot.Empty"/>）。</summary>
	public MarkdownStreamingSnapshot Latest => _latest;

	/// <summary>追加文本切片（任意线程）。</summary>
	public void Append(string text)
	{
		ArgumentNullException.ThrowIfNull(text);
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (_completed)
			{
				throw new InvalidOperationException(MarkdownLocalization.GetResource(MarkdownL.StreamAlreadyCompleted));
			}

			if (text.Length == 0)
			{
				return;
			}

			_source = _source.Length == 0 ? text : string.Concat(_source, text);
			_version++;
			_dirty = true;
		}

		EnsureWorker();
	}

	/// <summary>
	/// 结束流：整篇重解析一次并开新代，收敛跨片段的全局依赖
	/// （链接引用定义、脚注编号）。之后再调用 <see cref="Append"/> 会抛出异常。
	/// </summary>
	public void Complete()
	{
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			_completed = true;
			_dirty = true;
		}

		EnsureWorker();
	}

	/// <summary>
	/// 重置为整串文本并开新代（语义等同现有 <c>Markdown</c> 属性的整串赋值）。
	/// </summary>
	public void Reset(string? text = null)
	{
		lock (_gate)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			_source = text ?? string.Empty;
			_generation++;
			_version++;
			_completed = false;
			_dirty = true;
			_frozenEnd = 0;
			_frozenBlocks.Clear();
			_frozenPlain.Clear();
			_frozenFlags = MarkdownDependencyFlags.None;
			_segmenter.Reset();
		}

		EnsureWorker();
	}

	public void Dispose()
	{
		lock (_gate)
		{
			_disposed = true;
		}
	}

	private void EnsureWorker()
	{
		lock (_gate)
		{
			if (_running || _disposed)
			{
				return;
			}

			_running = true;
		}

		_ = Task.Run(ProcessLoop);
	}

	private void ProcessLoop()
	{
		while (true)
		{
			string source;
			bool complete;
			int generation;
			long version;
			lock (_gate)
			{
				if (!_dirty || _disposed)
				{
					_running = false;
					return;
				}

				_dirty = false;
				source = _source;
				complete = _completed;
				generation = _generation;
				version = _version;
			}

			MarkdownStreamingSnapshot snapshot;
			try
			{
				snapshot = BuildSnapshot(source, complete, generation, version);
			}
			catch (Exception exception)
			{
				// 解析失败保留上一份快照（规格 B2），异常上抛给宿主。
				Post(() => Failed?.Invoke(this, exception));
				continue;
			}

			lock (_gate)
			{
				_latest = snapshot;
			}

			Post(() => SnapshotReady?.Invoke(this, snapshot));
			if (complete)
			{
				// Complete 之后 Append 会抛出，本事件每次 Complete 周期只发一次。
				Post(() => Completed?.Invoke(this, EventArgs.Empty));
			}
		}
	}

	private MarkdownStreamingSnapshot BuildSnapshot(string source, bool complete, int generation, long version)
	{
		var start = Stopwatch.GetTimestamp();

		if (complete)
		{
			// 终局：整篇重解析（规格 B3），跨片段的全局依赖就此收敛。
			var finalModel = Parse(source);
			var duration = Stopwatch.GetElapsedTime(start);
			return new MarkdownStreamingSnapshot(
				generation, version, source, source.Length, true, finalModel.Blocks, finalModel, duration);
		}

		_segmenter.Scan(source, sourceComplete: false);
		var boundary = _segmenter.FrozenBoundary;

		// 冻结新定型的前缀：一次解析、块偏移映射回原始源码，实例从此跨快照共享。
		if (boundary > _frozenEnd)
		{
			var segmentText = source[_frozenEnd..boundary];
			var segmentModel = Parse(segmentText);
			foreach (var block in segmentModel.Blocks)
			{
				var plainStart = _frozenPlain.Length;
				if (block.PlainText.Length > 0)
				{
					_frozenPlain.Append(block.PlainText);
				}

				var plainEnd = _frozenPlain.Length;
				if (_frozenPlain.Length > 0)
				{
					_frozenPlain.AppendLine();
					_frozenPlain.AppendLine();
				}

				_frozenBlocks.Add(block with
				{
					SourceSpan = new MarkdownTextSpan(block.SourceSpan.Start + _frozenEnd, block.SourceSpan.End + _frozenEnd),
					PlainTextSpan = new MarkdownTextSpan(plainStart, plainEnd),
				});
			}

			_frozenFlags |= segmentModel.DependencyFlags;
			_frozenEnd = boundary;
		}

		// 尾部：开放围栏快速路径（规格 §4.4），未命中时容错补全后解析，
		// 块偏移经映射回到原始源码坐标。
		var tailText = source[boundary..];
		var tailBlocks = new List<MarkdownDocumentBlock>();
		var tailPlain = new StringBuilder();
		var tailFlags = MarkdownDependencyFlags.None;

		if (TryBuildOpenFenceTailBlock(source, boundary, tailText) is { } fenceBlock)
		{
			tailBlocks.Add(fenceBlock);
			tailFlags = fenceBlock.DependencyFlags;
			if (fenceBlock.PlainText.Length > 0)
			{
				tailPlain.Append(fenceBlock.PlainText);
				tailPlain.AppendLine();
				tailPlain.AppendLine();
			}
		}
		else
		{
			var remend = MarkdownTailRemender.Apply(tailText);
			var tailModel = Parse(remend.RemendedText);

			foreach (var block in tailModel.Blocks)
			{
				var sourceStart = boundary + remend.OffsetMap.MapTempToOriginal(block.SourceSpan.Start);
				var sourceEnd = boundary + remend.OffsetMap.MapTempToOriginal(block.SourceSpan.End);
				var plainStart = _frozenPlain.Length + tailPlain.Length;
				if (block.PlainText.Length > 0)
				{
					tailPlain.Append(block.PlainText);
				}

				var plainEnd = _frozenPlain.Length + tailPlain.Length;
				if (tailPlain.Length > 0)
				{
					tailPlain.AppendLine();
					tailPlain.AppendLine();
				}

				tailFlags |= block.DependencyFlags;
				tailBlocks.Add(block with
				{
					SourceSpan = new MarkdownTextSpan(
						Math.Min(sourceStart, source.Length),
						Math.Clamp(sourceEnd, Math.Min(sourceStart, source.Length), source.Length)),
					PlainTextSpan = new MarkdownTextSpan(plainStart, plainEnd),
				});
			}
		}

		var blocks = new List<MarkdownDocumentBlock>(_frozenBlocks.Count + tailBlocks.Count);
		blocks.AddRange(_frozenBlocks);
		blocks.AddRange(tailBlocks);
		var plainText = tailPlain.Length == 0
			? _frozenPlain.ToString()
			: _frozenPlain.ToString() + tailPlain.ToString();
		var model = new MarkdownDocumentModel(source, blocks, plainText.TrimEnd(), _frozenFlags | tailFlags);

		return new MarkdownStreamingSnapshot(
			generation, version, source, boundary, false, _frozenBlocks, model, Stopwatch.GetElapsedTime(start));
	}

	private static MarkdownDocumentModel Parse(string text) => MarkdownParser.Parse(text, Pipeline);

	/// <summary>
	/// 开放围栏快速路径（规格 §4.4）：尾部以完整的围栏开启行开始且未闭合时，
	/// 跳过补全与 Markdig，按围栏规则直接构造代码块。返回 null 表示未命中，
	/// 调用方走常规补全解析路径。
	/// </summary>
	private MarkdownDocumentBlock? TryBuildOpenFenceTailBlock(string source, int boundary, string tailText)
	{
		if (!_segmenter.IsScanningFence)
		{
			return null;
		}

		var (fenceChar, fenceLength) = _segmenter.OpenFence;

		// 末行（可能未完成）已构成闭合围栏时未命中（Q2）：补全后解析才能得到完整块。
		var lastLineStart = tailText.LastIndexOf('\n') + 1;
		if (IsFenceCloseLine(tailText[lastLineStart..], fenceChar, fenceLength))
		{
			return null;
		}

		// 开围栏行 = 尾部首行（切分器在围栏行首冻结）；未完成的开围栏行不会进入围栏态。
		var openLineEnd = tailText.IndexOf('\n');
		var openLine = tailText[..openLineEnd];
		var content = tailText[(openLineEnd + 1)..];

		// 行切片：与补全形态（content + 追加的"\n" + 闭合围栏）的行序列逐项一致——
		// 追加的换行参与分行（终结末行，或在内容已换行结尾/为空时产生空行），
		// 切片文本不含换行符，NewLine 标记 '\n'（末行无标记，ToString 由此插换行）。
		var indent = openLine.Length - openLine.TrimStart(' ').Length;
		var remendedTail = tailText + "\n" + new string(fenceChar, Math.Max(3, fenceLength));
		var fenced = new FencedCodeBlock(new FencedCodeBlockParser())
		{
			Info = ExtractFenceInfo(openLine, fenceLength),
			// 未闭合标记：渲染层据此在流式输出期间禁用折叠（用户正在看的内容不该被收起）。
			IsOpen = true
		};
		fenced.Span = new SourceSpan(indent, remendedTail.Length - 1);

		var lines = (content + "\n").Split('\n');
		for (var i = 0; i < lines.Length - 1; i++)
		{
			AddFenceLine(fenced, lines[i], indent, i == lines.Length - 2 ? NewLine.None : NewLine.LineFeed);
		}

		var kind = MarkdownBlockClassifier.GetKind(fenced, remendedTail);
		var flags = MarkdownBlockClassifier.GetDependencyFlags(fenced, kind);
		var sourceText = remendedTail[indent..];
		var blockText = MarkdownParser.ExtractPlainText(fenced).TrimEnd();
		var plainStart = _frozenPlain.Length;
		var plainEnd = plainStart + (blockText.Length > 0 ? blockText.Length : 0);

		return new MarkdownDocumentBlock(
			fenced,
			new MarkdownTextSpan(boundary + indent, source.Length),
			new MarkdownTextSpan(plainStart, plainEnd),
			kind,
			flags,
			MarkdownParser.ComputeBlockHash(sourceText, kind, flags),
			blockText,
			sourceText,
			MarkdownBlockClassifier.IsHeavy(fenced, kind));
	}

	private static void AddFenceLine(FencedCodeBlock fenced, string text, int indent, NewLine newLine)
	{
		var slice = new StringSlice(StripFenceIndent(text, indent))
		{
			NewLine = newLine,
		};
		fenced.AppendLine(ref slice, 0, 0, 0, trackTrivia: false);
	}

	/// <summary>围栏闭合行判定（与切分器/补全器同规则：≤3 空格缩进、同字符不短于开围栏、行尾无其他内容）。</summary>
	private static bool IsFenceCloseLine(string line, char fenceChar, int fenceLength)
	{
		var trimmed = line.TrimStart(' ');
		if (line.Length - trimmed.Length > 3)
		{
			return false;
		}

		var run = 0;
		while (run < trimmed.Length && trimmed[run] == fenceChar)
		{
			run++;
		}

		return run >= fenceLength && trimmed[run..].Trim().Length == 0;
	}

	/// <summary>开围栏行 info string：围栏字符后取首个空白分隔词；无内容时为空串（Markdig 同义）。</summary>
	private static string ExtractFenceInfo(string openLine, int fenceLength)
	{
		var rest = openLine.TrimStart(' ');
		if (rest.Length <= fenceLength)
		{
			return string.Empty;
		}

		var info = rest[fenceLength..].Trim();
		if (info.Length == 0)
		{
			return string.Empty;
		}

		var space = info.IndexOfAny([' ', '\t']);
		return space < 0 ? info : info[..space];
	}

	private static string StripFenceIndent(string line, int indent)
	{
		if (indent <= 0)
		{
			return line;
		}

		var stripped = 0;
		while (stripped < indent && stripped < line.Length && line[stripped] == ' ')
		{
			stripped++;
		}

		return line[stripped..];
	}

	private void Post(Action action)
	{
		if (_synchronizationContext is { } context)
		{
			// 回调抛异常不能打断后台工作者。
			context.Post(static state =>
			{
				try
				{
					((Action)state!).Invoke();
				}
				catch (Exception exception)
				{
					System.Diagnostics.Debug.WriteLine($"Markdown streaming callback failed: {exception}");
				}
			}, action);
			return;
		}

		try
		{
			action();
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine($"Markdown streaming callback failed: {exception}");
		}
	}
}
