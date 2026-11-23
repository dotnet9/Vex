namespace Vex.Core.Models;

/// <summary>
/// 工作区文件(夹)的 Git 状态——图标语义对齐 GitHub Desktop:
/// 新增/未跟踪/复制同用 diff-added 字形(绿色),修改 diff-modified(琥珀),
/// 重命名 diff-renamed(蓝),删除 diff-removed(红,文件不在磁盘故仅 Git 面板展示),
/// 冲突 alert(橙)。非 None 状态由 Shell 层批量应用到节点并驱动角标渲染。
/// </summary>
public enum WorkspaceGitStatus
{
    None,
    New,
    Untracked,
    Modified,
    Renamed,
    Deleted,
    Conflicted,
}

/// <summary>
/// Git 状态到文件树的批量应用与文件夹聚合。
/// 聚合取后代最强状态(优先级:冲突 &gt; 删除 &gt; 修改 &gt; 重命名 &gt; 新增 &gt; 未跟踪),
/// 自底向上计算后写在文件夹节点上,供树/列表模板直接绑定。
/// </summary>
public static class WorkspaceGitStatusTree
{
    /// <summary>聚合优先级:数值越大越优先呈现。</summary>
    public static int Priority(WorkspaceGitStatus status) => status switch
    {
        WorkspaceGitStatus.Conflicted => 6,
        WorkspaceGitStatus.Deleted => 5,
        WorkspaceGitStatus.Modified => 4,
        WorkspaceGitStatus.Renamed => 3,
        WorkspaceGitStatus.New => 2,
        WorkspaceGitStatus.Untracked => 1,
        _ => 0,
    };

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
            // 子文件夹的 GitStatus 已在自底向上递归中聚合完成。
            if (Priority(child.GitStatus) > Priority(best))
            {
                best = child.GitStatus;
            }
        }

        return best;
    }
}
