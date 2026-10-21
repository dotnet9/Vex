using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class PopupLayoutVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1280; window.Height = 820;
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectedLanguage = vm.Appearance.LanguageOptions.First(x => x.CultureName == "zh-CN");
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        var title = window.FindControl<ShellTitleMenuView>("TitleMenuView")!;
        var file = title.FileMenuTarget;
        Check(file.Items.Count == 16, "File menu matches the prototype's four groups and has no Save All row");
        var theme = title.FindControl<MenuItem>("ThemeMenuItem")!;
        var typography = title.FindControl<MenuItem>("TypographyMenuItem")!;
        Check(theme.Items.OfType<MenuItem>().Count() == 19, "View menu exposes three modes and all sixteen palettes in a cascade");
        Check(typography.Items.OfType<MenuItem>().Count() == 28, "View menu exposes twenty-seven typography choices and compact layout");
        var choice = theme.Items.OfType<MenuItem>().Last();
        choice.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Render();
        Check(choice.IsChecked, "Selecting a palette synchronizes the title menu checkmark");
        foreach (var item in ((Menu)title.Content!).Items.OfType<MenuItem>())
        {
            item.IsSubMenuOpen = true; Render();
            var popup = item.GetVisualDescendants().OfType<Popup>().First();
            var panel = popup.Child!;
            Check(panel.Bounds.Width == 248, "Menu panel is 248px: " + item.Header);
            Check(item.Items.OfType<MenuItem>().All(row => row.Bounds.Height >= 32), "Menu rows retain the prototype's 32px target: " + item.Header);
            foreach (var row in item.Items.OfType<MenuItem>())
            {
                var header = row.GetVisualDescendants().OfType<ContentPresenter>().First(c => c.Name == "PART_HeaderPresenter");
                Check(Math.Abs(header.TranslatePoint(default, panel)!.Value.X - 41) < 1, "Every menu row reserves the state column: " + row.Header);
            }
            var name = item.Name ?? item.Header?.ToString() ?? "menu";
            using var image = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
            image.Render(panel); image.Save(Path.Combine(output, name + ".png"));
            File.WriteAllLines(Path.Combine(output, name + ".txt"), panel.GetVisualDescendants().Prepend(panel).OfType<Control>()
                .Select(c => $"{c.GetType().Name} {c.Name} {c.Bounds} {(c is TextBlock t ? t.Text : "")}"));
            Console.WriteLine($"CAPTURE {name}: {panel.Bounds}");
            item.IsSubMenuOpen = false; Render();
        }
        foreach (var culture in vm.Appearance.LanguageOptions)
        {
            vm.Appearance.SelectedLanguage = culture; Render();
            var view = title.FindControl<MenuItem>("ViewMenuItem")!;
            view.IsSubMenuOpen = true; Render();
            typography.IsSubMenuOpen = true; Render();
            var panel = typography.GetVisualDescendants().OfType<Popup>().First().Child!;
            Check(panel.Bounds.Height <= window.ClientSize.Height - 56, "Long typography cascade scrolls within the window: " + culture.CultureName);
            var scroll = panel.GetVisualDescendants().OfType<ScrollViewer>().First();
            Check(scroll.Extent.Height > scroll.Viewport.Height && scroll.Viewport.Height > 100, $"Last typography option stays reachable: {culture.CultureName} ({scroll.Extent}/{scroll.Viewport})");
            typography.IsSubMenuOpen = false; Render();
            view.IsSubMenuOpen = false; Render();
        }
    }
}
