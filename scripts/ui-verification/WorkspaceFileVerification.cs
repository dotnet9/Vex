using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;

/// <summary>验证目录缓存、索引、摘要和监控恢复，磁盘数据全部位于独立输出目录。</summary>
internal static class WorkspaceFileVerification
{
    public static void Run(VerificationVexApp app, Window window, string output)
    {
        var results = new List<string>();
        var main = (MainWindowViewModel)window.DataContext!;
        var tree = app.Resolve<ShellFilesViewModel>();
        var service = (WorkspaceFileService)app.Resolve<IWorkspaceFileService>();
        var factory = (ProbeDocumentFileFactory)app.Resolve<IDocumentFileFactory>();
        if (tree.IsListView) tree.ToggleListView();
        tree.FilterText = string.Empty;
        var root = Path.Combine(output, "workspace-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        // 首层结构固定，改变后代规模，以读取次数而非机器相关耗时作为门槛。
        foreach (var count in new[] { 1_000, 10_000 })
        {
            var folder = Path.Combine(root, "scale-" + count);
            Directory.CreateDirectory(Path.Combine(folder, "nested"));
            File.WriteAllText(Path.Combine(folder, "root.md"), "# Root\n");
            for (var index = 0; index < count; index++)
                File.WriteAllText(Path.Combine(folder, "nested", $"note-{index:D5}.md"), "# Nested\n");
            var before = service.Diagnostics;
            var created = factory.CreateCount;
            var previews = factory.PreviewReadCount;
            var allocated = GC.GetTotalAllocatedBytes();
            var elapsed = Stopwatch.StartNew();
            Pump(main.OpenStartupDocumentAsync([folder]));
            Until(() => tree.DocumentFileNodes.Count == 1 && tree.DocumentFileNodes[0].Path == folder
                && tree.DocumentFileNodes[0].Children.Count == 2);
            elapsed.Stop();
            Check(service.Diagnostics.DirectoryEnumerationCount - before.DirectoryEnumerationCount == 1
                && factory.CreateCount - created == 1 && CountNodes(tree.DocumentFileNodes) == 3,
                $"{count:N0} descendants retain one directory enumeration and three tree nodes");
            Check(service.Diagnostics.IndexScanCount == before.IndexScanCount && factory.PreviewReadCount == previews,
                $"{count:N0} descendants cause no index scan or summary reads at initial opening");
            results.Add($"SAMPLE {count}: elapsed={elapsed.Elapsed.TotalMilliseconds:F1}ms; process allocation={GC.GetTotalAllocatedBytes() - allocated:N0} bytes; root entries=2; warm process; platform={Environment.OSVersion}");
        }

        var contracts = Path.Combine(root, "contracts");
        var deep = Path.Combine(contracts, "deep");
        Directory.CreateDirectory(deep);
        Directory.CreateDirectory(Path.Combine(contracts, "empty"));
        Directory.CreateDirectory(Path.Combine(contracts, ".hidden"));
        File.WriteAllText(Path.Combine(contracts, "root.md"), "# Root summary\n");
        var deepFile = Path.Combine(deep, "deep.markdown");
        File.WriteAllText(deepFile, "# Initial deep summary\n");
        File.WriteAllText(Path.Combine(deep, "ignore.json"), "{}");
        File.WriteAllText(Path.Combine(contracts, ".hidden", "hidden.md"), "# Hidden\n");
        Pump(main.OpenStartupDocumentAsync([contracts]));
        Until(() => tree.DocumentFileNodes.Count == 1 && tree.DocumentFileNodes[0].Path == contracts
            && tree.DocumentFileNodes[0].Children.Count == 3);
        var treeRoot = tree.DocumentFileNodes.Single();
        var deepNode = treeRoot.Children.Single(node => node.Path == deep);
        var emptyNode = treeRoot.Children.Single(node => node.Name == "empty");
        emptyNode.IsExpanded = true;
        Until(() => emptyNode.LoadState == DocumentFileNode.DirectoryLoadState.Loaded);
        Check(emptyNode.Children.Count == 0, "An empty visible directory can finish loading without an error or placeholder");
        var previewBefore = factory.PreviewReadCount;
        var indexed = Get(service.GetFilesAsync());
        Check(indexed.Select(file => file.Path).Order().SequenceEqual(new[] { Path.Combine(contracts, "root.md"), deepFile }.Order()),
            "The requested index includes unopened deep documents and excludes hidden and unsupported files");
        Check(factory.PreviewReadCount == previewBefore && !deepNode.IsExpanded && deepNode.Children.All(node => !node.IsDocument),
            "Index creation neither reads summaries nor opens tree directories");
        var indexScans = service.Diagnostics.IndexScanCount;
        Get(service.GetFilesAsync());
        Check(service.Diagnostics.IndexScanCount == indexScans, "An unchanged path index is reused");
        var preview = Get(service.GetPreviewAsync(deepFile));
        Check(preview.Contains("Initial deep summary") && factory.PreviewReadCount == previewBefore + 1,
            "The first requested summary reads one file");
        Check(Get(service.GetPreviewAsync(deepFile)) == preview && factory.PreviewReadCount == previewBefore + 1,
            "The same summary is cached");
        var eventCount = 0;
        void Changed(object? sender, WorkspaceFilesChangedEventArgs args)
        {
            if (args.Generation == service.Generation) Interlocked.Increment(ref eventCount);
        }
        service.Changed += Changed;
        try
        {
            File.WriteAllText(deepFile, "# Updated deep summary\n");
            Until(() => Volatile.Read(ref eventCount) > 0);
            Until(() => Get(service.GetPreviewAsync(deepFile)).Contains("Updated deep summary"));
            Check(!deepNode.IsExpanded && deepNode.Children.All(node => !node.IsDocument),
                "Summary invalidation does not materialize the unopened directory");
            deepNode.IsExpanded = true;
            Until(() => deepNode.Children.Any(node => node.Path == deepFile));
            treeRoot.IsExpanded = false;
            var rootScans = service.Diagnostics.DirectoryEnumerationCount;
            var eventsBefore = Volatile.Read(ref eventCount);
            File.WriteAllText(Path.Combine(contracts, "later.txt"), "later");
            Until(() => Volatile.Read(ref eventCount) > eventsBefore);
            Observe(200);
            Check(!treeRoot.IsExpanded && service.Diagnostics.DirectoryEnumerationCount == rootScans,
                "Changes mark a loaded collapsed root stale without reopening or enumerating it");
            treeRoot.IsExpanded = true;
            Until(() => treeRoot.Children.Any(node => node.Name == "later.txt"));

            // 真正的磁盘写入风暴；等待最终目录内容，避免假定系统报告事件的确切个数。
            var eventsBeforeStorm = Volatile.Read(ref eventCount);
            for (var index = 0; index < 80; index++)
                File.WriteAllText(Path.Combine(deep, $"burst-{index:D3}.md"), "# Burst\n");
            Until(() => deepNode.Children.Count(node => node.IsDocument) == 81);
            var expected = Directory.EnumerateFiles(deep).Where(path => Path.GetExtension(path) is ".md" or ".markdown" or ".mdown" or ".txt").Order().ToArray();
            Check(deepNode.Children.Where(node => node.IsDocument).Select(node => node.Path).Order().SequenceEqual(expected),
                "The real event storm converges to the directory's disk contents");
            Check(Volatile.Read(ref eventCount) - eventsBeforeStorm < 80 && service.Diagnostics.MaximumConcurrentOperations <= 4,
                "Real storm notifications merge and directory operations remain bounded");
            var summaries = factory.PreviewReadCount;
            Check(Get(service.GetFilesAsync()).Count == 83 && factory.PreviewReadCount == summaries,
                "The previously built index catches storm additions without reading every summary");

            var movedDocument = Path.Combine(deep, "burst-000.md");
            var unsupported = Path.Combine(deep, "burst-000.json");
            File.Move(movedDocument, unsupported);
            Until(() => deepNode.Children.All(node => node.Path != movedDocument));
            Check(Get(service.GetFilesAsync()).All(file => file.Path != movedDocument),
                "Renaming a supported document to an unsupported extension removes tree and index entries");
            File.Move(unsupported, movedDocument);
            Until(() => deepNode.Children.Any(node => node.Path == movedDocument));
            Check(Get(service.GetFilesAsync()).Any(file => file.Path == movedDocument),
                "Renaming back to a supported extension restores the tree and index entry");
            var visibleDirectory = Path.Combine(contracts, "visible-new");
            Directory.CreateDirectory(visibleDirectory);
            File.WriteAllText(Path.Combine(visibleDirectory, "new.md"), "# New directory\n");
            Until(() => treeRoot.Children.Any(node => node.Path == visibleDirectory));
            var hiddenDirectory = Path.Combine(contracts, ".visible-new");
            Directory.Move(visibleDirectory, hiddenDirectory);
            Until(() => treeRoot.Children.All(node => node.Path != visibleDirectory));
            Check(Get(service.GetFilesAsync()).All(file => !file.Path.Contains("visible-new", StringComparison.Ordinal)),
                "Renaming a visible directory into the hidden range removes its indexed subtree");

            var recoveryEvents = 0;
            void Recovery(object? sender, WorkspaceFilesChangedEventArgs args)
            {
                if (args.IsRecovery) Interlocked.Increment(ref recoveryEvents);
            }
            service.Changed += Recovery;
            try
            {
                var enumerationsBeforeRecovery = service.Diagnostics.DirectoryEnumerationCount;
                service.RequestRecovery(new InternalBufferOverflowException("Verification injection"));
                Until(() => Volatile.Read(ref recoveryEvents) > 0);
                Until(() => service.Diagnostics.DirectoryEnumerationCount > enumerationsBeforeRecovery
                    && deepNode.LoadState == DocumentFileNode.DirectoryLoadState.Loaded
                    && deepNode.Children.Count(node => node.IsDocument) == 81);
                Check(Get(service.GetFilesAsync()).Count == 83 && factory.PreviewReadCount == summaries,
                    "Injected watcher Error restores directory and index consistency without loading summaries");
            }
            finally { service.Changed -= Recovery; }
        }
        finally { service.Changed -= Changed; }

        VerifyDelayedWorkspaceSwitch();
        File.WriteAllLines(Path.Combine(output, "workspace-contracts.txt"), results);

        static int CountNodes(IEnumerable<DocumentFileNode> nodes) => nodes.Sum(node => node.IsPlaceholder ? 0 : 1 + CountNodes(node.Children));
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
            Console.WriteLine("PASS " + message);
        }
        void Pump(Task? task = null)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                if (task is null || task.IsCompleted) break;
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Workspace operation timed out.");
                Thread.Sleep(10);
            } while (true);
            task?.GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
        T Get<T>(Task<T> task) { Pump(task); return task.GetAwaiter().GetResult(); }
        void Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!condition())
            {
                Pump(); Thread.Sleep(15);
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Workspace did not reach the expected state.");
            }
            Pump();
        }
        void Observe(int milliseconds)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds) { Pump(); Thread.Sleep(15); }
        }

        void VerifyDelayedWorkspaceSwitch()
        {
            using var delayed = new DelayedWorkspaceFiles();
            var first = Path.Combine(root, "controlled-a");
            var second = Path.Combine(root, "controlled-b");
            delayed.Open(first);
            using var isolatedTree = new ShellFilesViewModel(workspaceFiles: delayed);
            Until(() => delayed.PendingDirectoryCount == 1);
            var pendingRoot = isolatedTree.DocumentFileNodes.Single();
            pendingRoot.IsExpanded = false; pendingRoot.IsExpanded = true;
            pendingRoot.IsExpanded = false; pendingRoot.IsExpanded = true;
            Pump();
            Check(delayed.DirectoryRequestCount == 1, "Repeated expansion while a directory is loading reuses its pending request");
            var oldGeneration = delayed.Generation;
            delayed.Open(second);
            Until(() => delayed.PendingDirectoryCount == 2);
            delayed.Complete(second, delayed.Generation, "current.md");
            Until(() => isolatedTree.DocumentFileNodes.Count == 1
                && isolatedTree.DocumentFileNodes[0].Children.Any(node => node.Name == "current.md"));
            delayed.Complete(first, oldGeneration, "obsolete.md");
            Observe(100);
            Check(isolatedTree.DocumentFileNodes.Single().Path == second
                && isolatedTree.DocumentFileNodes.Single().Children.All(node => node.Name != "obsolete.md"),
                "A deliberately delayed A result cannot replace B after a workspace switch");

            var third = Path.Combine(root, "controlled-failure");
            delayed.Open(third);
            Until(() => delayed.PendingDirectoryCount == 3);
            delayed.Fail(third, delayed.Generation);
            Until(() => isolatedTree.DocumentFileNodes.Single().LoadState == DocumentFileNode.DirectoryLoadState.Failed);
            Check(isolatedTree.DocumentFileNodes.Single().Children.Any(node => node.IsPlaceholder),
                "A failed first directory read preserves a retry placeholder instead of pretending it is empty");
            var retry = isolatedTree.RetryDirectoryAsync(isolatedTree.DocumentFileNodes.Single());
            Until(() => delayed.DirectoryRequestCount == 4);
            isolatedTree.DocumentFileNodes.Single().IsExpanded = false;
            delayed.Complete(third, delayed.Generation, "retried.md");
            Pump(retry);
            Check(!isolatedTree.DocumentFileNodes.Single().IsExpanded
                && isolatedTree.DocumentFileNodes.Single().Children.Any(node => node.Name == "retried.md"),
                "A successful retry caches the result but does not reopen a directory collapsed during reading");
        }
    }

    /// <summary>故意不响应取消，以验证 UI 自身仍拒绝已过期的快照。</summary>
    private sealed class DelayedWorkspaceFiles : IWorkspaceFileService
    {
        private readonly Dictionary<(string Path, long Generation), TaskCompletionSource<WorkspaceDirectorySnapshot>> _pending = [];
        public string? RootPath { get; private set; }
        public long Generation { get; private set; }
        public int PendingDirectoryCount => _pending.Count;
        public int DirectoryRequestCount { get; private set; }
        public event EventHandler<WorkspaceFilesChangedEventArgs>? Changed;
        public void Open(string rootPath)
        {
            RootPath = rootPath;
            Generation++;
            Changed?.Invoke(this, new WorkspaceFilesChangedEventArgs(Generation, rootPath, [rootPath], []));
        }
        public void Close() { RootPath = null; Generation++; }
        public void Refresh()
        {
            if (RootPath is { } root) Changed?.Invoke(this, new WorkspaceFilesChangedEventArgs(Generation, root, [root], [], true));
        }
        public Task<WorkspaceDirectorySnapshot> GetDirectoryAsync(string path, bool refresh = false, CancellationToken cancellationToken = default)
        {
            DirectoryRequestCount++;
            var source = new TaskCompletionSource<WorkspaceDirectorySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[(path, Generation)] = source;
            return source.Task;
        }
        public void Complete(string root, long generation, string name)
        {
            var path = Path.Combine(root, name);
            var file = new DocumentFile(path, name, Path.GetFileName(root), string.Empty, string.Empty);
            _pending[(root, generation)].SetResult(new WorkspaceDirectorySnapshot(root, generation, root, 1,
                [new WorkspaceDirectoryEntry(path, name, false, file)]));
        }
        public void Fail(string root, long generation) => _pending[(root, generation)].SetException(new IOException("Controlled directory read failure"));
        public Task<IReadOnlyList<DocumentFile>> GetFilesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<DocumentFile>>([]);
        public Task<string> GetPreviewAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public bool IsCurrent(WorkspaceDirectorySnapshot snapshot) => snapshot.Generation == Generation && snapshot.RootPath == RootPath;
        public void Dispose() { }
    }
}
