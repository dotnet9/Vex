using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Prism.Ioc;
using CodeWF.Avalonia.Controls.Themes;
using SkiaSharp;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Workspace.Services;
using Vex.Core.Messaging;

var mode = args.FirstOrDefault() ?? "vex";
Console.WriteLine("Markdown library " + typeof(CodeWF.Avalonia.Markdown.Controls.MarkdownViewer).Assembly.GetName().Version);
var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/ui-verification");
Directory.CreateDirectory(output);
if (mode == "parity")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = app.Shell!;
    var vm = (MainWindowViewModel)window.DataContext!;
    window.Show();
    SettleRendering();
    window.FindControl<Vex.Modules.Shell.Views.ShellWorkbenchToolbarView>("WorkbenchToolbar")!.FindControl<Button>("QuickStartButton")!.IsVisible = true;
    vm.Appearance.SelectPaletteByKey("aurora");
    var sample = Path.GetFullPath("artifacts/parity/documents/笔记/主题设计笔记.md");
    Pump(vm.OpenStartupDocumentAsync(new[] { Path.GetDirectoryName(Path.GetDirectoryName(sample))! }));
    Pump(vm.OpenPathAsync(sample));
    Check(vm.DocumentFiles.Count == 5, "Parity captures retain the complete five file workspace");
    foreach (var width in new[] { 1280, 980 })
    {
        window.Width = width; window.Height = width == 1280 ? 820 : 640;
        foreach (var theme in new[] { "light", "dark" })
        {
            vm.Appearance.SelectThemeByKey(theme);
            foreach (var view in new[] { "instant", "source", "read", "mindmap" })
            {
                vm.Workbench.ShowTypography = false; vm.Workbench.ShowMapSettings = false; vm.Workbench.SelectedNode = null;
                vm.Workbench.ShowMapSource = false;
                switch (view) { case "instant": vm.Workbench.ShowInstant(); break; case "source": vm.Workbench.ShowSource(); break; case "read": vm.Workbench.ShowRead(); break; default: vm.Workbench.ShowMindMap(); break; }
                Pump();
                PumpUntil(() => vm.Workbench.Root is not null);
                SettleRendering();
                var sourceView = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.FindControl<MarkdownEditorView>("MarkdownEditor")!;
                sourceView.Editor.CaretOffset = 0;
                sourceView.Editor.ScrollToHome();
                foreach (var scroll in window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
                foreach (var scroll in window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
                Pump();
                Capture(window, $"{width}-{theme}-{(view is "instant" or "source" ? "edit" : view)}-{(view == "source" ? "source" : "instant")}");
            }
        }
    }
    window.Width = 1280; window.Height = 820; vm.Appearance.SelectThemeByKey("light");
    vm.Workbench.ShowInstant(); vm.Workbench.ShowTypography = true; Pump(); SettleRendering(); Capture(window, "typography");
    vm.Workbench.ShowMindMap(); vm.Workbench.ShowMapSettings = true; Pump(); SettleRendering(); Capture(window, "map-settings");
    vm.Workbench.ShowMapSettings = false; vm.Workbench.SelectedNode = vm.Workbench.Root!.Children.First(); Pump(); SettleRendering(); Capture(window, "map-inspector");
    window.Close();
    Console.WriteLine("Native parity captures: " + output);
    return;
}
if (mode is "vex" or "preferences")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = app.Shell ?? throw new Exception("Prism shell was not initialized");
    var vm = (MainWindowViewModel)window.DataContext!;
    window.Show();
    if (mode == "preferences")
    {
        vm.Workbench.ShowSource(); Pump(); VerifyEditingPreferences(app, window, vm); VerifyLocalizedGuide(window, vm); window.Close(); return;
    }
    vm.Workbench.ShowSource();
    Pump();
    VerifyTitleLayout(window);
    VerifyAppearanceControls(window, vm);
    Check(Math.Abs(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width - 226) < 1,
        "Vex sidebar initially occupies 226 px");
    var titleMenu = window.FindControl<Vex.Modules.Shell.Views.ShellTitleMenuView>("TitleMenuView")!;
    var viewMenu = titleMenu.FindControl<MenuItem>("ViewMenuItem")!;
    viewMenu.IsSubMenuOpen = true;
    Pump();
    var sidebarMenu = titleMenu.FindControl<MenuItem>("SidebarMenuItem")!;
    Check(sidebarMenu.IsChecked, "Vex Sidebar menu is initially checked");
    Check(sidebarMenu.Header?.ToString() == app.Resolve<IAppLocalizer>().Get(Vex.VexL.ToggleSidebar),
        "Vex Sidebar menu label is localized");
    viewMenu.IsSubMenuOpen = false;
    viewMenu.IsSelected = false;
    Pump();
    // 每轮复制到独立输出目录，保存路径回归不会污染测试输入或用户文档。
    var sampleFolder = Path.Combine(output, "documents");
    Directory.CreateDirectory(sampleFolder);
    foreach (var sample in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "MarkdownSamples"), "*.md"))
        File.Copy(sample, Path.Combine(sampleFolder, Path.GetFileName(sample)), overwrite: true);
    var files = Directory.GetFiles(sampleFolder, "*.md").Order().Take(5).ToArray();
    Check(files.Length == 5, "Vex regression uses five real sample files");
    foreach (var file in files)
    {
        vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
            new DocumentFile(file, Path.GetFileName(file), sampleFolder, string.Empty, string.Empty), null));
        PumpUntil(() => vm.DocumentInfo.CurrentFilePath == file);
        Check(!vm.DocumentInfo.IsModified, "Opening an untouched file stays clean: " + Path.GetFileName(file));
    }
    var editorVm = app.Resolve<MarkdownEditorViewModel>();
    VerifySourceLineHeight(window, window.GetVisualDescendants().OfType<MarkdownEditorView>().Single(), 21.875, "Vex");
    editorVm.InsertText(" regression edit");
    Pump();
    Check(vm.DocumentInfo.IsModified, "Typing marks Vex document dirty");
    vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
        new DocumentFile(files[0], Path.GetFileName(files[0]), sampleFolder, string.Empty, string.Empty), null));
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Any());
    var dialog = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Single();
    Check(vm.DocumentInfo.CurrentFilePath == files[^1], "Dirty-file switch waits for save confirmation");
    Check(!titleMenu.IsEffectivelyEnabled, "Modal confirmation disables title menu commands");
    Capture(window, "vex-unsaved-confirmation");
    ((ShellUnsavedDialogViewModel)dialog.DataContext!).Cancel();
    Pump();
    Check(vm.DocumentInfo.IsModified, "Cancel keeps the edited document dirty");
    Check(vm.DocumentInfo.CurrentFilePath == files[^1], "Cancel keeps the current document selected");
    Check(titleMenu.IsEffectivelyEnabled, "Closing the modal confirmation re-enables title menu commands");
    var unchanged = File.ReadAllText(files[^1]);
    vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
        new DocumentFile(files[0], Path.GetFileName(files[0]), sampleFolder, string.Empty, string.Empty), null));
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Any());
    ((ShellUnsavedDialogViewModel)window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>()
        .Single().DataContext!).Discard();
    PumpUntil(() => vm.DocumentInfo.CurrentFilePath == files[0]);
    Check(!vm.DocumentInfo.IsModified && File.ReadAllText(files[^1]) == unchanged,
        "Discard opens the requested document without saving the previous edits");
    editorVm.InsertText(" saved regression edit");
    Pump();
    var expectedSaved = window.GetVisualDescendants().OfType<MarkdownEditorView>().Single().Text;
    vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
        new DocumentFile(files[1], Path.GetFileName(files[1]), sampleFolder, string.Empty, string.Empty), null));
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Any());
    ((ShellUnsavedDialogViewModel)window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>()
        .Single().DataContext!).Save();
    PumpUntil(() => vm.DocumentInfo.CurrentFilePath == files[1]);
    Check(!vm.DocumentInfo.IsModified && File.ReadAllText(files[0]) == expectedSaved,
        "Save writes the current document before switching to the requested document");
    File.Copy(Path.Combine(AppContext.BaseDirectory, "MarkdownSamples", Path.GetFileName(files[0])), files[0], overwrite: true);
    Pump(vm.OpenPathAsync(files[0]));
    var drafts = (MemoryDrafts)app.Resolve<IAutoSaveDraftService>();
    drafts.RestoredMarkdown = "# recovered draft";
    Pump(vm.OpenPathAsync(files[1]));
    Check(vm.DocumentInfo.IsModified, "A recovered draft different from disk remains unsaved");
    drafts.RestoredMarkdown = null;
    Pump(vm.OpenPathAsync(files[0]));
    var aboutTask = app.Resolve<IShellOverlayService>().ShowAboutAsync();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Any());
    var about = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Single();
    Check(about.IsEffectivelyVisible && about.Bounds.Width > 0 && !titleMenu.IsEffectivelyEnabled
        && !window.FindControl<StackPanel>("TitleBarAppearance")!.IsEffectivelyEnabled,
        "About opens inside the real main window modal host");
    var aboutModel = (ShellAboutViewModel)about.DataContext!;
    Check(aboutModel.VersionText == CodeWF.Toolkit.Core.UpdateChecking.UpdateVersion.Display(typeof(Vex.App).Assembly),
        "About displays the actual application assembly version");
    Check(DateTime.TryParse(aboutModel.CompileTimeText, out _) && aboutModel.PlatformText.Contains("Avalonia"),
        "About metadata remains available in NativeAOT");
    foreach (var culture in new[] { "zh-CN", "en-US", "zh-Hant", "ja-JP" })
    {
        vm.Appearance.SelectLanguageByCulture(culture); Pump();
        Check(CodeWF.Toolkit.Lang.I18nManager.Instance.Culture.Name == culture && aboutModel.ActionText == app.Resolve<IAppLocalizer>().Get(Vex.VexL.AboutCheckUpdate),
            "The open About dialog changes language: " + culture);
        Capture(window, "vex-about-" + culture);
    }
    vm.Appearance.SelectLanguageByCulture("zh-CN"); Pump();
    Capture(window, "vex-about");
    aboutModel.Close();
    Pump(aboutTask);
    VerifyFileTreeLayout(window, app.Resolve<ShellFilesViewModel>(), sampleFolder);
    Check(vm.Appearance.PaletteOptions.Count == 8 && vm.Appearance.ThemeOptions.Count == 3,
        "Vex exposes eight palettes and three independent theme modes");
    foreach (var palette in vm.Appearance.PaletteOptions)
    foreach (var modeOption in vm.Appearance.ThemeOptions.Where(option => option.Mode != StudioThemeMode.System))
    {
        vm.Appearance.SelectedPalette = palette;
        vm.Appearance.SelectedTheme = modeOption;
        var theme = Application.Current!.ActualThemeVariant;
        Check(theme == StudioTheme.GetVariant(palette.Palette, modeOption.Mode == StudioThemeMode.Dark),
            "Vex selected mode and palette reach the shared theme controller: " + theme);
        Check(app.Resolve<IAppSettingsStore>().Current is { } settings
            && settings.ThemeKey == modeOption.Key && settings.PaletteKey == palette.Key,
            "Vex stores palette and mode independently: " + theme);
        Pump();
        SettleRendering();
        Application.Current.TryGetResource("VexAccentBrush", theme, out var appAccent);
        Application.Current.TryGetResource("StudioAccent", theme, out var sharedAccent);
        Check(SameBrush(appAccent as IBrush, sharedAccent as IBrush),
            "Vex brushes follow the shared palette tokens: " + theme);
        Capture(window, "vex-" + theme);
        VerifyVexDetails(window, theme);
        VerifyFileColors(window, theme);
        var preview = window.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
            .Single(viewer => viewer.Name == "PreviewMarkdownViewer");
        var previewScroll = preview.GetVisualAncestors().OfType<ScrollViewer>().First();
        previewScroll.Offset = new Vector(0, (previewScroll.Extent.Height - previewScroll.Viewport.Height) / 2);
        Pump();
        Capture(window, "vex-" + theme + "-middle");
        previewScroll.Offset = default;
        Pump();
    }
    VerifyWorkbench(app, window, vm);
    VerifyPrototypeInteractions(app, window, vm);
    VerifyEditingPreferences(app, window, vm);
    VerifyLocalizedGuide(window, vm);
    vm.Appearance.SelectThemeByKey("system");
    Check(vm.Appearance.SelectedTheme?.Mode == StudioThemeMode.System,
        "Vex can return to system mode after explicit light and dark selections");
    vm.Layout.IsSidebarVisible = false;
    Pump();
    Check(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width == 0, "Vex sidebar collapses");
    Check(!sidebarMenu.IsChecked && app.Resolve<IAppSettingsStore>().Current.IsSidebarVisible == false,
        "Sidebar check and persisted state follow collapse");
    vm.Layout.IsSidebarVisible = true;
    Pump();
    Check(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width == 226, "Vex sidebar reopens");
    Check(sidebarMenu.IsChecked && app.Resolve<IAppSettingsStore>().Current.IsSidebarVisible == true,
        "Sidebar check and persisted state follow reopening");
    window.Width = 980;
    window.Height = 640;
    Pump();
    VerifyTitleLayout(window);
    Capture(window, "vex-980");
    vm.Workbench.ShowInstant(); Pump();
    var editableTable = window.GetVisualDescendants().OfType<Grid>().FirstOrDefault(grid => grid.GetType().Name == "MarkdownTableGrid" && grid.ColumnDefinitions.Count == 2);
    Check(editableTable is not null && editableTable.ColumnDefinitions[0].ActualWidth < editableTable.ColumnDefinitions[1].ActualWidth / 2,
        "Instant editing allocates natural table widths for short API names and long descriptions");
    Check(editableTable?.Parent is Border tableOutline &&
        Math.Abs(tableOutline.Bounds.Width - editableTable.ColumnDefinitions.Sum(column => column.ActualWidth) - 2) <= 2,
        "Instant table outline fits the actual columns without a blank trailing area");
    Capture(window, "vex-native-instant-980");
    vm.Workbench.ShowRead(); Pump(); Capture(window, "vex-native-read-980");
    vm.Workbench.ShowMindMap(); vm.Workbench.ShowMapSource = false; vm.Workbench.ShowMapSettings = false;
    Pump(); Capture(window, "vex-native-mindmap-980");
    ((Vex.Modules.Shell.Views.MainWindow)window).BeginOnboardingGuide(); Pump();
    var narrowGuide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    for (var step = 0; step < 5; step++)
    {
        narrowGuide.ShowStep(step); Pump();
        var card = narrowGuide.Children.OfType<Border>().Single();
        var origin = card.TranslatePoint(default, window)!.Value;
        Check(origin.X >= 0 && origin.Y >= 0 && origin.X + card.Bounds.Width <= window.Bounds.Width + 1
            && origin.Y + card.Bounds.Height <= window.Bounds.Height + 1,
            "Guide card stays inside a 980 x 640 window: step " + (step + 1));
        Capture(window, "vex-native-guide-980-" + (step + 1));
    }
    narrowGuide.Finish(); Pump();
    window.Close();
}
else throw new ArgumentException("Expected vex");
Console.WriteLine("UI verification passed; images: " + output);

