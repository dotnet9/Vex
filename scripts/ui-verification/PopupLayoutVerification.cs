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
using Vex.Controls;
using Vex.Modules.Workspace.Views;

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
        static IEnumerable<MenuItem> MenuItems(System.Collections.IEnumerable items)
        {
            foreach (var item in items.OfType<MenuItem>())
            {
                yield return item;
                foreach (var child in MenuItems(item.Items)) yield return child;
                if (item.ItemsSource is System.Collections.IEnumerable source)
                    foreach (var child in MenuItems(source)) yield return child;
            }
        }
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
        var titleItems = MenuItems(((Menu)title.Content!).Items).ToArray();
        var topLevelItems = ((Menu)title.Content!).Items.OfType<MenuItem>().ToArray();
        Check(topLevelItems.Where(item => item.ToggleType == MenuItemToggleType.None).All(item => item.Icon is Viewbox { Width: 12, Height: 12 } host && host.Child is Avalonia.Controls.Shapes.Path { Width: 24, Height: 24 }),
            "顶级菜单入口使用 12px Lucide 图标");
        Check(titleItems.Where(item => item.ToggleType == MenuItemToggleType.None).All(item => item.Icon is Viewbox host && (host.Width == 12 || host.Width == 14) && (host.Height == 12 || host.Height == 14) && host.Child is Avalonia.Controls.Shapes.Path { Width: 24, Height: 24 }),
            "普通顶部菜单项使用固定 Viewbox Lucide 图标");
        Check(titleItems.Where(item => item.ToggleType != MenuItemToggleType.None).All(item => item.Icon is null),
            "勾选和单选菜单项不配置操作图标");
        Check(titleItems.All(item => Math.Abs(item.FontSize - 13) < 0.1),
            "顶部菜单及级联菜单统一使用 13px 字号");
        var help = title.FindControl<MenuItem>("HelpMenuItem")!;
        var helpIconKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [localizer.Get(Vex.VexL.OnboardingGuide)] = "book-open",
            [localizer.Get(Vex.VexL.Changelog)] = "history",
            [localizer.Get(Vex.VexL.Thanks)] = "heart",
            [localizer.Get(Vex.VexL.Website)] = "external",
            [localizer.Get(Vex.VexL.Feedback)] = "message-circle",
            [localizer.Get(Vex.VexL.AboutCheckUpdate)] = "refresh",
            [localizer.Get(Vex.VexL.About)] = "info",
        };
        foreach (var item in help.Items.OfType<MenuItem>())
        {
            if (!helpIconKeys.TryGetValue(HeaderText(item.Header), out var key)) continue;
            Check(MenuIconVerification.HasIcon(item, key),
                $"帮助菜单“{item.Header}”使用语义图标 {key}");
        }
        var paragraph = title.FindControl<MenuItem>("InsertMenuItem")!;
        var paragraphIconKeys = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [localizer.Get(Vex.VexL.UnorderedList)] = "list",
            [localizer.Get(Vex.VexL.OrderedList)] = "list-ordered",
            [localizer.Get(Vex.VexL.TaskList)] = "list-check",
            [localizer.Get(Vex.VexL.Quote)] = "quote",
        };
        foreach (var item in paragraph.Items.OfType<MenuItem>())
        {
            if (!paragraphIconKeys.TryGetValue(HeaderText(item.Header), out var key)) continue;
            Check(MenuIconVerification.HasIcon(item, key),
                $"段落菜单“{item.Header}”使用语义图标 {key}");
        }
        foreach (var item in ((Menu)title.Content!).Items.OfType<MenuItem>())
        {
            item.IsSubMenuOpen = true; Render();
            var popup = item.GetVisualDescendants().OfType<Popup>().First();
            var panel = popup.Child!;
            Check(panel.Bounds.Width == 248, "Menu panel is 248px: " + item.Header);
            Check(Math.Abs(item.FontSize - 13) < 0.1, "Title menu uses the unified 13px caption size: " + item.Header);
            Check(item.Items.OfType<MenuItem>().All(row => Math.Abs(row.FontSize - 13) < 0.1 && row.Bounds.Height >= 32),
                "Menu rows use 13px text and retain the 32px target: " + item.Header);
            foreach (var row in item.Items.OfType<MenuItem>())
            {
                var header = row.GetVisualDescendants().OfType<ContentPresenter>().First(c => c.Name == "PART_HeaderPresenter");
                var headerX = header.TranslatePoint(default, panel)!.Value.X;
                Check(Math.Abs(headerX - 35) < 1, $"Every menu row reserves the state column (x={headerX:0.##}): " + row.Header);
            }
            var name = item.Name ?? item.Header?.ToString() ?? "menu";
            using var image = new RenderTargetBitmap(new PixelSize((int)panel.Bounds.Width, (int)panel.Bounds.Height));
            image.Render(panel); image.Save(Path.Combine(output, name + ".png"));
            File.WriteAllLines(Path.Combine(output, name + ".txt"), panel.GetVisualDescendants().Prepend(panel).OfType<Control>()
                .Select(c => $"{c.GetType().Name} {c.Name} {c.Bounds} {(c is TextBlock t ? t.Text : "")}"));
            Console.WriteLine($"CAPTURE {name}: {panel.Bounds}");
            item.IsSubMenuOpen = false; Render();
        }
        var files = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        foreach (var menu in new[] { files.FindControl<TreeView>("FileTree")!.ContextMenu!, files.FindControl<ListBox>("FileList")!.ContextMenu! })
            Check(MenuItems(menu.Items).Where(item => item.ToggleType == MenuItemToggleType.None).All(item => item.Icon is Viewbox { Width: 14, Height: 14 } host && host.Child is Avalonia.Controls.Shapes.Path { Width: 24, Height: 24 }),
                "文件树和平铺列表右键菜单使用 Lucide 图标");
        var editor = window.FindControl<MarkdownEditorView>("EditorGuideTarget")!;
        var editorMenu = editor.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!.ContextMenu!;
        Check(MenuItems(editorMenu.Items).All(item => Math.Abs(item.FontSize - 13) < 0.1),
            "源码编辑器右键菜单统一使用 13px 字号");
        var instant = window.FindControl<MarkdownInstantView>("InstantGuideTarget")!;
        Check(MenuItems(instant.Editor.ContextMenu!.Items).Where(item => item.ToggleType == MenuItemToggleType.None).All(item => item.Icon is Viewbox { Width: 14, Height: 14 } host && host.Child is Avalonia.Controls.Shapes.Path { Width: 24, Height: 24 }),
            "即时编辑器动态右键菜单使用共享 Lucide 映射");
        var instantHeaders = MenuItems(instant.Editor.ContextMenu.Items).Select(item => HeaderText(item.Header)).ToArray();
        Check(instantHeaders.Length > 0 && instantHeaders.All(header => !header.StartsWith("Vex.VexL.", StringComparison.Ordinal)),
            "即时编辑器右键菜单及子菜单不显示本地化资源键");
        var elementActionKeys = new[]
        {
            Vex.VexL.InsertParagraphAbove, Vex.VexL.InsertParagraphBelow, Vex.VexL.ToggleTask,
            Vex.VexL.CopyBlock, Vex.VexL.DeleteBlock,
        };
        Check(elementActionKeys.All(key => localizer.Get(key) != key && instantHeaders.Contains(localizer.Get(key))),
            "插入段落、切换任务、复制块和删除块显示当前语言的菜单名称");
        var separateMenu = window.FindControl<MainWindow>("ShellWindow")!.LayoutTarget.ContextMenu!;
        Check(MenuItems(separateMenu.Items).All(item => item.Icon is Viewbox { Width: 14, Height: 14 } host && host.Child is Avalonia.Controls.Shapes.Path { Width: 24, Height: 24 }), "分离视图动态菜单使用 Lucide 图标");
        MenuIconVerification.Run(window, output);
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
        var densityPill = gallery.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("appearance-pill"));
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
