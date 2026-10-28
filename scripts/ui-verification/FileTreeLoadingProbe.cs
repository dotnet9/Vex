using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;

/// <summary>以真实工作区和磁盘事件验证按层加载、缓存和监控的读取范围。</summary>
internal static class FileTreeLoadingProbe
{
    public static void Run(VerificationVexApp app, Window window, string output)
    {
        void Pump(Task? task = null)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            do
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                if (task is null || task.IsCompleted) break;
                if (DateTime.UtcNow > deadline) throw new TimeoutException("File tree probe timed out.");
                Thread.Sleep(10);
            } while (true);
            task?.GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
        void Until(Func<bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!predicate())
            {
                Pump(); Thread.Sleep(20);
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Watcher did not refresh the file tree.");
            }
            Pump();
        }
        void ObserveFor(int milliseconds)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < milliseconds) { Pump(); Thread.Sleep(20); }
        }
        var observations = new List<string>();
        void Report(string value) { observations.Add(value); Console.WriteLine(value); }
        static int CountFiles(IEnumerable<DocumentFileNode> nodes) => nodes.Sum(node => node.IsDocument ? 1 : CountFiles(node.Children));

        var workspace = Path.Combine(output, "workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        File.WriteAllText(Path.Combine(workspace, "root.md"), "# Root\n");
        for (var first = 0; first < 8; first++)
        for (var second = 0; second < 6; second++)
        {
            var folder = Path.Combine(workspace, $"section-{first:D2}", $"sub-{second:D2}");
            Directory.CreateDirectory(folder);
            for (var file = 0; file < 5; file++) File.WriteAllText(Path.Combine(folder, $"note-{file:D2}.md"), "# Probe preview\n\ncontent");
        }
        var factory = (ProbeDocumentFileFactory)app.Resolve<IDocumentFileFactory>();
        var main = (MainWindowViewModel)window.DataContext!;
        var tree = app.Resolve<ShellFilesViewModel>();
        var collectionChangesOffUiThread = 0;
        tree.DocumentFileNodes.CollectionChanged += TrackCollection;
        void TrackCollection(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs args)
        {
            if (!Dispatcher.UIThread.CheckAccess()) Interlocked.Increment(ref collectionChangesOffUiThread);
            if (args.NewItems is null) return;
            foreach (DocumentFileNode node in args.NewItems)
            {
                node.Children.CollectionChanged += TrackCollection;
            }
        }
        if (tree.IsListView) { tree.ToggleListView(); Pump(); }
        tree.FilterText = string.Empty;
        var service = (WorkspaceFileService)app.Resolve<IWorkspaceFileService>();
        var before = factory.CreateCount;
        var previewBefore = factory.PreviewReadCount;
        var diagnostics = service.Diagnostics;
        var watch = Stopwatch.StartNew();
        Pump(main.OpenStartupDocumentAsync([workspace]));
        Until(() => tree.DocumentFileNodes.Count == 1 && tree.DocumentFileNodes[0].Children.Count == 9);
        watch.Stop();
        var root = tree.DocumentFileNodes.Single();
        Report($"INITIAL: disk documents=241; factory calls={factory.CreateCount - before}; model documents={CountFiles(tree.DocumentFileNodes)}; root expanded={tree.DocumentFileNodes[0].IsExpanded}; elapsed={watch.Elapsed.TotalMilliseconds:F1}ms");
        Check(factory.CreateCount - before == 1 && CountFiles(tree.DocumentFileNodes) == 1 && root.IsExpanded,
            "Opening 241 documents loads only the root's direct file and expands only the root");
        Check(root.Children.Where(node => node.IsFolder).All(node => !node.IsExpanded), "Every child folder starts collapsed");
        Check(service.Diagnostics.DirectoryEnumerationCount - diagnostics.DirectoryEnumerationCount == 1,
            "The initial tree enumerates exactly one directory");
        Check(service.Diagnostics.IndexScanCount == diagnostics.IndexScanCount && factory.PreviewReadCount == previewBefore,
            "Opening the tree does not request an index or document summaries");
        Report($"PREVIEWS: reads={factory.PreviewReadCount - previewBefore}; first-level documents=1; nested documents=240");
        before = factory.CreateCount;
        diagnostics = service.Diagnostics;
        var section = root.Children.Single(node => node.Name == "section-00");
        section.IsExpanded = true;
        Until(() => section.Children.Count == 6 && section.Children.All(node => node.IsFolder));
        Check(factory.CreateCount == before && service.Diagnostics.DirectoryEnumerationCount - diagnostics.DirectoryEnumerationCount == 1,
            "Expanding the first level reads only its six child directories");
        var sub = section.Children.Single(node => node.Name == "sub-00");
        sub.IsExpanded = true;
        Until(() => sub.Children.Count == 5 && sub.Children.All(node => node.IsDocument));
        Check(factory.CreateCount - before == 5 && CountFiles(tree.DocumentFileNodes) == 6,
            "Only the selected second-level folder creates its five document models");
        diagnostics = service.Diagnostics;
        section.IsExpanded = false; Pump(); section.IsExpanded = true; Pump();
        sub.IsExpanded = false; Pump(); sub.IsExpanded = true; Pump();
        ObserveFor(100);
        Check(service.Diagnostics.DirectoryEnumerationCount == diagnostics.DirectoryEnumerationCount && factory.PreviewReadCount == previewBefore,
            "Expanding valid cached folders performs no extra enumeration or summary reads");
        Report($"EXPAND: additional factory calls={factory.CreateCount - before}; model documents={CountFiles(tree.DocumentFileNodes)}; repeated expansion scans=0");

        var added = Path.Combine(workspace, "section-00", "sub-00", "watch-added.md");
        before = factory.CreateCount;
        File.WriteAllText(added, "# Added from outside\n");
        Until(() => sub.Children.Any(node => node.Path == added));
        ObserveFor(700);
        Check(CountFiles(tree.DocumentFileNodes) == 7 && ReferenceEquals(root, tree.DocumentFileNodes.Single())
            && ReferenceEquals(section, root.Children.Single(node => node.Name == "section-00")),
            "Creation updates the loaded target directory while retaining unrelated nodes");
        Report($"WATCHER CREATE: loaded nested document detected; factory calls={factory.CreateCount - before}; model documents={CountFiles(tree.DocumentFileNodes)}");
        var renamed = Path.Combine(Path.GetDirectoryName(added)!, "watch-renamed.md");
        before = factory.CreateCount;
        File.Move(added, renamed);
        Until(() => sub.Children.Any(node => node.Path == renamed) && sub.Children.All(node => node.Path != added));
        ObserveFor(700);
        Report($"WATCHER RENAME: detected; factory calls={factory.CreateCount - before}");
        before = factory.CreateCount;
        File.Delete(renamed);
        Until(() => sub.Children.All(node => node.Path != renamed));
        ObserveFor(700);
        Report($"WATCHER DELETE: detected; factory calls={factory.CreateCount - before}");

        diagnostics = service.Diagnostics;
        before = factory.CreateCount;
        var unloadedChange = Path.Combine(workspace, "section-01", "sub-00", "not-loaded.md");
        File.WriteAllText(unloadedChange, "# Do not preload\n");
        ObserveFor(1600);
        Check(service.Diagnostics.DirectoryEnumerationCount == diagnostics.DirectoryEnumerationCount && factory.CreateCount == before,
            "Changes in an unexpanded directory do not enumerate or create its tree nodes");

        before = factory.CreateCount;
        diagnostics = service.Diagnostics;
        File.WriteAllText(Path.Combine(workspace, "not-a-document.json"), "{}");
        ObserveFor(1600);
        Check(factory.CreateCount == before && service.Diagnostics.DirectoryEnumerationCount == diagnostics.DirectoryEnumerationCount,
            "An unrelated JSON file triggers no tree enumeration");
        Report($"WATCHER NON-DOCUMENT: factory calls={factory.CreateCount - before}; scans=0");
        before = factory.CreateCount;
        Directory.CreateDirectory(Path.Combine(workspace, ".ignored"));
        File.WriteAllText(Path.Combine(workspace, ".ignored", "hidden.md"), "# Hidden");
        ObserveFor(1600);
        Check(factory.CreateCount == before && service.Diagnostics.DirectoryEnumerationCount == diagnostics.DirectoryEnumerationCount,
            "A hidden directory and its contents trigger no tree enumeration");
        Check(service.Diagnostics.IndexScanCount == diagnostics.IndexScanCount && factory.PreviewReadCount == previewBefore,
            "Watcher changes do not initialize an unused index or summaries");
        Report($"WATCHER HIDDEN DIRECTORY: factory calls={factory.CreateCount - before}; scans=0; index scans=0");

        var direct = Path.Combine(output, "direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(direct);
        var current = Path.Combine(direct, "current.md");
        File.WriteAllText(current, "# Direct\n");
        File.WriteAllText(Path.Combine(direct, "other.md"), "# Other\n");
        before = factory.UiThreadCreateCount;
        Pump(main.OpenPathAsync(current));
        Until(() => tree.DocumentFileNodes.Count == 1 && tree.DocumentFileNodes[0].Path == direct
            && tree.DocumentFileNodes[0].Children.Count == 2);
        Check(factory.UiThreadCreateCount == before, "Direct file opening performs directory model creation off the UI thread");
        Report($"DIRECT OPEN: document factory calls on UI thread={factory.UiThreadCreateCount - before}");
        var sibling = Path.Combine(direct, "new-sibling.md");
        File.WriteAllText(sibling, "# New sibling\n");
        Until(() => tree.DocumentFileNodes.Single().Children.Any(node => node.Path == sibling));
        Check(tree.DocumentFileNodes.Single().IsExpanded, "The parent workspace remains expanded after direct file opening");
        Check(collectionChangesOffUiThread == 0, "All observed tree collection updates run on the UI thread");
        Report("DIRECT OPEN WATCHER: newly created sibling detected=True");
        tree.DocumentFileNodes.CollectionChanged -= TrackCollection;
        File.WriteAllLines(Path.Combine(output, "observations.txt"), observations);

        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Report("PASS " + message);
        }
    }
}

public sealed class ProbeDocumentFileFactory(IAppLocalizer localizer) : IDocumentFileFactory
{
    private readonly DocumentFileFactory _inner = new(localizer);
    private int _createCount;
    private int _uiThreadCreateCount;
    private int _previewReadCount;
    public int CreateCount => Volatile.Read(ref _createCount);
    public int UiThreadCreateCount => Volatile.Read(ref _uiThreadCreateCount);
    public int PreviewReadCount => Volatile.Read(ref _previewReadCount);

    public DocumentFile Create(string path, string? workspaceRoot = null)
    {
        Interlocked.Increment(ref _createCount);
        if (Dispatcher.UIThread.CheckAccess()) Interlocked.Increment(ref _uiThreadCreateCount);
        return _inner.Create(path, workspaceRoot);
    }

    public string GetModifiedText(string path) => _inner.GetModifiedText(path);

    public string ReadPreview(string path)
    {
        Interlocked.Increment(ref _previewReadCount);
        return _inner.ReadPreview(path);
    }
}