void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine("PASS " + message);
}

void Pump(Task? pending = null)
{
    var deadline = DateTime.UtcNow.AddSeconds(20);
    do
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        if (pending is null || pending.IsCompleted) break;
        if (DateTime.UtcNow > deadline) throw new TimeoutException("UI operation timed out");
        Thread.Sleep(10);
    } while (true);
    pending?.GetAwaiter().GetResult();
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
}

void Capture(Window window, string name)
{
    Pump();
    using var frame = window.CaptureRenderedFrame() ?? throw new Exception("No rendered frame");
    frame.Save(Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    if (name is "vex-Light" or "vex-Dark")
    {
        using var bitmap = SKBitmap.Decode(Path.Combine(output, name + ".png"));
        using var crop = new SKBitmap(bitmap.Width, Math.Min(120, bitmap.Height));
        bitmap.ExtractSubset(crop, new SKRectI(0, 0, crop.Width, crop.Height));
        using var data = crop.Encode(SKEncodedImageFormat.Png, 100);
        using var file = File.Create(Path.Combine(output, name + "-header.png"));
        data.SaveTo(file);
    }
}

void SettleRendering()
{
    // 后台解析与能力渲染需要跨多个 dispatcher tick；不操作真实桌面。
    var deadline = DateTime.UtcNow.AddMilliseconds(400);
    while (DateTime.UtcNow < deadline)
    {
        Pump();
        Thread.Sleep(10);
    }
}

void PumpUntil(Func<bool> completed)
{
    var deadline = DateTime.UtcNow.AddSeconds(15);
    while (!completed())
    {
        Pump();
        if (DateTime.UtcNow > deadline) throw new TimeoutException("Expected UI state did not arrive");
        Thread.Sleep(10);
    }
    Pump();
}

void VerifyFileColors(Window window, ThemeVariant theme)
{
    var titles = window.GetLogicalDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("file-title") && t.IsEffectivelyVisible).ToArray();
    Check(titles.Length > 0, "Vex file cards rendered under " + theme);
    Check(titles.All(t =>
    {
        var card = t.GetLogicalAncestors().OfType<Border>().First(b => b.Classes.Contains("file-card"));
        var background = (card.Background as ISolidColorBrush)?.Color ?? Colors.Transparent;
        if (background.A == 0)
        {
            Application.Current!.TryGetResource("VexShellBackgroundBrush", theme, out var resource);
            background = ((ISolidColorBrush)resource!).Color;
        }
        if (card.Classes.Contains("selected"))
        {
            Application.Current!.TryGetResource("VexAccentBrush", theme, out var accent);
            Application.Current!.TryGetResource("VexAccentSoftBrush", theme, out var selectedBackground);
            return t.Foreground is ISolidColorBrush selectedForeground
                && selectedForeground.Color == ((ISolidColorBrush)accent!).Color
                && background == ((ISolidColorBrush)selectedBackground!).Color;
        }
        return t.Foreground is ISolidColorBrush foreground && Contrast(foreground.Color, background) >= 4.5;
    }), "Vex ordinary file-title contrast >= 4.5; selected card follows prototype accent colors under " + theme);
    foreach (var title in titles.Take(2)) Console.WriteLine($"COLOR {theme} {title.Foreground}");
}

