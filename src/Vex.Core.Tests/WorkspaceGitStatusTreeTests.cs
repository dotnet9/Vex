using Vex.Core.Models;

using Xunit;

namespace Vex.Core.Tests;

/// <summary>
/// Git 状态到文件树的批量应用与文件夹聚合：文件按绝对路径查表（Windows 口径不区分大小写），
/// 文件夹自底向上取后代最强状态；重复应用同一字典必须零变化（差量刷新的依据）。
/// </summary>
public sealed class WorkspaceGitStatusTreeTests
{
    private static DocumentFileNode File(string path) => new(System.IO.Path.GetFileName(path), path, isFolder: false);

    private static DocumentFileNode Folder(string path, params DocumentFileNode[] children) =>
        new(System.IO.Path.GetFileName(path), path, isFolder: true, children: children);

    private static IReadOnlyDictionary<string, WorkspaceGitStatus> Dict(params (string Path, WorkspaceGitStatus Status)[] entries)
    {
        var dictionary = new Dictionary<string, WorkspaceGitStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, status) in entries)
        {
            dictionary[path] = status;
        }

        return dictionary;
    }

    [Fact]
    public void Apply_FilesAssignedFromDictionary_MissingPathsAreNone()
    {
        var readme = File(@"C:\ws\README.md");
        var clean = File(@"C:\ws\clean.md");
        var roots = new List<DocumentFileNode> { readme, clean };
        var statuses = Dict((@"C:\ws\README.md", WorkspaceGitStatus.Modified));

        var changed = WorkspaceGitStatusTree.Apply(roots, statuses);

        Assert.Equal(1, changed);
        Assert.Equal(WorkspaceGitStatus.Modified, readme.GitStatus);
        Assert.Equal(WorkspaceGitStatus.None, clean.GitStatus);
    }

    [Fact]
    public void Apply_PathLookupIsCaseInsensitive()
    {
        var node = File(@"C:\WS\readme.md");
        var statuses = Dict((@"c:\ws\README.MD", WorkspaceGitStatus.Modified));

        Assert.Equal(1, WorkspaceGitStatusTree.Apply([node], statuses));
        Assert.Equal(WorkspaceGitStatus.Modified, node.GitStatus);
    }

    [Fact]
    public void Apply_FolderAggregatesStrongestDescendant_BottomUp()
    {
        // docs 下只有修改,src 下有嵌套的新增与冲突——冲突应穿透两层文件夹聚合胜出。
        var nested = Folder(@"C:\ws\src\deep", File(@"C:\ws\src\deep\a.cs"));
        var src = Folder(@"C:\ws\src", File(@"C:\ws\src\b.cs"), nested);
        var docs = Folder(@"C:\ws\docs", File(@"C:\ws\docs\rule.md"));
        var statuses = Dict(
            (@"C:\ws\src\deep\a.cs", WorkspaceGitStatus.Conflicted),
            (@"C:\ws\src\b.cs", WorkspaceGitStatus.New),
            (@"C:\ws\docs\rule.md", WorkspaceGitStatus.Modified));

        Assert.Equal(6, WorkspaceGitStatusTree.Apply([src, docs], statuses));

        Assert.Equal(WorkspaceGitStatus.Conflicted, nested.GitStatus);
        Assert.Equal(WorkspaceGitStatus.Conflicted, src.GitStatus);
        Assert.Equal(WorkspaceGitStatus.Modified, docs.GitStatus);
    }

    [Fact]
    public void Apply_FolderStatusIsRecomputed_WhenChildrenChange()
    {
        var file = File(@"C:\ws\a.cs");
        var folder = Folder(@"C:\ws", file);
        var roots = new List<DocumentFileNode> { folder };

        WorkspaceGitStatusTree.Apply(roots, Dict());
        Assert.Equal(WorkspaceGitStatus.None, folder.GitStatus);

        WorkspaceGitStatusTree.Apply(roots, Dict((@"C:\ws\a.cs", WorkspaceGitStatus.Untracked)));
        Assert.Equal(WorkspaceGitStatus.Untracked, folder.GitStatus);

        WorkspaceGitStatusTree.Apply(roots, Dict());
        Assert.Equal(WorkspaceGitStatus.None, folder.GitStatus);
    }

    [Fact]
    public void Apply_ReapplyingSameDictionary_IsZeroChange()
    {
        var file = File(@"C:\ws\a.cs");
        var folder = Folder(@"C:\ws", file);
        var roots = new List<DocumentFileNode> { folder };
        var statuses = Dict((@"C:\ws\a.cs", WorkspaceGitStatus.Modified));

        Assert.Equal(2, WorkspaceGitStatusTree.Apply(roots, statuses));
        Assert.Equal(0, WorkspaceGitStatusTree.Apply(roots, statuses));
        Assert.Equal(0, WorkspaceGitStatusTree.Apply(roots, statuses));
    }

    [Fact]
    public void Apply_PropertyChangedFiresOnlyForActualChanges()
    {
        var file = File(@"C:\ws\a.cs");
        var fireCount = 0;
        file.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(file.GitStatus)) fireCount++; };

        WorkspaceGitStatusTree.Apply([file], Dict((@"C:\ws\a.cs", WorkspaceGitStatus.Modified)));
        Assert.Equal(1, fireCount);

        WorkspaceGitStatusTree.Apply([file], Dict((@"C:\ws\a.cs", WorkspaceGitStatus.Modified)));
        Assert.Equal(1, fireCount);

        WorkspaceGitStatusTree.Apply([file], Dict((@"C:\ws\a.cs", WorkspaceGitStatus.None)));
        Assert.Equal(2, fireCount);
    }

    [Fact]
    public void Priority_OrdersConflictAboveAll_AndUntrackedAboveNone()
    {
        Assert.True(WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Conflicted)
            > WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Deleted));
        Assert.True(WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Deleted)
            > WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Modified));
        Assert.True(WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Modified)
            > WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Renamed));
        Assert.True(WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Renamed)
            > WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.New));
        Assert.True(WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.New)
            > WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Untracked));
        Assert.True(WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.Untracked)
            > WorkspaceGitStatusTree.Priority(WorkspaceGitStatus.None));
    }
}
