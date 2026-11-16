using System.Text;
using LibGit2Sharp;

namespace Vex.Git;

/// <summary>文件状态枚举：对应原型描边标志（M 黄点 / A 绿加号 / D 红减号 / ? 未跟踪）。</summary>
public enum GitChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
}

/// <summary>工作区改动条目：路径 + 状态 + 行级增删统计（-1 = 未知，二进制或统计失败，UI 不显示计数）。</summary>
public sealed record ChangeEntry(string Path, GitChangeKind Kind, int LinesAdded = -1, int LinesDeleted = -1)
{
    public string KindLabel => Kind switch
    {
        GitChangeKind.Added => "A",
        GitChangeKind.Deleted => "D",
        GitChangeKind.Modified => "M",
        GitChangeKind.Renamed => "R",
        _ => "?",
    };

    public bool HasLineStats => LinesAdded >= 0 && LinesDeleted >= 0;
}

public sealed record CommitInfo(string Sha, string Message, string Author, DateTimeOffset When, string? Tag = null);

/// <summary>历史某次提交里的文件改动（状态描边 + 路径）。</summary>
public sealed record CommitFileChange(string Path, GitChangeKind Kind);

/// <summary>单行 diff：Kind 为 ' '（上下文）/ '+'（新增）/ '-'（删除）。</summary>
public sealed record DiffLine(char Kind, string Text);

public sealed record DiffHunk(string Header, IReadOnlyList<DiffLine> Lines);

/// <summary>一个文件的文本 diff。</summary>
public sealed record TextDiff(string OldPath, IReadOnlyList<DiffHunk> Hunks);

/// <summary>内置 git 基座：LibGit2Sharp 进程内完成本地操作与 HTTPS+PAT 推送拉取；
/// SSH 远程推送交由系统 git（原型口径：SSH 需系统 Git 环境，改用 HTTPS 则无需安装）。</summary>
public sealed class GitService
{
    private readonly string _repoRoot;
    private readonly object _repoGate = new();
    private Repository? _repo;

    public GitService(string repoRoot) => _repoRoot = repoRoot;

    /// <summary>常驻仓库句柄：面板高频刷新/提交复用同一 handle，消除每调用创建/释放
    /// native 仓库句柄的风暴（AOT 下句柄释放与 GC 时序交错会随机无栈 NRE）。</summary>
    private Repository Repo()
    {
        lock (_repoGate)
        {
            _repo ??= new Repository(_repoRoot);
            return _repo;
        }
    }

    public void Dispose()
    {
        lock (_repoGate)
        {
            _repo?.Dispose();
            _repo = null;
        }
    }