void VerifyFileTreeLayout(Window window, ShellFilesViewModel vm, string sampleFolder)
{
    // 仅构造文件列表快照，不创建或打开这些布局验证路径。
    var documents = new[]
    {
        new DocumentFile(Path.Combine(sampleFolder, "Regression", "01-basics.md"), "01-basics.md", "Regression", "16:02", "Summary remains in the tooltip"),
        new DocumentFile(Path.Combine(sampleFolder, "Regression", "02-a-very-long-document-name-that-must-trim-inside-the-sidebar.md"), "02-a-very-long-document-name-that-must-trim-inside-the-sidebar.md", "Regression", "Yesterday", "Long summary does not expand the row"),
        new DocumentFile(Path.Combine(sampleFolder, "Regression", "Nested", "deep.md"), "deep.md", "Nested", "Monday", "Nested document"),
        new DocumentFile(Path.Combine(sampleFolder, "root.md"), "root.md", "MarkdownSamples", "09-28", "Root document")
    };
    vm.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, documents[0], sampleFolder));
    Pump();
    var tree = window.GetVisualDescendants().OfType<TreeView>().Single(t => t.Classes.Contains("side-tree"));
    var items = tree.GetVisualDescendants().OfType<TreeViewItem>().Where(i => i.IsEffectivelyVisible).ToArray();
    Check(items.Length == 6, "Vex tree renders root, sibling and nested documents");
    foreach (var item in items)
    {
        var card = item.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("file-card"));
        var chevron = item.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
            .First(b => b.Name == "PART_ExpandCollapseChevron");
        var point = card.TranslatePoint(default, tree)!.Value;
        Check(card.Bounds.Height is >= 31 and <= 38 && point.X + card.Bounds.Width <= tree.Bounds.Width + 1,
            "Vex tree has compact rows within sidebar bounds: " + ((DocumentFileNode)item.DataContext!).Name);
        Check(Math.Abs(chevron.TranslatePoint(default, tree)!.Value.Y + chevron.Bounds.Height / 2
            - point.Y - card.Bounds.Height / 2) < 1,
            "Vex tree chevron aligns with its own row");
        var icon = card.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First(p => p.IsEffectivelyVisible);
        var label = card.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible &&
            (t.Classes.Contains("file-title") || t.Classes.Contains("file-heading")));
        Check(Math.Abs(icon.TranslatePoint(default, card)!.Value.Y + icon.Bounds.Height / 2
            - label.TranslatePoint(default, card)!.Value.Y - label.Bounds.Height / 2) < 1,
            "Vex tree icon and title align vertically");
    }
    var cardPositions = items.Select(item => new
    {
        item.Level,
        X = item.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("file-card"))
            .TranslatePoint(default, tree)!.Value.X
    }).ToArray();
    Check(cardPositions.GroupBy(p => p.Level).All(g => g.Max(p => p.X) - g.Min(p => p.X) < 1)
        && cardPositions.All(p => Math.Abs(p.X - cardPositions.Min(x => x.X) - p.Level * 16) < 1),
        "Vex tree siblings align and each level indents by 16 px");
    Capture(window, "vex-file-tree");
    var folder = items.First(i => i.Level == 0 && ((DocumentFileNode)i.DataContext!).IsFolder);
    var toggle = folder.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
        .First(b => b.Name == "PART_ExpandCollapseChevron");
    var click = toggle.TranslatePoint(new Point(8, 12), window)!.Value;
    window.MouseDown(click, MouseButton.Left);
    window.MouseUp(click, MouseButton.Left);
    Pump();
    Check(!folder.IsExpanded, "Vex folder chevron collapses its subtree");
    window.MouseDown(click, MouseButton.Left);
    window.MouseUp(click, MouseButton.Left);
    Pump();
    Check(folder.IsExpanded, "Vex folder chevron expands its subtree");
    vm.FilterText = "deep";
    Pump();
    Check(tree.GetVisualDescendants().OfType<TextBlock>().Count(t => t.IsEffectivelyVisible && t.Classes.Contains("file-title")) == 1,
        "Vex file filter retains the matching document and its ancestors");
    vm.FilterText = string.Empty;
    Pump();
}

