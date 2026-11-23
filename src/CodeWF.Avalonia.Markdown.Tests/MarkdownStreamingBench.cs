using System.Diagnostics;
using System.Text;

using CodeWF.Avalonia.Markdown.Shared.Rendering;
using CodeWF.Avalonia.Markdown.Streaming;

using Markdig;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

/// <summary>
/// 流式渲染内核基准（手动跑，结果登记 docs/基准报告.md；清洁数字请单独过滤运行本类）：
/// 现状管线 = 每次追加全文重新 Markdig 解析；流式内核 = append-only 缓冲 + 冻结边界 + 尾部容错。
/// 对照口径与 2026-10-19/10-26 外仓基准一致：20KB 混合文档 4 字符/token；
/// 围栏快速路径 = 50KB 单一代码块 4 字符/token，常规路径对照为逐步 remend + 解析尾块。
/// </summary>
public class MarkdownStreamingBench(ITestOutputHelper output)
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().UseAlertBlocks().UseYamlFrontMatter()
        .Build();

    private static string BuildMixedDocument()
    {
        var builder = new StringBuilder();
        var index = 0;
        while (builder.Length < 20 * 1024)
        {
            builder.Append($"## 第 {index} 章 流式渲染\n\n")
                .Append($"这是第 {index} 段正文，**加粗**与`行内码`混排，还有[链接](https://example.com/{index})，写长一点才像真实文档。\n\n")
                .Append($"- 列表项甲 {index}\n- 列表项乙 {index}\n- [ ] 待办 {index}\n\n")
                .Append($"> 引用第 {index} 条\n> [!NOTE]\n> 提示块正文。\n\n")
                .Append("```csharp\nvar index = ").Append(index).Append(";\nConsole.WriteLine(index);\n```\n\n")
                .Append($"| 列一 | 列二 |\n|---|---|\n| 甲 {index} | 乙 {index} |\n\n");
            index++;
        }

        return builder.ToString();
    }

    private static string BuildCodeBlockDocument()
    {
        var builder = new StringBuilder("```csharp\n");
        var index = 0;
        while (builder.Length < 50 * 1024)
        {
            builder.Append("// 第 ").Append(index).Append(" 段注释，说明这段示例代码的用途\n")
                .Append("var value").Append(index).Append(" = Compute(").Append(index).Append(");\n")
                .Append("Console.WriteLine(value").Append(index).Append(");\n\n");
            index++;
        }

        return builder.ToString();
    }

    private static List<string> Tokenize(string text, int tokenSize)
    {
        var tokens = new List<string>((text.Length + tokenSize - 1) / tokenSize);
        for (var offset = 0; offset < text.Length; offset += tokenSize)
        {
            tokens.Add(text.Substring(offset, Math.Min(tokenSize, text.Length - offset)));
        }

        return tokens;
    }

    private static MarkdownStreamingSnapshot StreamAll(List<string> tokens)
    {
        using var document = new MarkdownStreamingDocument();
        foreach (var token in tokens)
        {
            document.Append(token);
        }

        document.Complete();
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var snapshot = document.Latest;
            if (snapshot.IsComplete)
            {
                return snapshot;
            }

            if (stopwatch.Elapsed > TimeSpan.FromSeconds(60))
            {
                throw new TimeoutException("等待流式终局快照超时");
            }

            Thread.Sleep(1);
        }
    }

    [Fact]
    public void MixedDocument_FullReparsePerToken_Vs_StreamingKernel()
    {
        var text = BuildMixedDocument();
        var tokens = Tokenize(text, 4);

        // 现状管线：每次追加都整篇重新解析（每步 substring 前缀 + 全篇 Markdig）。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var naiveAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        var naiveStopwatch = Stopwatch.StartNew();
        var current = string.Empty;
        foreach (var token in tokens)
        {
            current = string.Concat(current, token);
            _ = MarkdownParser.Parse(current, Pipeline);
        }

        naiveStopwatch.Stop();
        var naiveAlloc = GC.GetTotalAllocatedBytes(precise: true) - naiveAllocStart;

        // 流式内核：冻结前缀只解析一次，尾部容错补全后单独解析。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var streamingAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        var streamingStopwatch = Stopwatch.StartNew();
        var final = StreamAll(tokens);
        streamingStopwatch.Stop();
        var streamingAlloc = GC.GetTotalAllocatedBytes(precise: true) - streamingAllocStart;

        // 等价性：终局快照 ≡ 直接整篇解析（性能数字与正确性同时钉住）。
        var expected = MarkdownParser.Parse(text, Pipeline).Blocks;
        Assert.Equal(expected.Count, final.Model.Blocks.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.True(expected[i].Kind == final.Model.Blocks[i].Kind
                && expected[i].SourceSpan.Start == final.Model.Blocks[i].SourceSpan.Start
                && expected[i].ContentHash == final.Model.Blocks[i].ContentHash, $"终局块 #{i} 不一致");
        }

        output.WriteLine($"文档 {text.Length / 1024}KB / {tokens.Count} 步（4 字符/token）");
        output.WriteLine($"整篇重解析：{naiveStopwatch.ElapsedMilliseconds} ms，分配 {naiveAlloc / 1024 / 1024:F1} MB");
        output.WriteLine($"流式内核　：{streamingStopwatch.ElapsedMilliseconds} ms，分配 {streamingAlloc / 1024 / 1024:F1} MB");
        output.WriteLine($"提速 {naiveStopwatch.ElapsedMilliseconds / (double)streamingStopwatch.ElapsedMilliseconds:F1}×，降分配 {naiveAlloc / (double)streamingAlloc:F1}×");

        Assert.True(streamingStopwatch.ElapsedMilliseconds < naiveStopwatch.ElapsedMilliseconds, "流式内核不应慢于整篇重解析");
    }

    [Fact]
    public void LongCodeBlock_FenceFastPath_Vs_RegularTailParse()
    {
        var text = BuildCodeBlockDocument();
        var tokens = Tokenize(text, 4);
        var boundary = "```csharp\n".Length; // 常规路径对照的尾块起点（开围栏行之后）

        // 流式内核：开放围栏快速路径（规格 §4.4）命中整个流式过程。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var streamingAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        var streamingStopwatch = Stopwatch.StartNew();
        var final = StreamAll(tokens);
        streamingStopwatch.Stop();
        var streamingAlloc = GC.GetTotalAllocatedBytes(precise: true) - streamingAllocStart;

        // 常规路径对照：每步对尾块 remend 补全 + Markdig 解析（不含切分与快照组装）。
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var regularAllocStart = GC.GetTotalAllocatedBytes(precise: true);
        var regularStopwatch = Stopwatch.StartNew();
        var length = 0;
        foreach (var token in tokens)
        {
            length += token.Length;
            if (length <= boundary)
            {
                continue;
            }

            var tail = text[boundary..length];
            _ = MarkdownParser.Parse(MarkdownTailRemender.Apply(tail).RemendedText, Pipeline);
        }

        regularStopwatch.Stop();
        var regularAlloc = GC.GetTotalAllocatedBytes(precise: true) - regularAllocStart;

        output.WriteLine($"文档 {text.Length / 1024}KB 单一代码块 / {tokens.Count} 步（4 字符/token）");
        output.WriteLine($"常规路径（逐步 remend+解析尾块）：{regularStopwatch.ElapsedMilliseconds} ms，分配 {regularAlloc / 1024 / 1024:F1} MB");
        output.WriteLine($"围栏快速路径　　　　　　　　　　：{streamingStopwatch.ElapsedMilliseconds} ms，分配 {streamingAlloc / 1024 / 1024:F1} MB");
        output.WriteLine($"提速 {regularStopwatch.ElapsedMilliseconds / (double)streamingStopwatch.ElapsedMilliseconds:F1}×，降分配 {regularAlloc / (double)streamingAlloc:F1}×");

        Assert.Equal(MarkdownBlockKind.Code, final.Model.Blocks[0].Kind);
        Assert.True(streamingStopwatch.ElapsedMilliseconds < regularStopwatch.ElapsedMilliseconds, "快速路径不应慢于常规路径");
    }
}
