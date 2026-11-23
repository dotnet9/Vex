namespace Vex.Core.Models;

/// <summary>
/// 工作区文件(夹)的 Git 状态——树内前导状态槽语义（VS 经典语言，design/Git-Badges.html REV2）：
/// TrackedClean=受管干净挂灰锁（文件行）；Modified/Renamed 红勾、New 绿加号、Deleted 红减（面板）、
/// Conflicted 橙警（面板）；Untracked 不受管控=树内空槽（枚举保留供面板与 tooltip）。
/// </summary>
public enum WorkspaceGitStatus
{
    None,
    TrackedClean,
    New,
    Untracked,
    Modified,
    Renamed,
    Deleted,
    Conflicted,
}

/// <summary>
/// Git 状态到文件树的批量应用与文件夹聚合。
/// 聚合取后代最强<b>变更</b>状态（挂锁与未跟踪不参与——文件夹只呈现变更字形，
/// 干净文件夹无字形；优先级：冲突 &gt; 删除 &gt; 修改 &gt; 重命名 &gt; 新增）。
/// </summary>
public static class WorkspaceGitStatusTree
{
    /// <summary>聚合优先级：数值越大越优先呈现（挂锁/未跟踪不参与聚合）。</summary>
    public static int Priority(WorkspaceGitStatus status) => status switch
    {
        WorkspaceGitStatus.Conflicted => 6,
        WorkspaceGitStatus.Deleted => 5,
        WorkspaceGitStatus.Modified => 4,
        WorkspaceGitStatus.Renamed => 3,
        WorkspaceGitStatus.New => 2,
        WorkspaceGitStatus.TrackedClean => 1,
        WorkspaceGitStatus.Untracked => 0,
        _ => 0,
    };

    /// <summary>是否为会呈现变更字形的聚合参与状态。</summary>
    public static bool IsChange(WorkspaceGitStatus status) =>
        status is WorkspaceGitStatus.Conflicted or WorkspaceGitStatus.Deleted
            or WorkspaceGitStatus.Modified or WorkspaceGitStatus.Renamed or WorkspaceGitStatus.New;

    /// <summary>
    /// 把状态字典写到整棵树上。字典键为文件绝对路径(不区分大小写,Windows 口径);
    /// 文件按查表赋值,文件夹取后代聚合。返回发生变化的节点数(含文件夹聚合变化),
    /// 重复应用同一字典时应返回 0——调用方据此判断是否有 UI 更新。
    /// </summary>
    public static int Apply(IReadOnlyList<DocumentFileNode> roots, IReadOnlyDictionary<string, WorkspaceGitStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(statuses);

        var changed = 0;
        foreach (var root in roots)
        {
            changed += ApplyNode(root, statuses);
        }

        return changed;
    }

    private static int ApplyNode(DocumentFileNode node, IReadOnlyDictionary<string, WorkspaceGitStatus> statuses)
    {
        var changed = 0;
        if (node.IsFolder)
        {
            foreach (var child in node.Children)
            {
                changed += ApplyNode(child, statuses);
            }

            var aggregate = AggregateChildren(node.Children);
            if (node.GitStatus != aggregate)
            {
                node.GitStatus = aggregate;
                changed++;
            }

            return changed;
        }

        var status = statuses.TryGetValue(node.Path, out var value) ? value : WorkspaceGitStatus.None;
        if (node.GitStatus != status)
        {
            node.GitStatus = status;
            changed++;
        }

        return changed;
    }

    private static WorkspaceGitStatus AggregateChildren(IEnumerable<DocumentFileNode> children)
    {
        WorkspaceGitStatus best = WorkspaceGitStatus.None;
        foreach (var child in children)
        {
            // 子文件夹的 GitStatus 已在自底向上递归中聚合完成；
            // 挂锁与未跟踪不是变更，文件夹只呈现变更字形（干净文件夹无字形）。
            if (!IsChange(child.GitStatus))
            {
                continue;
            }

            if (Priority(child.GitStatus) > Priority(best))
            {
                best = child.GitStatus;
            }
        }

        return best;
    }
}