void VerifyWorkbench(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    var original = vm.Markdown;
    vm.Workbench.ShowInstant(); Pump();
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
    Check(instant.IsEffectivelyVisible && instant.Bounds.Width > 500 && instant.Editor.Text == original, "Instant editor has a real viewport and shares the current document");
    instant.Editor.FocusBlock(0); instant.Editor.InsertText(" 实时编辑验证"); Pump();
    Check(vm.Markdown.Contains("实时编辑验证"), "Instant edits update the real Markdown document");
    var changed = vm.Markdown;
    vm.Workbench.ShowRead(); Pump();
    app.Resolve<MarkdownEditorViewModel>().Execute(EditorActionKind.Bold); Pump();
    Check(vm.Markdown == changed, "Read view does not route format actions to hidden editors");
    vm.Workbench.ShowMindMap(); Pump();
    PumpUntil(() => vm.Workbench.Root?.FullText.Contains("实时编辑验证") == true);
    var map = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!;
    Check(map.IsEffectivelyVisible && map.Bounds.Width > 500 && vm.Workbench.Root is not null, "Mind map has a real viewport and parses the shared Markdown");
    var node = vm.Workbench.Root!.Children.First();
    vm.Workbench.SelectedNode = node;
    map.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!.ToggleCollapse(node); Pump();
    Check(node.IsCollapsed && vm.Markdown == changed, "Folding never modifies the source");
    vm.Workbench.ShowMapSource = true; Pump();
    Check(window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.IsEffectivelyVisible, "Map can display source comparison");
    vm.Workbench.LocateSource(node); Pump();
    Check(vm.Workbench.IsSourceEditing && vm.Markdown == changed, "Mind map locates source without changing content");
    vm.Workbench.ShowMindMap(); vm.Workbench.ShowMapSettings = true; Pump();
    var previousRoot = vm.Workbench.Root;
    vm.Workbench.SummaryLength = 12; Pump();
    PumpUntil(() => !ReferenceEquals(vm.Workbench.Root, previousRoot));
    Capture(window, "vex-native-mindmap");
    vm.Workbench.ShowInstant(); Pump(); Capture(window, "vex-native-instant");
    vm.Workbench.ShowTypography = true; Pump();
    var typographyGallery = window.FindControl<Border>("TypographyGallery")!;
    Check(typographyGallery.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == app.Resolve<IAppLocalizer>().Get(Vex.VexL.TypographySimple)),
        "Typography cards use the same localized names as the menu");
    vm.Workbench.ShowTypography = false;
    var preferences = app.Resolve<IAppSettingsStore>().Current;
    var sourceDocument = window.GetVisualDescendants().OfType<MarkdownEditorView>().Single().Editor.Document;
    var mapControl = map.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
    mapControl.SetZoom(.85);
    ((Vex.Modules.Shell.Views.MainWindow)window).BeginOnboardingGuide(); Pump();
    var guide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    for (var i = 0; i < 5; i++)
    {
        Check(guide.IsVisible && guide.StepIndex == i, "Interactive guide step " + (i + 1));
        if (i == 3) PumpUntil(() => vm.Workbench.Root?.Title == "从文字到结构");
        Capture(window, "vex-native-guide-" + (i + 1));
        guide.NextStep(); Pump();
    }
    Check(!guide.IsVisible && vm.Markdown == changed, "Guide restores the original unsaved Markdown");
    Check(vm.Workbench.SummaryLength == 12 && vm.Workbench.IsInstantEditing, "Guide restores map and editor preferences");
    PumpUntil(() => vm.Workbench.Root?.FullText.Contains("实时编辑验证") == true);
    Check(Math.Abs(mapControl.Zoom - .85) < .001, "Guide restores map zoom after asynchronous document parsing");
    Check(ReferenceEquals(sourceDocument, window.GetVisualDescendants().OfType<MarkdownEditorView>().Single().Editor.Document), "Guide preserves the source document and undo stack");
    app.Resolve<MarkdownEditorViewModel>().Execute(EditorActionKind.Undo); Pump();
    Check(vm.Markdown == original, "Guide preserves instant editor undo history");
    app.Resolve<MarkdownEditorViewModel>().Execute(EditorActionKind.Redo); Pump();
    Check(vm.Markdown == changed, "Instant editor redo restores the pre-guide changes");
    ((Vex.Modules.Shell.Views.MainWindow)window).BeginOnboardingGuide(); Pump(); guide.ShowStep(3); Pump(); guide.Finish(); Pump();
    Check(vm.Markdown == changed && vm.Workbench.IsInstantEditing, "Skipping the restarted guide restores state");
    vm.Workbench.ShowSource(); Pump();
}

