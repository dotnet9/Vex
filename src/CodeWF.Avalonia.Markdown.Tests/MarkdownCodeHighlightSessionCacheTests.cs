using CodeWF.Avalonia.Markdown.Rendering;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 高亮会话跨切换指纹缓存回归：换文档整篇重建会话后，同 (Tag, code) 命中指纹缓存
/// 整表搬回 token（分词器零调用）；不同 Tag 或不同代码视为键不同，照常分词。
/// 静态缓存在测试进程内跨用例共享：代码一律掺 GUID 保证键唯一，避免用例间互相命中。
/// </summary>
public sealed class MarkdownCodeHighlightSessionCacheTests
{
	private static string UniqueCode(string body) => body + " // " + Guid.NewGuid().ToString("N");

	private static (IReadOnlyList<object> Tokens, int TokenizeCalls) Run(MarkdownCodeHighlightSession session, string code)
	{
		var calls = 0;
		var tokens = session.Update(code, (string line, object? _, out object? endState) =>
		{
			calls++;
			endState = null;
			return "T:" + line;
		});
		return (tokens, calls);
	}

	[Fact]
	public void CrossSwitch_SameTagAndCode_HitsCacheAndSkipsTokenizer()
	{
		var code = UniqueCode("int a;\nint b;");
		var first = new MarkdownCodeHighlightSession { Tag = "csharp|dark" };
		var (tokens1, calls1) = Run(first, code);
		Assert.Equal(2, calls1);
		Assert.True(MarkdownCodeHighlightSession.CrossSwitchCacheCount >= 1, "入指纹缓存");

		var second = new MarkdownCodeHighlightSession { Tag = "csharp|dark" };
		var (tokens2, calls2) = Run(second, code);
		Assert.Equal(0, calls2);
		Assert.Equal(tokens1, tokens2);
	}

	[Fact]
	public void CrossSwitch_DifferentTag_IsCacheMiss()
	{
		var first = new MarkdownCodeHighlightSession { Tag = "csharp|dark" };
		Run(first, UniqueCode("int a;"));
		var second = new MarkdownCodeHighlightSession { Tag = "csharp|light" };
		var (tokens, calls) = Run(second, UniqueCode("int a;"));
		Assert.Equal(1, calls);
		Assert.Single(tokens);
	}

	[Fact]
	public void CrossSwitch_DifferentCode_IsCacheMiss()
	{
		var first = new MarkdownCodeHighlightSession { Tag = "csharp|dark" };
		Run(first, UniqueCode("int a;"));
		var second = new MarkdownCodeHighlightSession { Tag = "csharp|dark" };
		var (tokens, calls) = Run(second, UniqueCode("int b;"));
		Assert.Equal(1, calls);
		Assert.Single(tokens);
	}

	[Fact]
	public void StreamingAppend_StillReusesPrefixAndRefreshesCache()
	{
		var session = new MarkdownCodeHighlightSession { Tag = "csharp|dark" };
		var head = UniqueCode("int a;");
		var (_, firstCalls) = Run(session, head);
		var (tokens, secondCalls) = Run(session, head + "\nint b;");
		Assert.Equal(1, firstCalls);
		// 流式追加走既有前缀复用：只有新增行分词。
		Assert.Equal(1, secondCalls);
		Assert.Equal(2, tokens.Count);
	}
}
