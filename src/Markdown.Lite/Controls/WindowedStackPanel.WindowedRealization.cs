using System;
using System.Collections.Generic;

// 与 Controls 主工程同名类型互为副本（WindowedStackPanel 的配套实现）：
// Markdown 包不依赖 Controls 主工程，修改任一侧须评估同步另一侧。
namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 视口窗口物化判定（纯逻辑，无 UI 依赖）：给定每个子项的高度、是否固定物化、
/// 间距与视口窗口区间，计算哪些子项需要真实创建控件。
/// <para>
/// 抽成纯函数是为了让「窗口裁剪策略」可被独立单元测试，并被多种面板实现复用。
/// 窗口区间用 <see cref="double.NegativeInfinity"/> / <see cref="double.PositiveInfinity"/>
/// 表示「不裁剪」。
/// </para>
/// </summary>
public static class WindowedRealization
{
    /// <summary>
    /// 计算每个子项是否需要物化。区间判定为「与窗口重叠」，边界相切也算命中，
    /// 避免滚动到恰好边界时出现空白行。
    /// </summary>
    public static bool[] Resolve(
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> pinned,
        double spacing,
        double windowTop,
        double windowBottom)
    {
        var result = new bool[heights.Count];
        var noWindow = double.IsNegativeInfinity(windowTop) || double.IsPositiveInfinity(windowBottom);
        var y = 0d;
        for (var i = 0; i < heights.Count; i++)
        {
            var height = Math.Max(0, heights[i]);
            result[i] = noWindow
                        || (i < pinned.Count && pinned[i])
                        || (y + height >= windowTop && y <= windowBottom);
            y += height + spacing;
        }

        return result;
    }

    /// <summary>视口窗口加上下 overscan 后的区间。</summary>
    public static (double Top, double Bottom) ResolveWindow(
        double offsetY,
        double viewportHeight,
        double overscanViewports)
    {
        var height = double.IsFinite(viewportHeight) && viewportHeight > 0 ? viewportHeight : 0d;
        if (height <= 0)
        {
            return (double.NegativeInfinity, double.PositiveInfinity);
        }

        var margin = Math.Max(0, overscanViewports) * height;
        return (offsetY - margin, offsetY + height + margin);
    }

    /// <summary>
    /// 计算「原本物化但已彻底离开视口窗口」的子项中，哪些值得保留控件不解体。
    /// <para>
    /// 只在窗口判定从「窗口内」变为「窗口外」时才发现解体候选：overscan 边缘反复进出
    /// 不会触发物化/解体循环——这是变高列表滚动时抖动的常见来源。
    /// </para>
    /// <para>
    /// 候选按两类保留：已测量的「大块」按高度从大到小装进
    /// <paramref name="keepAliveBudget"/>；尺寸未定的块按
    /// <paramref name="keepAliveUnknownCount"/> 保留「离视口最近」的那些，
    /// 距离相同时优先 <paramref name="scrollDown"/> 指向的刚滚出去的一侧
    /// （向下滚动保留窗口上沿外的那一项，向上滚动保留下沿外的），这类块按估算值占位，
    /// 保留不会改变总高度。
    /// </para>
    /// <para>
    /// <paramref name="keepAliveCandidate"/> 为 null 时全部离屏子项都是候选；
    /// 文档块这类「整体虚拟化」场景由调用方只把重块标为候选，
    /// 让轻量段落照常解体（内存占用最小）。
    /// </para>
    /// </summary>
    public static bool[] ResolveInPlace(
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> pinned,
        IReadOnlyList<bool> realized,
        IReadOnlyList<bool> inWindow,
        bool hasWindow,
        double keepAliveBudget,
        double keepAliveMinItemHeight,
        int keepAliveUnknownCount,
        IReadOnlyList<bool>? keepAliveCandidate = null,
        bool scrollDown = true)
    {
        var result = new bool[heights.Count];
        var budget = double.IsFinite(keepAliveBudget) && keepAliveBudget > 0 ? keepAliveBudget : 0d;
        var unknownQuota = Math.Max(0, keepAliveUnknownCount);
        if (!hasWindow || budget <= 0 && unknownQuota == 0)
        {
            return result;
        }

        var candidates = new List<int>();
        for (var i = 0; i < heights.Count; i++)
        {
            if (keepAliveCandidate is not null && !IsCandidate(keepAliveCandidate, i))
            {
                continue;
            }

            if (!IsRealized(realized, i)
                || IsPinned(pinned, i)
                || IsInWindow(inWindow, i))
            {
                continue;
            }

            candidates.Add(i);
        }

        // 未知尺寸：先满足保留配额，取离视口最近的那些（同距离时按滚动方向定先后）。
        if (unknownQuota > 0)
        {
            var unknownCandidates = new List<int>();
            foreach (var index in candidates)
            {
                if (IsUnknown(heights, index))
                {
                    unknownCandidates.Add(index);
                }
            }

            foreach (var index in OrderUnknownByProximity(heights, inWindow, unknownCandidates, scrollDown))
            {
                if (unknownQuota == 0)
                {
                    break;
                }

                result[index] = true;
                unknownQuota--;
            }
        }

        if (budget <= 0)
        {
            return result;
        }

        // 已测量的大块：从大到小装预算，保留大块才能省下重建（解码/光栅化）成本。
        var known = new List<int>();
        var minHeight = double.IsFinite(keepAliveMinItemHeight) && keepAliveMinItemHeight > 0
            ? keepAliveMinItemHeight
            : 0d;
        foreach (var index in candidates)
        {
            if (result[index] || IsUnknown(heights, index) || ItemHeight(heights, index) < minHeight)
            {
                continue;
            }

            known.Add(index);
        }

        known.Sort((left, right) => ItemHeight(heights, right).CompareTo(ItemHeight(heights, left)));
        foreach (var index in known)
        {
            var height = ItemHeight(heights, index);
            if (height > budget)
            {
                continue;
            }

            result[index] = true;
            budget -= height;
        }

        return result;
    }