void VerifyPrototypeInteractions(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    var document = vm.Markdown;
    var source = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!;
    var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
    var host = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!;
    var map = host.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
    var wb = vm.Workbench;
    wb.ShowInstant(); Pump();
    Check(!source.IsEffectivelyVisible && !preview.IsEffectivelyVisible && instant.IsEffectivelyVisible, "Instant mode hides the source and read views");
    Check(instant.Editor.DocumentMaxWidth == 810 && instant.Editor.EditorFontSize == 13.5, "Instant document uses the prototype reading width and base font");
    Check(window.FindControl<Vex.Modules.Shell.Views.ShellWorkbenchToolbarView>("WorkbenchToolbar")!.Bounds.Height == 62
        && window.FindControl<Vex.Modules.Shell.Views.ShellEditorOptionsView>("EditorOptions")!.Bounds.Height == 42,
        "Workbench and editing context rows are 62 and 42 px");
    wb.ShowSource(); wb.SourcePreviewEnabled = false; Pump();
    Check(source.IsEffectivelyVisible && !preview.IsEffectivelyVisible && source.Bounds.Width > 1000, "Source preview can be disabled for full width editing");
    Check(app.Resolve<IAppSettingsStore>().Current.SourcePreviewEnabled == false, "Source preview preference is persisted separately from the active view");
    wb.ShowRead(); Pump();
    Check(preview.IsEffectivelyVisible && preview.IsReadingMode && !source.IsEffectivelyVisible && !instant.IsEffectivelyVisible, "Read mode has one read only document and no duplicate preview header");
    app.Resolve<MarkdownEditorViewModel>().ApplySearch(new EditorSearchCommand(EditorSearchAction.Count, "Markdown")); Pump();
    Check(wb.IsReading && vm.Markdown == document, "Counting search matches keeps the reading view and document unchanged");
    wb.SourcePreviewEnabled = true;
    wb.ShowInstant(); wb.ShowTypography = true; Pump();
    var gallery = window.FindControl<Border>("TypographyGallery")!;
    var cards = gallery.GetVisualDescendants().OfType<Border>().Where(b => b.Width == 90 && b.Height == 58).ToArray();
    Check(gallery.Bounds.Width == 338 && cards.Length == 18, "Typography gallery contains all 18 themes in a 338 px panel");
    Check(cards.GroupBy(c => Math.Round(c.TranslatePoint(default, gallery)!.Value.Y)).First().Count() == 3, "Typography cards form three columns");
    var typography = vm.Appearance.SelectedTypography;
    foreach (var option in vm.Appearance.TypographyOptions)
    {
        vm.Appearance.SelectedTypography = option; Pump();
        Check(instant.Editor.TypographyTheme == option.Key && app.Resolve<MarkdownPreviewViewModel>().TypographyTheme == option.Key && vm.Markdown == document,
            "Typography stays consistent across instant and reading views: " + option.Key);
    }
    vm.Appearance.IsCompactLayout = true; Pump();
    Check(instant.Editor.TypographySize == "Small" && vm.Markdown == document, "Compact typography applies without rewriting Markdown");
    vm.Appearance.IsCompactLayout = false; vm.Appearance.SelectedTypography = typography; wb.ShowTypography = false;
    wb.ShowMapSource = false; wb.ShowMindMap(); Pump(); SettleRendering();
    var oldRoot = wb.Root;
    wb.ShowParagraphs = wb.ShowLists = wb.ShowCode = wb.ShowTables = false;
    PumpUntil(() => !ReferenceEquals(oldRoot, wb.Root));
    Check(Flatten(wb.Root!).All(n => n.Kind is CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Heading or CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Document), "Turning off summaries leaves the heading structure intact");
    wb.SummaryLength = 999; Check(wb.SummaryLength == 120, "Summary length is capped at 120 graphemes");
    wb.SummaryLength = -10; Check(wb.SummaryLength == 1, "Summary length is at least one grapheme");
    oldRoot = wb.Root; wb.ResetMapSettings();
    PumpUntil(() => !ReferenceEquals(oldRoot, wb.Root)); Pump();
    Check(wb.SummaryLength == 30 && wb.ShowParagraphs && wb.ShowLists && !wb.ShowCode && !wb.ShowTables, "Reset restores every map display default");
    Check(wb.VisibleNodeCount == map.VisibleNodeCount && wb.SummaryPreview.Length > 0, "Map count and summary preview update from the real tree");
    map.CollapseAll(); Pump();
    var heading = Flatten(wb.Root!).LastOrDefault(n => n.Kind == CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Heading) ?? wb.Root!;
    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(heading.SourceLine)); Pump();
    Check(wb.IsMindMap && map.SelectedNode?.Id == heading.Id && wb.SelectedNode?.Id == heading.Id, "Outline navigation reveals and selects the corresponding map node");
    wb.CloseInspector(); Pump(); Check(map.SelectedNode is null && !host.FindControl<Border>("NodeInspector")!.IsEffectivelyVisible, "Closing full text also clears canvas selection");
    map.SetZoom(.01); Check(Math.Abs(map.Zoom - .15) < .001, "Map zoom respects the lower bound");
    map.SetZoom(50); Check(Math.Abs(map.Zoom - 2.5) < .001, "Map zoom respects the upper bound"); map.Fit();
    wb.ShowMapSource = true; Pump();
    Check(source.FindControl<MarkdownEditorView>("MarkdownEditor")!.Editor.IsReadOnly, "Source comparison remains read only in map mode");
    wb.ShowMapSource = false;
    wb.ShowInstant();
    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(heading.SourceLine)); Pump();
    Check(wb.IsInstantEditing && !source.IsEffectivelyVisible && vm.Markdown == document, "Outline navigation in instant mode scrolls without activating hidden source");
    var statsTask = app.Resolve<ShellStatusBarViewModel>().ShowStatisticsAsync();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellStatisticsOverlayView>().Any());
    var stats = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellStatisticsOverlayView>().Single();
    Check(stats.IsEffectivelyVisible, "Status bar character count opens real document statistics");
    ShellOverlayHost.Dismiss(stats); Pump(statsTask);
    var shell = (Vex.Modules.Shell.Views.MainWindow)window;
    wb.SourcePreviewEnabled = false;
    shell.BeginOnboardingGuide(); Pump();
    var guide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    guide.ShowStep(1); Pump();
    Check(guide.SpotlightTargets.Count >= 2, "Instant guide highlights editing options and editable content together");
    var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = source.FindControl<MarkdownEditorView>("MarkdownEditor")!.Editor.TextArea, Key = Key.Right };
    Check(!guide.HandleKey(key) && guide.StepIndex == 1, "Guide leaves editor arrow keys available for typing");
    vm.Appearance.SelectTypographyByKey("OrangeHeart"); wb.SourcePreviewEnabled = true; guide.ShowStep(3); Pump();
    Check(guide.SpotlightTargets.Count >= 2, "Map guide highlights the settings and canvas together");
    guide.Finish(); Pump();
    Check(!wb.SourcePreviewEnabled && vm.Appearance.SelectedTypography == typography && vm.Markdown == document, "Guide restores preview, typography and original document after real interactions");
    wb.SourcePreviewEnabled = true;
    window.Width = 980; window.Height = 640; Pump();
    Check(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width == 200, "Narrow windows use the prototype 200 px default sidebar");
    foreach (var mode in new[] { "instant", "read", "mindmap" })
    {
        if (mode == "instant") wb.ShowInstant(); else if (mode == "read") wb.ShowRead(); else wb.ShowMindMap(); Pump();
        wb.ShowTypography = mode != "mindmap"; wb.ShowMapSettings = mode == "mindmap"; Pump();
        var panel = mode == "mindmap" ? host.FindControl<Border>("SettingsTarget")! : gallery;
        var position = panel.TranslatePoint(default, window)!.Value;
        Check(position.X >= 0 && position.Y >= 0 && position.X + panel.Bounds.Width <= 980 && position.Y + panel.Bounds.Height <= 640, "Settings panel stays within a 980 x 640 window: " + mode);
        Capture(window, "vex-parity-narrow-" + mode);
        wb.ShowTypography = wb.ShowMapSettings = false;
    }
    window.Width = 1280; window.Height = 820; wb.ShowSource(); Pump();
    static IEnumerable<CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode> Flatten(CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode root)
    { yield return root; foreach (var child in root.Children) foreach (var node in Flatten(child)) yield return node; }
}

