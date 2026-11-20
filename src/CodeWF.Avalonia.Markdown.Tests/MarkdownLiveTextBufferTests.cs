using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using global::Avalonia.Controls;
using global::Avalonia.Threading;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 即时编辑全文缓存（脏组增量回写）的正确性面：
/// <para>
/// 1. <see cref="MarkdownBlockParser.WriteWithSpans"/> 的文本必须与 <see cref="MarkdownBlockParser.Write"/> 逐字符一致，
///    组区间恰好划分全文，非生成组可单独用 <see cref="MarkdownBlockParser.WriteGroupSegment"/> 复现片段；
/// 2. <see cref="MarkdownLiveTextBuffer.Splice"/> 对纯文本/任务勾选/表格单元格/引用组/围栏内容的修改，
///    结果必须与整篇重算一致（I4 等价性的编辑侧对应物）；
/// 3. 生成组（结构编辑产物）拒绝增量、回退整体重算；
/// 4. 行数缓存与整篇扫描一致。
/// </para>
/// </summary>
public class MarkdownLiveTextBufferTests
{
    public static TheoryData<string> Documents { get; } = new()
    {
        string.Empty,
        "单段落",
        "# 标题\n\n第一段\n\n第二段\n\n- 列表项\n",
        "- [ ] 待办\n- [x] 已办\n",
        "> 引用一\n> 引用二\n> 引用三\n",
        "1. 有序\n2. 第二项\n   - 嵌套\n",
        "| 甲 | 乙 |\n| --- | --- |\n| 一 | 二 |\n",
        "```csharp\nvar answer = 42;\nvar more = true;\n```\n",
        "---\ntitle: 标题\n---\n\n正文\n",
        "开头\n\n尾随文本",
        "Windows 换行\r\n\r\n第二段\r\n",
    };