    /// <summary>是否持有该路径的常驻句柄（同路径复用，跨路径才需要重建）。</summary>
    public bool Owns(string repoRoot) =>
        string.Equals(Path.GetFullPath(_repoRoot), Path.GetFullPath(repoRoot), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>路径是否已是 Git 仓库（Git 面板入口探测）。</summary>
    public static bool IsRepository(string path) =>
        !string.IsNullOrWhiteSpace(path) && Directory.Exists(path) && Repository.IsValid(path);

    /// <summary>在目录初始化本地仓库，默认分支 main（原型口径）。
    /// LibGit2Sharp 的 Init 第二参是 gitdir 而非分支名：先按默认惯例 init，
    /// 再把 unborn HEAD 的符号引用改指 main（unborn 状态下 HEAD 是普通文件，直接改写安全）。</summary>
    public static void InitRepository(string path)
    {
        Repository.Init(path);
        var headPath = System.IO.Path.Combine(path, ".git", "HEAD");
        if (File.Exists(headPath)) File.WriteAllText(headPath, "ref: refs/heads/main\n");
    }

    public RepositoryStatus Status()
    {
        var repo = Repo();
        return repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true });
    }

    /// <summary>分支名与领先/落后远端的提交数（顶栏芯片与推送可用性）；
    /// 未配置 upstream（首次推送前）TrackingDetails 为 null，领先落后记 0。</summary>
    public (string Branch, int? AheadBy, int? BehindBy) BranchState()
    {
        var repo = Repo();
        var head = repo.Head;
        var tracking = head.TrackingDetails;
        return (head.FriendlyName, tracking?.AheadBy ?? 0, tracking?.BehindBy ?? 0);
    }

    /// <summary>工作区改动清单（含未跟踪），带 +/- 行数统计。</summary>
    public IReadOnlyList<ChangeEntry> Changes()
    {
        var pending = new List<(string Path, GitChangeKind Kind)>();
        foreach (var item in Status())
        {
            var path = item.FilePath.Replace('\\', '/');
            GitChangeKind kind;
            if (item.State.HasFlag(FileStatus.NewInWorkdir)) kind = GitChangeKind.Untracked;
            else if (item.State.HasFlag(FileStatus.NewInIndex)) kind = GitChangeKind.Added;
            else if (item.State.HasFlag(FileStatus.DeletedFromWorkdir) || item.State.HasFlag(FileStatus.DeletedFromIndex)) kind = GitChangeKind.Deleted;
            else if (item.State.HasFlag(FileStatus.RenamedInWorkdir) || item.State.HasFlag(FileStatus.RenamedInIndex)) kind = GitChangeKind.Renamed;
            else if (item.State.HasFlag(FileStatus.ModifiedInWorkdir) || item.State.HasFlag(FileStatus.ModifiedInIndex)) kind = GitChangeKind.Modified;
            else continue;
            pending.Add((path, kind));
        }

        var lineStats = CountLineStats(pending.Select(p => p.Path));
        return pending
            .Select(p => new ChangeEntry(p.Path, p.Kind, lineStats.GetValueOrDefault(p.Path, (-1, -1)).Item1, lineStats.GetValueOrDefault(p.Path, (-1, -1)).Item2))
            .ToList();
    }

    /// <summary>一批文件的 +/- 行数：HEAD（或空）→ 工作区一次性 diff 取 Patch 行统计；失败的文件保持 -1（不阻断列表）。</summary>
    private Dictionary<string, (int Added, int Deleted)> CountLineStats(IEnumerable<string> paths)
    {
        var stats = new Dictionary<string, (int, int)>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var repo = Repo();
            var tip = repo.Head.Tip;
            foreach (var path in paths)
            {
                if (IsBinaryPath(path)) continue;
                var full = System.IO.Path.Combine(_repoRoot, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (new FileInfo(full).Length > 1024 * 1024) continue; // 大文件跳过行统计（-1 不显示计数）
                var newLines = ReadWorkingLines(path);
                IReadOnlyList<string> oldLines = [];
                if (tip is not null && tip.Tree[path]?.Target is Blob oldBlob)
                {
                    oldLines = TextDiffBuilder.ReadBlobLines(oldBlob);
                }
                var (added, deleted) = TextDiffBuilder.CountRows(oldLines, newLines);
                stats[path.Replace('\\', '/')] = (added, deleted);
            }
        }
        catch
        {
            // 行统计失败（如无 HEAD 的空仓库 diff 异常）：保持空表，UI 不显示计数。
        }

        // 未跟踪文件在部分 diff 口径下不产生 patch：新增的文本文件按行数补齐（二进制保持未知）。
        foreach (var path in paths)
        {
            if (stats.ContainsKey(path)) continue;
            try
            {
                var full = System.IO.Path.Combine(_repoRoot, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (File.Exists(full) && !IsBinaryPath(path)) stats[path] = (File.ReadLines(full).Count(), 0);
            }
            catch
            {
                // 读取失败（占用/编码）：不补。
            }
        }
        return stats;
    }

    private static bool IsBinaryPath(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" or ".ico" or ".pdf" or ".zip" or ".dll" or ".exe";
    }

    public IReadOnlyList<CommitInfo> History(int count = 30) => History(skip: 0, count);

    /// <summary>分页取历史（skip 之后的 count 条）：面板滚动近底部时续拉下一批。</summary>
    public IReadOnlyList<CommitInfo> History(int skip, int count)
    {
        var repo = Repo();
        var tags = TagLookup(repo);
        return repo.Commits.Skip(skip).Take(count)
            .Select(c => new CommitInfo(c.Sha[..7], c.MessageShort, c.Author.Name, c.Author.When, tags.GetValueOrDefault(c.Sha)))
            .ToList();
    }

    /// <summary>提交 sha → 首个轻量/附注标签名（历史行的标签芯片）。</summary>
    private static Dictionary<string, string> TagLookup(Repository repo)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in repo.Tags)
        {
            var sha = tag.Target is LibGit2Sharp.Commit commit ? commit.Sha : tag.Target?.Sha;
            if (sha is not null && !map.ContainsKey(sha)) map[sha] = tag.FriendlyName;
        }
        return map;
    }

    /// <summary>某次提交相对其父提交的改动文件列表（History 右区文件列）。
    /// 初始提交（无父）直接枚举提交树全部按新增处理——AOT 下 Compare 传 null 树会抛无栈 NRE fail-fast。</summary>
    public IReadOnlyList<CommitFileChange> CommitChanges(string sha)
    {
        var repo = Repo();
        var commit = FindCommit(repo, sha);
        if (!commit.Parents.Any())
        {
            var changes = new List<CommitFileChange>();
            CollectTreeChanges(commit.Tree, changes);
            return changes;
        }
        var treeChanges = repo.Diff.Compare<TreeChanges>(commit.Parents.First().Tree, commit.Tree);
        return treeChanges.Select(c => new CommitFileChange(c.Path.Replace('\\', '/'), KindOf(c.Status))).Distinct().ToList();
    }

    private static void CollectTreeChanges(LibGit2Sharp.Tree tree, List<CommitFileChange> list)
    {
        foreach (var entry in tree)
        {
            if (entry.Target is LibGit2Sharp.Tree sub) CollectTreeChanges(sub, list);
            else if (entry.Target is LibGit2Sharp.Blob) list.Add(new CommitFileChange(entry.Path.Replace('\\', '/'), GitChangeKind.Added));
        }
    }

    private static GitChangeKind KindOf(LibGit2Sharp.ChangeKind status) => status switch
    {
        LibGit2Sharp.ChangeKind.Added => GitChangeKind.Added,
        LibGit2Sharp.ChangeKind.Deleted => GitChangeKind.Deleted,
        LibGit2Sharp.ChangeKind.Renamed => GitChangeKind.Renamed,
        LibGit2Sharp.ChangeKind.Copied => GitChangeKind.Added,
        _ => GitChangeKind.Modified,
    };

    private static LibGit2Sharp.Commit FindCommit(Repository repo, string sha) =>
        repo.Commits.FirstOrDefault(c => c.Sha.StartsWith(sha, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"提交不存在：{sha}");

    /// <summary>工作区单文件文本 diff（HEAD → 工作区）；未跟踪新文件整文件视为新增；二进制返回 null。</summary>
    public TextDiff? TextDiff(string path)
    {
        var repo = Repo();
        var tip = repo.Head.Tip;
        // 未跟踪/新文件不在 HEAD 树里：部分 diff 口径不产出 patch，统一按整文件新增读取。
        if (tip == null || tip.Tree[path] == null)
        {
            return ReadFileAsAdded(path);
        }
        // AOT：Diff.Compare 会 fail-fast，改走托管 diff（HEAD blob ↔ 工作区）。
        var headBlob = tip.Tree[path]?.Target as Blob;
        var oldLines = headBlob is null ? [] : TextDiffBuilder.ReadBlobLines(headBlob);
        var newLines = ReadWorkingLines(path);
        if (oldLines.Count == 0 && newLines.Count == 0) return null;
        return TextDiffBuilder.Build(path, oldLines, newLines);
    }

    private TextDiff? ReadFileAsAdded(string path)
    {
        if (IsBinaryPath(path)) return null;
        var full = System.IO.Path.Combine(_repoRoot, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return null;
        try
        {
            var lines = File.ReadAllLines(full);
            return new TextDiff(path, [new DiffHunk("@@ -0,0 +1," + lines.Length + " @@", lines.Select(l => new DiffLine('+', l)).ToList())]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>历史提交里某文件的文本 diff（该提交 → 其父提交）；二进制返回 null。</summary>
    public TextDiff? CommitTextDiff(string sha, string path)
    {
        var repo = Repo();
        var commit = FindCommit(repo, sha);
        var parent = commit.Parents.FirstOrDefault();
        if (parent is null)
        {
            // 初始提交：与空树比等价于整文件新增（AOT 下 Compare(null, …) 会抛无栈 NRE）。
            var blob = commit.Tree[path]?.Target as LibGit2Sharp.Blob;
            if (blob is null) return null;
            using var reader = new StreamReader(blob.GetContentStream(), Encoding.UTF8);
            var lines = new List<DiffLine>();
            string? line;
            while ((line = reader.ReadLine()) is not null) lines.Add(new DiffLine('+', line));
            return new TextDiff(path, [new DiffHunk("@@ -0,0 +1," + lines.Count + " @@", lines)]);
        }
        // AOT：Diff.Compare 会 fail-fast，改走托管 diff（父 blob ↔ 提交 blob）。
        var parentBlob = parent.Tree[path]?.Target as Blob;
        var commitBlob = commit.Tree[path]?.Target as Blob;
        if (parentBlob is null && commitBlob is null) return null;
        var oldLines = parentBlob is null ? [] : TextDiffBuilder.ReadBlobLines(parentBlob);
        var newLines = commitBlob is null ? [] : TextDiffBuilder.ReadBlobLines(commitBlob);
        return TextDiffBuilder.Build(path, oldLines, newLines);
    }

    private static TextDiff? ParsePatchContent(string path, string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Contains("Binary files", StringComparison.Ordinal)) return null;
        return ParsePatch(path, content);
    }

    /// <summary>解析 unified diff 文本为分块结构（无 hunk 头时整段按上下文行解析）。</summary>
    public static TextDiff ParsePatch(string path, string content)
    {
        var hunks = new List<DiffHunk>();
        List<DiffLine>? current = null;
        // 尾随换行的 split artifact（末尾空串）不是真实 diff 行：真实文件末空行在 patch 里带空格前缀。
        var rows = content.Split('\n');
        if (rows.Length > 0 && rows[^1].Length == 0) rows = rows[..^1];
        foreach (var raw in rows)
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("diff --git", StringComparison.Ordinal) ||
                line.StartsWith("index ", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal) ||
                line.StartsWith("+++ ", StringComparison.Ordinal) ||
                line.StartsWith("new file", StringComparison.Ordinal) ||
                line.StartsWith("deleted file", StringComparison.Ordinal) ||
                line.StartsWith("similarity ", StringComparison.Ordinal) ||
                line.StartsWith("rename ", StringComparison.Ordinal))
            {
                continue;
            }
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                current = [];
                hunks.Add(new DiffHunk(line, current));
                continue;
            }
            if (current == null)
            {
                if (line.Length == 0) continue;
                current = [];
                hunks.Add(new DiffHunk(string.Empty, current));
            }
            if (line.Length == 0)
            {
                current.Add(new DiffLine(' ', string.Empty));
            }
            else if (line[0] is ' ' or '+' or '-')
            {
                current.Add(new DiffLine(line[0], line[1..]));
            }
        }
        return new TextDiff(path, hunks);
    }

    /// <summary>HEAD 中某文件的流（二进制对比扩展点）；HEAD 不含该文件（新增）返回 null。</summary>
    public Stream? OpenHeadStream(string path)
    {
        var repo = Repo();
        var entry = repo.Head.Tip?.Tree[path]?.Target as LibGit2Sharp.Blob;
        return entry?.GetContentStream();
    }

    /// <summary>仓库是否已有提交（unborn HEAD = 无提交，不能打 tag）。</summary>
    public bool HasCommits()
    {
        using var repo = Repo();
        return repo.Head.Tip is not null;
    }

    /// <summary>仓库级提交身份是否缺失（供外部决定是否补配）。</summary>
    public bool HasIdentityGap
    {
        get
        {
            using var repo = Repo();
            var (missing, _, _) = IdentityOf(repo);
            return missing;
        }
    }

    /// <summary>提交身份缺失时，用偏好设置的用户名/邮箱补配仓库级身份（不覆盖已配置值）。</summary>
    public void EnsureIdentity(string? userName, string? userEmail)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(userEmail)) return;
        using var repo = Repo();
        var (missing, _, _) = IdentityOf(repo);
        if (missing) SetIdentity(userName.Trim(), userEmail.Trim());
    }

    /// <summary>勾选暂存后提交（部分提交）；paths 为仓库相对路径。
    /// 提交身份未配置时抛出带指引的异常（不冒名兜底）。</summary>
    public Commit CommitPaths(IEnumerable<string> paths, string message)
    {
        var repo = Repo();
        foreach (var path in paths)
        {
            Commands.Stage(repo, path);
        }
        var (missing, name, email) = IdentityOf(repo);
        if (missing)
        {
            throw new InvalidOperationException("尚未配置 Git 提交身份：请先设置 user.name 与 user.email。");
        }
        var signature = new Signature(name, email, DateTimeOffset.Now);
        return repo.Commit(message, signature, signature);
    }

    /// <summary>全部改动提交（定时自动提交用）；无改动返回 null（自动跳过）。</summary>
    public Commit? CommitAll(string message)
    {
        var repo = Repo();
        if (!repo.RetrieveStatus(new StatusOptions { IncludeUntracked = true }).IsDirty) return null;
        Commands.Stage(repo, "*");
        var (missing, name, email) = IdentityOf(repo);
        if (missing)
        {
            throw new InvalidOperationException("尚未配置 Git 提交身份：请先设置 user.name 与 user.email。");
        }
        var signature = new Signature(name, email, DateTimeOffset.Now);
        return repo.Commit(message, signature, signature);
    }

    /// <summary>放弃一个文件的未提交更改：HEAD 中存在则还原（含暂存态），新增文件从索引与磁盘移除。</summary>
    public void Discard(string path)
    {
        var repo = Repo();
        var inHead = repo.Head.Tip?.Tree[path] != null;
        if (inHead)
        {
            repo.CheckoutPaths("HEAD", new[] { path }, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
            return;
        }
        try { repo.Index.Remove(path); }
        catch (ArgumentException) { /* 未被暂存，忽略 */ }
        var full = System.IO.Path.Combine(_repoRoot, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (File.Exists(full)) File.Delete(full);
    }

    /// <summary>拉取并仅允许快进合并；HTTPS 远端用 PAT 凭据拉取（公开仓库可传 null）；
    /// 远端分叉时抛出可读异常（请用外部 git 处理）。</summary>
    public int PullFastForward(string? pat)
    {
        var repo = Repo();
        var remote = repo.Network.Remotes["origin"] ?? throw new InvalidOperationException("未配置 origin 远端。");
        var before = repo.Head.TrackingDetails.BehindBy ?? 0;
        var options = new FetchOptions();
        var isHttps = !remote.Url.StartsWith("git@", StringComparison.OrdinalIgnoreCase)
            && !remote.Url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase);
        if (isHttps && !string.IsNullOrWhiteSpace(pat))
        {
            options.CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials { Username = pat, Password = string.Empty };
        }
        repo.Network.Fetch(remote.Name, null, options);
        var identity = IdentityOf(repo);
        var signature = new Signature(identity.Missing ? "Vex" : identity.Name, identity.Missing ? "vex@local" : identity.Email, DateTimeOffset.Now);
        var result = repo.MergeFetchedRefs(signature, new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly });
        if (result.Status == MergeStatus.NonFastForward)
        {
            throw new InvalidOperationException("远端历史与本地分叉，无法快进合并——请用外部 Git 工具处理。");
        }
        return (repo.Head.TrackingDetails.BehindBy ?? 0) == 0 ? before : 0;
    }

    /// <summary>origin 远端原始地址（SSH/HTTPS 形态判断用）；无 origin 或本地路径远端返回 null。</summary>
    public string? RemoteRawUrl()
    {
        try
        {
            var repo = Repo();
            return repo.Network.Remotes["origin"]?.Url;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>保存 origin 远端地址（新增或更新）。</summary>
    public void SetRemote(string url)
    {
        var repo = Repo();
        var remote = repo.Network.Remotes["origin"];
        if (remote is null) repo.Network.Remotes.Add("origin", url);
        else repo.Network.Remotes.Update("origin", r => r.Url = url);
    }

    /// <summary>移除 origin 远端（原型未提供入口，远程同步回本地管理用）。</summary>
    public void RemoveRemote()
    {
        var repo = Repo();
        repo.Network.Remotes.Remove("origin");
    }

    /// <summary>为当前分支建立对 origin 的跟踪（首次推送前保证 ahead/behind 有值）。</summary>
    public void TrackOrigin()
    {
        var repo = Repo();
        var remote = repo.Network.Remotes["origin"];
        if (remote is null) return;
        var branch = repo.Head;
        if (branch.IsTracking) return;
        repo.Branches.Update(branch, b => b.Remote = remote.Name, b => b.UpstreamBranch = $"refs/heads/{branch.FriendlyName}");
    }

    /// <summary>origin 远端归一为 Web 首页（https://{host}/{owner}/{repo}）；SSH/HTTPS 形态统一，无 origin 或本地路径远端返回 null。</summary>
    public string? RemoteUrl() => NormalizeRemoteUrl(RemoteRawUrl());

    /// <summary>任意远端地址 → https Web 首页：去 .git 尾、SSH（git@host:owner/repo / ssh://git@host/...）归一为 https。本地路径远端返回 null。</summary>
    public static string? NormalizeRemoteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = url.Trim().TrimEnd('/');
        if (url.Contains('\\')) return null; // Windows 本地路径远端
        if (url.Length >= 2 && url[1] == ':' && char.IsAsciiLetter(url[0])) return null; // 盘符形态（D:/repos/notes）
        if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) url = url[..^4];
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var hostPath = url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ? url["ssh://".Length..] : url;
            var at = hostPath.IndexOf('@');
            if (at >= 0) hostPath = hostPath[(at + 1)..];
            url = "https://" + hostPath.Replace(':', '/');
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !uri.Host.Contains('.')) return null; // 无点主机多为本地路径误归一
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 2 || segments.Any(string.IsNullOrEmpty)) return null;
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath.TrimEnd('/')}";
    }

    /// <summary>提交在远端（GitHub 等）的 Web 地址：https://{host}/{owner}/{repo}/commit/{sha}；无 origin 返回 null。</summary>
    public string? CommitUrl(string sha) => RemoteUrl() is { } home ? $"{home}/commit/{sha}" : null;

    /// <summary>HTTPS 远端的凭据主机（github.com 等）；SSH 或本地远端返回 null。</summary>
    public string? RemoteCredentialHost() =>
        IsSshRemote() ? null : (RemoteUrl() is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null);

    /// <summary>是否 SSH 远程（git@host:… / ssh://…）：面板以警示芯片提示系统 Git 依赖。</summary>
    public bool IsSshRemote()
    {
        var url = RemoteRawUrl();
        return url is not null && (url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) || url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>在 HEAD 打轻量 tag（发布流程：升版提交后打发布 tag）。</summary>
    public void CreateTag(string name)
    {
        var repo = Repo();
        repo.Tags.Add(name, repo.Head.Tip ?? throw new InvalidOperationException("当前分支没有任何提交，无法打 tag。"));
    }

    /// <summary>推送分支或 tag：HTTPS remote 走 PAT 凭据（LibGit2）；SSH remote（git@host / ssh://）交给系统 git
    /// 命令行（用户已配置的 agent / 密钥生效；未装 git 时报错原文透出）。</summary>
    public void Push(string pat, string remoteName = "origin", string? branch = null, string? tag = null)
    {
        var repo = Repo();
        {
            var remote = repo.Network.Remotes[remoteName] ?? throw new InvalidOperationException("未配置 origin 远端。");
            var pushRef = tag is not null ? $"refs/tags/{tag}" : $"refs/heads/{branch ?? repo.Head.FriendlyName}";
            if (remote.Url.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
                remote.Url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
            {
                PushViaSystemGit(remoteName, pushRef);
                return;
            }
            repo.Network.Push(remote, pushRef, new PushOptions
            {
                CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
                {
                    Username = pat,
                    Password = string.Empty,
                },
            });
        }
    }

    /// <summary>系统 git 命令行推送：SSH 密钥/凭据管理器由用户环境自理；失败抛出真实 stderr（不静默）。</summary>
    private void PushViaSystemGit(string remoteName, string pushRef)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = $"push {remoteName} {pushRef}",
            WorkingDirectory = _repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var process = System.Diagnostics.Process.Start(psi)!;
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(120_000);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error) ? $"git push 退出码 {process.ExitCode}" : error.Trim());
        }
    }

    public (string Name, string Email) Identity()
    {
        var repo = Repo();
        var identity = IdentityOf(repo);
        return (identity.Missing ? string.Empty : identity.Name, identity.Missing ? string.Empty : identity.Email);
    }

    /// <summary>身份未配置时返回缺失标记：调用方阻断提交并指引配置（不静默兜底冒名提交）。</summary>
    public (bool Missing, string Name, string Email) IdentityOrDefault()
    {
        var repo = Repo();
        return IdentityOf(repo);
    }

    private static (bool Missing, string Name, string Email) IdentityOf(Repository repo)
    {
        var name = repo.Config.Get<string>("user.name")?.Value;
        var email = repo.Config.Get<string>("user.email")?.Value;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        {
            return (true, string.Empty, string.Empty);
        }
        return (false, name, email);
    }

    public void SetIdentity(string name, string email)
    {
        var repo = Repo();
        repo.Config.Set("user.name", name);
        repo.Config.Set("user.email", email);
    }

    /// <summary>读工作区文件行（统一 UTF8，路径分隔归一）。</summary>
    private List<string> ReadWorkingLines(string path)
    {
        var full = System.IO.Path.Combine(_repoRoot, path.Replace('/', System.IO.Path.DirectorySeparatorChar));
        if (!File.Exists(full)) return [];
        return [.. File.ReadAllLines(full)];
    }

    /// <summary>把模式追加到仓库根 .gitignore（已存在则跳过）。</summary>
    public void AddToGitignore(string pattern)
    {
        var file = System.IO.Path.Combine(_repoRoot, ".gitignore");
        var lines = File.Exists(file) ? File.ReadAllLines(file).ToList() : [];
        if (lines.Any(l => string.Equals(l.Trim(), pattern.Trim(), StringComparison.Ordinal))) return;
        if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add(string.Empty);
        lines.Add(pattern);
        File.WriteAllLines(file, lines);
    }
}