void VerifyEditingPreferences(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    var source = window.GetVisualDescendants().OfType<MarkdownEditorView>().Single();
    var sourceVm = app.Resolve<MarkdownEditorViewModel>();
    var sourceDocument = sourceVm.BeginTemporaryDocument();
    var document = vm.Markdown;
    var live = vm.Workbench.LiveEditor!;
    var savedLive = live.CaptureState();
    var oldAutoPair = vm.EditorDisplay.EnableAutoPair;
    vm.BeginGuideDocument("# 滚动验证\n\n" + string.Join("\n\n", Enumerable.Range(1, 100).Select(i => $"## 章节 {i}\n\n正文内容 {i}")));
    vm.Workbench.ShowSource(); Pump();
    vm.Layout.IsTypewriterMode = true; Pump();
    source.Editor.CaretOffset = source.Editor.Document.GetLineByNumber(150).Offset; Pump(); SettleRendering();
    var view = source.Editor.TextArea.TextView;
    Check(Math.Abs(source.Editor.TextArea.Caret.CalculateCaretRectangle().Center.Y - view.ScrollOffset.Y - view.Bounds.Height / 2) < 3,
        "Typewriter source mode centers the actual caret in a long document");
    source.Editor.CaretOffset = source.Text.Length; Pump();
    Check(Math.Abs(source.Editor.TextArea.Caret.CalculateCaretRectangle().Center.Y - view.ScrollOffset.Y - view.Bounds.Height / 2) < 3,
        "Typewriter source mode centers the last line using trailing scroll space");
    var text = vm.Markdown;
    vm.Layout.IsTypewriterMode = false; Pump();
    Check(!source.Editor.Options.AllowScrollBelowDocument && vm.Markdown == text,
        "Turning off source typewriter restores normal scrolling without changing Markdown");
    vm.Workbench.ShowInstant(); Pump();
    live.NavigateToSourceLine(150, beginEdit: true); Pump();
    vm.Layout.IsTypewriterMode = true; Pump();
    CheckLiveCaretCentered("Typewriter instant mode centers the active paragraph caret");
    live.NavigateToSourceLine(400, beginEdit: true); Pump();
    CheckLiveCaretCentered("Typewriter instant mode centers the last paragraph caret");
    vm.Layout.IsTypewriterMode = false; Pump();
    Check(live.DocumentPadding.Bottom <= 70 && vm.Markdown == text,
        "Turning off instant typewriter restores normal padding without changing Markdown");
    foreach (var enabled in new[] { false, true })
    {
        vm.EditorDisplay.EnableAutoPair = enabled; Pump();
        Check(live.EnableAutoPair == enabled && source.EnableAutoPair == enabled,
            "Auto pair preference reaches both editors: " + enabled);
        var box = live.GetVisualDescendants().OfType<TextBox>().First(b => b.IsEffectivelyVisible);
        box.Focus(); box.CaretIndex = box.Text!.Length; box.SelectionStart = box.SelectionEnd = box.CaretIndex;
        window.KeyTextInput("("); Pump();
        Check(box.Text!.EndsWith(enabled ? "()" : "("), "Instant typing observes auto pair: " + enabled);
    }
    vm.EditorDisplay.EnableAutoPair = oldAutoPair;
    vm.EndGuideDocument(); sourceVm.RestoreDocument(sourceDocument); live.RestoreState(savedLive);
    vm.Workbench.ShowSource(); Pump();
    Check(vm.Markdown == document, "Editing preference verification restores the original document");

    void CheckLiveCaretCentered(string message)
    {
        var box = live.GetVisualDescendants().OfType<TextBox>().First(b => b.IsEffectivelyVisible);
        var presenter = box.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().First();
        var scroll = live.GetVisualDescendants().OfType<ScrollViewer>().First();
        var point = presenter.TranslatePoint(presenter.TextLayout.HitTestTextPosition(box.CaretIndex).Center, scroll)!.Value;
        Check(Math.Abs(point.Y - scroll.Viewport.Height / 2) < 4 && scroll.Offset.Y > 0, message);
    }
}

