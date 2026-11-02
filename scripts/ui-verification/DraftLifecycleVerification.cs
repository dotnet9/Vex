using System.Diagnostics;
using System.Reflection;
using System.Text;
using Vex.Core.Models;
using Vex.Modules.Shell.Services;

internal static class DraftLifecycleVerification
{
    private static DocumentSnapshot Document(string root, bool untitled = false) =>
        new(untitled ? null : Path.Combine(root, "source.md"), "source.md", "disk baseline", new UTF8Encoding(false), false);

    public static void RunChild(string root, bool untitled)
    {
        using var drafts = new AutoSaveDraftService { StorageDirectory = root };
        var document = Document(root, untitled);
        drafts.QueueSave(document, "unsaved child content", document.Markdown);
        if (!drafts.Flush()) throw new IOException(drafts.LastError);
        Console.WriteLine("READY");
        while (Console.ReadLine() is { } command)
        {
            if (command == "close")
            {
                if (!drafts.CompleteSession()) throw new IOException(drafts.LastError);
                Console.WriteLine("CLOSED");
                return;
            }
            Console.WriteLine("ACTIVE");
        }
    }

    public static void Run(string output)
    {
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        static string Read(Process child) => child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("Draft child exited: " + child.StandardError.ReadToEnd());
        static Process Start(string root, bool untitled)
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("draft-child"); start.ArgumentList.Add(root);
            start.ArgumentList.Add(untitled ? "untitled" : "named");
            var child = Process.Start(start)!;
            while (Read(child) != "READY") { }
            return child;
        }

        foreach (var untitled in new[] { false, true })
        {
            var root = Path.Combine(output, "process-" + Guid.NewGuid().ToString("N"));
            var document = Document(root, untitled);
            using var observer = new AutoSaveDraftService { StorageDirectory = root };
            using (var child = Start(root, untitled))
            {
                try
                {
                    Check(observer.TryRestore(document) is null, $"Live process has no recovery notice (untitled={untitled})");
                    child.StandardInput.WriteLine("cancel"); child.StandardInput.Flush();
                    Check(Read(child) == "ACTIVE" && Directory.GetFiles(root, "*.json").Length == 1
                        && observer.TryRestore(document) is null, "Cancel keeps the active backup without recovery in other windows");
                    child.StandardInput.WriteLine("close"); child.StandardInput.Flush();
                    Check(Read(child) == "CLOSED" && child.WaitForExit(15000) && child.ExitCode == 0,
                        "Normal child process completes the session");
                    Check(observer.TryRestore(document) is null && Directory.GetFiles(root, "*.json").Length == 0,
                        "Normal exit leaves no recovery notice or active draft");
                }
                finally { if (!child.HasExited) { child.Kill(true); child.WaitForExit(); } }
            }
            using (var child = Start(root, untitled))
            {
                child.Kill(true); child.WaitForExit();
            }
            Check(observer.TryRestore(document)?.Markdown == "unsaved child content", "A force-killed process offers the exact unsaved content");
            observer.ClearRestored(document);
            Check(observer.TryRestore(document) is null && Directory.GetFiles(root, "*.resolved").Length == 1,
                "Resolved recovery stays archived and does not recur");

            using (var crashed = new AutoSaveDraftService { StorageDirectory = root })
            {
                crashed.QueueSave(document, "recovered then edited", document.Markdown);
                Check(crashed.Flush(), "A recovered document has its own durable backup");
            }
            Check(observer.TryRestore(document)?.Markdown == "recovered then edited", "A second abnormal exit preserves edits after recovery");
            Check(observer.CompleteSession(), "Normal exit resolves discovered but unopened historical candidates");
            using var reopened = new AutoSaveDraftService { StorageDirectory = root };
            Check(reopened.TryRestore(document) is null, "The next launch has no repeated historical recovery notice");
        }

        var storage = Path.Combine(output, "storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storage);
        var blocked = Path.Combine(storage, "blocked"); File.WriteAllText(blocked, "not a directory");
        var snapshot = Document(storage);
        using var writer = new AutoSaveDraftService { StorageDirectory = blocked };
        writer.QueueSave(snapshot, "retry content", snapshot.Markdown);
        Check(!writer.Flush() && writer.LastError is not null, "Storage failure is visible and retains pending content");
        File.Delete(blocked);
        Check(writer.Flush() && writer.LastError is null, "Retry writes the original pending content");
        using var reader = new AutoSaveDraftService { StorageDirectory = blocked };
        Check(reader.TryRestore(snapshot) is null, "Concurrent services also honor the active session lock");
        writer.Dispose();
        Check(reader.TryRestore(snapshot with { Markdown = "newer disk content" })?.Markdown == "retry content",
            "Newer disk content does not erase an abandoned backup");
        var valid = Directory.GetFiles(blocked, "*.json").Single();
        var corruptSession = Guid.NewGuid().ToString("N");
        var corrupt = Path.Combine(blocked, Path.GetFileName(valid).Split('.')[0] + "." + corruptSession + ".json");
        File.WriteAllText(Path.Combine(blocked, corruptSession + ".session"), "active");
        File.WriteAllText(corrupt, "{ corrupt");
        Check(reader.TryRestore(snapshot) is not null && reader.LastError is not null && File.ReadAllText(corrupt) == "{ corrupt",
            "A corrupt abandoned record remains intact and does not block a valid recovery");
        using (File.Open(valid, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(!reader.CompleteSession() && reader.LastError is not null, "Recovery cleanup failure cannot report a successful normal exit");
        Check(reader.CompleteSession() && reader.LastError is null, "Normal session completion can retry after a cleanup failure");
        File.WriteAllText(valid, File.ReadAllText(valid + ".resolved"));
        File.Delete(Path.Combine(blocked, Path.GetFileNameWithoutExtension(valid).Split('.').Last() + ".session"));
        using var legacy = new AutoSaveDraftService { StorageDirectory = blocked };
        Check(legacy.TryRestore(snapshot) is null && File.Exists(valid), "Legacy unclassified backups remain on disk without automatic crash notices");
    }
}