    /// <summary>
    /// 未知尺寸候选按「离视口窗口最近」排序：窗口上方由近及远、窗口下方由近及远交替取用；
    /// 上下距离相同时优先滚动方向上刚滚出去的那一侧（<paramref name="scrollDown"/> 为真取上方）。
    /// </summary>
    private static List<int> OrderUnknownByProximity(
        IReadOnlyList<double> heights,
        IReadOnlyList<bool> inWindow,
        List<int> unknownCandidates,
        bool scrollDown)
    {
        var result = new List<int>(unknownCandidates.Count);
        var firstInWindow = -1;
        var lastInWindow = -1;
        for (var i = 0; i < heights.Count; i++)
        {
            if (!IsInWindow(inWindow, i))
            {
                continue;
            }

            if (firstInWindow < 0)
            {
                firstInWindow = i;
            }

            lastInWindow = i;
        }

        if (firstInWindow < 0)
        {
            // 没有窗口信息时退化为文档顺序。
            result.AddRange(unknownCandidates);
            return result;
        }

        var windowTop = CumulativeTop(heights, firstInWindow);
        var windowBottom = CumulativeTop(heights, lastInWindow) + ItemHeight(heights, lastInWindow);

        var above = new List<int>();
        var below = new List<int>();
        foreach (var index in unknownCandidates)
        {
            if (index < firstInWindow)
            {
                above.Add(index);
            }
            else
            {
                below.Add(index);
            }
        }

        above.Reverse();
        var aboveCursor = 0;
        var belowCursor = 0;
        while (aboveCursor < above.Count || belowCursor < below.Count)
        {
            if (aboveCursor >= above.Count)
            {
                result.Add(below[belowCursor++]);
                continue;
            }

            if (belowCursor >= below.Count)
            {
                result.Add(above[aboveCursor++]);
                continue;
            }

            var aboveIndex = above[aboveCursor];
            var belowIndex = below[belowCursor];
            var aboveDistance = windowTop - CumulativeTop(heights, aboveIndex) - ItemHeight(heights, aboveIndex);
            var belowDistance = CumulativeTop(heights, belowIndex) - windowBottom;
            if (scrollDown ? aboveDistance <= belowDistance : aboveDistance < belowDistance)
            {
                result.Add(above[aboveCursor++]);
            }
            else
            {
                result.Add(below[belowCursor++]);
            }
        }

        return result;
    }

    private static double CumulativeTop(IReadOnlyList<double> heights, int index)    {
        var top = 0d;
        for (var i = 0; i < index && i < heights.Count; i++)
        {
            top += ItemHeight(heights, i);
        }

        return top;
    }

    private static bool IsRealized(IReadOnlyList<bool> realized, int index) =>
        index < realized.Count && realized[index];

    private static bool IsCandidate(IReadOnlyList<bool> candidate, int index) =>
        index < candidate.Count && candidate[index];

    private static bool IsPinned(IReadOnlyList<bool> pinned, int index) =>
        index < pinned.Count && pinned[index];

    private static bool IsInWindow(IReadOnlyList<bool> inWindow, int index) =>
        index < inWindow.Count && inWindow[index];

    private static bool IsUnknown(IReadOnlyList<double> heights, int index) =>
        index >= heights.Count || !double.IsFinite(heights[index]) || heights[index] <= 0;

    private static double ItemHeight(IReadOnlyList<double> heights, int index) =>
        index < heights.Count && double.IsFinite(heights[index]) ? Math.Max(0, heights[index]) : 0d;
}
