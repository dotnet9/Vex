using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

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
                "Real locked-file failure preserves content and unsaved state");
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
