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
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

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
        void Capture(Control control, string name)
        {
            Render();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height));
            bitmap.Render(control); bitmap.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine($"CAPTURE {name}: {control.Bounds}");
        }
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
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Appearance.SelectPaletteByKey("weft"); Render();
        var appearance = window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")!;
        foreach (var (button, name) in new[] { (appearance.AppearanceButton, "appearance"), (appearance.LanguageButton, "language") })
        {
            ((Flyout)button.Flyout!).ShowAt(button); Render();
            var content = (Control)((Flyout)button.Flyout).Content!;
            var presenter = content.GetVisualAncestors().OfType<FlyoutPresenter>().First();
            var frame = presenter.GetVisualDescendants().OfType<Border>().First(b => b.Name == "LayoutRoot");
            Check(Math.Abs(frame.Bounds.Width - 248) < 1, "Title picker uses the same 248px visible frame: " + name);
            Capture(presenter, name); button.Flyout.Hide(); Render();
        }
        vm.Workbench.ShowTypography = true; Render();
        var gallery = window.FindControl<ShellTypographyGallery>("TypographyGallery")!;
        var cardScroll = gallery.GetVisualDescendants().OfType<ScrollViewer>().First();
        var compact = gallery.GetVisualDescendants().OfType<CheckBox>().Single();
        Check(compact.TranslatePoint(default, gallery)!.Value.Y > cardScroll.TranslatePoint(default, gallery)!.Value.Y + cardScroll.Bounds.Height, "Compact layout stays in the fixed footer below the scrolling cards");
        var cards = gallery.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Typography_") == true && b.IsVisible).Take(3).ToArray();
        Check(cards.Max(c => c.Bounds.Height) - cards.Min(c => c.Bounds.Height) < 1, "Typography cards in a row stretch to the same height");
        Capture(gallery, "typography");
        vm.Workbench.ShowTypography = false; vm.Workbench.ShowLayouts = true; Render();
        Capture(window.FindControl<WorkspaceLayoutGallery>("LayoutGallery")!, "layouts");
        vm.Workbench.ShowLayouts = false;
        var overlay = app.Resolve<IShellOverlayService>();
        var task = overlay.ShowPreferencesAsync(app.Resolve<ShellPreferencesViewModel>(), "general"); Render();
        var preferences = window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Single();
        var preferenceCard = preferences.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("dialog-card"));
        Check(preferenceCard.Bounds.Size == new Size(675, 700), "Preferences retains the prototype's 675 by 700 frame");
        Check(preferences.GetVisualDescendants().OfType<ListBox>().Single().Bounds.Width == 148, "Preferences navigation is 148px wide");
        var preferenceGrid = (Grid)preferenceCard.Child!;
        Check(preferenceGrid.Children[0].Bounds.Height == 36 && preferenceGrid.Children[2].Bounds.Height == 54, "Preferences title and fixed footer retain their 36px and 54px heights");
        foreach (var toggle in preferences.GetVisualDescendants().OfType<ToggleSwitch>())
        {
            var track = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SwitchBackgroundBorder");
            var knob = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SwitchKnobIndicator");
            var point = knob.TranslatePoint(default, track)!.Value;
            Check(point.X >= 0 && point.Y >= 0 && point.X + knob.Bounds.Width <= track.Bounds.Width && point.Y + knob.Bounds.Height <= track.Bounds.Height, "Preference switch knob remains inside its track: " + toggle.Name);
        }
        Capture(preferenceCard, "preferences");
        ((ShellPreferencesViewModel)preferences.DataContext!).Close(); Render();
    }
}
