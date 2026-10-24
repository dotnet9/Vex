using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Services;

/// <summary>使用真实磁盘事件核验外部候选不覆盖正在编辑的正文及比较修订保护。</summary>
internal static class CurrentFileProtectionVerification
{
    public static void Run(VerificationVexApp app, Window window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var results = new List<string>();
        var root = Path.Combine(output, "current-protection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "current.md");
        File.WriteAllText(path, "# Saved baseline\n");
        Pump(vm.OpenPathAsync(path));
        vm.Workbench.ShowRead();
        Pump();
        var baseline = vm.Markdown;

        var timestamp = File.GetLastWriteTimeUtc(path);
        File.WriteAllText(path, "# Same timestamp external\n");
        File.SetLastWriteTimeUtc(path, timestamp);
        Until(() => vm.Protection.External?.Markdown.Contains("Same timestamp external") == true);
        Check(vm.Markdown == baseline && !vm.DocumentInfo.IsModified,
            "Reading a saved document reports same-timestamp changes without replacing shared content");
        Keep();

        File.WriteAllText(path, "# Earlier timestamp external\n");
        File.SetLastWriteTimeUtc(path, timestamp.AddMinutes(-10));
        Until(() => vm.Protection.External?.Markdown.Contains("Earlier timestamp external") == true);
        Check(vm.Markdown == baseline, "A rollback timestamp cannot hide an external candidate");
        Keep();

        var replacement = Path.Combine(root, "replacement.tmp");
        File.WriteAllText(replacement, "# Atomic replacement\n");
        File.Move(replacement, path, overwrite: true);
        Until(() => vm.Protection.External?.Markdown.Contains("Atomic replacement") == true);
        Check(vm.Markdown == baseline, "Replacing the file atomically still offers an external candidate");
        var compare = vm.CompareExternalAsync();
        Until(() => Review() is not null);
        Review()!.Load(); Pump(compare);
        Check(vm.Markdown.Contains("Atomic replacement") && !vm.DocumentInfo.IsModified,
            "Confirmed loading updates the shared document baseline");
        vm.Workbench.UndoDocument(); Pump();
        Check(vm.Markdown == baseline && vm.DocumentInfo.IsModified, "External loading preserves undo to the prior shared content");
        Pump(vm.SaveAsync());
        Observe(1200);
        Check(!vm.Protection.HasExternal, "Own-save watcher events do not produce a false external conflict");

        vm.Workbench.ShowSource();
        vm.Markdown += "\nUnsaved edit"; Pump();
        File.WriteAllText(path, "# Candidate before user input\n");
        Until(() => vm.Protection.External?.Markdown.Contains("Candidate before user input") == true);
        var pendingCommitted = false;
        void CommitPending(object? sender, EventArgs args)
        {
            if (pendingCommitted) return;
            pendingCommitted = true;
            vm.Markdown += "\nPending editor input";
        }
        vm.CommitPendingEditsRequested += CommitPending;
        compare = vm.CompareExternalAsync();
        Until(() => Review() is not null);
        vm.CommitPendingEditsRequested -= CommitPending;
        Check(pendingCommitted && Review()!.Current.Contains("Pending editor input"),
            "Opening a comparison first commits pending editor input into its shared-current snapshot");
        vm.Markdown += "\nInput during comparison"; Pump();
        var latestInput = vm.Markdown;
        Review()!.Load(); PumpUntilComparisonSettles(compare);
        Check(vm.Markdown == latestInput && vm.DocumentInfo.IsModified && vm.Protection.HasExternal,
            "An outdated load decision cannot overwrite input entered during comparison");
        CloseReview(compare);

        compare = vm.CompareExternalAsync();
        Until(() => Review() is not null);
        File.WriteAllText(path, "# Newer candidate during comparison\n");
        Until(() => vm.Protection.External?.Markdown.Contains("Newer candidate during comparison") == true);
        Review()!.Load(); PumpUntilComparisonSettles(compare);
        Check(vm.Markdown == latestInput && vm.Protection.External?.Markdown.Contains("Newer candidate during comparison") == true,
            "A newer candidate invalidates an already open comparison's load decision");
        CloseReview(compare);
        compare = vm.CompareExternalAsync();
        Until(() => Review() is not null);
        Review()!.Close(); Pump(compare);
        Check(vm.Markdown == latestInput && vm.DocumentInfo.IsModified && vm.Protection.HasExternal,
            "Cancel keeps the latest input, unsaved state and pending candidate");
        Keep();

        for (var index = 0; index < 8; index++)
            File.WriteAllText(path, $"# Consecutive external {index}\n");
        Until(() => vm.Protection.External?.Markdown.Contains("Consecutive external 7") == true);
        Check(vm.Markdown == latestInput, "Consecutive disk writes converge to the latest candidate without replacing input");
        Keep();

        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Pump(vm.RetryCurrentFileAsync());
            Check(vm.Protection.HasFileReadError && vm.Markdown == latestInput,
                "A temporarily locked file exhausts bounded reading retries without substituting empty content");
        }
        Pump(vm.RetryCurrentFileAsync());
        Check(!vm.Protection.HasFileReadError && vm.Markdown == latestInput,
            "Explicit current-file retry recovers after the lock is released");
        Keep();

        File.Delete(path);
        Until(() => vm.Protection.HasFileReadError);
        Check(vm.Markdown == latestInput && vm.DocumentInfo.CurrentFilePath == path,
            "Deleting the current file reports unavailability and preserves its in-memory identity and content");
        File.WriteAllText(path, "# Restored external\n");
        Until(() => vm.Protection.External?.Markdown.Contains("Restored external") == true);
        Check(!vm.Protection.HasFileReadError && vm.Markdown == latestInput,
            "Restoring the path retries candidate reading without overwriting memory");
        Keep();

        var renamed = Path.Combine(root, "renamed.md");
        File.Move(path, renamed);
        Until(() => vm.Protection.HasFileReadError);
        Check(vm.DocumentInfo.CurrentFilePath == path && vm.Markdown == latestInput,
            "External rename does not silently switch the current document identity");
        File.Move(renamed, path);
        Until(() => !vm.Protection.HasFileReadError);
        Keep();
        ((WorkspaceFileService)app.Resolve<IWorkspaceFileService>()).RequestRecovery(new InternalBufferOverflowException("Verification injection"));
        Observe(1500);
        Check(vm.Markdown == latestInput && vm.DocumentInfo.IsModified,
            "Injected workspace watcher recovery preserves unsaved current content");

        // 清理本验证器的未保存状态，使正常关闭不进入额外确认对话框。
        Keep();
        Pump(vm.SaveAsync());
        var controlled = (ControlledDocumentService)app.Resolve<IDocumentService>();
        var other = Path.Combine(root, "other.md");
        File.WriteAllText(other, "# Other document\n");
        var savedA = vm.Markdown;
        controlled.DelayNextReload(path);
        File.WriteAllText(path, "# Captured old A candidate\n");
        Until(() => controlled.HasCapturedReload);
        Pump(vm.OpenPathAsync(other));
        File.WriteAllText(path, savedA);
        Pump(vm.OpenPathAsync(path));
        controlled.ReleaseReload();
        Observe(1200);
        Check(vm.DocumentInfo.CurrentFilePath == path && vm.Markdown == savedA && !vm.Protection.HasExternal,
            "A captured slow read from the first A cannot publish after A to B to A in the same workspace");

        controlled.DelayNextReload(path);
        File.WriteAllText(path, "# Read before another disk write\n");
        Until(() => controlled.HasCapturedReload);
        File.WriteAllText(path, "# Latest write during candidate reading\n");
        controlled.ReleaseReload();
        Until(() => vm.Protection.External?.Markdown.Contains("Latest write during candidate reading") == true);
        Check(vm.Markdown == savedA, "A write during candidate reading invalidates the old snapshot and schedules the newest one");
        Keep();
        File.WriteAllLines(Path.Combine(output, "current-file-protection.txt"), results);

        ShellDocumentReviewModel? Review() => window.GetVisualDescendants().OfType<ShellDocumentReviewView>()
            .Select(view => view.DataContext).OfType<ShellDocumentReviewModel>().SingleOrDefault();
        void Keep()
        {
            if (!vm.Protection.HasExternal) return;
            var task = vm.CompareExternalAsync();
            Until(() => Review() is not null);
            Review()!.Keep(); Pump(task);
        }
        void CloseReview(Task task)
        {
            Review()?.Close();
            Pump(task);
        }
        void PumpUntilComparisonSettles(Task task)
        {
            // 生产可选择直接保留提示或重新比较；两者都不得先覆盖正文。
            var deadline = DateTime.UtcNow.AddSeconds(3);
            do { Pump(); Thread.Sleep(10); } while (!task.IsCompleted && Review() is null && DateTime.UtcNow < deadline);
            Pump();
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message); Console.WriteLine("PASS " + message);
        }
        void Pump(Task? task = null)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            do
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                if (task is null || task.IsCompleted) break;
                if (DateTime.UtcNow > deadline) throw new TimeoutException("Current-file protection operation timed out.");
                Thread.Sleep(10);
            } while (true);
            task?.GetAwaiter().GetResult();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
        void Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(20);
            while (!condition())
            {
                Pump(); Thread.Sleep(20);
                if (DateTime.UtcNow > deadline) throw new TimeoutException("The current-file watcher did not reach the expected state.");
            }
            Pump();
        }
        void Observe(int milliseconds)
        {
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < milliseconds) { Pump(); Thread.Sleep(20); }
        }
    }
}

