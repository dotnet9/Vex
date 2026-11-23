using CodeWF.Avalonia.Markdown.Streaming;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 冻结边界规则表逐条钉死（规格 docs/流式规则.md §3）：每个用例给出完整文本，
/// 断言边界停在期望偏移；另用逐字符递进扫描钉住未完成行前缀规则（§3.4.1）与单调性。
/// </summary>
public class MarkdownStreamSegmenterTests
{
    private static int BoundaryOf(string text)
    {
        var segmenter = new MarkdownStreamSegmenter();
        segmenter.Scan(text, sourceComplete: false);
        return segmenter.FrozenBoundary;
    }

    private static List<int> ProgressiveBoundaries(string text)
    {
        var segmenter = new MarkdownStreamSegmenter();
        var boundaries = new List<int>(text.Length);
        for (var length = 1; length <= text.Length; length++)
        {
            segmenter.Scan(text[..length], sourceComplete: false);
            boundaries.Add(segmenter.FrozenBoundary);
        }

        return boundaries;
    }

    // §3.1 空行规则：围栏/数学/原始 HTML 开启期间，空行不产生冻结。

    [Fact]
    public void BlankLine_InsideOpenFence_DoesNotFreeze()
    {
        Assert.Equal(0, BoundaryOf("```python\nx = 1\n\n"));
    }

    [Fact]
    public void BlankLine_InsideOpenMath_DoesNotFreeze()
    {
        Assert.Equal(0, BoundaryOf("$$\nx = 1\n\n"));
    }

    [Fact]
    public void BlankLine_InsideRawHtml_DoesNotFreeze()
    {
        Assert.Equal(0, BoundaryOf("<script>\nfoo\n\n"));
    }

    [Fact]
    public void HtmlRaw_CloseLine_FreezesAfter()
    {
        Assert.Equal(23, BoundaryOf("<script>\nfoo\n</script>\nafter"));
    }

    [Fact]
    public void BlankLine_IndentedNextLine_DoesNotFreeze()
    {
        // 空行后缩进 = 缩进代码块续行，不能冻结。
        Assert.Equal(0, BoundaryOf("para\n\n    code\n"));
    }

    [Fact]
    public void BlankLine_LooseListItem_DoesNotFreeze()
    {
        // 松散列表：空行拆不开同一个列表。
        Assert.Equal(0, BoundaryOf("- a\n\n- b\n"));
    }

    [Fact]
    public void BlankLine_QuoteContinuation_DoesNotFreeze()
    {
        Assert.Equal(0, BoundaryOf("> a\n\n> b\n"));
    }

    [Fact]
    public void BlankLine_LinkReferenceDefinition_DoesNotFreeze()
    {
        Assert.Equal(0, BoundaryOf("para\n\n[a]: /u\n"));
    }

    [Fact]
    public void BlankLine_PlainParagraph_FreezesAtNextLineStart()
    {
        Assert.Equal(6, BoundaryOf("para\n\nnext para\n"));
    }

    [Fact]
    public void BlankLine_SafeIncompletePrefix_FreezesAtLineStart()
    {
        // E1：下一行未完成但前缀安全，边界提前推进到该行行首。
        Assert.Equal(6, BoundaryOf("para\n\nnext para"));
    }

    // §3.2 显式闭合规则。

    [Fact]
    public void FenceCloseLine_FreezesAfterClose()
    {
        Assert.Equal(20, BoundaryOf("pre\n\n```cs\ncode\n```\nafter"));
    }

    [Fact]
    public void MathCloseLine_FreezesAfterClose()
    {
        Assert.Equal(8, BoundaryOf("$$\nx\n$$\nafter"));
    }

    [Fact]
    public void SingleLineMath_FreezesAtLineEnd()
    {
        Assert.Equal(8, BoundaryOf("$$ x $$\nafter"));
    }

    [Fact]
    public void AtxHeading_FreezesAtLineEnd_EvenMidDocument()
    {
        Assert.Equal(10, BoundaryOf("para\n# 标题\nrest"));
    }

    [Fact]
    public void ThematicBreak_AfterBlank_FreezesAtLineEnd()
    {
        Assert.Equal(7, BoundaryOf("a\n\n---\nb"));
    }

    // §3.3 容器上下文抑制（lazy continuation）。

    [Fact]
    public void SetextUnderline_InsideContainer_DoesNotFreeze()
    {
        // `> a\nb\n===` 的 setext 仍属于引用，抑制冻结。
        Assert.Equal(0, BoundaryOf("> a\nb\n===\n"));
    }

    [Fact]
    public void DecisiveBlockStart_InsideContainer_DoesNotFreeze()
    {
        Assert.Equal(0, BoundaryOf("> a\n- item\n"));
    }

    [Fact]
    public void Fence_InsideContainer_AtZeroIndent_StillFreezes()
    {
        Assert.Equal(19, BoundaryOf("> a\n```cs\ncode\n```\n"));
    }

    [Fact]
    public void IndentedHeading_InsideContainer_IsContainerContent()
    {
        // 容器开启期间缩进 1-3 的结构行可能是容器内容，不识别为标题。
        Assert.Equal(0, BoundaryOf("> a\n  # x\n"));
    }

    [Fact]
    public void BlankLine_ClosesContainer_ThenFreezes()
    {
        // 冻结点 = 空行后下一行的行首（空行连同其换行都归冻结前缀）。
        Assert.Equal(5, BoundaryOf("> a\n\nb\n"));
    }

    // §3.2 F5：文档首行 `---` 是 YAML front matter 候选，整块悬置到闭合行。

