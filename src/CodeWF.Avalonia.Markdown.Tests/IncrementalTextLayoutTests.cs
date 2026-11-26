using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

using CodeWF.Avalonia.Markdown.Rendering;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 行级增量排版引擎等价性回归：从差异行起重排 ≡ 整段从零排版。
/// 覆盖：全量排版与 TextLayout 行界一致、追加/中插/中删增量等价、
/// 禁则（行首句号被拉回）由整篇对照兜住、硬换行尾空行、两端对齐接管、命中测试一致。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class IncrementalTextLayoutTests(AvaloniaPlatformFixture platform)
{
	private const string MixedText =
		"The quick brown fox jumps over the lazy dog. 敏捷的棕色狐狸跳过了懒惰的狗。第二句以句号结束。ABCDEF GHIJKLMN 0123456789 汉字与拉丁混排换行点更密集。";

	private const double Width = 240;

	private sealed class StringTextSource(string text, GenericTextRunProperties runs) : ITextSource
	{
		public TextRun? GetTextRun(int textSourceIndex) =>
			textSourceIndex >= text.Length ? null : new TextCharacters(text[textSourceIndex..], runs);
	}

	private static GenericTextRunProperties BuildRuns() =>
		new(Typeface.Default, fontRenderingEmSize: 14);

	private static GenericTextParagraphProperties BuildParagraph(GenericTextRunProperties runs,
		TextAlignment alignment = TextAlignment.Left, double lineHeight = double.NaN) =>
		new(runs, alignment, TextWrapping.Wrap, lineHeight);

	private static (IncrementalTextLayout Layout, GenericTextRunProperties Runs, string Text) BuildLayout(
		string text, double width = Width, TextAlignment alignment = TextAlignment.Left, double lineHeight = double.NaN)
	{
		var runs = BuildRuns();
		var layout = new IncrementalTextLayout();
		layout.Format(new StringTextSource(text, runs), text.Length, width,
			BuildParagraph(runs, alignment, lineHeight), BuildParagraph(runs, alignment), 0, null);
		return (layout, runs, text);
	}

	private static TextLayout ReferenceLayout(string text, double width = Width, TextAlignment alignment = TextAlignment.Left)
	{
		var runs = BuildRuns();
		return new TextLayout(new StringTextSource(text, runs), BuildParagraph(runs, alignment), maxWidth: width);
	}

	private static void AssertLinesMatch(IncrementalTextLayout incremental, TextLayout reference, string scenario)
	{
		Assert.True(incremental.TextLines.Count == reference.TextLines.Count,
			$"{scenario}: 行数 {incremental.TextLines.Count} ≠ TextLayout {reference.TextLines.Count}");
		for (var i = 0; i < incremental.TextLines.Count; i++)
		{
			var inc = incremental.TextLines[i];
			var reference_line = reference.TextLines[i];
			Assert.True(inc.Length == reference_line.Length && inc.FirstTextSourceIndex == reference_line.FirstTextSourceIndex,
				$"{scenario}: 行 {i} 边界不一致（增量 {inc.FirstTextSourceIndex}+{inc.Length} vs 对照 {reference_line.FirstTextSourceIndex}+{reference_line.Length}）");
			Assert.True(Math.Abs(inc.Height - reference_line.Height) < 0.01,
				$"{scenario}: 行 {i} 高度不一致");
		}
		Assert.True(Math.Abs(incremental.Height - reference.Height) < 0.01,
			$"{scenario}: 总高不一致（{incremental.Height} vs {reference.Height}）");
	}

	[Fact]
	public void FullFormat_MatchesTextLayoutLineBoundaries() => platform.Run(() =>
	{
		var (layout, _, text) = BuildLayout(MixedText);
		Assert.True(layout.TextLines.Count > 3, "测试文本应产生多行");
		AssertLinesMatch(layout, ReferenceLayout(text), "全量排版");
	});

	[Theory]
	[InlineData(30)]    // 纯追加：差异点在段中
	[InlineData(60)]   // 追加跨过换行点
	[InlineData(90)]   // 追加接近段尾，禁则字符推到行首候选
	public void IncrementalAppend_FromDiffLine_MatchesFullFormat(int prefixLength) => platform.Run(() =>
	{
		var prefix = MixedText[..prefixLength];
		var full = MixedText + "追加片段 ABCDEF 追加片段 GBH 混排。";
		var (incremental, runs, _) = BuildLayout(prefix);
		var fixedHeight = BuildParagraph(runs);
		var natural = BuildParagraph(runs);
		incremental.Format(new StringTextSource(full, runs), full.Length, Width, fixedHeight, natural, prefix.Length, null);
		AssertLinesMatch(incremental, ReferenceLayout(full), $"追加 diff@{prefixLength}");
	});

	[Theory]
	[InlineData(30, 90)]   // 中段替换（删除+插入复合）
	[InlineData(100, 40)]  // 删到更短：行数收缩
	public void IncrementalReplace_FromDiffLine_MatchesFullFormat(int keep, int replaceTo) => platform.Run(() =>
	{
		var before = MixedText;
		var after = before[..keep] + new string('X', 20) + before[replaceTo..];
		var (incremental, runs, _) = BuildLayout(before);
		var commonPrefix = 0;
		while (commonPrefix < before.Length && commonPrefix < after.Length && before[commonPrefix] == after[commonPrefix])
		{
			commonPrefix++;
		}
		var fixedHeight = BuildParagraph(runs);
		var natural = BuildParagraph(runs);
		incremental.Format(new StringTextSource(after, runs), after.Length, Width, fixedHeight, natural, commonPrefix, null);
		AssertLinesMatch(incremental, ReferenceLayout(after), $"替换 diff@{commonPrefix}");
	});

	[Fact]
	public void IncrementalCJKRetreat_CoversLineHeadKinsuku() => platform.Run(() =>
	{
		// 禁则场景：句号不能落在行首。前缀末尾恰好在断行机会附近，追加「。」后
		// 依照禁则它必须与前一行末字同行或被前一行的字顶替——与整篇排版逐行一致即视为覆盖。
		var body = "设置参数后保存配置文件并重新启动服务进程测试禁则处理规则";
		var prefix = body[..26];
		var full = body + "。";
		var (incremental, runs, _) = BuildLayout(prefix);
		var fixedHeight = BuildParagraph(runs);
		var natural = BuildParagraph(runs);
		incremental.Format(new StringTextSource(full, runs), full.Length, Width, fixedHeight, natural, prefix.Length, null);
		AssertLinesMatch(incremental, ReferenceLayout(full), "禁则追加");
		// 文本以句号结尾且无硬换行：末行不应是空行。
		Assert.True(incremental.TextLines[^1].Length > 0 || full.EndsWith('\n'), "末行不应为空");
	});

	[Fact]
	public void TrailingHardBreak_ProducesEmptyLastLine() => platform.Run(() =>
	{
		var (layout, _, text) = BuildLayout("abc def\n");
		var bare = BuildLayout("abc def").Layout;
		Assert.True(layout.TextLines.Count == bare.TextLines.Count + 1,
			$"硬换行应追加空行（{layout.TextLines.Count} vs {bare.TextLines.Count}）");
		Assert.True(layout.Height > bare.Height, "空行应占行高");
	});

	[Fact]
	public void Justify_AdoptsTextLayoutLines() => platform.Run(() =>
	{
		var (layout, _, text) = BuildLayout(MixedText, alignment: TextAlignment.Justify);
		var reference = ReferenceLayout(text, alignment: TextAlignment.Justify);
		Assert.True(layout.TextLines.Count > 3);
		Assert.True(Math.Abs(layout.Height - reference.Height) < 0.01, "两端对齐总高应与 TextLayout 一致");
	});

	[Fact]
	public void HitTestPosition_MatchesTextLayout() => platform.Run(() =>
	{
		var (layout, _, text) = BuildLayout(MixedText);
		var reference = ReferenceLayout(text);
		foreach (var position in new[] { 0, 1, text.Length / 3, text.Length / 2, text.Length - 1, text.Length })
		{
			var incrementalRect = layout.HitTestTextPosition(position);
			var referenceRect = reference.HitTestTextPosition(position);
			Assert.True(Math.Abs(incrementalRect.Y - referenceRect.Y) < 0.01,
				$"位置 {position} 的行顶不一致（{incrementalRect.Y} vs {referenceRect.Y}）");
		}
	});
}
