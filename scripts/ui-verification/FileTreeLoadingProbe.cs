using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Services;

/// <summary>通过真实打开和磁盘事件记录文件树扫描范围，不改变产品加载策略。</summary>
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
        var before = factory.CreateCount;
        var watch = Stopwatch.StartNew();
        Pump(main.OpenStartupDocumentAsync([workspace]));
        watch.Stop();
        Report($"INITIAL: disk documents=241; factory calls={factory.CreateCount - before}; model documents={CountFiles(tree.DocumentFileNodes)}; root expanded={tree.DocumentFileNodes[0].IsExpanded}; elapsed={watch.Elapsed.TotalMilliseconds:F1}ms");
        if (factory.CreateCount - before != 241 || CountFiles(tree.DocumentFileNodes) != 241 || tree.DocumentFileNodes[0].IsExpanded)
            throw new InvalidOperationException("Unexpected initial loading observation.");
        Report($"PREVIEWS: populated={main.DocumentFiles.Count(file => file.Preview.Length > 0)}; first-level documents=1; nested documents=240");
        before = factory.CreateCount;
        tree.DocumentFileNodes[0].IsExpanded = true; Pump();
        tree.DocumentFileNodes[0].Children.First(node => node.IsFolder).IsExpanded = true; Pump();
        Report($"EXPAND: additional factory calls={factory.CreateCount - before}; model already contains all 241 documents");

        var added = Path.Combine(workspace, "section-00", "sub-00", "watch-added.md");
        before = factory.CreateCount;
        File.WriteAllText(added, "# Added from outside\n");
        Until(() => main.DocumentFiles.Any(file => file.Path == added));
        ObserveFor(700);
        Report($"WATCHER CREATE: nested document detected; factory calls={factory.CreateCount - before}; total documents={main.DocumentFiles.Count}");
        var renamed = Path.Combine(Path.GetDirectoryName(added)!, "watch-renamed.md");
        before = factory.CreateCount;
        File.Move(added, renamed);
        Until(() => main.DocumentFiles.Any(file => file.Path == renamed) && main.DocumentFiles.All(file => file.Path != added));
        ObserveFor(700);
        Report($"WATCHER RENAME: detected; factory calls={factory.CreateCount - before}");
        before = factory.CreateCount;
        File.Delete(renamed);
        Until(() => main.DocumentFiles.All(file => file.Path != renamed));
        ObserveFor(700);
        Report($"WATCHER DELETE: detected; factory calls={factory.CreateCount - before}");

        before = factory.CreateCount;
        File.WriteAllText(Path.Combine(workspace, "not-a-document.json"), "{}");
        ObserveFor(1600);
        Report($"WATCHER NON-DOCUMENT: factory calls={factory.CreateCount - before}; unchanged document count={main.DocumentFiles.Count}");
        before = factory.CreateCount;
        Directory.CreateDirectory(Path.Combine(workspace, ".ignored"));
        File.WriteAllText(Path.Combine(workspace, ".ignored", "hidden.md"), "# Hidden");
        ObserveFor(1600);
        Report($"WATCHER HIDDEN DIRECTORY: factory calls={factory.CreateCount - before}; hidden document excluded={main.DocumentFiles.All(file => !file.Path.Contains(".ignored"))}");

        var direct = Path.Combine(output, "direct-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(direct);
        var current = Path.Combine(direct, "current.md");
        File.WriteAllText(current, "# Direct\n");
        File.WriteAllText(Path.Combine(direct, "other.md"), "# Other\n");
        before = factory.UiThreadCreateCount;
        Pump(main.OpenPathAsync(current));
        Report($"DIRECT OPEN: document factory calls on UI thread={factory.UiThreadCreateCount - before}");
        var sibling = Path.Combine(direct, "new-sibling.md");
        File.WriteAllText(sibling, "# New sibling\n");
        ObserveFor(2000);
        Report($"DIRECT OPEN WATCHER: newly created sibling detected={main.DocumentFiles.Any(file => file.Path == sibling)}; expected with a folder watcher=True");
        File.WriteAllLines(Path.Combine(output, "observations.txt"), observations);
    }
}

public sealed class ProbeDocumentFileFactory(IAppLocalizer localizer) : IDocumentFileFactory
{
    private readonly DocumentFileFactory _inner = new(localizer);
    private int _createCount;
    private int _uiThreadCreateCount;
    public int CreateCount => Volatile.Read(ref _createCount);
    public int UiThreadCreateCount => Volatile.Read(ref _uiThreadCreateCount);

    public DocumentFile Create(string path, string? workspaceRoot = null)
    {
        Interlocked.Increment(ref _createCount);
        if (Dispatcher.UIThread.CheckAccess()) Interlocked.Increment(ref _uiThreadCreateCount);
        return _inner.Create(path, workspaceRoot);
    }

    public string GetModifiedText(string path) => _inner.GetModifiedText(path);
}