/// <summary>除指定的一次候选读取外，所有行为委托给真实文档服务。</summary>
public sealed class ControlledDocumentService : IDocumentService
{
    private readonly DocumentService _inner;
    private string? _delayedPath;
    private TaskCompletionSource? _release;
    private int _captured;
    public ControlledDocumentService(IDocumentFileFactory factory, IAppLocalizer localizer, EditorPreferences preferences, IWorkspaceFileService workspaceFiles)
        => _inner = new DocumentService(factory, localizer, preferences, workspaceFiles);
    public bool HasCapturedReload => Volatile.Read(ref _captured) != 0;
    public void DelayNextReload(string path)
    {
        _delayedPath = path;
        Volatile.Write(ref _captured, 0);
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public void ReleaseReload() => _release!.TrySetResult();
    public async Task<DocumentSnapshot> ReloadAsync(DocumentSnapshot document)
    {
        var snapshot = await _inner.ReloadAsync(document);
        if (document.FilePath == _delayedPath && Interlocked.CompareExchange(ref _captured, 1, 0) == 0)
        {
            _delayedPath = null;
            await _release!.Task;
        }
        return snapshot;
    }
    public string? LastOpenedFolderPath => _inner.LastOpenedFolderPath;
    public DocumentSnapshot CreateNew() => _inner.CreateNew();
    public Task<DocumentSnapshot?> OpenAsync() => _inner.OpenAsync();
    public Task<DocumentSnapshot?> ImportAsync(string format) => _inner.ImportAsync(format);
    public Task<DocumentSnapshot> OpenPathAsync(string path, string? encodingName = null) => _inner.OpenPathAsync(path, encodingName);
    public Task<IReadOnlyList<DocumentFile>> OpenFolderAsync() => _inner.OpenFolderAsync();
    public Task<IReadOnlyList<DocumentFile>> OpenFolderPathAsync(string folder) => _inner.OpenFolderPathAsync(folder);
    public bool IsSupportedDocumentPath(string path) => _inner.IsSupportedDocumentPath(path);
    public Task<DocumentSnapshot?> SaveAsync(DocumentSnapshot document) => _inner.SaveAsync(document);
    public Task<bool> TryAutoSaveAsync(DocumentSnapshot document, string lastSavedMarkdown) => _inner.TryAutoSaveAsync(document, lastSavedMarkdown);
    public Task<DocumentSnapshot?> SaveAsAsync(DocumentSnapshot document) => _inner.SaveAsAsync(document);
    public Task DeleteAsync(string path) => _inner.DeleteAsync(path);
    public Task<string> RenameAsync(string path, string newName) => _inner.RenameAsync(path, newName);
    public Task OpenFileLocationAsync(string path) => _inner.OpenFileLocationAsync(path);
}
