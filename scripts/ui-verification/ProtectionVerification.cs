using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Shell.Services;
using System.Text;

internal static class ProtectionVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Pump(Task? task = null)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            do { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); if (task is null || task.IsCompleted) break;
                if (DateTime.UtcNow > deadline) throw new TimeoutException(); Thread.Sleep(10); } while (true);
            task?.GetAwaiter().GetResult(); Dispatcher.UIThread.RunJobs();
        }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        void Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!condition()) { Pump(); if (DateTime.UtcNow > deadline) throw new TimeoutException(); Thread.Sleep(20); }
            Pump();
        }
        ShellDocumentReviewModel Review() => (ShellDocumentReviewModel)window.GetVisualDescendants().OfType<ShellDocumentReviewView>().Single().DataContext!;
        var path = Path.Combine(output, "protected.md");
        File.WriteAllText(path, "# original\n\nSaved content");
        Pump(vm.OpenPathAsync(path));
        vm.Markdown += "\n\nUnsaved changes"; Pump();
        var beforeFailure = vm.Markdown;
        var history = vm.Workbench.History.Capture();
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Pump(vm.SaveAsync());
            Check(vm.Protection.HasSaveError && vm.DocumentInfo.IsModified && vm.Markdown == beforeFailure,
                $"Real locked-file failure preserves content and unsaved state: error={vm.Protection.HasSaveError}, modified={vm.DocumentInfo.IsModified}, unchanged={vm.Markdown == beforeFailure}");
            Check(vm.Workbench.History.Capture().Current == history.Current, "Failed save preserves shared undo history");
            vm.Markdown += "\nContinue editing"; Pump();
            Check(vm.Protection.HasSaveError && window.GetVisualDescendants().OfType<ShellWorkflowNotice>().Single().IsVisible,
                "Save failure stays visible while editing continues");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "save-failure.png"));
        }
        Pump(vm.RetrySaveAsync());
        Check(!vm.Protection.HasSaveError && !vm.DocumentInfo.IsModified && File.ReadAllText(path) == vm.Markdown,
            "Retry writes the latest content and clears the persistent failure");
        vm.Markdown += "\nNot saved by export"; Pump();
        var beforeExport = vm.Markdown; var typography = vm.Appearance.SelectedTypography;
        var exports = (VerificationExports)app.Resolve<IMarkdownExportService>();
        exports.Fail = true; Pump(vm.Export("HTML"));
        Check(vm.Protection.HasExportError && vm.DocumentInfo.IsModified && vm.Markdown == beforeExport,
            "Export failure preserves Markdown and has its own retry state");
        exports.Fail = false; exports.Cancel = true; Pump(vm.RetryExportAsync());
        Check(vm.Protection.HasExportError, "Canceling an export retry does not erase the failure");
        exports.Cancel = false; Pump(vm.RetryExportAsync());
        Check(!vm.Protection.HasExportError && vm.DocumentInfo.IsModified && vm.Appearance.SelectedTypography == typography,
            "Successful export retry preserves typography and does not mark Markdown saved");
        Pump(vm.SaveAsync());

        // 使用真实磁盘监听，不直接调用待测的重新载入方法。
        var current = vm.Markdown;
        File.WriteAllText(path, "# external version\n\nDisk change");
        Until(() => vm.Protection.HasExternal);
        Check(vm.Markdown == current, "Actual file watcher offers external changes without replacing current content");
        var beforeCompare = vm.Workbench.History.Capture();
        var compareTask = vm.CompareExternalAsync(); Pump();
        Check(Review().Current == current && Review().Incoming.Contains("Disk change"), "Comparison exposes both complete versions");
        window.Width = 800; window.Height = 480; Pump();
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "external-comparison.png"));
        Review().Close(); Pump(compareTask);
        Check(vm.Markdown == current && vm.Protection.HasExternal && vm.Workbench.History.Capture().Current == beforeCompare.Current,
            "Cancel keeps content, undo history and the pending external version");
        compareTask = vm.CompareExternalAsync(); Pump(); Review().Keep(); Pump(compareTask);
        Check(vm.Markdown == current && !vm.Protection.HasExternal, "Keep-current explicitly resolves the external notice without rewriting content");
        File.WriteAllText(path, "# second external version\n\nMore disk changes");
        Until(() => vm.Protection.HasExternal);
        compareTask = vm.CompareExternalAsync(); Pump(); Review().Load(); Pump(compareTask);
        Check(vm.Markdown.Contains("More disk changes") && !vm.DocumentInfo.IsModified, "Loading the selected external version uses its disk baseline");
        vm.Workbench.UndoDocument(); Pump();
        Check(vm.Markdown == current && vm.DocumentInfo.IsModified, "External loading is one reversible shared-history edit");
        Pump(vm.SaveAsync());

        var memoryDrafts = (MemoryDrafts)app.Resolve<IAutoSaveDraftService>();
        memoryDrafts.RestoredMarkdown = "# unsaved recovery\n\nRecovered content";
        Pump(vm.OpenPathAsync(path));
        Check(vm.Protection.HasRecovery && vm.Markdown == current && !vm.DocumentInfo.IsModified,
            "Recovery discovery leaves the opened document untouched");
        var recoveryTask = vm.ReviewRecoveryAsync(); Pump(); Review().Close(); Pump(recoveryTask);
        Check(vm.Markdown == current && vm.Protection.HasRecovery, "Canceling recovery keeps both source and draft");
        recoveryTask = vm.ReviewRecoveryAsync(); Pump();
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "recovery-preview.png"));
        Review().Load(); Pump(recoveryTask);
        Check(vm.DocumentInfo.CurrentFilePath is null && vm.DocumentInfo.IsModified && vm.Markdown.Contains("Recovered content")
            && File.ReadAllText(path) == current, "Recovery creates an independent unsaved document and never writes the source file");
        memoryDrafts.RestoredMarkdown = null;
        Pump(vm.OpenPathAsync(path));
        vm.Markdown += "\nClose protection"; Pump();
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var closeTask = vm.BeginWindowCloseAsync(); Pump();
            var dialog = (ShellUnsavedDialogViewModel)window.GetVisualDescendants().OfType<ShellUnsavedDialogView>().Single().DataContext!;
            dialog.Save(); Pump(closeTask);
            Check(window.IsVisible && vm.DocumentInfo.IsModified && vm.Protection.HasSaveError, "Failed save keeps the actual application window open");
        }
        memoryDrafts.FailFlush = true;
        var failedClose = vm.BeginWindowCloseAsync(); Pump();
        ((ShellUnsavedDialogViewModel)window.GetVisualDescendants().OfType<ShellUnsavedDialogView>().Single().DataContext!).KeepDraft(); Pump(failedClose);
        Check(window.IsVisible && vm.Protection.HasDraftError, "Failed draft retention also keeps the application open");
        VerifyDraftStorage(output, Check);
        memoryDrafts.FailFlush = false; vm.RetryDraft(); Pump();
        var retainedClose = vm.BeginWindowCloseAsync(); Pump();
        ((ShellUnsavedDialogViewModel)window.GetVisualDescendants().OfType<ShellUnsavedDialogView>().Single().DataContext!).KeepDraft(); Pump(retainedClose);
        Check(!window.IsVisible, "Successful explicit draft retention completes the normal close workflow");
    }

    private static void VerifyDraftStorage(string output, Action<bool, string> check)
    {
        var root = Path.Combine(output, "real-draft-store");
        Directory.CreateDirectory(root);
        var blocked = Path.Combine(root, "blocked-root"); File.WriteAllText(blocked, "not a directory");
        var first = new AutoSaveDraftService { StorageDirectory = blocked };
        var source = Path.Combine(root, "source.md"); File.WriteAllText(source, "disk baseline");
        var snapshot = new DocumentSnapshot(source, "source.md", "disk baseline", new UTF8Encoding(false), false);
        first.QueueSave(snapshot, "first unsaved draft", snapshot.Markdown);
        check(!first.Flush() && first.LastError is not null, "Real draft storage failure is observable");
        File.Delete(blocked);
        check(first.Flush() && first.LastError is null, "Draft retry retains and writes the failed pending snapshot");
        var second = new AutoSaveDraftService { StorageDirectory = blocked };
        File.WriteAllText(source, "newer external disk version");
        File.SetLastWriteTimeUtc(source, DateTime.UtcNow.AddMinutes(1));
        check(second.TryRestore(snapshot with { Markdown = "newer external disk version" })?.Markdown == "first unsaved draft",
            "A newer disk file does not discard an older recovery draft");
        second.QueueSave(snapshot, "second process content", snapshot.Markdown); check(second.Flush(), "Second process draft is persisted");
        var drafts = Directory.GetFiles(blocked, "*.json");
        check(drafts.Length == 2, "Two process sessions retain independent draft files");
        var corrupt = Path.Combine(blocked, Path.GetFileName(drafts[0]).Split('.')[0] + ".corrupt.json");
        File.WriteAllText(corrupt, "{ corrupt draft"); File.SetLastWriteTimeUtc(corrupt, DateTime.UtcNow.AddMinutes(2));
        var third = new AutoSaveDraftService { StorageDirectory = blocked };
        check(third.TryRestore(snapshot) is not null && third.LastError is not null && File.ReadAllText(corrupt) == "{ corrupt draft",
            "Unreadable draft data stays intact while other valid drafts remain recoverable");
    }
}

public sealed class VerificationExports : IMarkdownExportService
{
    public bool Fail { get; set; }
    public bool Cancel { get; set; }
    private Task<string?> Export() => Fail ? Task.FromException<string?>(new IOException("Export target is unavailable"))
        : Task.FromResult<string?>(Cancel ? null : "verified-export.html");
    public Task<string?> ExportMarkdownAsync(DocumentSnapshot document) => Export();
    public Task<string?> ExportHtmlAsync(DocumentSnapshot document) => Export();
    public Task<string?> ExportPdfAsync(DocumentSnapshot document) => Export();
    public Task<string?> ExportPngAsync(DocumentSnapshot document) => Export();
    public Task<string?> ExportWordAsync(DocumentSnapshot document) => Export();
    public Task<bool> CopyHtmlAsync(DocumentSnapshot document, string? target) => Task.FromResult(false);
    public Task<string?> OpenHtmlPrintPreviewAsync(DocumentSnapshot document) => Export();
}
