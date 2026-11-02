using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Modules.Shell.ViewModels;

internal static class SelectionPickerVerification
{
    public static void Run(VerificationVexApp app, string output)
    {
        var shell = app.CreateShell();
        var model = (MainWindowViewModel)shell.DataContext!;
        var checks = new List<string>();
        foreach (var theme in new[] { "light", "dark" })
        {
            model.Appearance.SelectThemeByKey(theme);
            var changes = new List<string>();
            var pickerModel = new MarkdownSpanStylePickerViewModel("color", (_, value) => changes.Add(value), () => { }, _ => "#C92A2A");
            var picker = new MarkdownSpanStylePicker { DataContext = pickerModel };
            var flyout = new MarkdownSpanStyleFlyout { Content = picker };
            var button = new Button { Content = "Color", Flyout = flyout };
            var window = new Window { Width = 640, Height = 500, Content = button };
            window.Show(); Dispatcher.UIThread.RunJobs();
            try
            {
                for (var round = 0; round < 2; round++)
                {
                    flyout.ShowAt(button); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    picker.FindControl<TextBox>("HexInput")!.Text = "#30953B";
                    var apply = picker.FindControl<Button>("ApplyColor")!;
                    var point = apply.TranslatePoint(new Point(apply.Bounds.Width / 2, apply.Bounds.Height / 2), window)!.Value;
                    window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                    Dispatcher.UIThread.RunJobs();
                    if (flyout.IsOpen || changes.Count != round + 1 || changes[^1] != "#30953B")
                        throw new InvalidOperationException($"Color commit/reopen failed under {theme}, round {round}");
                }
                checks.Add($"PASS {theme}: actual color apply, popup detach and reopen preserve commit without missing theme resources");
                Console.WriteLine(checks[^1]);
            }
            finally { window.Close(); }
        }
        File.WriteAllLines(Path.Combine(output, "selection-picker.txt"), checks);
    }
}
