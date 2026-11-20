using System.Diagnostics;

using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 即时编辑每键热路径基准（手动跑，登记 docs/基准报告.md 后删除本文件）：
/// 旧路径 = 每键整篇 <see cref="MarkdownBlockParser.Write"/>（且宿主选区回读 Text 再来一次，即 ×2）；
/// 新路径 = 块文本同步 + 脏组 <see cref="MarkdownLiveTextBuffer.Splice"/>。
/// </summary>
public class MarkdownLiveSpliceBench(ITestOutputHelper output)
{
    private static string BuildDocument(int paragraphCount, int paragraphLength)
    {
        var builder = new System.Text.StringBuilder();
        var word = new string('字', paragraphLength);
        for (var i = 0; i < paragraphCount; i++)
        {
            builder.Append("## 第").Append(i).Append("章\n\n").Append(i).Append('段').Append(word).Append('\n');
            if (i % 7 == 0) builder.Append("- 列表项").Append(i).Append('\n');
            if (i % 11 == 0) builder.Append("> 引用").Append(i).Append('\n');
        }
        return builder.ToString();
    }

    public static TheoryData<int, int, int> Sizes { get; } = new()
    {
        // 段落数, 段长(≈字符), 每键次数
        { 400, 40, 100 },    // ≈20KB
        { 3000, 60, 50 },    // ≈200KB
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public void PerKeystroke_OldFullSerialize_Vs_NewDirtySplice(int paragraphs, int paragraphLength, int keystrokes)
    {
        var document = BuildDocument(paragraphs, paragraphLength);

        // 旧路径：每键同步块文本后整篇序列化（旧 RaiseChanged 的 Text 读取；选区回读再加倍）。
        var oldModels = MarkdownBlockParser.Parse(document);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var oldAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        var oldSw = Stopwatch.StartNew();
        for (var k = 0; k < keystrokes; k++)
        {
            oldModels[^1].Text += "字";
            _ = MarkdownBlockParser.Write(oldModels);
        }
        oldSw.Stop();
        var oldAlloc = GC.GetTotalAllocatedBytes(precise: true) - oldAllocStart;

        // 新路径：块文本同步 + 脏组拼接（新 RaiseChanged；宿主回读 Text 为 O(1) 缓存）。
        var newModels = MarkdownBlockParser.Parse(document);
        var buffer = new MarkdownLiveTextBuffer(newModels);
        buffer.Reset();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var newAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        var newSw = Stopwatch.StartNew();
        for (var k = 0; k < keystrokes; k++)
        {
            newModels[^1].Text += "字";
            buffer.Splice(newModels.Count - 1);
            _ = buffer.Text;
        }
        newSw.Stop();
        var newAlloc = GC.GetTotalAllocatedBytes(precise: true) - newAllocStart;

        // 等价性：两条路径的最终全文一致。
        Assert.Equal(MarkdownBlockParser.Write(oldModels), buffer.Text);

        output.WriteLine($"文档 {document.Length / 1024}KB / {paragraphs} 段 / {keystrokes} 键");
        output.WriteLine($"旧整篇序列化：{oldSw.ElapsedMilliseconds} ms（均 {oldSw.ElapsedMilliseconds / (double)keystrokes:F2} ms/键），分配 {oldAlloc / 1024 / 1024} MB");
        output.WriteLine($"新脏组拼接　：{newSw.ElapsedMilliseconds} ms（均 {newSw.ElapsedMilliseconds / (double)keystrokes:F2} ms/键），分配 {newAlloc / 1024 / 1024} MB");
    }
}

[Collection("AvaloniaPlatform")]
public class MarkdownPresenterCostBench(AvaloniaPlatformFixture platform, ITestOutputHelper output)
{
    [Fact]
    public void Parse_Vs_Reshape_OnLargeParagraph() => platform.Run(() =>
    {
        var paragraph = string.Concat(Enumerable.Repeat("这是一段用于测量的正文文字，包含中文与 ascii 123 混排。", 180)); // ≈9.7KB
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var parseStart = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 200; i++) _ = MarkdownTextRunParser.Parse(paragraph);
        sw.Stop();
        var parseAlloc = GC.GetTotalAllocatedBytes(precise: true) - parseStart;

        var face = new global::Avalonia.Media.Typeface(global::Avalonia.Media.FontFamily.Default);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var layoutStart = GC.GetTotalAllocatedBytes(precise: true);
        var sw2 = Stopwatch.StartNew();
        for (var i = 0; i < 200; i++)
        {
            _ = new global::Avalonia.Media.TextFormatting.TextLayout(paragraph, face, 15,
                global::Avalonia.Media.Brushes.Black, global::Avalonia.Media.TextAlignment.Left,
                global::Avalonia.Media.TextWrapping.Wrap, maxWidth: 800);
        }
        sw2.Stop();
        var layoutAlloc = GC.GetTotalAllocatedBytes(precise: true) - layoutStart;

        output.WriteLine($"9.7KB 段落 ×200：Parse {sw.ElapsedMilliseconds} ms（均 {sw.ElapsedMilliseconds / 200.0:F3} ms/次，分配 {parseAlloc / 200 / 1024} KB/次）；TextLayout {sw2.ElapsedMilliseconds} ms（均 {sw2.ElapsedMilliseconds / 200.0:F3} ms/次，分配 {layoutAlloc / 200 / 1024} KB/次）");
    });
}