    [Theory]
    [MemberData(nameof(Documents))]
    public void WriteWithSpans_TextMatchesWriteAndSpansPartition(string markdown)
    {
        var models = MarkdownBlockParser.Parse(markdown);
        var (text, spans) = MarkdownBlockParser.WriteWithSpans(models);

        Assert.Equal(MarkdownBlockParser.Write(models), text);

        var offset = 0;
        foreach (var span in spans)
        {
            Assert.Equal(offset, span.Start);
            offset = span.End;
        }
        Assert.Equal(text.Length, offset);

        // 非生成组片段可独立复现：与全文中该组区间逐字符一致（组序号与 spans 顺序对齐）。
        var groupOrdinal = 0;
        for (var index = 0; index < models.Count;)
        {
            var source = models[index].Source;
            var first = index;
            index++;
            while (source is not null && index < models.Count && ReferenceEquals(models[index].Source, source)) index++;
            if (source is { IsGenerated: false })
            {
                var group = models.GetRange(first, index - first);
                var span = spans[groupOrdinal];
                var segment = MarkdownBlockParser.WriteGroupSegment(group, source, isLast: index == models.Count);
                Assert.Equal(text.Substring(span.Start, span.Length), segment);
            }
            groupOrdinal++;
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Reset_TextAndLineCountMatchWholeScan(string markdown)
    {
        var models = MarkdownBlockParser.Parse(markdown);
        var buffer = new MarkdownLiveTextBuffer(models);
        buffer.Reset();

        Assert.Equal(MarkdownBlockParser.Write(models), buffer.Text);
        Assert.Equal(1 + buffer.Text.Count('\n'), buffer.LineCount);
    }

    [Fact]
    public void Splice_ParagraphEdit_MatchesWholeRewrite()
    {
        var buffer = CreateBuffer("# 标题\n\n第一段\n\n第二段\n\n- 列表项", out var models);
        var before = buffer.Text;

        models[1].Text = "第一段更多";
        Assert.True(buffer.Splice(1));
        Assert.NotEqual(before, buffer.Text);
        Assert.Equal("# 标题\n\n第一段更多\n\n第二段\n\n- 列表项", buffer.Text);
        Assert.Equal(7, buffer.LineCount);

        // 幂等：拼接结果与整篇重算一致。
        buffer.Reset();
        Assert.Equal("# 标题\n\n第一段更多\n\n第二段\n\n- 列表项", buffer.Text);
    }

    [Fact]
    public void Splice_SoftLineBreak_GrowsLineCount()
    {
        var buffer = CreateBuffer("# 标题\n\n第一段\n\n第二段\n\n- 列表项", out var models);
        models[1].Text = "第一段甲\n第一段乙";
        Assert.True(buffer.Splice(1));
        Assert.Equal("# 标题\n\n第一段甲\n第一段乙\n\n第二段\n\n- 列表项", buffer.Text);
        Assert.Equal(8, buffer.LineCount);
    }

    [Fact]
    public void Splice_TaskCheck_MatchesWholeRewrite()
    {
        var buffer = CreateBuffer("- [ ] 待办", out var models);
        models[0].IsChecked = true;
        Assert.True(buffer.Splice(0));
        Assert.Equal("- [x] 待办", buffer.Text);
    }

    [Fact]
    public void Splice_QuoteGroupRewritesWholeGroup()
    {
        // 引用组的多模型来自「引用内列表」（纯多行引用是单模型懒续行）。
        var buffer = CreateBuffer("> - 甲\n> - 乙\n> - 丙", out var models);
        Assert.True(models.Count >= 3, "引用内列表项应拆为多个模型");
        Assert.True(models[0].Source is { IsGenerated: false });
        Assert.True(ReferenceEquals(models[0].Source, models[1].Source), "引用组共享同一 Source 才能整组序列化");

        models[1].Text = "乙改";
        Assert.True(buffer.Splice(1));
        Assert.Equal("> - 甲\n> - 乙改\n> - 丙", buffer.Text);
    }

    /// <summary>回写保真回归：编辑列表项不得把紧列表改写成松散列表（成员间插空行）。</summary>
    [Fact]
    public void Splice_ListItemEdit_KeepsTightList()
    {
        var buffer = CreateBuffer("- 甲\n- 乙\n- 丙", out var models);
        models[1].Text = "乙改";
        Assert.True(buffer.Splice(1));
        Assert.Equal("- 甲\n- 乙改\n- 丙", buffer.Text);
    }

    /// <summary>回写保真回归：编辑引用组任一成员不得把引用内标题降级为普通行。</summary>
    [Fact]
    public void Splice_QuoteHeadingEdit_PreservesHeadingLevel()
    {
        var buffer = CreateBuffer("> ## 引用标题\n> 正文一\n> 正文二", out var models);
        models[1].Text = "正文一改";
        Assert.True(buffer.Splice(1));
        Assert.Equal("> ## 引用标题\n> 正文一改", buffer.Text);
    }

    [Fact]
    public void Splice_TableCell_MatchesWholeRewrite()
    {
        var buffer = CreateBuffer("| 甲 | 乙 |\n| --- | --- |\n| 一 | 二 |", out var models);
        Assert.Equal(MarkdownBlockKind.Table, models[0].Kind);
        models[0].Cells[1][0] = "一改";
        Assert.True(buffer.Splice(0));
        Assert.Equal("| 甲 | 乙 |\n| --- | --- |\n| 一改 | 二 |", buffer.Text);
    }

    [Fact]
    public void Splice_CodeFenceContent_MatchesWholeRewrite()
    {
        var buffer = CreateBuffer("```csharp\nvar answer = 42;\n```", out var models);
        Assert.Equal(MarkdownBlockKind.Code, models[0].Kind);
        // 块 Text 不含结尾换行（ExtractCode 口径），带尾换行会在闭合围栏前产生空行。
        models[0].Text = "var answer = 43;\nvar next = 44;";
        Assert.True(buffer.Splice(0));
        Assert.Equal("```csharp\nvar answer = 43;\nvar next = 44;\n```", buffer.Text);
        Assert.Equal(4, buffer.LineCount);
    }

    [Fact]
    public void Splice_HeadingLanguageEdit_MatchesWholeRewrite()
    {
        var buffer = CreateBuffer("```csharp\nvar a = 1;\n```", out var models);
        models[0].Language = "rust";
        Assert.True(buffer.Splice(0));
        Assert.Equal("```rust\nvar a = 1;\n```", buffer.Text);
    }

    [Fact]
    public void Splice_GeneratedGroup_FallsBackToWholeRewrite()
    {
        var models = MarkdownBlockParser.Parse("第一段");
        // 模拟 Core 结构编辑的产物：新块挂生成快照。
        models.Add(new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.Paragraph,
            Text = "新块",
            Source = new MarkdownBlockSource(string.Empty, string.Empty, string.Empty, "\n", string.Empty) { IsGenerated = true }
        });
        var buffer = new MarkdownLiveTextBuffer(models);
        buffer.Reset();

        Assert.False(buffer.Splice(1));
        // 回退路径下文本仍与整篇重算一致。
        Assert.Equal(MarkdownBlockParser.Write(models), buffer.Text);
    }

    [Fact]
    public void Splice_WithoutStructuralDrift_KeepsReferenceForUnchangedSegment()
    {
        var buffer = CreateBuffer("# 标题\n\n第一段", out var models);
        var before = buffer.Text;
        // 内容等值修改（同文本重写）：片段不变，文本引用不动。
        models[1].Text = "第一段";
        Assert.True(buffer.Splice(1));
        Assert.Equal("# 标题\n\n第一段", buffer.Text);
        Assert.Equal(before, buffer.Text);
    }

    private static MarkdownLiveTextBuffer CreateBuffer(string markdown, out List<MarkdownBlockModel> models)
    {
        models = MarkdownBlockParser.Parse(markdown);
        var buffer = new MarkdownLiveTextBuffer(models);
        buffer.Reset();
        return buffer;
    }
}

/// <summary>
/// 视图级接线验证：真实编辑框输入（InsertText）走脏组拼接后，
/// <see cref="MarkdownLiveEditorView.Text"/> 与 <see cref="MarkdownLiveEditorView.MarkdownChanged"/> 回抛值一致。
/// </summary>
[Collection("AvaloniaPlatform")]
public sealed class MarkdownLiveEditorSpliceTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void EditingMiddleBlock_RaisesCanonicalText()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            string? raised = null;
            var raiseCount = 0;
            view.MarkdownChanged += (_, text) => { raised = text; raiseCount++; };
            var window = new Window { Width = 600, Height = 400, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.SetText("# 标题\n\n第一段\n\n第二段\n\n- 列表项");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.Equal("# 标题\n\n第一段\n\n第二段\n\n- 列表项", view.Text);

                view.FocusBlock(1);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.InsertText("更多");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.Equal("# 标题\n\n第一段更多\n\n第二段\n\n- 列表项", view.Text);
                Assert.Equal("# 标题\n\n第一段更多\n\n第二段\n\n- 列表项", raised);
                Assert.True(raiseCount > 0);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
