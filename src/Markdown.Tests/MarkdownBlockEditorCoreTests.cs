using CodeWF.Avalonia.Markdown.Core;
using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 块编辑核心的键盘行为测试（Enter 语义对齐 Mark Text muya）：
/// 空文档首块、末尾连续回车加空行、选区拆块、空列表项降级、有序编号递增、退格合并。
/// </summary>
public class MarkdownBlockEditorCoreTests
{
    private static MarkdownBlockModel Paragraph(string text) =>
        new() { Kind = MarkdownBlockKind.Paragraph, Text = text };

    private static MarkdownBlockModel Heading(int level, string text) =>
        new() { Kind = MarkdownBlockKind.Heading, HeadingLevel = level, Text = text };

    [Fact]
    public void EnsureFirstBlock_OnEmptyDocument_AddsOneEmptyParagraph()
    {
        var core = new MarkdownBlockEditorCore([]);
        var changed = core.EnsureFirstBlock();
        Assert.True(changed);
        Assert.Equal(1, core.Count);
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[0].Kind);
        Assert.Equal(string.Empty, core.Blocks[0].Text);
    }

    [Fact]
    public void EnsureFirstBlock_WithExistingBlocks_DoesNothing()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("已有内容")]);
        var changed = core.EnsureFirstBlock();
        Assert.False(changed);
        Assert.Equal(1, core.Count);
    }

    [Fact]
    public void Enter_AtEndOfParagraph_SplitsAndFocusesNewEmptyBlock()
    {
        // 用户场景：末尾连续回车=持续加空行。
        var core = new MarkdownBlockEditorCore([Heading(1, "标题"), Paragraph("第一段")]);
        var result = core.Enter(1, 3); // 光标在段末

        Assert.True(result.Changed);
        Assert.Equal(2, result.FocusIndex);
        Assert.Equal(3, core.Count);
        Assert.Equal("第一段", core.Blocks[1].Text);
        Assert.Equal(string.Empty, core.Blocks[2].Text);
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[2].Kind);
    }

    [Fact]
    public void Enter_ConsecutiveAtEnd_AppendsEmptyParagraphsEachTime()
    {
        // 用户场景：末尾连敲三次回车，块数 2→5。
        var core = new MarkdownBlockEditorCore([Heading(1, "标题"), Paragraph("第一段")]);
        var focus = 1;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = core.Enter(focus, core.Blocks[focus].Text.Length);
            Assert.True(result.Changed);
            focus = result.FocusIndex;
        }
        Assert.Equal(5, core.Count);
    }

    [Fact]
    public void Enter_InMiddle_SplitsTextBetweenBlocks()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("前后拆分")]);
        var result = core.Enter(0, 2); // 光标在"前|后拆分"

        Assert.True(result.Changed);
        Assert.Equal(2, core.Count);
        Assert.Equal("前后", core.Blocks[0].Text);
        Assert.Equal("拆分", core.Blocks[1].Text);
        Assert.Equal(1, result.FocusIndex);
    }

    [Fact]
    public void Enter_InHeading_NewBlockBecomesParagraph()
    {
        var core = new MarkdownBlockEditorCore([Heading(2, "标题内容")]);
        var result = core.Enter(0, 2); // 光标在"标题|内容"

        Assert.True(result.Changed);
        Assert.Equal(MarkdownBlockKind.Heading, core.Blocks[0].Kind);
        Assert.Equal(2, core.Blocks[0].HeadingLevel);
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[1].Kind);
        Assert.Equal("内容", core.Blocks[1].Text);
    }

    [Fact]
    public void Enter_EmptyListItem_DegradesToParagraphWithoutNewBlock()
    {
        // Mark Text 语义：空列表项回车=退出列表（降级为段落），不加新块。
        var item = new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.ListItem, Text = string.Empty, IsOrdered = true, OrderedNumber = 2,
        };
        var core = new MarkdownBlockEditorCore([item]);

        var result = core.Enter(0, 0);

        Assert.True(result.Changed);
        Assert.Equal(1, core.Count); // 不新增块
        Assert.Equal(0, result.FocusIndex);
        Assert.Equal(MarkdownBlockKind.Paragraph, item.Kind);
        Assert.False(item.IsOrdered);
    }

    [Fact]
    public void Enter_OrderedList_SplitsAndIncrementsFollowingNumbers()
    {
        var source = new MarkdownBlockSource("", "", "", "\n", "");
        var first = new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.ListItem, Text = "第一项", IsOrdered = true, OrderedNumber = 1, Source = source,
        };
        var following = new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.ListItem, Text = "第三项", IsOrdered = true, OrderedNumber = 2, Source = source,
        };
        var core = new MarkdownBlockEditorCore([first, following]);

        var result = core.Enter(0, 3); // 第一项末尾回车

        Assert.True(result.Changed);
        Assert.Equal(3, core.Count);
        Assert.Equal("第一项", core.Blocks[0].Text);
        Assert.Equal(string.Empty, core.Blocks[1].Text);
        Assert.Equal(2, core.Blocks[1].OrderedNumber);
        Assert.Equal(3, core.Blocks[2].OrderedNumber); // 后续项编号被推后
    }

    [Fact]
    public void Enter_CodeBlock_ReturnsUnchanged()
    {
        var core = new MarkdownBlockEditorCore([new MarkdownBlockModel { Kind = MarkdownBlockKind.Code, Text = "code" }]);
        var result = core.Enter(0, 2);
        Assert.False(result.Changed);
        Assert.Equal(1, core.Count);
    }

    [Fact]
    public void Enter_CaretClampedToTextLength()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("短文")]);
        var result = core.Enter(0, 999); // 越界 caret 被钳制到末尾
        Assert.True(result.Changed);
        Assert.Equal(string.Empty, core.Blocks[1].Text);
    }

    [Fact]
    public void Backspace_AtStartOfParagraph_MergesIntoPrevious()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("前一段"), Paragraph("后一段")]);
        var result = core.Backspace(1);

        Assert.NotNull(result);
        Assert.True(result.Changed);
        Assert.Equal(1, core.Count);
        Assert.Equal("前一段后一段", core.Blocks[0].Text);
        Assert.Equal(0, result.FocusIndex);
        Assert.Equal(3, result.Caret); // 光标落在合并点
    }

    [Fact]
    public void Backspace_HeadingStart_DegradesToParagraph()
    {
        var core = new MarkdownBlockEditorCore([Heading(1, "标题文本")]);
        var result = core.Backspace(0);

        Assert.NotNull(result);
        Assert.True(result.Changed);
        Assert.Equal(1, core.Count);
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[0].Kind);
        Assert.Equal("标题文本", core.Blocks[0].Text);
    }

    [Fact]
    public void Backspace_FirstBlockWithNoPrevious_ReturnsNull()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("唯一段")]);
        Assert.Null(core.Backspace(0));
    }

    [Fact]
    public void Backspace_CodeBlock_ReturnsNull()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("前"), new MarkdownBlockModel { Kind = MarkdownBlockKind.Code, Text = "code" }]);
        Assert.Null(core.Backspace(1));
    }

    [Fact]
    public void Enter_OnDemoDocument_TyporaFlow()
    {
        // 完整流：标题+段落文档 → 段末连续回车加空行 → 再输入文字。
        var core = new MarkdownBlockEditorCore([Heading(1, "探针标题"), Paragraph("第一段内容。")]);
        var focus = 1;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var result = core.Enter(focus, core.Blocks[focus].Text.Length);
            focus = result.FocusIndex;
        }
        Assert.Equal(4, core.Count);
        core.Blocks[focus].Text = "新输入的文字";
        Assert.Equal("新输入的文字", core.Blocks[focus].Text);
        Assert.Equal(4, core.Count);
    }

    // ---- 块转换矩阵（ConvertBlock，语义对齐 muya）----

    [Fact]
    public void Convert_ParagraphToHeading_SetsLevelAndKeepsText()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("标题文字")]);
        Assert.True(core.ConvertBlock(0, MarkdownBlockKind.Heading, headingLevel: 2));
        Assert.Equal(MarkdownBlockKind.Heading, core.Blocks[0].Kind);
        Assert.Equal(2, core.Blocks[0].HeadingLevel);
        Assert.Equal("标题文字", core.Blocks[0].Text);
    }

    [Fact]
    public void Convert_ParagraphToOrderedListItem_StartsAtOne()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("第一步")]);
        Assert.True(core.ConvertBlock(0, MarkdownBlockKind.ListItem, isOrdered: true));
        Assert.True(core.Blocks[0].IsOrdered);
        Assert.Equal(1, core.Blocks[0].OrderedNumber);
        Assert.Equal("1. 第一步", MarkdownBlockParser.Write(core.Blocks).Trim());
    }

    [Fact]
    public void Convert_ParagraphToQuote_DepthAtLeastOne()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("引用内容")]);
        Assert.True(core.ConvertBlock(0, MarkdownBlockKind.Quote));
        Assert.Equal(1, core.Blocks[0].QuoteDepth);
        Assert.Equal("> 引用内容", MarkdownBlockParser.Write(core.Blocks).Trim());
    }

    [Fact]
    public void Convert_ListItemToParagraph_ClearsListMarkers()
    {
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.ListItem, Text = "待办", IsOrdered = true, OrderedNumber = 3, IndentLevel = 2, IsChecked = true }]);
        Assert.True(core.ConvertBlock(0, MarkdownBlockKind.Paragraph));
        var model = core.Blocks[0];
        Assert.Equal(MarkdownBlockKind.Paragraph, model.Kind);
        Assert.False(model.IsOrdered);
        Assert.False(model.IsTask);
        Assert.False(model.IsChecked);
        Assert.Equal(0, model.OrderedNumber);
        Assert.Equal(0, model.IndentLevel);
        Assert.Equal("待办", MarkdownBlockParser.Write(core.Blocks).Trim());
    }

    [Fact]
    public void Convert_QuoteToListItem_DropsQuotePrefix()
    {
        var core = new MarkdownBlockEditorCore([new MarkdownBlockModel { Kind = MarkdownBlockKind.Quote, QuoteDepth = 1, Text = "引用转列表" }]);
        Assert.True(core.ConvertBlock(0, MarkdownBlockKind.ListItem));
        Assert.Equal(0, core.Blocks[0].QuoteDepth);
        Assert.Equal("- 引用转列表", MarkdownBlockParser.Write(core.Blocks).Trim());
    }

    [Fact]
    public void Convert_HeadingToParagraph_ClearsHeadingLevel()
    {
        var core = new MarkdownBlockEditorCore([Heading(3, "降级标题")]);
        Assert.True(core.ConvertBlock(0, MarkdownBlockKind.Paragraph));
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[0].Kind);
        Assert.Equal(0, core.Blocks[0].HeadingLevel);
    }

    [Fact]
    public void Convert_HeadingLevel_ClampedTo1To6()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("越界层级")]);
        core.ConvertBlock(0, MarkdownBlockKind.Heading, headingLevel: 9);
        Assert.Equal(6, core.Blocks[0].HeadingLevel);
        core.ConvertBlock(0, MarkdownBlockKind.Heading, headingLevel: 0);
        Assert.Equal(1, core.Blocks[0].HeadingLevel);
    }

    [Fact]
    public void Convert_CodeBlock_IsRejected()
    {
        var core = new MarkdownBlockEditorCore([new MarkdownBlockModel { Kind = MarkdownBlockKind.Code, Text = "code", IsFenced = true }]);
        Assert.False(core.ConvertBlock(0, MarkdownBlockKind.Heading, 1));
        Assert.Equal(MarkdownBlockKind.Code, core.Blocks[0].Kind);
    }

    [Fact]
    public void Convert_IndexOutOfRange_ReturnsFalse()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("唯一一块")]);
        Assert.False(core.ConvertBlock(1, MarkdownBlockKind.Heading, 1));
        Assert.False(core.ConvertBlock(-1, MarkdownBlockKind.Heading, 1));
    }

    // ---- 列表缩进矩阵（IndentListItem，Tab/Shift+Tab）----

    [Fact]
    public void Indent_ListItem_IncreasesLevel()
    {
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.ListItem, Text = "子项" }]);
        Assert.True(core.IndentListItem(0, 1));
        Assert.Equal(1, core.Blocks[0].IndentLevel);
        Assert.Equal("  - 子项", MarkdownBlockParser.Write(core.Blocks).TrimEnd());
    }

    [Fact]
    public void Indent_Paragraph_IsRejected()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("不是列表")]);
        Assert.False(core.IndentListItem(0, 1));
        Assert.Equal(0, core.Blocks[0].IndentLevel);
    }

    [Fact]
    public void Outdent_AtLevel0_ClampsToZero()
    {
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.ListItem, Text = "顶格" }]);
        Assert.True(core.IndentListItem(0, -1));
        Assert.Equal(0, core.Blocks[0].IndentLevel);
    }
}
