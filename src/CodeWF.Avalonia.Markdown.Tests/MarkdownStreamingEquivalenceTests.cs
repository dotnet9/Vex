using System.Diagnostics;

using CodeWF.Avalonia.Markdown.Shared.Rendering;
using CodeWF.Avalonia.Markdown.Streaming;

using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 流式内核等价性验收（规格 docs/流式规则.md §3.5）：
/// 任意分片驱动逐 token 追加，每步快照断言——
/// I1 边界单调、I4 冻结块 ≡ 前缀独立解析（kind + 源码 span + 内容哈希）、
/// 冻结块对象跨快照共享；另断言"任意续写不推翻冻结前缀"的未来安全性
/// （§3.1 的语义：边界之后无论到达什么文本，边界之前的解析结果都不变）。
/// Complete 后整篇重解析必须与直接整篇解析完全一致（B3 终局收敛）。
/// </summary>
public class MarkdownStreamingEquivalenceTests
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter()
        .Build();

    private sealed class StreamDriver : IDisposable
    {
        private readonly MarkdownStreamingDocument _document = new();
        private volatile Exception? _failure;

        public StreamDriver()
        {
            _document.Failed += (_, exception) => _failure = exception;
        }

        public MarkdownStreamingSnapshot AppendAndWait(string chunk, int expectedSourceLength)
        {
            _document.Append(chunk);
            return Wait(snapshot => snapshot.Source.Length >= expectedSourceLength, $"追加至 {expectedSourceLength}");
        }

        public MarkdownStreamingSnapshot CompleteAndWait()
        {
            _document.Complete();
            return Wait(snapshot => snapshot.IsComplete, "终局");
        }

        private MarkdownStreamingSnapshot Wait(Func<MarkdownStreamingSnapshot, bool> condition, string stage)
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var snapshot = _document.Latest;
                if (condition(snapshot))
                {
                    return snapshot;
                }

                if (_failure is { } failure)
                {
                    throw new InvalidOperationException($"流式解析失败（{stage}）", failure);
                }

                if (stopwatch.Elapsed > TimeSpan.FromSeconds(30))
                {
                    throw new TimeoutException($"等待快照超时（{stage}）");
                }

                Thread.Sleep(1);
            }
        }

        public void Dispose() => _document.Dispose();
    }

    private static void AssertBlockEquivalent(MarkdownDocumentBlock expected, MarkdownDocumentBlock actual, string context)
    {
        Assert.True(expected.Kind == actual.Kind, $"{context}:kind 不一致 {expected.Kind} != {actual.Kind}");
        Assert.True(expected.SourceSpan.Start == actual.SourceSpan.Start && expected.SourceSpan.End == actual.SourceSpan.End,
            $"{context}:span 不一致 ({expected.SourceSpan.Start},{expected.SourceSpan.End}) != ({actual.SourceSpan.Start},{actual.SourceSpan.End})");
        Assert.True(expected.ContentHash == actual.ContentHash,
            $"{context}:内容哈希不一致（source=\"{expected.SourceText[..Math.Min(40, expected.SourceText.Length)]}…\"）");
    }

    /// <summary>每步快照的全部不变量。checkContinuation=false 用于"意义取决于后文"的结构（front matter）。</summary>
    private static void AssertSnapshotInvariants(
        MarkdownStreamingSnapshot? previous, MarkdownStreamingSnapshot current, bool checkContinuation = true)
    {
        Assert.InRange(current.FrozenOffset, 0, current.Source.Length);

        if (previous is { Version: > 0 })
        {
            // I1：冻结边界单调前进，永不回退。
            Assert.True(current.FrozenOffset >= previous.FrozenOffset,
                $"I4 边界回退:{previous.FrozenOffset} -> {current.FrozenOffset}");

            // 冻结块实例跨快照共享（对象级复用，视觉树据此免重建）。
            for (var i = 0; i < previous.FrozenBlocks.Count; i++)
            {
                Assert.Same(previous.FrozenBlocks[i], current.FrozenBlocks[i]);
            }
        }

        if (current.FrozenOffset == 0)
        {
            return;
        }

        var prefix = current.Source[..current.FrozenOffset];

        // I4：冻结块 ≡ 前缀独立整篇解析。
        var prefixBlocks = MarkdownParser.Parse(prefix, Pipeline).Blocks;
        Assert.Equal(prefixBlocks.Count, current.FrozenBlocks.Count);
        for (var i = 0; i < prefixBlocks.Count; i++)
        {
            AssertBlockEquivalent(prefixBlocks[i], current.FrozenBlocks[i], $"I4（源长度 {current.Source.Length}）块 #{i}");
        }

        if (!checkContinuation)
        {
            return;
        }

        // 未来安全性：任意续写不改变冻结前缀的解析结果（把最后一个未完成行再延长）。
        var continuedBlocks = MarkdownParser.Parse(current.Source + "x", Pipeline).Blocks;
        Assert.True(continuedBlocks.Count >= current.FrozenBlocks.Count,
            $"续写后块数 {continuedBlocks.Count} 少于冻结块数 {current.FrozenBlocks.Count}");
        for (var i = 0; i < current.FrozenBlocks.Count; i++)
        {
            AssertBlockEquivalent(continuedBlocks[i], current.FrozenBlocks[i], $"续写后冻结前缀失守 块 #{i}");
        }
    }

    private static void StreamAndAssert(string text, int chunkSize, bool checkContinuation = true)
    {
        using var driver = new StreamDriver();
        MarkdownStreamingSnapshot? previous = null;
        for (var offset = 0; offset < text.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, text.Length - offset);
            var snapshot = driver.AppendAndWait(text.Substring(offset, length), offset + length);
            AssertSnapshotInvariants(previous, snapshot, checkContinuation);
            previous = snapshot;
        }

        var final = driver.CompleteAndWait();
        Assert.True(final.IsComplete);

        // B3：终局整篇重解析 ≡ 直接整篇解析。
        var expected = MarkdownParser.Parse(text, Pipeline).Blocks;
        Assert.Equal(expected.Count, final.Model.Blocks.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            AssertBlockEquivalent(expected[i], final.Model.Blocks[i], $"终局收敛 块 #{i}");
        }
    }

    private static string BuildComprehensiveDocument()
    {
        return """
            # 流式渲染等价性样本

            第一段正文，**加粗**与*斜体*、`行内代码`、~~删除线~~和[外链](https://example.com/a)混排，还有自动链接 <https://example.com/auto>。行内数学 $a^2+b^2=c^2$ 也来一句。

            ## 列表与任务

            - 无序项目甲
            - 无序项目乙
              - 嵌套项目
            - [ ] 未完成任务
            - [x] 已完成任务

            1. 有序第一项
            2. 有序第二项

            ## 引用与提示

            > 引用第一行
            > 引用第二行，含**加粗**

            > [!NOTE]
            > 提示块正文，流式期间不应炸版。

            ## 代码块

            ```csharp
            var frozen = 42;
            Console.WriteLine(frozen);
            ```

            波浪线围栏：

            ~~~text
            波浪围栏内容
            ~~~

            ## 表格

            | 名称 | 值 |
            |---|---|
            | 冻结 | 42 |
            | 边界 | 7 |

            ## 数学块

            $$
            E = mc^2
            $$

            ## 杂项结构

            ---

            带 setext 的标题
            ===

                缩进代码一行
                缩进代码两行

            [refdef]: https://example.com/ref

            使用 [refdef] 引用定义的段落。

            > 引用内的段落
            引用的懒续行

            - 列表项内容
            列表的懒续行

            结尾段落，流到此结束。
            """;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(32)]
    [InlineData(500)]
    public void ComprehensiveDocument_EverySnapshotSatisfiesInvariants(int chunkSize)
    {
        StreamAndAssert(BuildComprehensiveDocument(), chunkSize);
    }

    [Fact]
    public void ComprehensiveDocument_RandomChunkSizes_EverySnapshotSatisfiesInvariants()
    {
        var text = BuildComprehensiveDocument();
        using var driver = new StreamDriver();
        var random = new Random(20261123);
        MarkdownStreamingSnapshot? previous = null;
        var offset = 0;
        while (offset < text.Length)
        {
            var length = Math.Min(random.Next(1, 65), text.Length - offset);
            var snapshot = driver.AppendAndWait(text.Substring(offset, length), offset + length);
            AssertSnapshotInvariants(previous, snapshot);
            previous = snapshot;
            offset += length;
        }

        var final = driver.CompleteAndWait();
        var expected = MarkdownParser.Parse(text, Pipeline).Blocks;
        Assert.Equal(expected.Count, final.Model.Blocks.Count);
    }

    [Fact]
    public void CrlfDocument_EverySnapshotSatisfiesInvariants()
    {
        StreamAndAssert(BuildComprehensiveDocument().Replace("\n", "\r\n"), 9);
    }

    [Fact]
    public void FrontMatterDocument_EverySnapshotSatisfiesInvariants()
    {
        // F5：front matter 整块悬置到闭合 `---` 行，冻结块 = FrontMatter 块——
        // 前缀独立解析与整篇解析同形，续写安全性照常成立
        // （此前逐行规则把 `---\ntitle\n---` 误冻结成分隔线 + setext 标题，违反 I4，由本用例回归）。
        StreamAndAssert("---\ntitle: 流式样本\n---\n\n正文段落一。\n\n## 标题\n\n正文段落二。\n", 3);
    }

    [Fact]
    public void LargeMixedDocument_EverySnapshotSatisfiesInvariants()
    {
        var builder = new System.Text.StringBuilder();
        var index = 0;
        while (builder.Length < 8 * 1024)
        {
            builder.Append($"## 第 {index} 章\n\n")
                .Append($"第 {index} 段正文，**加粗**与`行内码`混排，链接[示例](https://example.com/{index})。\n\n")
                .Append($"- 项目甲 {index}\n- [ ] 待办 {index}\n\n")
                .Append("```csharp\nvar i = ").Append(index).Append(";\n```\n\n")
                .Append($"> 引用第 {index} 条\n\n");
            index++;
        }

        StreamAndAssert(builder.ToString(), 7);
    }

    // §4 尾部容错：半截语法按目标形态呈现（通过文档级流式验证，补全不写回源码）。

    private static MarkdownStreamingSnapshot StreamTo(string text, int chunkSize)
    {
        using var driver = new StreamDriver();
        MarkdownStreamingSnapshot? previous = null;
        for (var offset = 0; offset < text.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, text.Length - offset);
            var snapshot = driver.AppendAndWait(text.Substring(offset, length), offset + length);
            AssertSnapshotInvariants(previous, snapshot);
            previous = snapshot;
        }

        return previous ?? throw new InvalidOperationException("空文本");
    }

    private static IEnumerable<Inline> DescendantInlines(MarkdownDocumentBlock block)
    {
        if (block.SyntaxBlock is not LeafBlock leaf || leaf.Inline is null)
        {
            return [];
        }

        static IEnumerable<Inline> Collect(Inline inline)
        {
            yield return inline;
            if (inline is not ContainerInline container)
            {
                yield break;
            }

            var child = container.FirstChild;
            while (child is not null)
            {
                foreach (var descendant in Collect(child))
                {
                    yield return descendant;
                }

                child = child.NextSibling;
            }
        }

        return Collect(leaf.Inline);
    }

    [Fact]
    public void UnclosedBold_RendersAsEmphasis()
    {
        var snapshot = StreamTo("段落开头**加粗到一半", 4);
        var tail = snapshot.Model.Blocks[^1];
        Assert.Contains(DescendantInlines(tail), inline => inline is EmphasisInline
        {
            DelimiterChar: '*', FirstChild: LiteralInline literal
        } && literal.Content.ToString().Contains("加粗到一半"));
    }

    [Fact]
    public void UnclosedLink_Parenthesis_RendersAsLink()
    {
        var snapshot = StreamTo("参见 [文本](https://example.com", 5);
        var tail = snapshot.Model.Blocks[^1];
        Assert.Contains(DescendantInlines(tail), inline => inline is LinkInline link && link.Url == "https://example.com");
    }

    [Fact]
    public void HalfHtmlTag_StrippedFromTail()
    {
        var snapshot = StreamTo("正文一段\n\n半截标签前的文本 <di", 3);
        var tail = snapshot.Model.Blocks[^1];
        Assert.DoesNotContain("<di", tail.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void OpenFence_FastPath_BuildsCodeBlock()
    {
        var snapshot = StreamTo("前言段落。\n\n```csharp\nvar x = 1;\nConsole.Write", 4);
        var tail = snapshot.Model.Blocks[^1];
        Assert.Equal(MarkdownBlockKind.Code, tail.Kind);
        // 快速路径代码块吃到源末尾（内容仍在生长）。
        Assert.Equal(snapshot.Source.Length, tail.SourceSpan.End);
        // 补全不写回源码：源文本仍是没有闭合围栏的半截。
        Assert.False(snapshot.Source.EndsWith("```", StringComparison.Ordinal));
    }

    [Fact]
    public void OpenFence_FastPath_MatchesRemendedRegularPath()
    {
        // §4.4 Q3：快速路径产物与"补全后走 Markdig"的常规路径逐字段一致。
        var source = "前言段落。\n\n```csharp\nvar x = 1;\nConsole.Write";
        var snapshot = StreamTo(source, 4);
        var fastPath = snapshot.Model.Blocks[^1];
        Assert.Equal(MarkdownBlockKind.Code, fastPath.Kind);

        var boundary = snapshot.FrozenOffset;
        var tail = source[boundary..];
        var remend = MarkdownTailRemender.Apply(tail);
        var regular = MarkdownParser.Parse(remend.RemendedText, Pipeline).Blocks[^1];
        Assert.Equal(MarkdownBlockKind.Code, regular.Kind);
        Assert.Equal(fastPath.Kind, regular.Kind);
        Assert.Equal(fastPath.ContentHash, regular.ContentHash);
        Assert.Equal(fastPath.PlainText, regular.PlainText);
        Assert.Equal((fastPath.SyntaxBlock as FencedCodeBlock)?.Info, (regular.SyntaxBlock as FencedCodeBlock)?.Info);
    }

    // §4.1 处理器优先级表：直接钉补全行为与偏移映射。

    [Theory]
    [InlineData("参见 [文本](https://example.com", "参见 [文本](https://example.com)")]
    [InlineData("参见 [文本", "参见 [文本](codewf-md:pending)")]
    [InlineData("a **b", "a **b**")]
    [InlineData("a *b", "a *b*")]
    [InlineData("`code", "`code`")]
    [InlineData("~~del", "~~del~~")]
    [InlineData("$$x", "$$x\n$$")]
    [InlineData("```cs\ncode", "```cs\ncode\n```")]
    public void Remend_ClosesIncompleteSyntax(string tail, string expected)
    {
        Assert.Equal(expected, MarkdownTailRemender.Apply(tail).RemendedText);
    }

    [Fact]
    public void Remend_CompleteTail_Unchanged()
    {
        var result = MarkdownTailRemender.Apply("完整段落，没有半截语法。");
        Assert.Equal("完整段落，没有半截语法。", result.RemendedText);
    }

    [Fact]
    public void Remend_SingleDollar_LeftAlone()
    {
        // "价格 $5" 不做数学化（§4.1 P40：误报成本高于收益）。
        Assert.Equal("价格 $5", MarkdownTailRemender.Apply("价格 $5").RemendedText);
    }

    [Fact]
    public void Remend_SetextAmbiguity_BrokenByZeroWidthSpace()
    {
        // 段落后的半个 "-" 会被解析器当成 setext 下划线，补零宽空格打破。
        Assert.Equal("段落\n-\u200B", MarkdownTailRemender.Apply("段落\n-").RemendedText);
    }

    [Fact]
    public void Remend_HalfHtmlTag_Stripped()
    {
        var result = MarkdownTailRemender.Apply("文本 <di");
        Assert.Equal("文本 ", result.RemendedText);
    }

    [Fact]
    public void Remend_OffsetMap_MapsTempOffsetsBackToOriginal()
    {
        // "**bo" 补成 "**bo**"：临时偏移 6（临时文本末尾）映射回原始偏移 4。
        var result = MarkdownTailRemender.Apply("**bo");
        Assert.Equal("**bo**", result.RemendedText);
        Assert.Equal(4, result.OffsetMap.MapTempToOriginal(6));
        Assert.Equal(4, result.OffsetMap.MapTempToOriginal(5));
        Assert.Equal(0, result.OffsetMap.MapTempToOriginal(0));
        Assert.Equal(4, result.OffsetMap.OriginalLength);
    }
}
