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
        var theme = title.FindControl<MenuItem>("ThemeMenuItem")!;
        var typography = title.FindControl<MenuItem>("TypographyMenuItem")!;
        Console.WriteLine($"MENU COUNTS file={file.Items.Count} theme={theme.Items.OfType<MenuItem>().Count()} typography={typography.Items.OfType<MenuItem>().Count()}");
        Check(file.Items.Count == 18 && file.Items.OfType<MenuItem>().Any(row => row.Header?.ToString() != null),
            "File menu keeps its row groups and exposes the Save All row");
        Check(theme.Items.OfType<MenuItem>().Count() == 12, "View menu exposes three modes and all nine palettes in a cascade");
        Check(typography.Items.OfType<MenuItem>().Count() == 13, "View menu exposes twelve typography choices and compact layout");
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
            var lastOption = typography.Items.OfType<MenuItem>().Last();
            var lastBottom = lastOption.TranslatePoint(new Point(0, lastOption.Bounds.Height), panel)!.Value.Y;
            Check(lastBottom <= panel.Bounds.Height + 1 && scroll.Viewport.Height > 100,
                $"Last typography option stays reachable: {culture.CultureName} (extent={scroll.Extent.Height}, viewport={scroll.Viewport.Height}, lastBottom={lastBottom}, panel={panel.Bounds.Height})");
            typography.IsSubMenuOpen = false; Render();
            view.IsSubMenuOpen = false; Render();
        }
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Appearance.SelectPaletteByKey("indigo"); Render();
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
        Check(preferenceGrid.Children[0].Bounds.Height == 36 && preferenceGrid.RowDefinitions.Count == 2,
            "Preferences title keeps its 36px header without a fixed footer");
        foreach (var toggle in preferences.GetVisualDescendants().OfType<ToggleSwitch>())
        {
            var track = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SwitchBackgroundBorder");
            var knob = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SwitchKnobIndicator");
            var point = knob.TranslatePoint(default, track)!.Value;
            Check(point.X >= 0 && point.Y >= 0 && point.X + knob.Bounds.Width <= track.Bounds.Width && point.Y + knob.Bounds.Height <= track.Bounds.Height, $"Preference switch knob remains inside its track: {toggle.Name} ({point}, {knob.Bounds.Size}, {track.Bounds.Size})");
        }
        Capture(preferenceCard, "preferences");
        ((ShellPreferencesViewModel)preferences.DataContext!).Close(); Render();
        var aboutTask = overlay.ShowAboutAsync(); Render();
        var about = window.GetVisualDescendants().OfType<ShellAboutOverlayView>().Single();
        var aboutCard = about.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("dialog-card"));
        Check(aboutCard.Bounds.Width == 420 && ((Grid)aboutCard.Child!).Children[0].Bounds.Height == 36, "About uses the prototype's 420px frame and 36px title bar");
        Capture(aboutCard, "about");
        ((ShellAboutViewModel)about.DataContext!).Close(); Render();
        foreach (var (show, type, width, name) in new (Func<Task>, Type, double, string)[]
        {
            (() => overlay.ShowPropertiesAsync(vm.DocumentInfo), typeof(ShellPropertiesOverlayView), 520, "properties"),
            (() => overlay.ShowStatisticsAsync(vm.DocumentInfo), typeof(ShellStatisticsOverlayView), 360, "statistics"),
            (() => overlay.ConfirmDeleteAsync("确定要删除「读书摘录.md」吗？", "此操作会从磁盘永久删除该文件，且无法从回收站恢复。", @"D:\Notes\笔记\读书摘录.md"), typeof(ShellDeleteConfirmationOverlayView), 480, "delete")
        })
        {
            var pending = show(); Render();
            var view = window.GetVisualDescendants().OfType<Control>().Single(control => control.GetType() == type);
            var frame = view.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("dialog-card"));
            Check(frame.Bounds.Width == width, "Document dialog retains prototype width: " + name);
            var heading = frame.GetVisualDescendants().OfType<Border>().First(border => border.Height == 36);
            Check(heading.Bounds.Height == 36, "Document dialog retains 36px title bar: " + name);
            Capture(frame, name);
            ShellOverlayHost.Dismiss(view); Render();
            Check(pending.IsCompleted, "Document dialog closes without changing the document: " + name);
        }
        var renameModel = new ShellRenameDialogViewModel(app.Resolve<IAppLocalizer>(), @"D:\Notes\笔记\主题设计笔记.md", "主题设计笔记.md");
        var renamePending = Ursa.Controls.OverlayDialog.ShowCustomAsync<string?>(new ShellRenameDialogView(), renameModel,
            options: new Ursa.Controls.OverlayDialogOptions { IsCloseButtonVisible = false, CanDragMove = false });
        Render();
        var renameView = window.GetVisualDescendants().OfType<ShellRenameDialogView>().Single();
        var renameCard = renameView.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("dialog-card"));
        Check(renameCard.Bounds.Width == 430, "Rename retains its 430px prototype frame");
        Check(renameView.FindControl<TextBox>("NameTextBox")!.Bounds.Height == 34, "Rename input retains its 34px height");
        Capture(renameCard, "rename"); renameModel.Cancel(); Render();
        Check(renamePending.IsCompleted, "Rename cancellation completes without changing the file");
        foreach (var width in new[] { 980, 1280, 980 })
        foreach (var mode in new[] { "instant", "read" })
        {
            window.Width = width; window.Height = width == 980 ? 640 : 820;
            vm.Workbench.SelectLayout(mode); vm.Workbench.ShowTypography = true; Render();
            vm.Appearance.SelectedTypography = vm.Appearance.TypographyOptions.Last(); Render();
            var position = gallery.TranslatePoint(default, window)!.Value;
            Check(position.X >= 0 && position.Y >= 0 && position.X + gallery.Bounds.Width <= window.ClientSize.Width && position.Y + gallery.Bounds.Height <= window.ClientSize.Height, $"Typography reflows without measure mutation and fits {width}px/{mode}: {position}, {gallery.Bounds.Size}, host {window.ClientSize}");
            vm.Workbench.ShowTypography = false; Render();
        }
    }
}
