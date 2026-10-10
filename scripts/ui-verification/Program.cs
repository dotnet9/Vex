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
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using Prism.Ioc;
using Semi.Avalonia;
using SkiaSharp;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Workspace.Services;
using Vex.Core.Messaging;
using SampleVm = CodeWF.Avalonia.Markdown.Sample.ViewModels.MainWindowViewModel;

var mode = args.FirstOrDefault() ?? "sample";
Console.WriteLine("Markdown library " + typeof(CodeWF.Avalonia.Markdown.Controls.MarkdownViewer).Assembly.GetName().Version);
var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/ui-verification");
Directory.CreateDirectory(output);
if (mode is "sample" or "sample-switch")
{
    AppBuilder.Configure<CodeWF.Avalonia.Markdown.Sample.App>().UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var window = new CodeWF.Avalonia.Markdown.Sample.Views.MainWindow();
    var vm = (SampleVm)window.DataContext!;
    window.Show();
    var sourceView = window.GetVisualDescendants().OfType<MarkdownEditorView>().Single();
    Pump();
    VerifySourceLineHeight(window, sourceView, 22.1, "Demo");
    Check(vm.Markdown.StartsWith("# 基础元素"), "Demo default document matches prototype");
    var original = vm.Markdown;
    VerifyDemoDocumentSwitches(window, vm);
    if (mode == "sample-switch")
    {
        window.Close();
        return;
    }
    foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "MarkdownSamples"), "*.md", SearchOption.AllDirectories))
    {
        var source = File.ReadAllText(file);
        Check(MarkdownBlockParser.Write(MarkdownBlockParser.Parse(source)) == source,
            "Live parser preserves exact sample source: " + Path.GetFileName(file));
    }
    foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
    {
        vm.SelectedThemeVariant = vm.ThemeVariants.Single(option => option.ThemeVariant == theme);
        foreach (var size in new[] { (1400d, 900d), (980d, 640d) })
        {
            window.Width = size.Item1;
            window.Height = size.Item2;
            foreach (var viewMode in new[] { "split", "live", "preview", "edit" })
            {
                vm.ViewMode = viewMode;
                Pump();
                SettleRendering();
                Check(vm.Markdown == original, $"Demo {viewMode} mode preserves source");
                VerifyDemoDetails(window, theme, viewMode);
                Capture(window, $"demo-{theme}-{size.Item1}-{viewMode}");
                if (size.Item1 == 1400 && viewMode is "preview" or "live")
                {
                    var scroll = window.GetVisualDescendants().OfType<ScrollViewer>()
                        .Where(s => s.IsEffectivelyVisible && s.Bounds.Height > 500 && s.Extent.Height > s.Viewport.Height)
                        .OrderByDescending(s => s.Bounds.Width).First();
                    scroll.Offset = new Vector(0, (scroll.Extent.Height - scroll.Viewport.Height) / 2);
                    Pump();
                    Capture(window, $"demo-{theme}-{viewMode}-middle");
                    scroll.Offset = new Vector(0, scroll.Extent.Height);
                    Pump();
                    Check(scroll.Offset.Y > 0, $"Demo {viewMode} screenshot scrolls the visible document");
                    Capture(window, $"demo-{theme}-{viewMode}-bottom");
                    scroll.Offset = default;
                    Pump();
                }
            }
        }
    }
    vm.ViewMode = "live";
    window.Width = 1400;
    window.Height = 900;
    vm.SelectedThemeVariant = vm.ThemeVariants.Single(option => option.ThemeVariant == ThemeVariant.Light);
    Pump();
    SettleRendering();
    var live = window.GetVisualDescendants().OfType<MarkdownLiveEditorView>().Single();
    var heading = live.GetVisualDescendants().OfType<SelectableTextBlock>().First(text => text.IsEffectivelyVisible);
    var click = heading.TranslatePoint(new Point(10, heading.Bounds.Height / 2), window)!.Value;
    window.MouseDown(click, MouseButton.Left);
    window.MouseUp(click, MouseButton.Left);
    Pump();
    Check(live.ActiveBlockIndex == 0, "Demo clicking the rendered heading activates its editor");
    Capture(window, "demo-live-editing");
    Check(window.FocusManager?.GetFocusedElement() is TextBox, "Demo click keeps keyboard focus in the live editor");
    window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
    window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
    window.KeyTextInput("基础元素（离屏验证）");
    Pump();
    Check(vm.Markdown == original.Replace("# 基础元素", "# 基础元素（离屏验证）"),
        "Demo live text input updates only the active heading and synchronizes the document");
    window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
    window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
    Pump();
    SettleRendering();
    Capture(window, "demo-live-edited");
    vm.ViewMode = "split";
    Pump();
    Check(window.GetVisualDescendants().OfType<MarkdownEditorView>().Single().Text == vm.Markdown,
        "Demo returning to split mode preserves the live edit in the source editor");
    vm.Markdown = original;
    vm.ViewMode = "live";
    Pump();
    SettleRendering();
    var quoteGroup = live.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("MdLiveQuote"));
    var quoteParagraphs = quoteGroup.GetVisualDescendants().OfType<SelectableTextBlock>().ToArray();
    Check(quoteParagraphs.Length == 2, "Demo multi-paragraph quote shares one container");
    var quoteClick = quoteParagraphs[0].TranslatePoint(new Point(10, quoteParagraphs[0].Bounds.Height / 2), window)!.Value;
    window.MouseDown(quoteClick, MouseButton.Left);
    window.MouseUp(quoteClick, MouseButton.Left);
    Pump();
    Check(window.FocusManager?.GetFocusedElement() is TextBox, "Demo grouped quote paragraph keeps editing focus");
    window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
    window.KeyRelease(Key.A, RawInputModifiers.Control, PhysicalKey.A, "a");
    window.KeyTextInput("引用段落编辑（离屏验证）");
    Pump();
    Check(vm.Markdown.Contains("> 引用段落编辑（离屏验证）") && vm.Markdown.Contains("[跳到代码示例](#代码高亮)")
        && vm.Markdown.Contains("## 任务列表"), "Demo grouped quote edit preserves sibling link and following blocks");
    window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
    window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
    Pump();
    SettleRendering();
    Capture(window, "demo-live-quote-edited");
    vm.Markdown = original;
    vm.ViewMode = "split";
    vm.StartIncrementalStressCommand.Execute("insert");
    vm.StopIncrementalStressCommand.Execute(null);
    Check(!vm.IsIncrementalStressRunning, "Demo stop command stops the timer");
    Check(vm.Markdown.Contains("中部插入锚点"), "Demo insert command inserts a section");
    vm.SetViewModeCommand.Execute("pair");
    Pump();
    Check(vm.IsPairVisible, "Demo dual preview is reachable");
    Capture(window, "demo-pair");
    window.Close();
}
else if (mode == "vex")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = app.Shell ?? throw new Exception("Prism shell was not initialized");
    var vm = (MainWindowViewModel)window.DataContext!;
    window.Show();
    Pump();
    Check(Math.Abs(window.FindControl<Border>("SidebarGuideTarget")!.Bounds.Width - 252) < 1,
        "Vex sidebar initially occupies 252 px");
    var titleMenu = window.FindControl<Vex.Modules.Shell.Views.ShellTitleMenuView>("TitleMenuView")!;
    var viewMenu = titleMenu.FindControl<MenuItem>("ViewMenuItem")!;
    viewMenu.IsSubMenuOpen = true;
    Pump();
    var sidebarMenu = titleMenu.FindControl<MenuItem>("SidebarMenuItem")!;
    Check(sidebarMenu.IsChecked, "Vex Sidebar menu is initially checked");
    Check(sidebarMenu.Header?.ToString() == "侧边栏", "Vex Sidebar menu label is localized");
    viewMenu.IsSubMenuOpen = false;
    viewMenu.IsSelected = false;
    Pump();
    var sampleFolder = Path.Combine(AppContext.BaseDirectory, "MarkdownSamples");
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
    Capture(window, "vex-unsaved-confirmation");
    ((ShellUnsavedDialogViewModel)dialog.DataContext!).Cancel();
    Pump();
    Check(vm.DocumentInfo.IsModified, "Cancel keeps the edited document dirty");
    Pump(vm.OpenPathAsync(files[0]));
    var drafts = (MemoryDrafts)app.Resolve<IAutoSaveDraftService>();
    drafts.RestoredMarkdown = "# recovered draft";
    Pump(vm.OpenPathAsync(files[1]));
    Check(vm.DocumentInfo.IsModified, "A recovered draft different from disk remains unsaved");
    drafts.RestoredMarkdown = null;
    Pump(vm.OpenPathAsync(files[0]));
    VerifyFileTreeLayout(window, app.Resolve<ShellFilesViewModel>(), sampleFolder);
    foreach (var theme in new[] { ThemeVariant.Light, ThemeVariant.Dark, SemiTheme.Aquatic, SemiTheme.Desert, SemiTheme.Dusk, SemiTheme.NightSky })
    {
        Application.Current!.RequestedThemeVariant = theme;
        Pump();
        SettleRendering();
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
    Capture(window, "vex-980");
    window.Close();
}
else throw new ArgumentException("Expected sample or vex");
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
    frame.Save(Path.Combine(output, name + ".png"));
    if (name is "vex-Light" or "vex-Dark" or "demo-Light-1400-split" or "demo-Dark-1400-split")
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