void VerifyLocalizedGuide(Window window, MainWindowViewModel vm)
{
    var shell = (Vex.Modules.Shell.Views.MainWindow)window;
    var guide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    var document = vm.Markdown;
    window.Width = 980; window.Height = 640; Pump();
    foreach (var culture in new[] { "zh-CN", "zh-Hant", "en-US", "ja-JP" })
    {
        vm.Appearance.SelectLanguageByCulture(culture); Pump();
        vm.Layout.EnableMotion = culture == "en-US";
        shell.BeginOnboardingGuide(); Pump();
        for (var step = 0; step < 5; step++)
        {
            guide.ShowStep(step); Pump(); SettleRendering();
            if (step == 1) Check(guide.SpotlightTargets[0].Top > 100 && guide.SpotlightTargets[0].Bottom < guide.Bounds.Height,
                "Guide scrolls the actual editable paragraph into view: " + culture);
            var card = guide.GetVisualDescendants().OfType<Border>().Single(b => b.Width == 330);
            if (step == 0) Check((card.Transitions?.Count > 0) == vm.Layout.EnableMotion,
                "Replayed guide follows the current motion preference: " + culture);
            var position = card.TranslatePoint(default, window)!.Value;
            Check(position.X >= 0 && position.Y >= 0 && position.X + card.Bounds.Width <= window.Bounds.Width + 1 && position.Y + card.Bounds.Height <= window.Bounds.Height + 1,
                $"Localized guide card fits narrow window: {culture} step {step + 1}");
            Capture(window, $"vex-guide-{culture}-{step + 1}");
        }
        guide.Finish(); Pump();
        Check(vm.Markdown == document, "Localized guide restores document: " + culture);
    }
    vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Layout.EnableMotion = false;
    window.Width = 1280; window.Height = 820; Pump();
}

void VerifyAppearanceControls(Window window, MainWindowViewModel vm)
{
    var appearance = vm.Appearance;
    var originalTheme = appearance.SelectedTheme;
    var originalPalette = appearance.SelectedPalette;
    var originalLanguage = appearance.SelectedLanguage;
    var toggle = window.FindControl<Ursa.Controls.ThemeToggleButton>("TitleBarThemeToggle")!;
    var language = window.FindControl<ComboBox>("TitleBarLanguageSelector")!;
    var swatches = window.FindControl<ListBox>("TitleBarPalettes")!;
    var button = toggle.GetVisualDescendants().OfType<Button>().Single(item => item.Name == "PART_ThemeButton");
    foreach (var palette in appearance.PaletteOptions)
    {
        swatches.SelectedItem = palette; appearance.ThemeMode = StudioThemeMode.System; Pump();
        foreach (var expected in new[] { StudioThemeMode.Light, StudioThemeMode.Dark, StudioThemeMode.System })
        {
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(appearance.ThemeMode == expected && ReferenceEquals(appearance.SelectedPalette, palette), "Title bar cycles mode and preserves palette: " + palette.Key + "/" + expected);
            var settings = ((VerificationVexApp)Application.Current!).Resolve<IAppSettingsStore>().Current;
            Check(settings.ThemeKey == appearance.SelectedTheme!.Key && settings.PaletteKey == palette.Key, "Title bar persists the shared appearance state");
        }
    }
    foreach (var width in new[] { 980, 1280 })
    foreach (var option in appearance.LanguageOptions)
    {
        window.Width = width; language.IsDropDownOpen = true; Pump();
        Check(language.IsDropDownOpen && language.Items.Count == appearance.LanguageOptions.Count, "Language icon opens the complete language menu");
        language.SelectedItem = option; language.IsDropDownOpen = false; Pump();
        Check(CodeWF.Toolkit.Lang.I18nManager.Instance.Culture.Name == option.CultureName, "Title bar language follows the single global manager");
        Check(ToolTip.GetTip(button) is TextBlock tip && tip.Text == ((VerificationVexApp)Application.Current!).Resolve<IAppLocalizer>().Get(
            appearance.ThemeMode switch { StudioThemeMode.Light => Vex.VexL.ThemeLight, StudioThemeMode.Dark => Vex.VexL.ThemeDark, _ => Vex.VexL.ThemeSystem }),
            "Ursa mode tooltip changes with the host language");
        var left = (Control)((CodeWF.Avalonia.Controls.Controls.CodeWFWindow)window).LeftContent!;
        var right = window.FindControl<StackPanel>("TitleBarAppearance")!;
        Check(left.TranslatePoint(new Point(left.Bounds.Width, 0), window)!.Value.X <= right.TranslatePoint(default, window)!.Value.X,
            "Title bar menus and appearance controls do not overlap: " + width + "/" + option.CultureName);
    }
    appearance.SelectedTheme = originalTheme; appearance.SelectedPalette = originalPalette; appearance.SelectedLanguage = originalLanguage;
    window.Width = 1280; Pump();
}

