using CodeWF.Avalonia.Controls.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Vex.Controls;
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
        Console.WriteLine($"MENU COUNTS file={file.Items.Count}");
        // 菜单树对齐原型 interact.js 的 MENUS：无「保存全部」，配色/排版入口在外观弹层而非菜单。
        var localizer = app.Resolve<Vex.Core.Services.IAppLocalizer>();
        string HeaderText(object? header) => header?.ToString() ?? string.Empty;
        var fileHeaders = file.Items.OfType<MenuItem>().Select(row => HeaderText(row.Header)).ToArray();
        Check(fileHeaders.Contains(localizer.Get(Vex.VexL.Save)) && !fileHeaders.Contains(localizer.Get(Vex.VexL.MenuSaveAll))
            && fileHeaders.Contains(localizer.Get(Vex.VexL.MenuImport)),
            "File menu matches the prototype groups: save without Save All, import submenu present");
        Check(title.FindControl<MenuItem>("ThemeMenuItem") is null && title.FindControl<MenuItem>("TypographyMenuItem") is null,
            "Palette and typography entries stay in the appearance flyout, not the title menu");
        Check(file.Items.OfType<MenuItem>().Count(row => row.Items.Count > 0) >= 4,
            "File menu exposes recent/export/import/reopen submenus");
        foreach (var item in ((Menu)title.Content!).Items.OfType<MenuItem>())
        {
            item.IsSubMenuOpen = true; Render();
            var popup = item.GetVisualDescendants().OfType<Popup>().First();
            var panel = popup.Child!;
            Console.WriteLine($"MENU PANEL {item.Header}: {panel.GetType().Name} {panel.Bounds} scroll={(panel is ScrollViewer ? 1 : 0) + panel.GetVisualDescendants().OfType<ScrollViewer>().Count()}");
            Check(panel.Bounds.Width == 248, "Menu panel is 248px: " + item.Header);
            // 原型 .menu-flyout > .flyout{max-height:calc(100vh - 76px);overflow-y:auto}：文件菜单并入工具条目后必须可滚动，不能裁掉尾部条目。
            Check(panel.Bounds.Height <= 820 - 56 + 1 && (panel is ScrollViewer || panel.GetVisualDescendants().OfType<ScrollViewer>().Any()),
                "Menu panel caps at the window height and scrolls: " + item.Header + " " + panel.Bounds.Height);
            Check(item.Items.OfType<MenuItem>().All(row => row.Bounds.Height >= 32), "Menu rows retain the prototype's 32px target: " + item.Header);
            foreach (var row in item.Items.OfType<MenuItem>())
            {
                var header = row.GetVisualDescendants().OfType<ContentPresenter>().First(c => c.Name == "PART_HeaderPresenter");
                var headerX = header.TranslatePoint(default, panel)!.Value.X;
                Check(Math.Abs(headerX - 41) < 1, $"Every menu row reserves the state column (x={headerX:0.##}): " + row.Header);
            }
            var name = item.Name ?? item.Header?.ToString() ?? "menu";
            using var image = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
            image.Render(panel); image.Save(Path.Combine(output, name + ".png"));
            File.WriteAllLines(Path.Combine(output, name + ".txt"), panel.GetVisualDescendants().Prepend(panel).OfType<Control>()
                .Select(c => $"{c.GetType().Name} {c.Name} {c.Bounds} {(c is TextBlock t ? t.Text : "")}"));
            Console.WriteLine($"CAPTURE {name}: {panel.Bounds}");
            item.IsSubMenuOpen = false; Render();
        }
        // 排版级联已迁到外观弹层（ShellAppearanceMenuView），滚动可达性由 prototype-appearance 模式覆盖。
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Appearance.SelectPaletteByKey("indigo"); Render();
        var appearance = window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")!;
        foreach (var (button, name, width) in new[] { (appearance.AppearanceButton, "appearance", (double)ShellAppearanceMenuView.MenuWidth), (appearance.LanguageButton, "language", 248.0) })
        {
            ((Flyout)button.Flyout!).ShowAt(button); Render();
            var content = (Control)((Flyout)button.Flyout).Content!;
            var presenter = content.GetVisualAncestors().OfType<FlyoutPresenter>().First();
            var frame = presenter.GetVisualDescendants().OfType<Border>().First(b => b.Name == "LayoutRoot");
            Check(Math.Abs(frame.Bounds.Width - width) < 1, $"Menu uses its {width:0}px visible frame: " + name);
            Capture(presenter, name); button.Flyout.Hide(); Render();
        }
        vm.Workbench.ShowTypography = true; Render();
        var gallery = window.FindControl<ShellTypographyGallery>("TypographyGallery")!;
        var cardScroll = gallery.GetVisualDescendants().OfType<ScrollViewer>().First();
        // 原型 #typography-panel：渲染密度胶囊在顶部（卡片滚动区之上），页脚复选框已随原型移除。
        var densityPill = gallery.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("density-pill"));
        Check(densityPill.TranslatePoint(default, gallery)!.Value.Y < cardScroll.TranslatePoint(default, gallery)!.Value.Y, "Render density pills sit above the scrolling cards");
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
        foreach (var toggle in preferences.GetVisualDescendants().OfType<ToggleButton>().Where(t => t.Classes.Contains("pref-switch")))
        {
            var track = toggle.GetVisualDescendants().OfType<Border>().First(b => b.Name == "SwitchTrack");
            var knob = toggle.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().First(b => b.Name == "SwitchKnob");
            var point = knob.TranslatePoint(default, track)!.Value;
            Check(point.X >= 0 && point.Y >= 0 && point.X + knob.Bounds.Width <= track.Bounds.Width && point.Y + knob.Bounds.Height <= track.Bounds.Height, $"Preference switch knob remains inside its track: {toggle.Name} ({point}, {knob.Bounds.Size}, {track.Bounds.Size})");
        }
        Capture(preferenceCard, "preferences");
        ((ShellPreferencesViewModel)preferences.DataContext!).Close(); Render();
        // 原型 .vex-pref-segmented：外观页的界面动效/界面圆角两行都是 196 等宽三段分段控件。
        overlay.ShowPreferencesAsync(app.Resolve<ShellPreferencesViewModel>(), "appearance"); Render();
        var appearancePage = window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Single();
        var appearanceCard = appearancePage.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("dialog-card"));
        foreach (var track in appearancePage.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("pref-segmented")))
        {
            var segments = ((SegmentedPanel)track.Child!).Children.OfType<Button>().ToArray();
            Check(track.Bounds.Width == 196 && segments.Length == 3 && segments.Count(s => s.Bounds.Width > 0) == 3
                  && segments.All(s => Math.Abs(s.Bounds.Width - segments[0].Bounds.Width) < .5),
                $"Preference segmented track is 196px with three equal segments: {track.Bounds.Width} / {string.Join(",", segments.Select(s => s.Bounds.Width))}");
            Check(segments.Count(s => SegmentedPanel.GetIsActive(s)) == 1, "Exactly one preference segment is selected");
        }
        Capture(appearanceCard, "preferences-appearance");
        ((ShellPreferencesViewModel)appearancePage.DataContext!).Close(); Render();
        var aboutTask = overlay.ShowAboutAsync(); Render();
        var about = window.GetVisualDescendants().OfType<ShellAboutOverlayView>().Single();
        var aboutCard = about.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("dialog-card"));
        Check(aboutCard.Bounds.Width == 480 && ((Grid)aboutCard.Child!).Children[0].Bounds.Height == 36, "About uses the unified 480px dialog frame and 36px title bar");
        Capture(aboutCard, "about");
        ((ShellAboutViewModel)about.DataContext!).Close(); Render();
        foreach (var (show, type, width, name) in new (Func<Task>, Type, double, string)[]
        {
            (() => overlay.ShowPropertiesAsync(vm.DocumentInfo), typeof(ShellPropertiesOverlayView), 480, "properties"),
            (() => overlay.ShowStatisticsAsync(vm.DocumentInfo), typeof(ShellStatisticsOverlayView), 480, "statistics"),
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
        Check(renameCard.Bounds.Width == 480, "Rename retains the unified 480px dialog frame");
        Check(renameView.FindControl<TextBox>("NameTextBox")!.Bounds.Height == 36, "Rename input keeps the 4pt-grid field height (control-h-lg 36)");
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
