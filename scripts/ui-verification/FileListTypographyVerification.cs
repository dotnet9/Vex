using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class FileListTypographyVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            LayoutSettle.Run(); window.UpdateLayout();
        }
        var view = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        var original = view.DataContext;
        var settings = new MemorySettings();
        var preferences = new EditorPreferences(settings);
        using var files = new ShellFilesViewModel(settings, preferences: preferences);
        view.DataContext = files;
        try
        {
            window.Width = 1280; window.Height = 820;
            var root = Path.Combine(output, "files");
            DocumentFile[] Documents(int count) => Enumerable.Range(1, count).Select(i =>
                new DocumentFile(Path.Combine(root, $"file-{i:D3}.md"), $"file-{i:D3}.md", "files", "", "")).ToArray();
            var tree = view.FindControl<TreeView>("FileTree")!;
            var list = view.FindControl<ListBox>("FileList")!;
            TextBlock[] Names(Control host, string name) => host.GetVisualDescendants().OfType<TextBlock>()
                .Where(t => t.IsEffectivelyVisible && t.Classes.Contains(name)).ToArray();
            void CheckSize(Control host, string name, double expected)
            {
                var labels = Names(host, name);
                Check(labels.Length > 0 && labels.All(t => t.FontSize == expected && t.Bounds.Height >= t.DesiredSize.Height - 1),
                    $"{name} uses {expected}px without vertical clipping");
            }
            files.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(Documents(30), workspaceRootPath: root)); Render();
            CheckSize(tree, "file-title", 12.5);
            files.ToggleListView(); Render();
            CheckSize(list, "file-list-name", 12.5);
            files.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(Documents(150), workspaceRootPath: root)); Render();
            Check(files.FlatDocumentFileNodes.Count == 150, "Large file list keeps every document");
            preferences.FileListFontSize = 11; Render();
            CheckSize(list, "file-list-name", 11);
            files.FilterText = "file-00"; Render();
            CheckSize(list, "file-list-name", 11);
            Check(preferences.FileListFontSize == 11, "Filtering keeps the chosen font size");
            files.ClearFilter(); files.ToggleListView(); Render();
            CheckSize(tree, "file-title", 11);
            CheckSize(tree, "file-heading", 10.5);
            Check(new EditorPreferences(settings).FileListFontSize == 11, "A new preferences instance restores the saved font size");
            foreach (var size in new[] { 10d, 18d })
            {
                preferences.FileListFontSize = size; Render(); CheckSize(tree, "file-title", size);
                files.ToggleListView(); Render(); CheckSize(list, "file-list-name", size);
                files.ToggleListView(); Render();
            }
            preferences.FileListFontSize = 99;
            Check(preferences.FileListFontSize == 18, "File font size clamps to the supported maximum");
            preferences.FileListFontSize = double.NaN;
            Check(preferences.FileListFontSize == 18, "Invalid font input does not overwrite the saved choice");
            Console.WriteLine("File list typography verification complete");
        }
        finally { view.DataContext = original; }
    }
}
