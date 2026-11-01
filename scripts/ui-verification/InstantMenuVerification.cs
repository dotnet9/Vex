using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

internal static class InstantMenuVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1280; window.Height = 820;
        vm.Workbench.SelectLayout("instant");
        const string original = "# Context actions\n\nKeep this paragraph.\n\nTarget paragraph.";
        vm.Markdown = original;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        Render();
        var view = window.GetVisualDescendants().OfType<MarkdownInstantView>().Single();
        var menu = view.Editor.ContextMenu!;
        foreach (var language in vm.Appearance.LanguageOptions.ToArray())
        {
            vm.Appearance.SelectLanguageByCulture(language.CultureName); Render();
            Check(ReferenceEquals(menu, view.Editor.ContextMenu)
                && menu.Items.OfType<MenuItem>().All(item => !string.IsNullOrWhiteSpace(item.Header?.ToString())),
                "AXAML menu updates localized labels without rebuilding controls: " + language.CultureName);
        }
        vm.Appearance.SelectLanguageByCulture("zh-CN"); Render();
        var text = view.Editor.GetVisualDescendants().OfType<SelectableTextBlock>()
            .First(block => (block.Inlines?.Text ?? block.Text ?? "").Contains("Target paragraph.", StringComparison.Ordinal));
        var point = text.TranslatePoint(new Point(12, text.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right); Render();
        Check(menu.IsOpen && view.Editor.ContextSourceLine == 5 && view.MenuActions.SourceLine == 5,
            "Actual right click binds the library block source line to the menu ViewModel");
        var convert = menu.Items.OfType<MenuItem>().First();
        menu.SelectedItem = convert; convert.IsSelected = true;
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
        Check(convert.IsSubMenuOpen, "Standard MenuItem opens the conversion submenu");
        var heading = convert.Items.OfType<MenuItem>().ElementAt(2);
        var actionPoint = heading.TranslatePoint(new Point(heading.Bounds.Width / 2, heading.Bounds.Height / 2), window)!.Value;
        window.MouseMove(actionPoint); window.MouseDown(actionPoint, MouseButton.Left); window.MouseUp(actionPoint, MouseButton.Left); Render();
        Check(vm.Markdown.ReplaceLineEndings("\n") == "# Context actions\n\nKeep this paragraph.\n\n## Target paragraph.",
            "The ViewModel command converts only the clicked block");
        Check(!menu.IsOpen, "A standard menu action dismisses the context menu");
        vm.Workbench.UndoDocument(); Render();
        Check(vm.Markdown == original, "Context actions preserve the application undo history");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "instant-menu-after-undo.png"));
    }
}
