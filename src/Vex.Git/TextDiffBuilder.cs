using LibGit2Sharp;
using System.Text;

namespace Vex.Git;

/// <summary>
/// 纯托管行级 diff：LibGit2Sharp 的 Diff.Compare（生成 patch 的管线）在 NativeAOT 下会抛
/// 无栈 NRE 直接 fail-fast（TreeChanges/树枚举/blob 读取等值调用 API 均正常，仅 Diff 崩），
/// 因此行级差异全部走这里的 Myers 算法实现。
/// </summary>
public static class TextDiffBuilder
{
    private const int MaxLcsRows = 300;

    /// <summary>构造整文件单 hunk 的行级 diff（hunk 头按显示口径合成，与原型 diffHtml 一致）。</summary>
    public static TextDiff Build(string path, IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var rows = Classify(oldLines, newLines);
        var header = $"@@ -1,{oldLines.Count} +1,{newLines.Count} @@";
        return new TextDiff(path, [new DiffHunk(header, rows)]);
    }

    public static TextDiff BuildFromText(string path, string? oldText, string? newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);
        return Build(path, oldLines, newLines);
    }

    private static List<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text)) return [];
        var lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        return [.. lines];
    }

    /// <summary>
    /// Myers 简化版：生成对齐行序列（context ' ' / 删除 '-' / 新增 '+'）。
    /// 中段超过 300 行退化为「公共前缀/后缀 + 中段全删全加」——LCS 的 O(n·m) DP 在
    /// UI 线程对大文件是秒级卡顿（64MB int 表），显示用途不需要精确最小编辑脚本。
    /// </summary>
    public static List<DiffLine> Classify(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var rows = new List<DiffLine>();
        var (start, oldEnd, newEnd) = TrimCommonEdges(oldLines, newLines);
        for (var i = 0; i < start; i++) rows.Add(new DiffLine(' ', oldLines[i]));

        var oldMiddle = oldEnd - start;
        var newMiddle = newEnd - start;
        if (oldMiddle == 0 && newMiddle == 0)
        {
            AppendTail(rows, oldLines, newLines, start, oldEnd, newEnd);
            return rows;
        }

        if (oldMiddle <= MaxLcsRows && newMiddle <= MaxLcsRows)
        {
            AppendLcs(rows, oldLines, newLines, start, oldEnd, newEnd);
        }
        else
        {
            for (var i = start; i < oldEnd; i++) rows.Add(new DiffLine('-', oldLines[i]));
            for (var j = start; j < newEnd; j++) rows.Add(new DiffLine('+', newLines[j]));
        }

        AppendTail(rows, oldLines, newLines, start, oldEnd, newEnd);
        return rows;
    }

    private static (int Start, int OldEnd, int NewEnd) TrimCommonEdges(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var start = 0;
        var min = Math.Min(oldLines.Count, newLines.Count);
        while (start < min && oldLines[start] == newLines[start]) start++;
        var oldEnd = oldLines.Count;
        var newEnd = newLines.Count;
        while (oldEnd > start && newEnd > start && oldLines[oldEnd - 1] == newLines[newEnd - 1])
        {
            oldEnd--;
            newEnd--;
        }
        return (start, oldEnd, newEnd);
    }

    private static void AppendLcs(List<DiffLine> rows, IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines,
        int start, int oldEnd, int newEnd)
    {
        var n = oldEnd - start;
        var m = newEnd - start;
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = oldLines[start + i] == newLines[start + j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var oi = 0;
        var nj = 0;
        while (oi < n && nj < m)
        {
            if (oldLines[start + oi] == newLines[start + nj])
            {
                rows.Add(new DiffLine(' ', oldLines[start + oi]));
                oi++;
                nj++;
            }
            else if (lcs[oi + 1, nj] >= lcs[oi, nj + 1])
            {
                rows.Add(new DiffLine('-', oldLines[start + oi]));
                oi++;
            }
            else
            {
                rows.Add(new DiffLine('+', newLines[start + nj]));
                nj++;
            }
        }
        while (oi < n) { rows.Add(new DiffLine('-', oldLines[start + oi])); oi++; }
        while (nj < m) { rows.Add(new DiffLine('+', newLines[start + nj])); nj++; }
    }

    private static void AppendTail(List<DiffLine> rows, IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines,
        int start, int oldEnd, int newEnd)
    {
        for (var i = oldEnd; i < oldLines.Count; i++) rows.Add(new DiffLine(' ', oldLines[i]));
        for (var j = newEnd; j < newLines.Count; j++) rows.Add(new DiffLine(' ', newLines[j]));
    }

    /// <summary>行级增删统计（diff 分类计数；二进制等场景由调用方决定不进此路径）。</summary>
    public static (int Added, int Deleted) CountRows(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        int added = 0, deleted = 0;
        foreach (var line in Classify(oldLines, newLines))
        {
            if (line.Kind == '+') added++;
            else if (line.Kind == '-') deleted++;
        }
        return (added, deleted);
    }

    /// <summary>blob 流 → 行列表（统一编码入口，供 HEAD/父提交内容读取）。</summary>
    public static List<string> ReadBlobLines(Blob blob)
    {
        using var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line) lines.Add(line);
        return lines;
    }
}
