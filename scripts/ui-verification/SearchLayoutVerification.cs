using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Core.Messaging;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.ViewModels;
using Microsoft.Extensions.DependencyInjection;

internal static class SearchLayoutVerification
{
    public static void Run(MainWindow window)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var editorVm = Vex.App.Services.GetRequiredService<MarkdownEditorViewModel>();
        var source = window.Workspace.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!
            .FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!;
        const string text = "# Search\n\n## First\n\nTarget alpha\n\n## Second\n\nTarget beta\n\nTargetGamma";
        vm.Markdown = text;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); LayoutSettle.Run(); Dispatcher.UIThread.RunJobs(); }
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        foreach (var layout in new[] { "instant", "read", "map", "source", "source-render", "source-map", "instant-map", "three" })
        {
            vm.Workbench.SelectLayout(layout); Render();
            source.Editor.CaretOffset = 0; source.Editor.SelectionLength = 0;
            var before = vm.Workbench.History.Capture();
            for (var i = 0; i < 2; i++)
            {
                editorVm.ApplySearch(new EditorSearchCommand(EditorSearchAction.FindNext, "Target", isWholeWord: true)); Render();
                Check(vm.Workbench.WorkspaceLayout == layout && source.SelectedText == "Target" && vm.FindBar.CurrentMatch == i + 1,
                    $"Search reveals source match {i + 1} without changing {layout}");
            }
            editorVm.ApplySearch(new EditorSearchCommand(EditorSearchAction.FindPrev, "Target", isWholeWord: true)); Render();
            Check(vm.FindBar.CurrentMatch == 1, $"Reverse search uses the same result order in {layout}");
            var after = vm.Workbench.History.Capture();
            Check(vm.Markdown == text && before.Current == after.Current && before.Undo.SequenceEqual(after.Undo) && before.Redo.SequenceEqual(after.Redo),
                $"Search leaves document and undo history untouched in {layout}");
            editorVm.ApplySearch(new EditorSearchCommand(EditorSearchAction.Count, "Target\\s+(alpha|beta)", isRegex: true)); Render();
            Check(vm.Workbench.WorkspaceLayout == layout && vm.FindBar.FindCountText.Contains('2'), $"Regex count shares source semantics in {layout}");
        }
        vm.Workbench.SelectLayout("read"); Render();
        editorVm.ApplySearch(new EditorSearchCommand(EditorSearchAction.ReplaceAll, "Target", "Found")); Render();
        Check(vm.Workbench.WorkspaceLayout == "source" && vm.Markdown.Contains("Found alpha"), "Replace opens the source editor and applies one text mutation");
    }
}