void VerifyTitleLayout(Window window)
{
    // 使用真实主窗口模板校验坐标，防止标题栏高度被内容区再次预留。
    var shell = (CodeWF.Avalonia.Controls.Controls.CodeWFWindow)window;
    var divider = window.FindControl<Border>("ShellTitleDivider")!;
    var content = window.FindControl<Grid>("ShellGuideRoot")!;
    var sidebar = window.FindControl<Border>("SidebarGuideTarget")!;
    var spacer = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_TitleBarSpacer");
    var titleBottom = spacer.TranslatePoint(default, window)!.Value.Y + spacer.Bounds.Height;
    var dividerY = divider.TranslatePoint(default, window)!.Value.Y;
    var contentY = content.TranslatePoint(default, window)!.Value.Y;
    Check(divider.BorderThickness.Top == 1 && Math.Abs(dividerY - titleBottom) < 0.01
        && Math.Abs(spacer.Bounds.Height - shell.TitleBarHeight) < 0.01,
        $"Vex content divider starts immediately after the {shell.TitleBarHeight} px title bar: y={dividerY}");
    Check(Math.Abs(contentY - dividerY - 1) < 0.01
        && Math.Abs(sidebar.TranslatePoint(default, window)!.Value.Y - (window.FindControl<Vex.Modules.Shell.Views.ShellWorkbenchToolbarView>("WorkbenchToolbar")!.TranslatePoint(default, window)!.Value.Y + window.FindControl<Vex.Modules.Shell.Views.ShellWorkbenchToolbarView>("WorkbenchToolbar")!.Bounds.Height)) < 0.01,
        $"Vex sidebar and workspace start immediately below the workbench toolbar: y={contentY}");
}

void VerifyVexDetails(Window window, ThemeVariant theme)
{
    VerifyTitleLayout(window);
    var divider = window.FindControl<Border>("ShellTitleDivider")!;
    using (var bitmap = SKBitmap.Decode(Path.Combine(output, "vex-" + theme + ".png")))
    {
        var pixel = bitmap.GetPixel(1000, (int)divider.TranslatePoint(default, window)!.Value.Y);
        var borderColor = ((ISolidColorBrush)divider.BorderBrush!).Color;
        Check(pixel.Red == borderColor.R && pixel.Green == borderColor.G && pixel.Blue == borderColor.B,
            "Vex screenshot contains the title border pixel under " + theme);
    }
    var tabs = window.GetVisualDescendants().OfType<TabItem>().Where(t => t.IsEffectivelyVisible).ToArray();
    Check(tabs.Length == 2 && tabs.All(t => t.Bounds.Height <= 44 && t.Bounds.Width < 100),
        "Vex sidebar uses compact tabs under " + theme);
    Check(tabs.All(t => t.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any()),
        "Vex Files and Outline tabs include icons under " + theme);
    var heading = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("MdH1Border"));
    Check(heading.BorderThickness.Bottom == 1 && SameBrush(heading.BorderBrush, divider.BorderBrush),
        "Vex H1 uses the normal border color under " + theme);
    var quote = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("MdQuote"));
    Application.Current!.TryGetResource("VexAccentBrush", theme, out var accent);
    Check(SameBrush(quote.BorderBrush, (IBrush)accent!) && quote.BorderThickness.Left == 3,
        "Vex quote uses a 3 px accent border under " + theme);
}

bool SameBrush(IBrush? first, IBrush? second) => first is ISolidColorBrush a && second is ISolidColorBrush b && a.Color == b.Color;

void VerifySourceLineHeight(Window window, MarkdownEditorView source, double expected, string name)
{
    var textView = source.Editor.TextArea.TextView;
    Check(Math.Abs(textView.DefaultLineHeight - expected) < 0.01, $"{name} source line height matches {expected} px");
    var click = textView.TranslatePoint(new Point(2, expected * 2.5), window)!.Value;
    window.MouseDown(click, MouseButton.Left);
    window.MouseUp(click, MouseButton.Left);
    Pump();
    Check(source.Editor.TextArea.Caret.Line == 3, $"{name} clicking the third source row positions the caret on line 3");
    var caretTop = source.Editor.TextArea.Caret.CalculateCaretRectangle().Top;
    window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
    window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
    Pump();
    Check(Math.Abs(source.Editor.TextArea.Caret.CalculateCaretRectangle().Top - caretTop - expected) < 1,
        $"{name} Down moves the caret by one visual source row, including wrapped lines");
    source.Editor.CaretOffset = 0;
}

double Contrast(Color first, Color second)
{
    double Luminance(Color c)
    {
        double Linear(byte channel)
        {
            var value = channel / 255d;
            return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }
    var a = Luminance(first);
    var b = Luminance(second);
    return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
}

public sealed class VerificationVexApp : Vex.App
{
    public Window? Shell { get; private set; }
    public T Resolve<T>() => Container.Resolve<T>();
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        base.RegisterTypes(registry);
        registry.RegisterInstance<IAppSettingsStore>(new MemorySettings());
        registry.RegisterInstance<IAutoSaveDraftService>(new MemoryDrafts());
        registry.RegisterInstance<IRecentDocumentStore>(new MemoryRecent());
    }
    protected override AvaloniaObject CreateShell()
    {
        Shell = (Window)base.CreateShell();
        return Shell;
    }
}

public sealed class MemorySettings : IAppSettingsStore
{
    public AppSettings Current { get; private set; } = new()
    {
        ThemeKey = "light", HasSeenOnboardingGuide = true, EnableMotion = false,
        IsMcpServerEnabled = false, IsSidebarVisible = true
    };
    public AppSettings Update(Func<AppSettings, AppSettings> update) => Current = update(Current);
}
public sealed class MemoryDrafts : IAutoSaveDraftService
{
    public string? RestoredMarkdown { get; set; }
    public DocumentSnapshot? TryRestore(DocumentSnapshot document) => RestoredMarkdown is null ? null : document with { Markdown = RestoredMarkdown };
    public void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown) { }
    public void Clear(DocumentSnapshot document) { }
    public void Flush() { }
}
public sealed class MemoryRecent : IRecentDocumentStore
{
    public IReadOnlyList<RecentDocument> Load(int maxCount) => [];
    public void Save(IEnumerable<RecentDocument> documents) { }
}