void VerifyDemoDocumentSwitches(Window window, SampleVm vm)
{
    var originalFile = vm.SelectedFile;
    var originalMode = vm.ViewMode;
    var files = vm.MarkdownFiles.ToArray();
    foreach (var viewMode in new[] { "split", "preview", "pair", "live" })
    {
        vm.ViewMode = viewMode;
        foreach (var file in files.Concat(files.Reverse()))
        {
            vm.SelectedFile = file;
            Pump();
            SettleRendering();
            foreach (var viewer in window.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
                         .Where(v => v.IsEffectivelyVisible))
            {
                Check(viewer.CurrentModel.Source == viewer.Markdown,
                    $"Demo {viewMode} preview finishes rendering {file.Name}");
                if (!viewer.EnableVirtualization || viewer.CurrentModel.Blocks.Count < viewer.VirtualizationThreshold)
                    Check(viewer.RealizedBlockCount == viewer.CurrentModel.Blocks.Count,
                        $"Demo {viewMode} rendered indices match the model: {file.Name}");
            }
            Check(vm.Markdown == File.ReadAllText(file.Path), $"Demo {viewMode} document switch preserves source: {file.Name}");
        }
    }
    vm.ViewMode = "split";
    for (var round = 0; round < 3; round++)
        foreach (var file in files) vm.SelectedFile = file;
    Pump();
    SettleRendering();
    Check(window.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
        .Where(v => v.IsEffectivelyVisible).All(v => v.CurrentModel.Source == vm.Markdown),
        "Demo rapid switches display only the final document");
    vm.SelectedFile = originalFile;
    vm.ViewMode = originalMode;
    Pump();
    SettleRendering();
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
        new DocumentFile(Path.Combine(sampleFolder, "Regression", "01-基础元素.md"), "01-基础元素.md", "Regression", "16:02", "摘要保留在提示中"),
        new DocumentFile(Path.Combine(sampleFolder, "Regression", "02-非常长的文件名称用于验证省略而不是挤出文件树.md"), "02-非常长的文件名称用于验证省略而不是挤出文件树.md", "Regression", "昨天", "长摘要不撑高节点"),
        new DocumentFile(Path.Combine(sampleFolder, "Regression", "Nested", "deep.md"), "deep.md", "Nested", "周一", "第三层文档"),
        new DocumentFile(Path.Combine(sampleFolder, "root.md"), "root.md", "MarkdownSamples", "09-28", "根层文档")
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

void VerifyVexDetails(Window window, ThemeVariant theme)
{
    var divider = window.FindControl<Border>("ShellTitleDivider")!;
    Check(divider.BorderThickness.Top == 1 && divider.TranslatePoint(default, window)!.Value.Y == 39,
        "Vex title divider is 1 px at y=39 under " + theme);
    using (var bitmap = SKBitmap.Decode(Path.Combine(output, "vex-" + theme + ".png")))
    {
        var pixel = bitmap.GetPixel(1000, 39);
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

void VerifyDemoDetails(Window window, ThemeVariant theme, string viewMode)
{
    Application.Current!.TryGetResource("DemoAccent", theme, out var accent);
    var selected = window.GetVisualDescendants().OfType<ListBoxItem>().Single(item => item.IsSelected);
    var icon = selected.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("doc-icon"));
    Check(icon.Bounds.Size == new Size(30, 30) && SameBrush(icon.BorderBrush, (IBrush)accent!),
        $"Demo selected file icon is 30 px with accent border ({theme}, {viewMode})");
    var activeBar = selected.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ActiveBar");
    Check(activeBar.IsVisible && activeBar.Bounds.Height >= 30,
        $"Demo selected file stripe spans the card ({theme}, {viewMode})");
    var segment = window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("seg") && b.Classes.Contains("on"));
    var text = segment.GetVisualDescendants().OfType<TextBlock>().First();
    Check(SameBrush(text.Foreground, (IBrush)accent!), $"Demo active view label uses accent ({theme}, {viewMode})");
    Check(window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("titlebar")).Bounds.Height == 44,
        $"Demo title bar is 44 px ({theme}, {viewMode})");
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
