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

    // ---- muya enterHandler/backspaceHandler spec 翻译（边界组合批）----

    [Fact]
    public void Enter_AtStartOfParagraph_NewEmptyBlockAbove_CaretAtTextStart()
    {
        // muya: "leaves the first block empty and carries the whole text onto the second"。
        var core = new MarkdownBlockEditorCore([Paragraph("正文")]);
        var result = core.Enter(0, 0);
        Assert.True(result.Changed);
        Assert.Equal(string.Empty, core.Blocks[0].Text);
        Assert.Equal("正文", core.Blocks[1].Text);
        Assert.Equal(1, result.FocusIndex);
        Assert.Equal(0, result.Caret);
    }

    [Fact]
    public void Backspace_EmptyListItemStart_DegradesToParagraph()
    {
        // muya list 语义：空列表项块首退格降级为段落（不是删除）。
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.ListItem, Text = string.Empty }]);
        var result = core.Backspace(0);
        Assert.NotNull(result);
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[0].Kind);
        Assert.False(core.Blocks[0].IsOrdered);
    }

    [Fact]
    public void MergeForward_AtParagraphEnd_MergesNextParagraph_CaretAtJunction()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("前半"), Paragraph("后半")]);
        var result = core.MergeForward(0);
        Assert.NotNull(result);
        Assert.Equal("前半后半", core.Blocks[0].Text);
        Assert.Equal(1, core.Count);
        Assert.Equal(2, result!.Caret);
        Assert.Equal(0, result.FocusIndex);
    }

    [Fact]
    public void MergeForward_AtParagraphEnd_IntoHeading_AppendsHeadingText()
    {
        var core = new MarkdownBlockEditorCore([Paragraph("引言："), Heading(2, "结论")]);
        var result = core.MergeForward(0);
        Assert.NotNull(result);
        Assert.Equal(1, core.Count);
        Assert.Equal("引言：结论", core.Blocks[0].Text);
    }

    [Fact]
    public void Enter_OnTaskListItem_ContinuesAsUncheckedTask()
    {
        // muya 任务列表语义：回车续接任务项，新项不继承勾选态。
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.ListItem, Text = "已完成", IsTask = true, IsChecked = true }]);
        var result = core.Enter(0, core.Blocks[0].Text.Length);
        Assert.True(result.Changed);
        Assert.True(core.Blocks[1].IsTask);
        Assert.False(core.Blocks[1].IsChecked);
        Assert.Equal(string.Empty, core.Blocks[1].Text);
    }

    [Fact]
    public void Backspace_QuoteStart_DegradesToParagraph_ClearsQuoteDepth()
    {
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.Quote, QuoteDepth = 1, Text = "引用内容" }]);
        var result = core.Backspace(0);
        Assert.NotNull(result);
        Assert.Equal(MarkdownBlockKind.Paragraph, core.Blocks[0].Kind);
        Assert.Equal(0, core.Blocks[0].QuoteDepth);
        Assert.Equal("引用内容", core.Blocks[0].Text);
    }

    [Theory]
    [InlineData(">", " ", "", 0)]
    [InlineData("", "> 示例", "示例", 2)]
    public void EnterBlockMarker_PromotesCommittedQuotePrefix(string text, string input, string body, int caret)
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = text };
        var core = new MarkdownBlockEditorCore([model]);
        var result = core.EnterBlockMarker(0, text, text.Length, text.Length, input);
        Assert.Equal(new MarkdownBlockEditorCore.BlockEditResult(true, 0, caret), result);
        Assert.Equal(MarkdownBlockKind.Quote, model.Kind);
        Assert.Equal(1, model.QuoteDepth);
        Assert.Equal(body, model.Text);
        Assert.NotNull(model.Source);
    }

    [Theory]
    [InlineData("", ">")]
    [InlineData("a >", " ")]
    [InlineData("    >", " ")]
    [InlineData(">", "\n")]
    public void EnterBlockMarker_LeavesOrdinaryInputToTextBox(string text, string input)
    {
        var model = new MarkdownBlockModel { Text = text };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterBlockMarker(0, text, text.Length, text.Length, input));
        Assert.Equal(MarkdownBlockKind.Paragraph, model.Kind);
        Assert.Equal(text, model.Text);
    }

    [Fact]
    public void EnterBlockMarker_InQuote_NestsWithoutDiscardingSourceOrSuffix()
    {
        var source = new MarkdownBlockSource("\n", "> tail", "> tail", "\n", "");
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Quote, QuoteDepth = 1, Text = "tail", Source = source };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Equal(0, core.EnterBlockMarker(0, model.Text, 0, 0, "> ")!.Caret);
        Assert.Equal(2, model.QuoteDepth);
        Assert.Equal("tail", model.Text);
        Assert.Same(source, model.Source);
    }

    // ---- 块首标记族（muya/Typora 输入转换）：# 标题、-/*/＋ 无序、N./N) 有序 ----

    [Theory]
    [InlineData("# ", 1)]
    [InlineData("## ", 2)]
    [InlineData("###### ", 6)]
    public void EnterBlockMarker_HashSpace_PromotesToHeading(string prefix, int level)
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        var result = core.EnterBlockMarker(0, "", 0, 0, prefix + "正文");
        Assert.NotNull(result);
        Assert.Equal(MarkdownBlockKind.Heading, model.Kind);
        Assert.Equal(level, model.HeadingLevel);
        Assert.Equal("正文", model.Text);
        // 整段输入（"# 正文"）后光标落在正文末尾。
        Assert.Equal(2, result!.Caret);
    }

    [Fact]
    public void EnterBlockMarker_BareMarkerThenLetter_IsRejected()
    {
        // ">x" 无空格分隔不是合法标记（收紧语义）：真机流中空格时刻已完成转换。
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "  >" };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterBlockMarker(0, "  >", 3, 3, "x"));
        Assert.Equal(MarkdownBlockKind.Paragraph, model.Kind);
    }

    [Fact]
    public void EnterBlockMarker_SevenHashes_IsRejected()
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterBlockMarker(0, "", 0, 0, "####### 正文"));
        Assert.Equal(MarkdownBlockKind.Paragraph, model.Kind);
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("* ")]
    [InlineData("+ ")]
    public void EnterBlockMarker_BulletSpace_PromotesToUnorderedListItem(string prefix)
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        var result = core.EnterBlockMarker(0, "", 0, 0, prefix + "待办");
        Assert.NotNull(result);
        Assert.Equal(MarkdownBlockKind.ListItem, model.Kind);
        Assert.False(model.IsOrdered);
        Assert.False(model.IsTask);
        Assert.Equal("待办", model.Text);
    }

    [Theory]
    [InlineData("2. ", 2)]
    [InlineData("10) ", 10)]
    public void EnterBlockMarker_OrderedMarker_CarriesTheTypedNumber(string prefix, int expected)
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        var result = core.EnterBlockMarker(0, "", 0, 0, prefix + "第二步");
        Assert.NotNull(result);
        Assert.Equal(MarkdownBlockKind.ListItem, model.Kind);
        Assert.True(model.IsOrdered);
        Assert.Equal(expected, model.OrderedNumber);
        Assert.Equal("第二步", model.Text);
    }

    [Fact]
    public void EnterBlockMarker_MidTextHash_IsNotConverted()
    {
        // 文档中部巧合命中不转换：光标必须紧跟标记之后。
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "前文 " };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterBlockMarker(0, "前文 ", 4, 4, "# 标题"));
        Assert.Equal(MarkdownBlockKind.Paragraph, model.Kind);
    }

    [Fact]
    public void EnterBlockMarker_InQuote_NonQuoteMarker_IsRejected()
    {
        // 引用块内只有 > 参与转换（嵌套加深），# 等标记原样保留。
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Quote, QuoteDepth = 1, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterBlockMarker(0, "", 0, 0, "# 标题"));
        Assert.Equal(MarkdownBlockKind.Quote, model.Kind);
    }

    [Fact]
    public void Enter_OnQuote_ContinuationInheritsSource()
    {
        // 引用续行共享 Source：同组引用合并渲染为同一引用块（视图分组与序列化都依赖同源）。
        var source = new MarkdownBlockSource(string.Empty, string.Empty, string.Empty, "\n", string.Empty);
        var core = new MarkdownBlockEditorCore(
            [new MarkdownBlockModel { Kind = MarkdownBlockKind.Quote, QuoteDepth = 1, Text = "第一行", Source = source }]);
        var result = core.Enter(0, core.Blocks[0].Text.Length);
        Assert.True(result.Changed);
        Assert.True(ReferenceEquals(core.Blocks[1].Source, source), "continuation must share the quote Source");
        Assert.Equal(MarkdownBlockKind.Quote, core.Blocks[1].Kind);
    }
    // ---- 围栏代码块补全（Typora 行为）：```lang + 回车 → 转代码块源码编辑 ----

    [Theory]
    [InlineData("```", "")]
    [InlineData("```csharp", "csharp")]
    [InlineData("```  json ", "json")]
    public void EnterFenceMarker_AtBlockStart_PromotesToFencedCode(string input, string language)
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        var result = core.EnterFenceMarker(0, "", 0, 0, input);
        Assert.NotNull(result);
        Assert.Equal(MarkdownBlockKind.Code, model.Kind);
        Assert.True(model.IsFenced);
        Assert.Equal(language, model.Language);
        Assert.Equal(string.Empty, model.Text);
        Assert.Equal(0, result!.Caret);
        Assert.NotNull(model.Source);
    }

    [Fact]
    public void EnterFenceMarker_WithLeadingSpaces_Promotes()
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "  " };
        var core = new MarkdownBlockEditorCore([model]);
        var result = core.EnterFenceMarker(0, "  ", 2, 2, "```python");
        Assert.NotNull(result);
        Assert.Equal("python", model.Language);
    }

    [Fact]
    public void EnterFenceMarker_InsideExistingCodeBlock_IsRejected()
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Code, Text = "", IsFenced = true };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterFenceMarker(0, "", 0, 0, "```"));
        Assert.Equal(MarkdownBlockKind.Code, model.Kind);
    }

    [Fact]
    public void EnterFenceMarker_InQuote_IsRejected()
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Quote, QuoteDepth = 1, Text = "" };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterFenceMarker(0, "", 0, 0, "```csharp"));
        Assert.Equal(MarkdownBlockKind.Quote, model.Kind);
    }

    [Fact]
    public void EnterFenceMarker_BareText_IsRejected()
    {
        var model = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = "普通文字" };
        var core = new MarkdownBlockEditorCore([model]);
        Assert.Null(core.EnterFenceMarker(0, "普通文字", 4, 4, "```"));
        Assert.Equal(MarkdownBlockKind.Paragraph, model.Kind);
        Assert.Equal("普通文字", model.Text);
    }
}