    [Fact]
    public void FrontMatter_SuspendsFreeze_UntilCloseLine()
    {
        Assert.Equal(0, BoundaryOf("---\ntitle: x\n"));
        Assert.Equal(17, BoundaryOf("---\ntitle: x\n---\n"));
        Assert.Equal(18, BoundaryOf("---\ntitle: x\n---\n\n正文。\n"));
    }

    [Fact]
    public void LeadingBlank_DisablesFrontMatter_DashLineIsThematic()
    {
        // front matter 只认文档首行；前置空行后的 `---` 是主题分隔线，照常冻结。
        Assert.Equal(5, BoundaryOf("\n---\nx\n"));
    }

    // §3.4 决定性新块起始：行首之前的所有内容就此定型。

    [Fact]
    public void FenceOpen_InterruptingParagraph_FreezesAtLineStart()
    {
        Assert.Equal(5, BoundaryOf("para\n```cs\n"));
    }

    [Fact]
    public void IncompleteFenceLine_InterruptingParagraph_FreezesAtLineStart()
    {
        // E2：围栏开启行未完成，但前缀已是决定性形态。
        Assert.Equal(5, BoundaryOf("para\n```cs"));
    }

    [Fact]
    public void QuoteLine_InterruptingParagraph_FreezesAtLineStart()
    {
        Assert.Equal(5, BoundaryOf("para\n> quote\n"));
    }

    [Fact]
    public void ListItem_InterruptingParagraph_FreezesAtLineStart()
    {
        Assert.Equal(5, BoundaryOf("para\n- item\n"));
    }

    [Fact]
    public void OrderedListItem_StartsWithOne_InterruptsParagraph()
    {
        Assert.Equal(5, BoundaryOf("para\n1. x\n"));
    }

    [Fact]
    public void OrderedListItem_StartsWithTwo_IsLazyContinuation()
    {
        // 只有 `1.` 可打断段落；`2.` 属 lazy continuation，不冻结。
        Assert.Equal(0, BoundaryOf("para\n2. x\n"));
    }

    [Fact]
    public void SetextUnderline_CompletesHeading_FreezesAtLineEnd()
    {
        Assert.Equal(9, BoundaryOf("para\n===\n"));
    }

    // §3.4.1 未完成行前缀：数字序列等未定型形态保持等待。

    [Theory]
    [InlineData("para\n\n123")]
    [InlineData("para\n\n123.")]
    [InlineData("para\n\n123. ")]
    public void DigitRunPrefix_WaitsUntilDecided(string text)
    {
        Assert.Equal(0, BoundaryOf(text));
    }

    [Fact]
    public void DigitRunPrefix_TurningIntoParagraph_Freezes()
    {
        Assert.Equal(6, BoundaryOf("para\n\n123x"));
    }

    [Theory]
    [InlineData("para\n-")]
    [InlineData("para\n\n-")]
    [InlineData("para\n=")]
    [InlineData("para\n==")]
    [InlineData("para\n---")]
    [InlineData("para\n#")]
    [InlineData("para\n*")]
    [InlineData("para\n`")]
    [InlineData("para\n~")]
    public void SetextRiskOrIncompleteMarkerPrefix_Waits(string text)
    {
        // E3：可能被后续字符推翻的前缀（setext 下划线、半个标记）一律等待。
        var boundaries = ProgressiveBoundaries(text);
        Assert.All(boundaries, boundary => Assert.Equal(0, boundary));
    }

    [Fact]
    public void DecisivePrefix_FreezesImmediately()
    {
        // E2：`- ` 前缀到达即冻结于行首，不等行结束。
        var boundaries = ProgressiveBoundaries("para\n- item");
        Assert.Equal(0, boundaries[4]);  // "para\n"
        Assert.Equal(0, boundaries[5]);  // "-" 可能是 setext
        Assert.Equal(5, boundaries[6]);  // "- " 决定性
        Assert.Equal(5, boundaries[^1]);
    }

    [Fact]
    public void IncompleteLine_InsideFence_NoEarlyFreeze()
    {
        // E4：围栏内不适用提前冻结。
        var boundaries = ProgressiveBoundaries("```cs\ncode - x\n");
        Assert.All(boundaries, boundary => Assert.Equal(0, boundary));
    }

    [Fact]
    public void HtmlBlockType6_RunsUntilBlank()
    {
        Assert.Equal(15, BoundaryOf("<div>\ncontent\n\nafter"));
    }

    // 边界不变量 I1：逐字符递进时边界单调不减。

    [Fact]
    public void ProgressiveStreaming_BoundaryIsMonotonic()
    {
        var boundaries = ProgressiveBoundaries("pre\n\n```cs\ncode\n```\nafter text");
        for (var i = 1; i < boundaries.Count; i++)
        {
            Assert.True(boundaries[i] >= boundaries[i - 1], $"边界回退:{boundaries[i - 1]} -> {boundaries[i]}（前缀长度 {i + 1}）");
        }

        Assert.Equal(20, boundaries[^1]);
    }

    // 围栏状态暴露给开放围栏快速路径（规格 §4.4 命中条件）。

    [Fact]
    public void OpenFenceState_IsExposedWhileInsideFence()
    {
        var segmenter = new MarkdownStreamSegmenter();
        segmenter.Scan("para\n```cs\ncode", sourceComplete: false);
        Assert.True(segmenter.IsScanningFence);
        Assert.Equal(('`', 3), segmenter.OpenFence);
        Assert.Equal(5, segmenter.FrozenBoundary);
    }
}
