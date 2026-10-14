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
if (mode == "vex")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = app.Shell ?? throw new Exception("Prism shell was not initialized");
    var vm = (MainWindowViewModel)window.DataContext!;
    window.Show();
    Pump();
    VerifyTitleLayout(window);
    Check(Math.Abs(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width - 252) < 1,
        "Vex sidebar initially occupies 252 px");
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
    Check(about.IsEffectivelyVisible && about.Bounds.Width > 0 && !titleMenu.IsEffectivelyEnabled,
        "About opens inside the real main window modal host");
    Capture(window, "vex-about");
    ((ShellAboutViewModel)about.DataContext!).Close();
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
    Check(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width == 252, "Vex sidebar reopens");
    Check(sidebarMenu.IsChecked && app.Resolve<IAppSettingsStore>().Current.IsSidebarVisible == true,
        "Sidebar check and persisted state follow reopening");
    window.Width = 980;
    window.Height = 640;
    Pump();
    VerifyTitleLayout(window);
    Capture(window, "vex-980");
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
        Check(card.Bounds.Height is >= 25 and <= 30 && point.X + card.Bounds.Width <= tree.Bounds.Width + 1,
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
        && Math.Abs(sidebar.TranslatePoint(default, window)!.Value.Y - contentY) < 0.01,
        $"Vex sidebar and workspace start below the 1 px divider without a blank row: y={contentY}");
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
    Check(tabs.Length == 2 && tabs.All(t => t.Bounds.Height < 40 && t.Bounds.Width < 100),
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
