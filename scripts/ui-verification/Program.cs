using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.ReactiveUI;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Prism.Ioc;
using CodeWF.Avalonia.Controls.Themes;
using SkiaSharp;
using Markdig;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Workspace.Services;
using Vex.Core.Messaging;
using CodeWF.Toolkit.Core.UpdateChecking;

var mode = args.FirstOrDefault() ?? "vex";
VerificationVexApp.ProbeFileTree = mode == "file-tree-probe";
if (mode == "protection") VerificationVexApp.Exports = new VerificationExports();
if (mode == "updates") VerificationVexApp.Updates = new VerificationUpdates();
Console.WriteLine("Markdown library " + typeof(CodeWF.Avalonia.Markdown.Controls.MarkdownViewer).Assembly.GetName().Version);
var output = Path.GetFullPath(args.ElementAtOrDefault(1) ?? "artifacts/ui-verification");
Directory.CreateDirectory(output);
if (mode == "settings-real-ui")
{
    VerificationVexApp.UseRealSettings = true;
    var config = Path.Combine(output, "settings.config");
    File.WriteAllText(config, "<configuration><appSettings /></configuration>");
    AppContext.SetData("APP_CONFIG_FILE", config);
}
if (mode == "parity-score")
{
    VisualParityScore.Run(args.Skip(1).ToArray());
    return;
}
if (mode is "popup-layout" or "elements" or "motion" or "desktop-parity" or "settings-ui" or "settings-real-ui" or "prototype-appearance" or "prototype-chrome" or "protection" or "visual" or "render-geometry")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().With(VexFonts.Options).UseReactiveUI()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = (Vex.Modules.Shell.Views.MainWindow)app.Shell!;
    window.Show();
    // 启动期的演示文档是异步装载的；不先等它落定，验证器写入的样例会随后被覆盖。
    var startupVm = (MainWindowViewModel)window.DataContext!;
    var startupDeadline = DateTime.UtcNow.AddSeconds(15);
    while (startupVm.DocumentInfo.CurrentFilePath is not { Length: > 0 })
    {
        Pump();
        if (DateTime.UtcNow > startupDeadline) throw new TimeoutException("The startup document did not load");
    }
    Pump();
    if (mode == "popup-layout") PopupLayoutVerification.Run(app, window, output);
    else if (mode == "elements") ElementVerification.Run(app, window, output);
    else if (mode == "motion") MotionVerification.Run(app, window, output);
    else if (mode == "protection") ProtectionVerification.Run(app, window, output);
    else if (mode == "prototype-appearance") PrototypeAppearanceVerification.Run(app, window, output);
    else if (mode == "prototype-chrome") PrototypeChromeVerification.Run(app, window, output);
    else if (mode == "visual") VisualParityCapture.Run(app, window, output);
    else if (mode == "render-geometry") RenderGeometryProbe.Run(app, window, output);
    else if (mode is "settings-ui" or "settings-real-ui") PreferencesVerification.Run(app, window, output);
    else DesktopParityVerification.Run(app, window, output);
    window.Close();
    return;
}
if (mode == "parity")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().With(VexFonts.Options).UseReactiveUI()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = app.Shell!;
    var vm = (MainWindowViewModel)window.DataContext!;
    window.Show();
    SettleRendering();
    vm.Appearance.SelectPaletteByKey("aurora");
    var sampleFolder = Path.GetFullPath(Path.Combine(output, "..", "笔记"));
    Directory.CreateDirectory(sampleFolder);
    var sample = Path.Combine(sampleFolder, "主题设计笔记.md");
    Check(File.Exists(sample), "Capture the accepted prototype documents first");
    Pump(vm.OpenStartupDocumentAsync(new[] { sampleFolder }));
    Pump(vm.OpenPathAsync(sample));
    Check(vm.DocumentFiles.Count == 5, "Parity captures retain the complete five file workspace");
    foreach (var width in new[] { 1280, 980 })
    {
        window.Width = width; window.Height = width == 1280 ? 820 : 640;
        foreach (var theme in new[] { "light", "dark" })
        {
            vm.Appearance.SelectThemeByKey(theme);
            foreach (var view in WorkbenchLayoutOption.All.Select(option => option.Id))
            {
                vm.Workbench.ShowTypography = false; vm.Workbench.ShowMapSettings = false; vm.Workbench.SelectedNode = null;
                vm.Workbench.ShowMapSource = false;
                vm.Workbench.SelectLayout(view);
                Pump();
                PumpUntil(() => vm.Workbench.Root is not null);
                SettleRendering();
                var sourceView = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.FindControl<MarkdownEditorView>("MarkdownEditor")!;
                sourceView.Editor.CaretOffset = 0;
                sourceView.Editor.ScrollToHome();
                foreach (var scroll in window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
                foreach (var scroll in window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
                Pump();
                Capture(window, $"{width}-{theme}-{view}");
            }
        }
    }
    window.Width = 1280; window.Height = 820; vm.Appearance.SelectThemeByKey("light");
    vm.Workbench.SelectLayout("instant"); vm.Workbench.ShowLayouts = true; Pump(); SettleRendering(); Capture(window, "layouts");
    vm.Workbench.ShowLayouts = false;
    vm.Workbench.ShowInstant(); vm.Workbench.ShowTypography = true; Pump(); SettleRendering(); Capture(window, "typography");
    vm.Workbench.ShowTypography = false;
    var live = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
    foreach (var scroll in live.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = new Vector(0, scroll.Extent.Height);
    Pump(); SettleRendering();
    var code = live.GetVisualDescendants().OfType<SelectableTextBlock>().First(text => text.Classes.Contains("MdCodeBlockText"));
    code.BringIntoView(); Pump(); SettleRendering(); Capture(window, "code-display");
    var codeClick = code.TranslatePoint(new Point(20, 8), window)!.Value;
    window.MouseDown(codeClick, MouseButton.Left); window.MouseUp(codeClick, MouseButton.Left); Pump(); Capture(window, "code-edit");
    window.FindControl<Button>("QuickStartButton")!.Focus(); Pump();
    var aboutTask = app.Resolve<IShellOverlayService>().ShowAboutAsync();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Any());
    Capture(window, "about");
    ((ShellAboutViewModel)window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Single().DataContext!).Close(); Pump(aboutTask);
    Pump(vm.OpenStartupDocumentAsync([BundledDocuments.GetPath("鸣谢.md")]));
    vm.Workbench.ShowRead(); Pump(); SettleRendering(); Capture(window, "thanks");
    vm.Workbench.ShowMindMap(); vm.Workbench.ShowMapSettings = true; Pump(); SettleRendering(); Capture(window, "map-settings");
    vm.Workbench.ShowMapSettings = false; vm.Workbench.SelectedNode = vm.Workbench.Root!.Children.First(); Pump(); SettleRendering(); Capture(window, "map-inspector");
    window.Close();
    Console.WriteLine("Native parity captures: " + output);
    return;
}
if (mode is "mermaid" or "documents" or "document-open-child" or "updates" or "vex" or "preferences" or "locale" or "buttons" or "danger-buttons" or "path-identity" or "layout" or "editing-tools" or "workspace" or "workbench" or "performance" or "file-tree" or "file-icons" or "file-tree-probe" or "pointer-regressions" or "stress" or "presentation" or "html-tables" or "media-layout")
{
    AppBuilder.Configure<VerificationVexApp>().UseSkia().With(VexFonts.Options).UseReactiveUI()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
    var app = (VerificationVexApp)Application.Current!;
    var window = app.Shell ?? throw new Exception("Prism shell was not initialized");
    var vm = (MainWindowViewModel)window.DataContext!;
    window.Show();
    // 启动期的演示文档/工作区恢复是异步的，等它尘埃落定再开始任何验证。
    PumpUntil(() => vm.DocumentInfo.CurrentFilePath is { Length: > 0 });
    if (mode == "mermaid") { VerifyMermaidWorkspace((Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "documents") { VerifyDocumentWindows(app, window, vm); window.Close(); return; }
    if (mode == "document-open-child")
    {
        Pump(vm.OpenStartupDocumentAsync([args[2]]));
        Check(vm.DocumentInfo.CurrentFilePath == args[2], "Child process opens the supplied ordinary document");
        vm.Markdown += "\n\n独立进程编辑并保存。\n";
        Pump(vm.SaveAsync());
        Check(!vm.DocumentInfo.IsModified, "Child process uses ordinary save workflow");
        File.WriteAllText(Path.Combine(output, "child-pid.txt"), Environment.ProcessId.ToString());
        window.Close(); return;
    }
    if (mode == "updates") { VerifyUpdates(app, window, vm); window.Close(); return; }
    if (mode == "media-layout") { VerifyMediaLayout((Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "html-tables") { VerifyHtmlTables((Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "presentation") { VerifyPresentation(app, (Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "pointer-regressions") { VerifyPointerRegressions(app, window, vm); window.Close(); return; }
    if (mode == "stress") { VerifyStress((Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "file-tree") { VerifyFileTreeLayout(window, app.Resolve<ShellFilesViewModel>(), output); window.Close(); return; }
    if (mode == "file-icons") { FileIconVerification.Run(app, window, output); window.Close(); return; }
    if (mode == "file-tree-probe") { FileTreeLoadingProbe.Run(app, window, output); window.Close(); return; }
    if (mode == "performance") { VerifyPerformance((Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "workspace") { VerifySingleDocumentWorkspace(app, (Vex.Modules.Shell.Views.MainWindow)window, vm); window.Close(); return; }
    if (mode == "workbench") { VerifyWorkbench(app, window, vm); window.Close(); return; }
    if (mode is "buttons" or "danger-buttons")
    {
        VerifyDialogButtonStates(app, window, vm, mode == "buttons" ? "primary" : "danger", args.Contains("--audit-only"));
        window.Close();
        return;
    }
    if (mode == "editing-tools") { VerifyEditingTools(app, window, vm); window.Close(); return; }
    if (mode == "layout") { VerifyWorkspaceLayout(app, window, vm); window.Close(); return; }
    ControlLocaleVerification.Run(window, ["zh-CN", "en-US", "zh-Hant", "ja-JP"], app.Resolve<IAppLocalizer>().SetCulture);
    var languageMenu = window.FindControl<Vex.Modules.Shell.Views.ShellAppearanceMenuView>("AppearanceMenu")!;
    foreach (var culture in new[] { "en-US", "zh-Hant", "ja-JP", "zh-CN" })
    {
        CodeWF.Toolkit.Lang.I18nManager.Instance.Culture = new System.Globalization.CultureInfo(culture);
        Pump();
        Check(vm.Appearance.SelectedLanguage?.CultureName == culture,
            "Global language synchronizes the actual title bar selection: " + culture);
        var returnCulture = culture == "zh-CN" ? "en-US" : "zh-CN";
        var languageContent = (StackPanel)((Flyout)languageMenu.LanguageButton.Flyout!).Content!;
        languageContent.Children.OfType<Button>().ElementAt(vm.Appearance.LanguageOptions.ToList().FindIndex(option => option.CultureName == returnCulture))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check(CodeWF.Toolkit.Lang.I18nManager.Instance.Culture?.Name == returnCulture,
            "Title bar selection switches back after global culture changes: " + returnCulture);
    }
    if (mode == "locale") { window.Close(); return; }
    if (mode == "preferences")
    {
        vm.Workbench.ShowSource(); Pump(); VerifyEditingPreferences(app, window, vm); VerifyLocalizedGuide(window, vm); window.Close(); return;
    }

    vm.Workbench.ShowSource();
    Pump();
    VerifyTitleLayout(window);
    VerifyAppearanceControls(window, vm);
    var associationFolder = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileName(output) + "-file-associations");
    Directory.CreateDirectory(associationFolder);
    var externalPaths = app.Resolve<IShellExternalPathResolver>();
    foreach (var extension in new[] { ".md", ".markdown", ".mdown" })
    {
        var associatedFile = Path.Combine(associationFolder, "markdown example" + extension);
        File.WriteAllText(associatedFile, "# File association verification\n");
        Check(externalPaths.ResolveStartupArgument([associatedFile]) == ShellExternalPathResult.File(associatedFile),
            "Markdown startup path keeps spaces and extension: " + extension);
        Check(externalPaths.ResolveDroppedPath(associatedFile) == ShellExternalPathResult.File(associatedFile),
            "Markdown drop path supports the same file extension: " + extension);
    }
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
    var sampleFolder = Path.GetFullPath(Path.Combine(output, "..", "笔记"));
    Directory.CreateDirectory(sampleFolder);
    foreach (var sample in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "MarkdownSamples"), "*.md"))
    {
        var samplePath = Path.Combine(sampleFolder, Path.GetFileName(sample));
        File.Copy(sample, samplePath, overwrite: true);
        File.SetLastWriteTimeUtc(samplePath, DateTime.UtcNow.AddHours(-36));
    }
    var files = Directory.GetFiles(sampleFolder, "*.md").Order().Take(5).ToArray();
    Check(files.Length == 5, "Vex regression uses five real sample files");
    foreach (var file in files)
    {
        vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
            new DocumentFile(file, Path.GetFileName(file), sampleFolder, string.Empty, string.Empty), null));
        PumpUntil(() => vm.DocumentInfo.CurrentFilePath == file);
        Check(!vm.DocumentInfo.IsModified, "Opening an untouched file stays clean: " + Path.GetFileName(file));
    }
    VerifyPathIdentity(app, window, vm, sampleFolder);
    if (mode == "path-identity") { window.Close(); return; }
    var editorVm = app.Resolve<MarkdownEditorViewModel>();
    VerifySourceLineHeight(window, window.GetVisualDescendants().OfType<MarkdownEditorView>().Single(), 26.25, "Vex");
    editorVm.InsertText(" regression edit");
    Pump();
    Check(vm.DocumentInfo.IsModified, "Typing marks Vex document dirty");
    vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
        new DocumentFile(files[0], Path.GetFileName(files[0]), sampleFolder, string.Empty, string.Empty), null));
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Any());
    var dialog = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Single();
    Check(vm.DocumentInfo.CurrentFilePath == files[^1], "Dirty-file switch waits for save confirmation");
    var switchLocalizer = app.Resolve<IAppLocalizer>();
    var switchTexts = dialog.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
    var switchActionsPanel = dialog.FindControl<StackPanel>("SwitchActions")!;
    var exitActionsPanel = dialog.FindControl<StackPanel>("ExitActions")!;
    var switchButtons = switchActionsPanel.Children.OfType<Button>().Select(b => b.Content?.ToString()).ToList();
    var switchCancel = ((Grid)switchActionsPanel.Parent!).Children.OfType<Button>().Select(b => b.Content?.ToString()).ToList();
    Check(switchActionsPanel.IsVisible && !exitActionsPanel.IsVisible
            && switchTexts.Contains(switchLocalizer.Get(Vex.VexL.Save))
            && switchTexts.Contains(switchLocalizer.Get(Vex.VexL.DialogSwitchHint))
            && switchTexts.Contains(switchLocalizer.Get(Vex.VexL.DialogLoseChangesHint))
            && switchButtons.Count(t => t == switchLocalizer.Get(Vex.VexL.DialogSaveAndSwitch)) == 1
            && switchButtons.Contains(switchLocalizer.Get(Vex.VexL.DialogDiscardAndSwitch))
            && switchButtons.Count == 2
            && switchCancel.SequenceEqual(new[] { switchLocalizer.Get(Vex.VexL.DialogContinueEditing) }),
        "Switch dialog matches the reference: save title, two fixed lines, exactly 保存/放弃更改/取消");
    var switchCard = dialog.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("dialog-card"));
    var switchSave = (Button)switchActionsPanel.Children[0];
    var switchCancelBtn = ((Grid)switchActionsPanel.Parent!).Children.OfType<Button>().Single();
    var switchTitle = dialog.FindControl<TextBlock>("TitleText")!;
    var switchHint = dialog.FindControl<TextBlock>("HintLine1")!;
    Check(switchCard.CornerRadius.TopLeft == 12 && Math.Abs(switchCard.Bounds.Width - 480) < 1
            && Math.Abs(switchTitle.FontSize - 15.5) < .01 && Math.Abs(switchTitle.LineHeight - 24.8) < .01
            && switchHint.FontSize == 12.5 && Math.Abs(switchHint.LineHeight - 20) < .01
            && Math.Abs(switchSave.Bounds.Height - 32) < 1 && switchSave.FontSize == 12
            && switchSave.CornerRadius.TopLeft == 16
            && Math.Abs(switchCancelBtn.Bounds.Height - 32) < 1 && switchCancelBtn.CornerRadius.TopLeft == 16
            && ((Button)switchActionsPanel.Children[1]).BorderThickness.Top == 1 && switchCancelBtn.BorderThickness.Top == 1
            && switchCancelBtn.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().First().Bounds.Height >= 12,
        "Switch dialog uses the measured prototype metrics: 12px card, 15.5px title, 12.5/20 body, 32px pill buttons at 12px, hairline secondary borders, visible labels");
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
    Check(!vm.DocumentInfo.IsModified && vm.Protection.HasRecovery && vm.Markdown != drafts.RestoredMarkdown,
        "A recovery draft is offered for preview without replacing the opened document");
    drafts.RestoredMarkdown = null;
    Pump(vm.OpenPathAsync(files[0]));
    window.Width = 980; window.Height = 640; Pump();
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
        Check(CodeWF.Toolkit.Lang.I18nManager.Instance.Culture.Name == culture && about.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == app.Resolve<IAppLocalizer>().Get("Vex.VexL.AboutDialogTitle")),
            "The open About dialog changes language: " + culture);
        var aboutScroll = about.GetVisualDescendants().OfType<ScrollViewer>().Single();
        VerifyInside(aboutScroll, window, "About viewport fits the minimum window: " + culture);
        aboutScroll.Offset = default; Pump();
        var releaseAction = about.GetVisualDescendants().OfType<Button>().Single(button => button.Command == aboutModel.ReleaseCommand);
        VerifyInside(releaseAction, aboutScroll, "About version link is reachable: " + culture);
        aboutScroll.ScrollToEnd(); Pump();
        var closeAction = about.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("vex-dismiss"));
        VerifyInside(closeAction, window, "About fixed close action is reachable after scrolling: " + culture);
        Capture(window, "vex-about-" + culture);
    }
    vm.Appearance.SelectLanguageByCulture("zh-CN"); Pump();
    Capture(window, "vex-about");
    aboutModel.Close();
    Pump(aboutTask);
    // 产品可达的关于与设置均使用 Ursa 宿主浮层，不触发 Prism 默认 DialogWindow。
    var settingsTask = app.Resolve<IShellOverlayService>().ShowMcpSettingsAsync(app.Resolve<McpSettingsViewModel>());
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.McpSettingsOverlayView>().Any());
    var settingsOverlay = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.McpSettingsOverlayView>().Single();
    Check(settingsOverlay.IsEffectivelyVisible && !titleMenu.IsEffectivelyEnabled,
        "Settings opens in the existing Ursa modal host without a Prism dialog window");
    ((McpSettingsViewModel)settingsOverlay.DataContext!).Cancel(); Pump(settingsTask);
    Check(!window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.McpSettingsOverlayView>().Any(),
        "Cancel closes the MCP settings modal through IDialogContext");
    VerifyDialogButtonStates(app, window, vm, "primary");
    VerifyDialogButtonStates(app, window, vm, "danger");
    vm.Workbench.ShowRead(); Pump(); SettleRendering();
    window.Width = 1280; window.Height = 820; Pump();
    VerifyFileTreeLayout(window, app.Resolve<ShellFilesViewModel>(), sampleFolder);
    Check(vm.Appearance.PaletteOptions.Count == 9 && vm.Appearance.ThemeOptions.Count == 3,
        "Vex exposes the nine registry palettes and three independent theme modes");
    vm.Layout.ShowOutline();
    foreach (var palette in vm.Appearance.PaletteOptions)
    foreach (var modeOption in vm.Appearance.ThemeOptions.Where(option => option.Mode != StudioThemeMode.System))
    {
        vm.Appearance.SelectedPalette = palette;
        vm.Appearance.SelectedTheme = modeOption;
        var theme = Application.Current!.ActualThemeVariant;
        Check(theme == StudioTheme.GetVariant(palette.Key, modeOption.Mode == StudioThemeMode.Dark),
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
    foreach (var culture in new[] { "zh-CN", "zh-Hant", "en-US", "ja-JP" })
    {
        vm.Appearance.SelectLanguageByCulture(culture); Pump();
        var statusBar = window.FindControl<Vex.Modules.Shell.Views.ShellStatusBarView>("StatusBarGuideTarget")!;
        foreach (var action in statusBar.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible))
            VerifyInside(action, statusBar, "Workbench action fits minimum window: " + culture);
        Capture(window, "vex-workbench-minimum-" + culture);
    }
    vm.Appearance.SelectLanguageByCulture("zh-CN"); Pump();
    Capture(window, "vex-980");
    void VerifyVisibleTable(string view)
    {
        var table = window.GetVisualDescendants().OfType<Grid>().SingleOrDefault(grid =>
            grid.IsEffectivelyVisible && grid.GetType().Name == "MarkdownTableGrid" && grid.ColumnDefinitions.Count == 2);
        Check(table is not null && table.ColumnDefinitions[0].ActualWidth < table.ColumnDefinitions[1].ActualWidth / 2,
            view + " allocates natural table widths for short API names and long descriptions");
        Check(table?.Parent is Border outline &&
            Math.Abs(outline.Bounds.Width - table.ColumnDefinitions.Sum(column => column.ActualWidth) - 2) <= 2,
            view + " visible table outline fits the columns without a blank trailing area");
        if (table is not null)
        {
            var scroller = table.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            var offset = scroller?.Offset;
            table.BringIntoView(); Pump();
            Capture(window, view.StartsWith("Instant", StringComparison.Ordinal) ? "vex-native-table-instant" : "vex-native-table-read");
            if (scroller is not null && offset.HasValue) { scroller.Offset = offset.Value; Pump(); }
        }
    }
    vm.Workbench.ShowInstant(); Pump();
    VerifyVisibleTable("Instant editing");
    Capture(window, "vex-native-instant-980");
    vm.Workbench.ShowRead(); Pump(); Capture(window, "vex-native-read-980");
    var ageFile = vm.DocumentFiles.Single(file => Path.GetFileName(file.Path) == Path.GetFileName(files[3]));
    var ageLabel = window.GetVisualDescendants().OfType<TextBlock>().Single(block =>
        block.Classes.Contains("file-title") && block.DataContext is DocumentFileNode node && ReferenceEquals(node.DocumentFile, ageFile));
    var filesModel = app.Resolve<ShellFilesViewModel>();
    var selectedNode = filesModel.SelectedDocumentFileNode;
    var savedFilter = filesModel.FilterText;
    var savedMarkdown = vm.Markdown;
    foreach (var culture in new[] { "en-US", "zh-Hant", "ja-JP", "zh-CN" })
    {
        Task.Run(() => CodeWF.Toolkit.Lang.I18nManager.Instance.Culture = new System.Globalization.CultureInfo(culture))
            .GetAwaiter().GetResult();
        Pump();
        Check(vm.Appearance.SelectedLanguage?.CultureName == culture,
            "Background global culture synchronizes the visible Markdown reader and title selection: " + culture);
        Check(ageLabel.Text == Path.GetFileName(ageFile.Path),
            "The same filename remains stable across background global culture: " + culture);
        Check(ReferenceEquals(selectedNode, filesModel.SelectedDocumentFileNode) && savedFilter == filesModel.FilterText && savedMarkdown == vm.Markdown,
            "File time translation preserves the tree selection, filter and unsaved content: " + culture);
        var returnCulture = culture == "zh-CN" ? "en-US" : "zh-CN";
        var languageContent = (StackPanel)((Flyout)languageMenu.LanguageButton.Flyout!).Content!;
        languageContent.Children.OfType<Button>().ElementAt(vm.Appearance.LanguageOptions.ToList().FindIndex(option => option.CultureName == returnCulture))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Pump();
        Check(CodeWF.Toolkit.Lang.I18nManager.Instance.Culture?.Name == returnCulture,
            "The same language selector switches back after background culture changes: " + returnCulture);
    }
    VerifyVisibleTable("Reading");
    vm.Workbench.ShowMindMap(); vm.Workbench.ShowMapSource = false; vm.Workbench.ShowMapSettings = false;
    Pump(); Capture(window, "vex-native-mindmap-980");
    ((Vex.Modules.Shell.Views.MainWindow)window).BeginOnboardingGuide(); Pump();
    var narrowGuide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    for (var step = 0; step < 6; step++)
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

void VerifyInside(Control control, Control host, string description)
{
    var position = control.TranslatePoint(default, host) ?? throw new InvalidOperationException(description + ": missing visual host");
    Check(control.IsEffectivelyVisible && control.Bounds.Width > 0 && control.Bounds.Height > 0
        && position.X >= -1 && position.Y >= -1 && position.X + control.Bounds.Width <= host.Bounds.Width + 1
        && position.Y + control.Bounds.Height <= host.Bounds.Height + 1,
        $"{description}; {control.Bounds.Size} at {position}, host {host.Bounds.Size}");
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

void VerifyPointerRegressions(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    string? openedWebsite = null;
    using (var about = new ShellAboutViewModel(app.Resolve<IAppLocalizer>(), url => openedWebsite = url))
    {
        about.WebsiteCommand.Execute().Subscribe();
        Check(openedWebsite == "https://vex.codewf.com/" && openedWebsite == ProjectLinks.Website,
            "About opens the configured Vex website");
    }
    window.Width = 1280; window.Height = 820;
    vm.Appearance.SelectThemeByKey("light");
    var samplePath = Path.Combine(output, "文本选择.md");
    const string sampleText = "# 文本选择\n\n第一段文字，用鼠标直接拖动选择。\n\n第二段文字，也需要能够连续选择。\n\n含有 **加粗文字** 和 `行内代码` 的正文。\n\n```csharp\nvar value = 42;\nConsole.WriteLine(value);\n```\n";
    File.WriteAllText(samplePath, sampleText);
    Pump(vm.OpenPathAsync(samplePath));
    vm.Workbench.SelectLayout("instant");
    Pump(); SettleRendering();
    var live = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
    PumpUntil(() => live.GetVisualDescendants().OfType<SelectableTextBlock>().Any(block => block.Classes.Contains("MdParagraph")));
    var paragraph = live.GetVisualDescendants().OfType<SelectableTextBlock>().First(block => block.Classes.Contains("MdParagraph"));
    var start = paragraph.TranslatePoint(new Point(2, 12), window)!.Value;
    var end = paragraph.TranslatePoint(new Point(112, 12), window)!.Value;
    window.MouseDown(start, MouseButton.Left); Pump();
    for (var i = 1; i <= 12; i++) { window.MouseMove(start + (end - start) * (i / 12d), RawInputModifiers.LeftMouseButton); Pump(); }
    window.MouseUp(end, MouseButton.Left); Pump();
    Capture(window, "instant-first-drag-selection");
    Console.WriteLine($"SELECTION first drag: [{live.SelectedText}]; source={live.Selection}");
    Check(live.SelectedText.Length >= 4 && sampleText.Substring(live.Selection.Start, live.Selection.Length) == live.SelectedText,
        "First press and drag selects visible instant text and maps to the original Markdown");
    Check(vm.Markdown == sampleText, "Selecting text does not modify the document");
    var firstSelection = live.SelectedText;
    Drag(end, start);
    Check(live.SelectedText == firstSelection, "Reverse drag in the active paragraph selects the same text");
    var paragraphs = live.GetVisualDescendants().OfType<SelectableTextBlock>().Where(block => block.Classes.Contains("MdParagraph")).ToArray();
    var secondEnd = paragraphs[1].TranslatePoint(new Point(112, 12), window)!.Value;
    Drag(start, secondEnd);
    Check(live.SelectedText.Contains("\n\n第二", StringComparison.Ordinal), "Pointer selection continues across paragraphs");
    var crossSelection = live.SelectedText; var crossRange = live.Selection;
    Pump(live.ExecuteAsync(CodeWF.Avalonia.Markdown.Editor.Services.MarkdownEditorAction.Copy));
    var copied = TopLevel.GetTopLevel(live)!.Clipboard!.TryGetTextAsync(); Pump(copied);
    Check(copied.Result == crossSelection && vm.Markdown == sampleText, "Copy uses the complete cross-paragraph range without modifying Markdown");
    Drag(secondEnd, start);
    Check(live.SelectedText == crossSelection && live.Selection == crossRange, "Reverse cross-paragraph drag preserves range and text");
    window.KeyTextInput("替换后的内容"); Pump();
    Check(vm.Markdown == sampleText.Remove(crossRange.Start, crossRange.Length).Insert(crossRange.Start, "替换后的内容"),
        "Typing replaces the complete cross-paragraph selection once");
    window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z"); window.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z"); Pump();
    Check(vm.Markdown == sampleText, "Shared undo restores cross-paragraph replacement");
    SettleRendering();
    var codeText = live.GetVisualDescendants().OfType<SelectableTextBlock>().First(block => block.Classes.Contains("MdCodeBlockText"));
    Drag(codeText.TranslatePoint(new Point(2, 10), window)!.Value, codeText.TranslatePoint(new Point(105, 10), window)!.Value);
    Check(live.SelectedText.Contains("var", StringComparison.Ordinal) && live.SelectedText.Length >= 5, "Code can be selected on the first drag");
    Check(vm.Markdown == sampleText, "Selection in code preserves the document and code language");
    Capture(window, "instant-code-selection");

    const string multilineSource = "# 多行段落\n\n第一行保持原来的位置。\n第二行含 **粗体** 和 `代码`，进入编辑时也保持两行。\n";
    vm.Markdown = multilineSource; Pump(); SettleRendering();
    var multiline = live.GetVisualDescendants().OfType<SelectableTextBlock>().First(block => block.Classes.Contains("MdParagraph"));
    var multilineOrigin = multiline.TranslatePoint(default, window)!.Value;
    var lineCount = multiline.TextLayout.TextLines.Count;
    Drag(multilineOrigin + new Point(2, 12), multilineOrigin + new Point(110, 12));
    var multilineInput = live.GetVisualDescendants().OfType<TextBox>().Single(input => input.IsEffectivelyVisible);
    var multilinePresenter = multilineInput.GetVisualDescendants().OfType<TextPresenter>().Single();
    Check(lineCount == 2 && multilinePresenter.TextLayout.TextLines.Count == lineCount
        && Math.Abs(multilinePresenter.TranslatePoint(default, window)!.Value.Y - multilineOrigin.Y) < 1,
        "Entering a multiline paragraph preserves soft line breaks, line count and text origin");
    Check(vm.Markdown == multilineSource, "Selecting a multiline paragraph preserves the original source");
    Capture(window, "instant-multiline-selection");

    var longCode = "# 长代码选择\n\n```csharp\n" + string.Join('\n', Enumerable.Range(1, 120)
        .Select(index => "var value" + index + " = \"" + new string('x', index == 1 ? 300 : 20) + "\"; // 高亮与横向滚动")) + "\n```\n";
    vm.Markdown = longCode; Pump(); SettleRendering();
    live.GetVisualDescendants().OfType<ScrollViewer>().First().Offset = default; Pump();
    var longCodeText = live.GetVisualDescendants().OfType<SelectableTextBlock>().First(block => block.Classes.Contains("MdCodeBlockText"));
    Check(longCodeText.Text?.Contains("value120", StringComparison.Ordinal) == true, "Long code uses selectable text style spans without a token control tree");
    Drag(longCodeText.TranslatePoint(new Point(2, 10), window)!.Value, longCodeText.TranslatePoint(new Point(105, 10), window)!.Value);
    Check(live.SelectedText.Contains("var", StringComparison.Ordinal), "Long highlighted code retains first-drag selection");
    Capture(window, "instant-long-code-selection");
    var codeScroll = longCodeText.GetVisualAncestors().OfType<ScrollViewer>().First();
    var horizontal = codeScroll.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
        .Single(bar => bar.Orientation == Avalonia.Layout.Orientation.Horizontal && bar.IsEffectivelyVisible);
    // 长代码的底部滚动条在文档下方；将当前块底部带进外层视口再拖动。
    horizontal.BringIntoView(); Pump();
    var horizontalThumb = horizontal.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().First();
    var horizontalFrom = horizontalThumb.TranslatePoint(new Rect(horizontalThumb.Bounds.Size).Center, window)!.Value;
    var horizontalTo = horizontal.TranslatePoint(new Point(horizontal.Bounds.Width - 20, horizontal.Bounds.Height / 2), window)!.Value;
    Capture(window, "long-code-before-horizontal");
    Console.WriteLine($"HORIZONTAL from={horizontalFrom}, to={horizontalTo}, bar={horizontal.Bounds}, scroll={codeScroll.Offset}, extent={codeScroll.Extent}, viewport={codeScroll.Viewport}, hit={window.InputHitTest(horizontalFrom)?.GetType().Name}");
    Drag(horizontalFrom, horizontalTo);
    Console.WriteLine($"HORIZONTAL after={codeScroll.Offset}, unchanged={vm.Markdown == longCode}");
    Check(codeScroll.Offset.X > 100 && vm.Markdown == longCode, "Dragging the long code horizontal scrollbar scrolls without editing its source");

    vm.Markdown = sampleText;
    Pump(vm.OpenStartupDocumentAsync([BundledDocuments.GetPath("更新日志.md")]));
    vm.Workbench.ShowRead(); Pump(); SettleRendering();
    var preview = window.GetVisualDescendants().OfType<Vex.Modules.Workspace.Views.MarkdownPreviewView>().Single();
    var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
    for (var pass = 0; pass < 3; pass++)
    {
        foreach (var direction in new[] { 1, -1 })
        {
            var bar = scroll.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().First(b => b.Orientation == Avalonia.Layout.Orientation.Vertical && b.IsEffectivelyVisible);
            var thumb = bar.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().First(t => t.IsEffectivelyVisible);
            Check(bar.Bounds.Width == 16 && thumb.Bounds.Width >= 10, $"Changelog scrollbar remains easy to hit: track={bar.Bounds.Width}, thumb={thumb.Bounds.Width}");
            var from = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
            var barOrigin = bar.TranslatePoint(default, window)!.Value;
            var to = new Point(from.X, barOrigin.Y + (direction > 0 ? bar.Bounds.Height - 18 : 18));
            window.MouseDown(from, MouseButton.Left); Pump();
            for (var step = 1; step <= 50; step++) { window.MouseMove(from + (to - from) * (step / 50d), RawInputModifiers.LeftMouseButton); Pump(); }
            window.MouseUp(to, MouseButton.Left); Pump(); SettleRendering();
            Console.WriteLine($"SCROLL drag {pass}/{direction}: offset={scroll.Offset}, extent={scroll.Extent}, viewport={scroll.Viewport}");
            Check(direction > 0 ? scroll.Offset.Y > scroll.Viewport.Height : scroll.Offset.Y < 1, "Actual scrollbar dragging moves through the document and returns to the top");
        }
    }
    Capture(window, "changelog-after-thumb-drag");

    void Drag(Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left); Pump();
        for (var i = 1; i <= 16; i++) { window.MouseMove(from + (to - from) * (i / 16d), RawInputModifiers.LeftMouseButton); Pump(); }
        window.MouseUp(to, MouseButton.Left); Pump();
    }
}

void VerifyDocumentWindows(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    var launcher = (VerificationDocumentWindows)app.Resolve<IDocumentWindowLauncher>();
    var initialMarkdown = vm.Markdown;
    vm.Markdown = "# 主窗口尚未保存\n\n打开帮助文档不能替换本窗口。";
    var original = vm.GetCurrentDocumentSnapshot();
    foreach (var (topic, file) in new[] { ("changelog", "更新日志.md"), ("thanks", "鸣谢.md") })
    {
        Pump(vm.Help.OpenHelpTopic(topic));
        Check(launcher.Paths.Last() == BundledDocuments.GetPath(file), "Help launches the packaged document: " + topic);
        Check(vm.GetCurrentDocumentSnapshot() == original && vm.DocumentInfo.IsModified, "Launching a document preserves the unsaved parent");
    }
    var aboutTask = app.Resolve<IShellOverlayService>().ShowAboutAsync();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Any());
    var about = (ShellAboutViewModel)window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Single().DataContext!;
    ((System.Windows.Input.ICommand)about.ChangelogCommand).Execute(null); Pump(aboutTask);
    Check(launcher.Paths.Count == 3 && launcher.Paths.Last() == BundledDocuments.GetPath("更新日志.md"), "About uses the same independent document launcher");
    Check(!window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Any(), "About closes before launching the document");
    var copyFolder = Path.Combine(output, "中文 空格文档"); Directory.CreateDirectory(copyFolder);
    var copyPath = Path.Combine(copyFolder, "更新日志 副本.md");
    File.Copy(BundledDocuments.GetPath("更新日志.md"), copyPath, true);
    var start = ShellProcessLauncher.CreateStartInfo(copyPath);
    Check(!start.UseShellExecute && start.ArgumentList.Last() == copyPath, "Document launch targets Vex with a single unescaped path argument");
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        Check(start.ArgumentList.First() == typeof(Vex.App).Assembly.Location, "Development launch invokes the Vex assembly");
    var child = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    if (Path.GetFileNameWithoutExtension(Environment.ProcessPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        child.ArgumentList.Add(typeof(VerificationVexApp).Assembly.Location);
    child.ArgumentList.Add("document-open-child"); child.ArgumentList.Add(copyFolder); child.ArgumentList.Add(copyPath);
    using var process = System.Diagnostics.Process.Start(child)!;
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    Check(process.WaitForExit(60000), "Independent document process exits without affecting the parent");
    Check(process.ExitCode == 0, "Independent document process succeeds: " + stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    Check(File.ReadAllText(Path.Combine(copyFolder, "child-pid.txt")) != Environment.ProcessId.ToString(), "Document workspace runs in a separate process");
    Check(File.ReadAllText(copyPath).EndsWith("独立进程编辑并保存。\n"), "The new process can edit and save an ordinary document");
    Check(vm.GetCurrentDocumentSnapshot() == original && vm.DocumentInfo.IsModified, "Closing the child preserves the parent and its edits");
    vm.Markdown = initialMarkdown;
    Pump(vm.OpenStartupDocumentAsync([BundledDocuments.GetPath("更新日志.md")]));
    vm.Workbench.ShowRead(); Pump(); SettleRendering(); Capture(window, "changelog-workspace");
    Check(vm.Markdown == File.ReadAllText(BundledDocuments.GetPath("更新日志.md")), "Full workspace loads the real packaged changelog");
    vm.Markdown += "\n普通文档允许编辑。";
    Check(vm.DocumentInfo.IsModified, "Bundled documents have no special read-only restriction");
    vm.Markdown = File.ReadAllText(BundledDocuments.GetPath("更新日志.md"));
    Pump(vm.Help.OpenHelpTopic("samples"));
    Check(launcher.Paths.Last() == BundledDocuments.SamplesFolder, "Sample menu launches the real packaged directory");
    Pump(vm.OpenStartupDocumentAsync([BundledDocuments.SamplesFolder]));
    Check(vm.WorkspaceFolderPath == BundledDocuments.SamplesFolder && vm.DocumentFiles.Count == 6, "Opening samples loads all six actual files into the ordinary file tree");
    foreach (var file in vm.DocumentFiles)
        Check(File.Exists(file.Path) && file.Path.StartsWith(BundledDocuments.SamplesFolder), "Sample tree contains packaged files: " + file.Name);
    Pump(vm.OpenStartupDocumentAsync([Path.Combine(BundledDocuments.SamplesFolder, "图文与链接.md")]));
    Check(vm.Markdown.Contains("图片/山间湖畔.png") && File.Exists(Path.Combine(BundledDocuments.SamplesFolder, "图片", "山间湖畔.png")), "Sample image resolves from the published document directory without a network");
    vm.Workbench.ShowRead(); Pump(); SettleRendering(); Capture(window, "packaged-samples");
    Console.WriteLine("PASS: document menu and about routes, process arguments, independent editable workspace and parent preservation");
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
        new DocumentFile(Path.Combine(sampleFolder, "root.md"), "root.md", "MarkdownSamples", "09-28", "Root document"),
        new DocumentFile(Path.Combine(sampleFolder, ".config", "hidden.md"), "hidden.md", ".config", "", "Hidden document"),
        new DocumentFile(Path.Combine(sampleFolder, "Regression", ".cache", "hidden.md"), "hidden.md", ".cache", "", "Nested hidden document")
    };
    vm.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(documents, documents[0], sampleFolder));
    Pump();
    Check(!vm.DocumentFileNodes[0].IsExpanded && !vm.DocumentFileNodes[0].Children.First(node => node.IsFolder).IsExpanded,
        "New workspace root and child folders start collapsed");
    void ExpandFixture(IEnumerable<DocumentFileNode> nodes)
    {
        foreach (var node in nodes.Where(node => node.IsFolder)) { node.IsExpanded = true; ExpandFixture(node.Children); }
    }
    ExpandFixture(vm.DocumentFileNodes); Pump();
    var tree = window.GetVisualDescendants().OfType<TreeView>().Single(t => t.Classes.Contains("side-tree"));
    var filesView = tree.FindAncestorOfType<Vex.Modules.Shell.Views.ShellFilesView>()!;
    var searchBox = filesView.FindControl<Border>("FileSearchBox")!;
    var treeLeft = tree.TranslatePoint(default, filesView)!.Value.X;
    var searchLeft = searchBox.TranslatePoint(default, filesView)!.Value.X;
    Check(Math.Abs(treeLeft - 5) < 0.1 && Math.Abs(searchLeft - treeLeft) < 0.1,
        "File tree and search box share an exact 5 px left inset");
    Check(Math.Abs(tree.Bounds.Width - searchBox.Bounds.Width) < 0.1,
        "File tree and search box right edges also align");
    var items = tree.GetVisualDescendants().OfType<TreeViewItem>().Where(i => i.IsEffectivelyVisible).ToArray();
    Check(items.Length == 7, "Vex tree renders the workspace root, sibling and nested documents without dot folders");
    foreach (var selected in items.Where(i => i.IsSelected))
    {
        var row = selected.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot");
        Check(Math.Abs(row.TranslatePoint(default, tree)!.Value.X) < 1
            && Math.Abs(row.Bounds.Width - tree.Bounds.Width) < 1,
            "Selected nested row background spans the entire file tree including indentation");
        Application.Current!.TryGetResource("VexAccentSoftBrush", Application.Current.ActualThemeVariant, out var accent);
        Check(SameBrush(row.Background, accent as IBrush), "Selected row uses the theme selection background");
    }
    foreach (var item in items)
    {
        var card = item.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("file-card"));
        var chevron = item.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
            .First(b => b.Name == "PART_ExpandCollapseChevron");
        var point = card.TranslatePoint(default, tree)!.Value;
        Check(Math.Abs(card.Bounds.Height - 24) < 0.1 && point.X + card.Bounds.Width <= tree.Bounds.Width + 1,
            "Vex tree has compact rows within sidebar bounds: " + ((DocumentFileNode)item.DataContext!).Name);
        Check(Math.Abs(chevron.TranslatePoint(default, tree)!.Value.Y + chevron.Bounds.Height / 2
            - point.Y - card.Bounds.Height / 2) < 1,
            "Vex tree chevron aligns with its own row");
        var icon = card.GetVisualDescendants().OfType<Control>().First(control => control.IsEffectivelyVisible
            && control.Classes.Contains("tree-ico") && control is Image or Avalonia.Controls.Shapes.Path);
        var label = card.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible &&
            (t.Classes.Contains("file-title") || t.Classes.Contains("file-heading")));
        Check(icon.Bounds.Width == 12 && icon.Bounds.Height == 12 && icon.Height <= label.FontSize,
            "Tree icons stay at text size without increasing row height");
        var arrow = chevron.GetVisualDescendants().OfType<PathIcon>().Single(path => path.Name == "PART_ExpandIconPath");
        Check(arrow.Bounds.Width == 8 && arrow.Bounds.Height == 8,
            "Tree expand arrows use compact 8 px icons");
        Check(Math.Abs(icon.TranslatePoint(default, card)!.Value.Y + icon.Bounds.Height / 2
            - label.TranslatePoint(default, card)!.Value.Y - label.Bounds.Height / 2) < 1,
            $"Vex tree icon and title align vertically: {icon.Bounds}/{icon.TranslatePoint(default, card)}; label {label.Bounds}/{label.TranslatePoint(default, card)}");
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
    var rowPositions = items.Select(item => item.GetVisualDescendants().OfType<Border>()
        .First(border => border.Name == "PART_LayoutRoot").TranslatePoint(default, tree)!.Value.Y).Order().ToArray();
    Check(rowPositions.Zip(rowPositions.Skip(1), (previous, next) => next - previous).All(gap => Math.Abs(gap - 24) < 0.1),
        "Tree rows use a consistent 24 px pitch without extra vertical margins");
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
    var folderCard = folder.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("file-card"));
    var rowClick = folderCard.TranslatePoint(new Point(60, 16), window)!.Value;
    window.MouseDown(rowClick, MouseButton.Left); window.MouseUp(rowClick, MouseButton.Left); Pump();
    Check(!folder.IsExpanded && ReferenceEquals(vm.SelectedDocumentFileNode, folder.DataContext),
        $"Single-clicking the workspace folder row selects and collapses it (expanded={folder.IsExpanded}, selection={vm.SelectedDocumentFileNode?.Name}, target={((DocumentFileNode)folder.DataContext!).Name})");
    window.MouseDown(rowClick, MouseButton.Left); window.MouseUp(rowClick, MouseButton.Left); Pump();
    Check(folder.IsExpanded, "Clicking the already selected folder row expands it again");
    var nestedFolder = items.First(item => ((DocumentFileNode)item.DataContext!).Name == "Nested");
    var nestedCard = nestedFolder.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("file-card"));
    var nestedClick = nestedCard.TranslatePoint(new Point(45, 16), window)!.Value;
    window.MouseDown(nestedClick, MouseButton.Left); window.MouseUp(nestedClick, MouseButton.Left); Pump();
    Check(!nestedFolder.IsExpanded && nestedFolder.IsSelected, "Nested folder names respond to single click");
    window.MouseDown(nestedClick, MouseButton.Left); window.MouseUp(nestedClick, MouseButton.Left); Pump();
    Check(nestedFolder.IsExpanded, "Nested folder single-click expands its contents");
    var nestedRow = nestedFolder.GetVisualDescendants().OfType<Border>().First(border => border.Name == "PART_LayoutRoot");
    Check(Math.Abs(nestedRow.TranslatePoint(default, tree)!.Value.X) < 1
        && Math.Abs(nestedRow.Bounds.Width - tree.Bounds.Width) < 1,
        "Selected nested folder background spans the full width");
    var main = (MainWindowViewModel)window.DataContext!;
    foreach (var theme in new[] { "dark", "light" })
    {
        main.Appearance.SelectThemeByKey(theme); Pump();
        Application.Current!.TryGetResource("VexAccentSoftBrush", Application.Current.ActualThemeVariant, out var accent);
        Check(SameBrush(nestedRow.Background, accent as IBrush), "Full-row folder selection follows the theme: " + theme);
        Capture(window, "vex-file-tree-folder-" + theme);
    }
    Capture(window, "vex-file-tree-folder-selected");
    vm.FilterText = "deep";
    // 过滤会重建树容器，等待布局队列完成后再核对实际可见节点。
    PumpUntil(() => tree.GetVisualDescendants().OfType<TextBlock>()
        .Count(t => t.IsEffectivelyVisible && t.Classes.Contains("file-title")) == 1);
    Check(tree.GetVisualDescendants().OfType<TextBlock>().Count(t => t.IsEffectivelyVisible && t.Classes.Contains("file-title")) == 1,
        "Vex file filter retains the matching document and its ancestors");
    vm.FilterText = string.Empty;
    Pump();

    // 真实扫描覆盖两个入口，避免只有 UI 快照过滤正确。
    var workspace = Path.Combine(sampleFolder, "workspace-scan");
    foreach (var relative in new[] { "normal.md", ".note.md", "visible.name/child.md", ".settings/hidden.md", "visible.name/.cache/nested.md" })
    {
        var path = Path.Combine(workspace, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "# " + relative);
    }
    var app = (VerificationVexApp)Application.Current!;
    var scan = app.Resolve<IDocumentService>().OpenFolderPathAsync(workspace); Pump(scan);
    Check(scan.Result.Count == 3 && scan.Result.Any(file => file.Name == ".note.md"),
        "Workspace scan skips all dot directories while retaining dot files and ordinary dotted directories");
    Pump(main.OpenStartupDocumentAsync([workspace])); Pump();
    Check(vm.DocumentFileNodes.Count == 1 && vm.SelectedDocumentFileNode is { IsFolder: true } root
        && root.Path == workspace && !root.IsExpanded,
        "Opening a folder selects its collapsed workspace root after document loading");
    var rootItem = tree.GetVisualDescendants().OfType<TreeViewItem>().First(item => item.Level == 0);
    Check(rootItem.IsKeyboardFocusWithin, "Opened workspace root receives keyboard focus");
    var rootCard = rootItem.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("file-card"));
    var rootClick = rootCard.TranslatePoint(new Point(70, 16), window)!.Value;
    window.MouseDown(rootClick, MouseButton.Left); window.MouseUp(rootClick, MouseButton.Left); Pump();
    Check(rootItem.IsExpanded && vm.DocumentFileNodes[0].Children.Where(node => node.IsFolder).All(node => !node.IsExpanded),
        "First folder click reveals only the next level, with child folders still collapsed");
    Pump(main.RefreshFilesAsync()); Pump();
    Check(tree.GetVisualDescendants().OfType<TreeViewItem>().First(item => item.Level == 0).IsExpanded,
        "Refreshing preserves a manually expanded workspace root");
    rootItem = tree.GetVisualDescendants().OfType<TreeViewItem>().First(item => item.Level == 0);
    rootCard = rootItem.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("file-card"));
    rootClick = rootCard.TranslatePoint(new Point(70, 12), window)!.Value;
    window.MouseDown(rootClick, MouseButton.Left); window.MouseUp(rootClick, MouseButton.Left); Pump();
    Pump(main.RefreshFilesAsync()); Pump();
    Check(!tree.GetVisualDescendants().OfType<TreeViewItem>().First(item => item.Level == 0).IsExpanded,
        "Refreshing preserves the actual collapsed workspace container");
    Pump(main.OpenStartupDocumentAsync([workspace])); Pump();
    Check(!tree.GetVisualDescendants().OfType<TreeViewItem>().First(item => item.Level == 0).IsExpanded,
        "Explicitly reopening a workspace keeps its actual root container collapsed");
    Pump(main.RefreshFilesAsync()); Pump();
    Check(vm.SelectedDocumentFileNode?.Path == workspace, "Refreshing retains the selected workspace folder");
    vm.ToggleSort(); Pump();
    Check(vm.SelectedDocumentFileNode?.Path == workspace && tree.SelectedItem == vm.SelectedDocumentFileNode,
        "Sorting retains selection on the displayed workspace root");
    vm.ToggleSort(); Pump();
    var directFolder = Path.Combine(sampleFolder, "direct-file-scan-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(directFolder, ".hidden"));
    var directFile = Path.Combine(directFolder, "visible.md");
    File.WriteAllText(directFile, "# Visible");
    File.WriteAllText(Path.Combine(directFolder, ".hidden", "hidden.md"), "# Hidden");
    Pump(main.OpenPathAsync(directFile)); Pump();
    Check(main.DocumentFiles.Count == 1 && vm.SelectedDocumentFileNode?.Path == directFile,
        "Direct file opening applies the same hidden-folder rule and selects the opened document");
    var otherFile = Path.Combine(directFolder, "other.md");
    File.WriteAllText(otherFile, "# Other");
    Pump(main.RefreshFilesAsync()); Pump();
    vm.SelectedDocumentFileNode = vm.DocumentFileNodes[0];
    main.Markdown += "\nUnsaved tree selection regression";
    vm.SelectedDocumentFileNode = vm.DocumentFileNodes[0].Children.First(node => node.Path == otherFile);
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Any());
    ((ShellUnsavedDialogViewModel)window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUnsavedDialogView>().Single().DataContext!).Cancel();
    Pump();
    Check(main.DocumentInfo.CurrentFilePath == directFile && vm.SelectedDocumentFileNode?.Path == directFile
        && main.DocumentInfo.IsModified, "Canceling a file switch after folder selection restores the actual document selection");
    Pump(main.SaveAsync());
    var emptyFolder = Path.Combine(sampleFolder, "empty-workspace");
    Directory.CreateDirectory(emptyFolder);
    Pump(main.OpenStartupDocumentAsync([emptyFolder])); Pump();
    Check(vm.DocumentFileNodes.Count == 1 && vm.DocumentFileNodes[0].Children.Count == 0
        && vm.SelectedDocumentFileNode?.Path == emptyFolder,
        "Empty workspace still displays and selects its root folder");
    Capture(window, "vex-file-tree-empty-root");
}

void MaximizePerformanceWindow(Window window)
{
    var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
    var scale = screen?.Scaling ?? 1;
    // Headless 记录最大化状态，同时显式采用屏幕工作区尺寸，避免只改变状态而保持小窗口。
    window.Width = screen is null ? 1920 : screen.WorkingArea.Width / scale;
    window.Height = screen is null ? 1080 : screen.WorkingArea.Height / scale;
    window.WindowState = WindowState.Maximized;
    Pump(); SettleRendering();
    Check(window.WindowState == WindowState.Maximized && window.Bounds.Width >= 1280,
        $"Performance window is maximized: {window.Bounds.Width} x {window.Bounds.Height}, scaling {window.RenderScaling}");
}

void VerifyMediaLayout(Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    var sample = Path.Combine(output, "adaptive.md");
    File.WriteAllText(Path.Combine(output, "diagram.svg"), """
        <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="450" viewBox="0 0 1200 450">
        <rect width="1200" height="450" fill="#f7f7f8"/><path d="M350 225H850" stroke="#5b54d6" stroke-width="5"/>
        <rect x="80" y="100" width="300" height="250" rx="16" fill="#e4e2fa"/><rect x="820" y="100" width="300" height="250" rx="16" fill="#e4e2fa"/>
        <text x="155" y="235" font-size="36">Markdown</text><text x="920" y="235" font-size="36">Vex</text></svg>
        """);
    using (var bitmap = new SKBitmap(240, 120))
    {
        using var canvas = new SKCanvas(bitmap); canvas.Clear(SKColors.Lavender);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(Path.Combine(output, "small.png")); data.SaveTo(stream);
    }
    var pipe = """
        ## 输出服务

        | 编号 | 功能说明 | 实现要点 |
        | --- | --- | --- |
        | 1 | 显示操作信息 | 应用服务发布结构化操作消息，包含时间、用户、文档和结果。长说明在有限的正文宽度内自动换行，编号和功能名称保留适合内容的宽度，调整窗口后各列仍应保持完整可读。阅读、源码预览和即时编辑均使用一致的内容布局。 |
        | 2 | 显示语法校验结果 | 保留错误规则、级别及定位信息。 |
        | 3 | 显示异常信息 | 界面保持可继续操作。 |
        """;
    var html = """
        ## HTML 表格

        <table><tr><th>编号</th><th>功能说明</th><th>实现要点</th></tr><tr><td>1</td><td>显示操作信息</td><td>应用服务发布结构化操作消息，包含时间、用户、文档和结果。长说明在有限的正文宽度内自动换行，编号和功能名称保留适合内容的宽度，调整窗口后各列仍应保持完整可读。阅读、源码预览和即时编辑均使用一致的内容布局。</td></tr><tr><td>2</td><td>显示语法校验结果</td><td>保留错误规则、级别及定位信息。</td></tr><tr><td>3</td><td>显示异常信息</td><td>界面保持可继续操作。</td></tr></table>
        """;
    Pump(vm.OpenPathAsync(WriteSample(pipe)));
    var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
    window.WindowState = WindowState.Normal;
    foreach (var width in new[] { 1380, 980 })
    foreach (var theme in new[] { "light", "dark" })
    foreach (var layout in new[] { "read", "source-render", "instant" })
    {
        window.Width = width; window.Height = 850;
        vm.Appearance.SelectThemeByKey(theme); vm.Workbench.SelectLayout(layout); Pump(); SettleRendering();
        var host = layout == "instant" ? (Control)instant : preview;
        foreach (var (kind, source) in new[] { ("pipe", pipe), ("html", html) })
        {
            vm.Markdown = source; Pump(); SettleRendering();
            PumpUntil(() => host.GetVisualDescendants().OfType<Grid>().Any(grid => grid.Classes.Contains("MdTable")));
            var grid = host.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("MdTable"));
            var documentWidth = DocumentWidth(grid);
            Check(Math.Abs(grid.Bounds.Width - documentWidth) < 3,
                $"{width} {theme} {layout} {kind}: table fills document width ({grid.Bounds.Width:F1}/{documentWidth:F1})");
            var headers = grid.Children.OfType<Border>().Where(cell => cell.Classes.Contains("MdTableHeaderCell")).ToArray();
            var cells = grid.Children.OfType<Border>().Where(cell => cell.Classes.Contains("MdTableCell")).ToArray();
            Check(headers.Length == 3 && headers.All(cell => cell.Background is ISolidColorBrush), "Header has a theme-aware background");
            Check(cells.Where(cell => Grid.GetRow(cell) == 2).All(cell => cell.Background is ISolidColorBrush)
                && cells.Where(cell => Grid.GetRow(cell) == 1).All(cell => cell.Background is null), "Body alternates row backgrounds");
            var borderColor = ((ISolidColorBrush)headers[0].BorderBrush!).Color;
            Check(borderColor == Color.Parse(theme == "dark" ? "#41454E" : "#DFE2E5"), "Table uses neutral borders independent of accent color");
            Capture(window, $"{width}-{theme}-{layout}-{kind}");
        }
        vm.Markdown = "| 编号 | 名称 |\n| --- | --- |\n| 1 | Vex |"; Pump(); SettleRendering();
        var compactTable = host.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("MdTable"));
        Check(compactTable.Bounds.Width > 0 && compactTable.Bounds.Width < DocumentWidth(compactTable) / 2,
            $"{width} {theme} {layout}: short tables keep their natural width as in the prototype");
        foreach (var imageName in new[] { "diagram.svg", "small.png" })
        {
            vm.Markdown = $"## 自适应图片\n\n![图示]({imageName})\n\n图片与正文宽度对齐。"; Pump();
            PumpUntil(() => host.GetVisualDescendants().OfType<Image>().Any(image => image.Classes.Contains("MdImageContent") && image.Width > 0));
            SettleRendering();
            var image = host.GetVisualDescendants().OfType<Image>().Single(image => image.Classes.Contains("MdImageContent"));
            var documentWidth = DocumentWidth(image);
            Check(Math.Abs(image.Bounds.Width - documentWidth) < 3,
                $"{width} {theme} {layout} {imageName}: image fills document width ({image.Bounds.Width:F1}/{documentWidth:F1})");
            Check(Math.Abs(image.Bounds.Height / image.Bounds.Width - (imageName.EndsWith("svg") ? .375 : .5)) < .005,
                "Image keeps its aspect ratio without a fixed height cap");
            Capture(window, $"{width}-{theme}-{layout}-{imageName.Replace('.', '-')}");
        }
    }
    vm.Markdown = pipe; vm.Workbench.SelectLayout("read"); Pump();
    vm.Workbench.StartPresentation(); Pump(); SettleRendering();
    var presentationGrid = preview.GetVisualDescendants().OfType<Grid>().Single(grid => grid.Classes.Contains("MdTable"));
    Check(Math.Abs(presentationGrid.Bounds.Width - DocumentWidth(presentationGrid)) < 3, "Presentation uses the same full-width table layout");
    Capture(window, "presentation"); vm.Workbench.StopPresentation(); Pump();

    string WriteSample(string text) { File.WriteAllText(sample, text); return sample; }

    // 阅读与即时编辑共用正文块宿主，以其可用宽度排除外围留白。
    double DocumentWidth(Control control) => control.GetVisualAncestors()
        .OfType<CodeWF.Avalonia.Controls.Controls.WindowedStackPanel>().First().Bounds.Width;
}

void VerifyHtmlTables(Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    var path = @"D:\github\company\shxskj\S-FlowC\docs\S-FlowC建模调试客户端_RELAP技术实现方案v1.3.md";
    var source = File.ReadAllText(path);
    var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
    var tables = Markdig.Markdown.Parse(source).OfType<Markdig.Syntax.HtmlBlock>()
        .Where(block => block.Lines.ToString().TrimStart().StartsWith("<table>", StringComparison.OrdinalIgnoreCase)).ToArray();
    var expected = new[] { "MOD-COMBO-01", "从模型库拖出形成新的图元", "属性分为三类", "编辑态：初始生成", "用户名和密码管理" };
    MaximizePerformanceWindow(window); vm.Appearance.SelectThemeByKey("light");
    Pump(vm.OpenPathAsync(path));
    var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
    foreach (var layout in new[] { "read", "instant" })
    {
        vm.Workbench.SelectLayout(layout); Pump(); SettleRendering();
        var host = layout == "read" ? (Control)preview : instant;
        for (var i = 0; i < tables.Length; i++)
        {
            CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(tables[i].Line + 1));
            Pump(); SettleRendering();
            Capture(window, $"relap-html-{layout}-{i + 1}");
            var cell = host.GetVisualDescendants().OfType<SelectableTextBlock>()
                .FirstOrDefault(text => (text.Text ?? text.Inlines?.Text)?.Contains(expected[i], StringComparison.Ordinal) == true);
            Check(cell is not null && cell.GetVisualAncestors().OfType<Grid>().Any(grid => grid.Classes.Contains("MdTable")),
                $"RELAP {layout} HTML table {i + 1} contains its actual cell text in a native table");
            var origin = cell!.TranslatePoint(default, host)!.Value;
            Check(new Rect(origin, cell.Bounds.Size).Intersects(new Rect(host.Bounds.Size)),
                $"RELAP {layout} navigation places HTML table {i + 1} inside the viewport");
        }
    }
    vm.Workbench.StartPresentation(); Pump();
    var index = vm.Workbench.PresentationSections.Select((section, index) => (section, index))
        .First(item => item.section.Title.Contains("4.9.2", StringComparison.Ordinal)).index;
    vm.Workbench.NavigatePresentation(index); Pump(); SettleRendering();
    Check(preview.GetVisualDescendants().OfType<SelectableTextBlock>().Any(text => (text.Text ?? text.Inlines?.Text)?.Contains(expected[0], StringComparison.Ordinal) == true),
        "RELAP content presentation displays the real HTML table cells");
    Capture(window, "relap-html-presentation"); vm.Workbench.StopPresentation(); Pump();
    Check(vm.Markdown == source && !vm.DocumentInfo.IsModified && before.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))),
        "HTML table navigation and presentation preserve the current document and original file");
}

void VerifyPresentation(VerificationVexApp app, Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    const string text = "---\ntitle: Slides\n---\n# **总览**\n\n开场。\n\n## 代码与公式\n\n```cs\n# not a heading\nvar n = 42;\n```\n\n$$\\sum_{n=1}^{10} n$$\n\n参考 [文档][doc] 和脚注[^note]。\n\n第三节\n====\n\n| a | b |\n| --- | --- |\n| 1 | 2 |\n\n[doc]: https://example.com/docs\n[^note]: 解释文字\n";
    var slides = new MarkdownPresentationDocument(text, "测试");
    Check(slides.Sections.Count == 3 && slides.Sections[0].Title == "总览" && slides.Sections[2].Title == "第三节", "Presentation uses actual headings, including Setext, and excludes YAML and fenced code headings");
    var html = Markdig.Markdown.ToHtml(slides.MarkdownAt(1), new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
    File.WriteAllText(Path.Combine(output, "presentation-section.md"), slides.MarkdownAt(1));
    File.WriteAllText(Path.Combine(output, "presentation-section.html"), html);
    Check(html.Contains("https://example.com/docs") && html.Contains("class=\"footnotes\"") && html.Contains("解释文字") && slides.MarkdownAt(1).Contains("$$"), "Section rendering retains external reference definitions, footnotes, code and math");
    Check(new MarkdownPresentationDocument("", "空文档").Sections.Count == 1 && new MarkdownPresentationDocument("没有标题。", "正文").MarkdownAt(0) == "没有标题。", "Empty and heading-free documents remain presentable");
    Check(new MarkdownPresentationDocument("前言\r\n\r\n## 第二页\r\n内容", "序言").Sections.Count == 2, "Presentation preserves a preamble with Windows line endings");
    var first = Path.Combine(output, "演示文档.md"); var second = Path.Combine(output, "替换文档.md");
    File.WriteAllText(first, text); File.WriteAllText(second, "# 替换后的文档\n\n保持当前内容。\n");
    Pump(vm.OpenPathAsync(first)); vm.Workbench.SelectLayout("instant-map"); Pump(); SettleRendering();
    MaximizePerformanceWindow(window);
    var settings = app.Resolve<IAppSettingsStore>().Current;
    var wb = vm.Workbench;
    var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
    var outline = window.FindControl<Vex.Modules.Shell.Views.ShellPresentationOutlineView>("PresentationOutline")!;
    var mapHost = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!;
    var map = mapHost.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
    var initialViewport = map.CaptureViewport();
    for (var round = 0; round < (args.Contains("--anchors-only") ? 0 : 6); round++)
    {
        vm.Appearance.SelectThemeByKey(round % 2 == 0 ? "light" : "dark");
        var prior = app.Resolve<IAppSettingsStore>().Current;
        wb.StartPresentation(); Pump(); SettleRendering();
        PumpUntil(() => preview.CurrentModel.Source == wb.PresentationMarkdown);
        Check(window.WindowState == WindowState.FullScreen && wb.PresentationProgress == "1 / 3" && !wb.CanPreviousPresentation, "Presentation opens full screen at the first section");
        wb.ShowPresentationMap(); Pump(); SettleRendering();
        Check(map.IsEffectivelyVisible && !map.IsChapterEditingEnabled && !mapHost.FindControl<Border>("MapEditingToolbar")!.IsEffectivelyVisible,
            "Mind map presentation shows a read-only graph without chapter editing tools");
        var beforePan = map.CaptureViewport();
        var mapPoint = map.TranslatePoint(new Point(map.Bounds.Width / 2, map.Bounds.Height / 2), window)!.Value;
        window.MouseWheel(mapPoint, new Vector(0, -4)); Pump(); map.ZoomIn(); Pump();
        Check(map.CaptureViewport().Y != beforePan.Y && map.Zoom > beforePan.Zoom, "Mind map presentation supports wheel pan and zoom");
        var zoomBeforeKey = map.Zoom;
        window.KeyPress(Key.Add, RawInputModifiers.None, PhysicalKey.NumPadAdd, null); window.KeyRelease(Key.Add, RawInputModifiers.None, PhysicalKey.NumPadAdd, null); Pump();
        Check(map.Zoom > zoomBeforeKey, "Plus key zooms the presented mind map");
        var mapChapter = Descendants(wb.Root!).Single(node => node.Kind == CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Heading && node.Title == "代码与公式");
        map.SelectNode(mapChapter.Id); map.Focus();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Pump();
        Check(wb.PresentationDocument && wb.PresentationIndex == 1, "Enter on a presented mind map chapter opens its content section");
        wb.ShowPresentationMap(); Pump(); map.SelectNode(wb.Root!.Id); map.Focus();
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); Pump();
        Check(wb.Root.IsCollapsed, "Presentation can collapse map branches without editing text");
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null); Pump();
        Capture(window, "presentation-map-" + round);
        for (var transition = 0; transition < 5; transition++) { wb.ShowPresentationDocument(); Pump(); wb.ShowPresentationMap(); Pump(); }
        wb.TogglePresentationOutline(); Pump(); SettleRendering();
        Check(outline.IsEffectivelyVisible && !window.FindControl<Grid>("WorkspaceColumns")!.IsEffectivelyVisible, "Presentation shows the document outline independently of the editing sidebar");
        var chapter = outline.GetVisualDescendants().OfType<Button>().Single(button => button.Tag is int index && index == 1);
        var point = chapter.TranslatePoint(new Point(chapter.Bounds.Width / 2, chapter.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Pump(); SettleRendering();
        PumpUntil(() => preview.CurrentModel.Source == wb.PresentationMarkdown);
        Check(wb.PresentationIndex == 1 && !wb.PresentationOutline && preview.CurrentModel.Source.Contains("var n = 42"), "Clicking an outline chapter opens its rendered code and math page");
        Capture(window, "presentation-content-" + round);
        var contentOrigin = preview.TranslatePoint(default, window)!.Value;
        var displayedWidth = preview.Bounds.Width * wb.PresentationZoom;
        Check(Math.Abs(contentOrigin.X + displayedWidth / 2 - window.Bounds.Width / 2) < 3, "Presentation content stays centered after zoom");
        window.KeyPress(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null); window.KeyRelease(Key.PageDown, RawInputModifiers.None, PhysicalKey.PageDown, null); Pump();
        Check(wb.PresentationIndex == 2 && !wb.CanNextPresentation, "PageDown advances to the last section");
        wb.NextPresentation(); Check(wb.PresentationIndex == 2, "Last page cannot advance past document bounds");
        for (var i = 0; i < 20; i++) { wb.PreviousPresentation(); Pump(); wb.NextPresentation(); Pump(); }
        wb.TogglePresentationOutline(); Pump(); SettleRendering(); Capture(window, "presentation-outline-" + round);
        wb.StopPresentation(); Pump(); SettleRendering();
        Check(window.WindowState == WindowState.Maximized && wb.WorkspaceLayout == "instant-map" && vm.Markdown == text && !vm.DocumentInfo.IsModified, "Exit restores maximized layout without altering the document");
        Check(app.Resolve<IAppSettingsStore>().Current == prior, "Presentation navigation and display do not overwrite preferences");
        var restored = map.CaptureViewport();
        Check(Math.Abs(restored.Zoom - initialViewport.Zoom) < .001 && Math.Abs(restored.X - initialViewport.X) < 1 && Math.Abs(restored.Y - initialViewport.Y) < 1,
            "Exit restores the original mind map viewport");
        Check(map.IsChapterEditingEnabled && restored.CollapsedIds.SequenceEqual(initialViewport.CollapsedIds), "Exiting presentation restores chapter editing and branch state");
    }
    window.WorkspaceViews.Float("map"); Pump();
    var detached = window.WorkspaceViews.FloatingWindows["map"];
    wb.StartPresentation(); Pump(); wb.StopPresentation(); Pump();
    Check(ReferenceEquals(detached, window.WorkspaceViews.FloatingWindows["map"]) && detached.IsVisible, "Exiting presentation restores the same independent view window");
    window.WorkspaceViews.Return("map"); Pump();
    wb.StartPresentation(); Pump(); Pump(vm.OpenPathAsync(second)); Pump(); wb.StopPresentation(); Pump();
    Check(vm.Markdown == File.ReadAllText(second) && wb.LiveEditor!.Text == vm.Markdown, "Opening a different file during presentation never replays the previous document snapshot");
    Check(File.ReadAllText(first) == text, "Presentation source file stays byte-for-byte unchanged");
    wb.ShowMindMap(); wb.StartPresentation(); Pump();
    Check(wb.PresentationMap && map.IsEffectivelyVisible, "Starting from a mind map opens directly into map presentation");
    wb.StopPresentation(); Pump();
    var readingFile = Path.Combine(output, "阅读位置.md");
    var readingText = "# 阅读位置\n\n" + string.Join("\n\n", Enumerable.Range(0, 100).Select(i => $"阅读段落 {i}，退出演示回到原处。"));
    File.WriteAllText(readingFile, readingText); Pump(vm.OpenPathAsync(readingFile)); wb.SelectLayout("read"); Pump(); SettleRendering();
    PumpUntil(() => preview.CurrentModel.Source == readingText);
    var readingScroll = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!.FindControl<ScrollViewer>("PreviewScrollViewer")!;
    readingScroll.Offset = new Vector(0, 1200); Pump();
    var priorOffset = readingScroll.Offset.Y;
    var priorReading = preview.SaveReadingPosition();
    wb.StartPresentation(); Pump(); SettleRendering(); wb.IncreasePresentationText(); Pump();
    wb.ShowPresentationMap(); Pump(); wb.StopPresentation(); Pump(); SettleRendering();
    Console.WriteLine($"READING before={priorOffset} {priorReading}, after={readingScroll.Offset.Y} {preview.SaveReadingPosition()}");
    Check(Math.Abs(readingScroll.Offset.Y - priorOffset) < 5, "Exiting presentation restores the actual reading position");
    var relapPath = Path.Combine(output, "long-html-tables.md");
    var relapText = "# 长文档与 HTML 表格\n\n" + string.Join("\n\n", Enumerable.Range(1, 150).Select(i =>
        $"## 章节 {i}\n\n用于验证阅读锚点、虚拟化和演示返回的本地正文。\n\n- 第一个项目\n- 第二个项目" +
        (i % 30 == 0 ? $"\n\n<table><tr><th>章节</th><th>内容</th><th>状态</th><th>备注</th></tr><tr><td>{i}</td><td>本地测试</td><td>有效</td><td>四列表格</td></tr></table>" : "")));
    File.WriteAllText(relapPath, relapText);
    var htmlTables = Markdig.Markdown.Parse(relapText).OfType<Markdig.Syntax.HtmlBlock>()
        .Where(block => block.Lines.ToString().TrimStart().StartsWith("<table>", StringComparison.OrdinalIgnoreCase)).ToArray();
    Check(htmlTables.Length == 5, "Portable long-document fixture includes five HTML tables");
    foreach (var table in htmlTables)
    {
        var tableViewer = new CodeWF.Avalonia.Markdown.Controls.MarkdownViewer();
        var rendered = ((CodeWF.Avalonia.Markdown.Rendering.IMarkdownRenderContext)tableViewer).ConvertBlock(table, relapText);
        Check(rendered is Border { Child: Grid grid } && grid.Classes.Contains("MdTable") && grid.ColumnDefinitions.Count == 4,
            "HTML table uses native cells through the registered application renderer");
    }
    Check(CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg.MarkdownBlockParser.Write(
        CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg.MarkdownBlockParser.Parse(relapText)) == relapText,
        "Instant editing preserves the original HTML source on round trip");
    Pump(vm.OpenPathAsync(relapPath)); wb.SelectLayout("read"); Pump(); SettleRendering();
    PumpUntil(() => preview.CurrentModel.Source == relapText);
    for (var round = 0; round < 3; round++)
    {
        for (var step = 0; step < 30; step++) { readingScroll.Offset = new Vector(0, readingScroll.Offset.Y + 600); Pump(); }
        SettleRendering();
        var anchor = preview.SaveReadingPosition()!.Value;
        wb.StartPresentation(); Pump(); SettleRendering();
        wb.NavigatePresentation(30); Pump(); wb.ShowPresentationMap(); Pump();
        wb.StopPresentation(); Pump(); SettleRendering();
        PumpUntil(() => preview.CurrentModel.Source == relapText);
        var restored = preview.SaveReadingPosition()!.Value;
        Console.WriteLine($"LONG-DOCUMENT-READING {round}: {anchor} -> {restored}");
        Check(anchor.BlockIndex == restored.BlockIndex && Math.Abs(anchor.BlockRatio - restored.BlockRatio) < .05,
            "Deep reading anchor survives content/map presentation and return");
        Check(preview.RealizedBlockCount < 100, "Returning to the long document keeps offscreen blocks virtualized");
    }
    static IEnumerable<CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode> Descendants(CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode node)
    { yield return node; foreach (var child in node.Children) foreach (var descendant in Descendants(child)) yield return descendant; }
}

void VerifyStress(Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    var folder = args.ElementAtOrDefault(2) ?? @"D:\github\md\mds";
    var rounds = int.TryParse(args.ElementAtOrDefault(3), out var count) ? count : 2;
    var paths = Directory.GetFiles(folder, "Vex-0*.md").Order().Append(@"D:\github\company\shxskj\S-FlowC\docs\S-FlowC建模调试客户端_RELAP技术实现方案v1.3.md").ToArray();
    Check(paths.Length >= 6 && paths.All(File.Exists), "Stress suite includes five rich local samples and the original RELAP document");
    if (args.FirstOrDefault(arg => arg.StartsWith("--sample=")) is { } sample) paths = paths.Where(path => Path.GetFileName(path).StartsWith(sample[9..], StringComparison.OrdinalIgnoreCase)).ToArray();
    var contents = paths.ToDictionary(path => path, File.ReadAllText);
    var hashes = paths.ToDictionary(path => path, path => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
    var rows = new List<object>();
    var presentations = new List<object>();
    var retained = new List<object>();
    MaximizePerformanceWindow(window);
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
    var source = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.FindControl<MarkdownEditorView>("MarkdownEditor")!;
    var previewHost = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
    var preview = previewHost.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
    var map = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
    var watch = System.Diagnostics.Stopwatch.StartNew();
    for (var round = 0; round < rounds; round++)
    {
        vm.Appearance.SelectThemeByKey(round % 2 == 0 ? "light" : "dark");
        foreach (var path in round % 2 == 0 ? paths : paths.Reverse())
        {
            if (args.Contains("--parse-trace"))
            {
                var tree = Markdig.Markdown.Parse(contents[path], new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().UseYamlFrontMatter().UsePreciseSourceLocation().Build());
                Console.WriteLine("AST " + string.Join(" | ", tree.Select(block => $"{block.GetType().Name}:{block.Span}")));
                CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg.MarkdownBlockParser.Parse(contents[path]);
            }
            watch.Restart(); Pump(vm.OpenPathAsync(path));
            PumpUntil(() => vm.Workbench.ImageBasePath == path && vm.Workbench.Root is not null);
            var openMs = watch.Elapsed.TotalMilliseconds;
            if (vm.Markdown != contents[path] || vm.DocumentInfo.CurrentFilePath != path)
            {
                Console.WriteLine($"OPEN actual={vm.DocumentInfo.CurrentFilePath}, requested={path}, chars={vm.Markdown.Length}/{contents[path].Length}");
                Console.WriteLine("VISIBLE " + string.Join(" | ", window.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text).Where(text => !string.IsNullOrEmpty(text))));
                File.WriteAllText(Path.Combine(output, "unexpected-document.md"), vm.Markdown);
            }
            Check(vm.Markdown == contents[path] && Path.GetFullPath(vm.DocumentInfo.CurrentFilePath!) == Path.GetFullPath(path), "File switch loads exactly the selected document: " + Path.GetFileName(path));
            watch.Restart(); vm.Workbench.StartPresentation(); Pump(); SettleRendering();
            var presentationEnterMs = watch.Elapsed.TotalMilliseconds;
            var presentationSteps = new List<double>();
            foreach (var index in new[] { 0, vm.Workbench.PresentationSections.Count / 2, vm.Workbench.PresentationSections.Count - 1, 0 })
            {
                watch.Restart(); vm.Workbench.NavigatePresentation(index); Pump(); SettleRendering();
                PumpUntil(() => preview.CurrentModel.Source == vm.Workbench.PresentationMarkdown);
                presentationSteps.Add(watch.Elapsed.TotalMilliseconds);
                Check(vm.Markdown == contents[path] && !vm.DocumentInfo.IsModified, "Presentation paging preserves the whole document");
                var slideScroll = previewHost.FindControl<ScrollViewer>("PreviewScrollViewer")!;
                slideScroll.Offset = new Vector(0, slideScroll.Extent.Height); Pump(); slideScroll.Offset = default; Pump();
            }
            vm.Workbench.TogglePresentationOutline(); Pump(); SettleRendering();
            Check(window.FindControl<Vex.Modules.Shell.Views.ShellPresentationOutlineView>("PresentationOutline")!.IsEffectivelyVisible, "Large document has a presentation outline");
            TracePresentation("outline");
            var pageCount = vm.Workbench.PresentationSections.Count;
            for (var transition = 0; transition < 6; transition++)
            {
                vm.Workbench.ShowPresentationMap(); Pump();
                Check(map.IsEffectivelyVisible && !map.IsChapterEditingEnabled, "Large mind map presentation remains read-only");
                var mapPoint = map.TranslatePoint(new Point(map.Bounds.Width / 2, map.Bounds.Height / 2), window)!.Value;
                window.MouseWheel(mapPoint, new Vector(0, -4)); Pump(); map.ZoomIn(); Pump(); map.ZoomOut(); Pump();
                vm.Workbench.ShowPresentationOutline(); Pump();
                vm.Workbench.ShowPresentationDocument(); Pump(); TracePresentation("transition-" + transition);
            }
            TracePresentation("before-exit"); vm.Workbench.StopPresentation(); TracePresentation("exit-queued"); Pump(); SettleRendering(); TracePresentation("after-exit");
            presentations.Add(new { round = round + 1, file = Path.GetFileName(path), sections = pageCount, enterMs = presentationEnterMs, pageStepsMs = presentationSteps });
            Check(window.WindowState == WindowState.Maximized, "Presentation restores maximized performance window");
            foreach (var option in round % 2 == 0 ? WorkbenchLayoutOption.All : WorkbenchLayoutOption.All.Reverse())
            {
                watch.Restart(); vm.Workbench.SelectLayout(option.Id); Pump();
                PumpUntil(() => (!option.Render || preview.CurrentModel.Source == contents[path]) && (option.Editor != "instant" || instant.Text == contents[path]));
                var layoutMs = watch.Elapsed.TotalMilliseconds;
                Check(!(TopLevel.GetTopLevel(source) is not null && source.IsEffectivelyVisible)
                    || !(TopLevel.GetTopLevel(instant) is not null && instant.IsEffectivelyVisible), "Source and instant editors stay exclusive during stress");
                if (option.Editor == "source") Check(source.Text == contents[path], "Visible source matches current file");
                if (option.Editor == "instant") Check(instant.Text == contents[path], "Visible instant editor matches current file");
                if (option.Render) Check(preview.CurrentModel.Source == contents[path], "Visible reading view matches current file");
                var stepTimes = new List<double>();
                var hosts = new List<ScrollViewer>();
                if (option.Editor == "instant") hosts.Add(instant.GetVisualDescendants().OfType<ScrollViewer>().First());
                if (option.Editor == "source") hosts.Add(source.GetVisualDescendants().OfType<ScrollViewer>().First(scroll => scroll.Extent.Height > scroll.Viewport.Height));
                if (option.Render) hosts.Add(previewHost.FindControl<ScrollViewer>("PreviewScrollViewer")!);
                foreach (var scroll in hosts)
                {
                    foreach (var direction in new[] { 1, -1, 1 })
                    {
                        var bar = scroll.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
                            .First(bar => bar.Orientation == Avalonia.Layout.Orientation.Vertical && bar.IsEffectivelyVisible);
                        var thumb = bar.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().First();
                        var from = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
                        var origin = bar.TranslatePoint(default, window)!.Value;
                        var to = new Point(from.X, origin.Y + (direction > 0 ? bar.Bounds.Height - 2 : 2));
                        window.MouseDown(from, MouseButton.Left); Pump();
                        for (var step = 1; step <= 10; step++)
                        {
                            watch.Restart(); window.MouseMove(from + (to - from) * (step / 10d), RawInputModifiers.LeftMouseButton); Pump();
                            stepTimes.Add(watch.Elapsed.TotalMilliseconds);
                        }
                        window.MouseUp(to, MouseButton.Left); Pump();
                    }
                }
                if (option.Map)
                {
                    var viewport = map.CaptureViewport();
                    map.ZoomIn(); Pump(); map.ZoomOut(); Pump(); map.SetZoom(viewport.Zoom); Pump();
                    var point = map.TranslatePoint(new Point(map.Bounds.Width / 2, map.Bounds.Height / 2), window)!.Value;
                    for (var pan = 0; pan < 6; pan++)
                    {
                        window.MouseWheel(point, new Vector(0, -4)); Pump();
                    }
                    Check(Math.Abs(map.CaptureViewport().Y - viewport.Y) > 100, "Mind map wheel pans the viewport during repeated layout changes");
                    map.RestoreViewport(viewport); Pump();
                    var nodes = CountNodes(vm.Workbench.Root);
                    if (nodes >= 200) Check(map.RealizedNodeCount < nodes / 2, "Long mind map creates only cards near the viewport");
                }
                Check(vm.Markdown == contents[path] && !vm.DocumentInfo.IsModified, "Repeated scrolling and view changes preserve the current document");
                foreach (var panel in window.GetVisualDescendants().OfType<CodeWF.Avalonia.Controls.Controls.WindowedStackPanel>().Where(panel => panel.IsEffectivelyVisible && panel.ItemCount > 100))
                    Check(panel.RealizedItemCount < panel.ItemCount / 2, $"Long document keeps virtualization enabled: {panel.RealizedItemCount}/{panel.ItemCount}");
                stepTimes.Sort();
                rows.Add(new { round = round + 1, file = Path.GetFileName(path), layout = option.Id, openMs, layoutMs,
                    pointerSteps = stepTimes.Count, pointerP95Ms = stepTimes.Count == 0 ? 0 : stepTimes[(int)Math.Floor((stepTimes.Count - 1) * .95)],
                    pointerMaxMs = stepTimes.Count == 0 ? 0 : stepTimes[^1], privateMiB = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576d });
                SaveReport(); Console.WriteLine("STRESS " + System.Text.Json.JsonSerializer.Serialize(rows[^1]));
            }
        }
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); Pump();
        retained.Add(new { round = round + 1, managedMiB = GC.GetTotalMemory(false) / 1048576d, privateMiB = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576d });
        SaveReport();
    }
    // 将三种视图迁移到独立窗口，切文件后继续操作，再切到即时编辑与导图。
    for (var pass = 0; pass < 2; pass++)
    {
        vm.Workbench.SelectLayout("three"); Pump();
        window.WorkspaceViews.Separate(); window.WorkspaceViews.Float("editor"); Pump();
        Check(window.WorkspaceViews.FloatingWindows.Count == 3, "Large-document source, reading and map views can use three windows");
        foreach (var floating in window.WorkspaceViews.FloatingWindows.Values) MaximizePerformanceWindow(floating);
        foreach (var path in paths)
        {
            Pump(vm.OpenPathAsync(path));
            PumpUntil(() => preview.CurrentModel.Source == contents[path] && vm.Workbench.ImageBasePath == path);
            Check(source.Text == contents[path] && window.WorkspaceViews.FloatingWindows.Values.All(view => view.Title.Contains(Path.GetFileName(path))),
                "Switching large documents replaces every detached view and title");
            foreach (var floating in window.WorkspaceViews.FloatingWindows.Values)
            {
                floating.MouseWheel(new Point(floating.Bounds.Width / 2, floating.Bounds.Height / 2), new Vector(0, -8)); Pump();
            }
        }
        vm.Workbench.SelectLayout("instant-map"); Pump();
        Check(window.WorkspaceViews.FloatingWindows.Count == 2 && !source.IsEffectivelyVisible && instant.IsEffectivelyVisible,
            "Detached views switch to instant editing without a second source editor");
        foreach (var id in window.WorkspaceViews.FloatingWindows.Keys.ToArray()) window.WorkspaceViews.Return(id);
        Pump();
    }
    // 不等待前一份文档完成读取，模拟在文件列表中快速改变目标。
    for (var i = 0; i < 10; i++)
    {
        var pending = paths.Select(path => vm.OpenPathAsync(path)).ToArray(); Pump(Task.WhenAll(pending));
        PumpUntil(() => vm.Workbench.ImageBasePath == paths[^1]);
        Check(vm.DocumentInfo.CurrentFilePath == paths[^1] && vm.Markdown == contents[paths[^1]], "Rapid file switching keeps the last requested document");
    }
    foreach (var path in paths) Check(hashes[path] == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))), "Original sample remains unchanged: " + Path.GetFileName(path));
    Capture(window, "stress-final"); SaveReport();

    static int CountNodes(CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode? node) =>
        node is null ? 0 : 1 + node.Children.Sum(CountNodes);

    void TracePresentation(string stage)
    {
        if (!args.Contains("--trace-presentation")) return;
        Console.WriteLine("PRESENTATION " + stage + " " + System.Text.Json.JsonSerializer.Serialize(new {
            privateMiB = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576d,
            panels = preview.GetVisualDescendants().OfType<CodeWF.Avalonia.Controls.Controls.WindowedStackPanel>().Select(panel => new { panel.ItemCount, panel.RealizedItemCount, panel.IsVirtualizing, scroll = panel.ScrollHost?.Name, viewport = panel.ScrollHost?.Viewport.ToString() }).ToArray() }));
    }

    void SaveReport() => File.WriteAllText(Path.Combine(output, "stress.json"), System.Text.Json.JsonSerializer.Serialize(new
    {
        environment = "Windows Release net10.0, Avalonia.Headless + Skia; pointer costs include layout and render jobs, not desktop FPS",
        windowState = window.WindowState.ToString(), width = window.Bounds.Width, height = window.Bounds.Height, scaling = window.RenderScaling, paths, hashes, rows, presentations, retained
    }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
}

void VerifyMermaidWorkspace(Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    window.Width = 1280; window.Height = 820;
    var file = Path.Combine(BundledDocuments.SamplesFolder,"Mermaid 图表图鉴.md");
    var text = File.ReadAllText(file);
    var originalHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file));
    var previewView = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
    var preview = previewView.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
    Border[] Diagrams(Control host) => host.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("MdMermaid")).ToArray();
    bool Ready(Control host) => Diagrams(host).Any(border =>
        (border.Child is Image { Source: not null })                       // 旧结构：Border > Image
        || border.GetVisualDescendants().OfType<Image>().Any(image => image.Source is not null)); // 新结构：Border > Grid(源码卡+预览+徽标) > Image
    vm.Workbench.SelectLayout("source"); Pump();
    var previousPreview = preview.Markdown;
    var timer = System.Diagnostics.Stopwatch.StartNew(); Pump(vm.OpenPathAsync(file)); Pump();
    var sourceFrameMs = timer.Elapsed.TotalMilliseconds;
    Check(preview.Markdown == previousPreview && preview.Markdown != text,"Hidden reading view keeps its old snapshot without parsing the diagram document");
    timer.Restart(); vm.Workbench.SelectLayout("instant"); Pump();
    var instantFrameMs = timer.Elapsed.TotalMilliseconds;
    PumpUntil(() => Ready(instant)); var instantFirstDiagramMs = timer.Elapsed.TotalMilliseconds;
    var initialDiagrams = Diagrams(instant).Length;
    if (!args.Contains("--measure-only")) Check(initialDiagrams < 22,"First screen does not instantiate all 22 diagrams");
    Capture(window,"mermaid-full-instant");
    timer.Restart();vm.Workbench.SelectLayout("read");Pump();
    PumpUntil(() => preview.CurrentModel.Source == text && Ready(preview));
    var readingReadyMs = timer.Elapsed.TotalMilliseconds;
    Check(preview.Markdown == text,"Showing reading view synchronizes the latest document");
    var blocks = Markdown.Parse(text).OfType<Markdig.Syntax.FencedCodeBlock>().Where(block => block.Info?.Trim() == "mermaid").ToArray();
    Check(blocks.Length == 22,"The shipped catalog contains all 22 diagrams");
    var warmSwitchMs = new List<double>();
    vm.Workbench.SelectLayout("instant");Pump();
    for (var pass=0;pass<3;pass++)
    {
        vm.Markdown="# 另一篇\n\n切换验证。\n";Pump();timer.Restart();vm.Markdown=text;Pump();
        warmSwitchMs.Add(timer.Elapsed.TotalMilliseconds);PumpUntil(()=>Ready(instant));
    }
    if (args.Contains("--measure-only"))
    {
        var measurements = new {sourceFrameMs,instantFrameMs,instantFirstDiagramMs,readingReadyMs,initialDiagrams,warmSwitchMs};
        File.WriteAllText(Path.Combine(output,"mermaid-performance.json"),System.Text.Json.JsonSerializer.Serialize(measurements));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(measurements));return;
    }
    var checks = 0;
    foreach (var theme in new[]{"light","dark"})
    {
        vm.Appearance.SelectThemeByKey(theme);Pump();
        foreach (var layout in new[]{"read","instant"})
        {
            vm.Workbench.SelectLayout(layout);Pump();
            foreach (var pair in blocks.Select((block,index)=>(block,index)))
            {
                // Isolate each real fence so a neighbouring diagram cannot hide a renderer failure.
                var source = text.Substring(pair.block.Span.Start,pair.block.Span.Length);
                var document = "# 图表验证\n\n"+source+"\n";
                vm.Markdown = document;Pump();
                var host = layout == "read" ? (Control)preview : instant;
                PumpUntil(() => (layout == "read" ? preview.CurrentModel.Source == document : instant.Editor.Text == document) && Ready(host) && !host.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("MdMermaidFallback")));
                foreach (var border in Diagrams(host))
                {
                    if (border.Child is not Image { Source: not null } image) continue;
                    Check(image.Bounds.Width <= border.Bounds.Width+1 && image.Bounds.Height>0,$"{theme}/{layout}/{pair.index}: complete diagram fits its block");
                }
                Check(!host.GetVisualDescendants().OfType<ScrollViewer>().Any(scroll => scroll.IsEffectivelyVisible && scroll.HorizontalScrollBarVisibility != Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled && scroll.Extent.Width > scroll.Viewport.Width+1),$"{theme}/{layout}/{pair.index}: no diagram scrollbar");
                if (pair.index is 0 or 15 or 16 or 19 or 20 or 21) Capture(window,$"mermaid-{theme}-{layout}-{pair.index}");
                checks++;
            }
        }
    }
    vm.Workbench.SelectLayout("source");Pump();vm.Markdown=text;Pump();
    vm.Workbench.SelectLayout("read");PumpUntil(()=>preview.CurrentModel.Source==text);
    Check(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(originalHash),"Rendering never rewrites the shipped catalog");
    var report = new {sourceFrameMs,instantFrameMs,instantFirstDiagramMs,readingReadyMs,initialDiagrams,warmSwitchMs,checks};
    File.WriteAllText(Path.Combine(output,"mermaid-performance.json"),System.Text.Json.JsonSerializer.Serialize(report));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
}

void VerifyPerformance(Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    MaximizePerformanceWindow(window);
    var path = args.ElementAtOrDefault(3) ?? @"D:\github\company\shxskj\S-FlowC\docs\S-FlowC建模调试客户端_RELAP技术实现方案v1.3.md";
    var layout = args.ElementAtOrDefault(2) ?? "instant";
    var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
    vm.Workbench.SelectLayout(layout); Pump();
    var watch = System.Diagnostics.Stopwatch.StartNew(); Pump(vm.OpenPathAsync(path)); Pump();
    var frameMs = watch.Elapsed.TotalMilliseconds;
    PumpUntil(() => vm.Workbench.ImageBasePath == path && vm.Workbench.Root is not null);
    var mapMs = watch.Elapsed.TotalMilliseconds;
    var after = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
    Check(before.SequenceEqual(after), "Performance sample remains byte-for-byte unchanged");
    var diagrams = window.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("MdMermaid")).ToArray();
    var report = new { windowState = window.WindowState.ToString(), width = window.Bounds.Width, height = window.Bounds.Height, scaling = window.RenderScaling, layout, frameMs, allViewsReadyMs = mapMs, diagrams = diagrams.Length, visibleDiagrams = diagrams.Count(border => border.IsEffectivelyVisible), bytes = new FileInfo(path).Length, characters = vm.Markdown.Length,
        sha256 = Convert.ToHexString(after), memoryMiB = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576d,
        environment = "Windows Release net10.0, Avalonia.Headless + Skia, cold process per layout" };
    File.WriteAllText(Path.Combine(output, layout + ".json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report));
    Capture(window, layout);
}

void VerifySingleDocumentWorkspace(VerificationVexApp app, Vex.Modules.Shell.Views.MainWindow window, MainWindowViewModel vm)
{
    var wb = vm.Workbench;
    Check(MarkdownTableInsertion.AtSelection("before\r\n\r\nafter", 10, 0, "| A |\n| --- |") == "| A |\r\n| --- |\r\n\r\n",
        "Table insertion preserves Windows line endings and existing blank lines");
    var folder = Path.Combine(output, "documents"); Directory.CreateDirectory(folder);
    var first = Path.Combine(folder, "章节写作.md"); var second = Path.Combine(folder, "另一篇文档.md");
    const string original = "# 写作计划\n\n点击文字，保留 **加粗**、*斜体* 和 [链接](https://codewf.com)。\n\n## 研究\n\n研究正文，不得丢失。\n\n### 资料\n\n```md\n# 围栏中的标题\n```\n\n## 方案\n\n方案正文。\n\n## 验证\n\n验证正文。\n";
    File.WriteAllText(first, original); File.WriteAllText(second, "# 第二篇\n\n这是替换后的文档。\n");
    Pump(vm.OpenPathAsync(first)); PumpUntil(() => wb.Root?.Title == "写作计划");
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
    var source = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.FindControl<MarkdownEditorView>("MarkdownEditor")!;
    wb.SelectLayout("instant"); Pump(); SettleRendering();
    var initialScroll = instant.Editor.GetVisualDescendants().OfType<ScrollViewer>().First();
    Check(initialScroll.Offset.Y < 1, "Opening a document starts at the first block without anchor drift");
    var mapHost = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!;
    var map = mapHost.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
    foreach (var option in WorkbenchLayoutOption.All)
    {
        wb.ShowLayouts = true; wb.SelectLayout(option.Id); Pump();
        Check(wb.ShowLayouts && (!wb.IsInstantEditing || !wb.IsSourceVisible), "Layout stays open and editors remain exclusive: " + option.Id);
        Check((TopLevel.GetTopLevel(instant) is not null && instant.IsEffectivelyVisible) == (option.Editor == "instant") && (TopLevel.GetTopLevel(source) is not null && source.IsEffectivelyVisible) == (option.Editor == "source")
            && (TopLevel.GetTopLevel(map) is not null && map.IsEffectivelyVisible) == option.Map, $"Actual view visibility follows layout: {option.Id}; instant={instant.IsEffectivelyVisible}, source={source.IsEffectivelyVisible}, map={map.IsEffectivelyVisible}");
        if (vm.Markdown != original) File.WriteAllText(Path.Combine(output, "unexpected-markdown.md"), vm.Markdown);
        Check(vm.Markdown == original, "Layout preserves the original Markdown: " + option.Id);
    }
    wb.ShowLayouts = false; wb.SelectLayout("instant"); Pump(); SettleRendering();
    var paragraph = instant.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>().First(viewer => viewer.Markdown?.StartsWith("点击文字", StringComparison.Ordinal) == true).GetVisualDescendants().OfType<SelectableTextBlock>().First();
    var beforePoint = paragraph.TranslatePoint(default, window)!.Value; var beforeSize = paragraph.Bounds.Size;
    Capture(window, "instant-before-click");
    var click = paragraph.TranslatePoint(new Point(32, 12), window)!.Value;
    window.MouseDown(click, MouseButton.Left); window.MouseUp(click, MouseButton.Left); Pump();
    var box = instant.GetVisualDescendants().OfType<TextBox>().Single(text => text.IsEffectivelyVisible);
    var presenter = box.GetVisualDescendants().OfType<TextPresenter>().Single();
    var afterPoint = presenter.TranslatePoint(default, window)!.Value;
    Check(Math.Abs(beforePoint.Y - afterPoint.Y) < 1 && Math.Abs(beforeSize.Height - presenter.TextLayout.Height) < 1,
        $"Instant click preserves text geometry: before={beforePoint}/{beforeSize}; after={afterPoint}/{presenter.TextLayout.Height}");
    Check(vm.Markdown == original && box.Text!.Contains("**加粗**"), "Click inserts a caret without rewriting formatted Markdown");
    var baselineHeight = presenter.TextLayout.Height; var baselineWidth = presenter.TextLayout.Width;
    box.SelectionStart = 0; box.SelectionEnd = box.Text!.Length; Pump();
    Check(Math.Abs(presenter.TextLayout.Height - baselineHeight) < 1, "Rich text selection preserves line geometry");
    box.CaretIndex = box.Text.IndexOf("加粗", StringComparison.Ordinal) + 1; box.SelectionStart = box.SelectionEnd = box.CaretIndex;
    presenter.PreeditText = "中文"; Pump();
    Check(presenter.TextLayout.Width <= baselineWidth + presenter.FontSize * 2.1, "IME preedit keeps Markdown syntax hidden and rich formatting in place");
    presenter.PreeditText = null; Pump();
    Capture(window, "instant-after-click");
    box.CaretIndex = box.Text.Length; box.SelectionStart = box.SelectionEnd = box.CaretIndex;
    window.KeyTextInput("继续写作"); Pump();
    Check(vm.Markdown.Contains("继续写作") && vm.Markdown.Contains("**加粗**"), "Native input preserves inline formatting and syncs the document");
    wb.SelectLayout("source-map"); wb.UndoDocument(); Pump();
    Check(vm.Markdown == original && source.Text == original, "Undo spans instant editing and source/map layouts");
    wb.SelectLayout("instant"); Pump();
    var codeViewer = instant.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
        .Single(viewer => viewer.Markdown?.StartsWith("```md", StringComparison.Ordinal) == true);
    var codeText = codeViewer.GetVisualDescendants().OfType<SelectableTextBlock>().Single(text => text.Classes.Contains("MdCodeBlockText"));
    codeViewer.BringIntoView(); Pump();
    var codePoint = codeText.TranslatePoint(default, window)!.Value; var codeHeight = codeViewer.Bounds.Height;
    codeText.SelectionStart = 0; codeText.SelectionEnd = 4; Pump();
    var codeClick = codeText.TranslatePoint(new Point(20, 8), window)!.Value;
    window.MouseDown(codeClick, MouseButton.Left); window.MouseUp(codeClick, MouseButton.Left); Pump();
    var codeBox = instant.GetVisualDescendants().OfType<TextBox>().Single(text => text.IsEffectivelyVisible && !text.Classes.Contains("MdCodeLanguageInput"));
    var codeLanguage = instant.GetVisualDescendants().OfType<TextBox>().Single(text => text.IsEffectivelyVisible && text.Classes.Contains("MdCodeLanguageInput"));
    var codePresenter = codeBox.GetVisualDescendants().OfType<TextPresenter>().Single();
    Check(codeText.SelectionStart == codeText.SelectionEnd && codeBox.SelectionStart == codeBox.SelectionEnd,
        "Entering code editing removes the read-only selection and leaves only a caret");
    Check(Math.Abs(codePresenter.TranslatePoint(default, window)!.Value.Y - codePoint.Y) < 1 && Math.Abs(codeViewer.Bounds.Height - codeHeight) < 1,
        "Code editing preserves its text origin, frame height and syntax highlight");
    var codeFrame = codeText.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("MdCodeBlock"));
    Check(codeLanguage.TranslatePoint(default, window)!.Value.Y >= codeFrame.TranslatePoint(new Point(0, codeFrame.Bounds.Height), window)!.Value.Y,
        "The language input appears below the code frame during editing");
    Check(!codeViewer.GetVisualDescendants().OfType<Control>().Any(control => control.Classes.Contains("MdCodeBlockHeader") && control.IsEffectivelyVisible),
        "Code has no visible top language bar or copy button");
    var languageClick = codeLanguage.TranslatePoint(new Rect(codeLanguage.Bounds.Size).Center, window)!.Value;
    window.MouseDown(languageClick, MouseButton.Left); window.MouseUp(languageClick, MouseButton.Left); Pump();
    Check(codeLanguage.IsKeyboardFocusWithin, "The language input is fully inside its hit-test area and can be clicked");
    codeBox.Focus(); Pump();
    window.KeyTextInput("原位"); Pump();
    Check(vm.Markdown.Contains("原位") && vm.Markdown.Contains("```md"), "Code input edits the fenced body without exposing fence markers");
    wb.UndoDocument(); Pump(); Check(vm.Markdown == original, "Shared undo restores inline code edits");
    wb.SelectLayout("source"); Pump(); source.Editor.TextArea.Focus(); source.Editor.CaretOffset = 0;
    window.KeyTextInput("撤销验证"); Pump();
    var beforeFilterUndo = vm.Markdown;
    Check(beforeFilterUndo != original, "Source typing joins document history");
    var fileFilter = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellFilesView>().Single()
        .GetVisualDescendants().OfType<TextBox>().Single();
    fileFilter.Focus(); Pump(); window.KeyTextInput("临时筛选"); Pump();
    window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
    window.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z"); Pump();
    Check(vm.Markdown == beforeFilterUndo && string.IsNullOrEmpty(fileFilter.Text), "Filter undo edits its query without undoing document content");
    source.Editor.TextArea.Focus(); Pump(); window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
    window.KeyRelease(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z"); Pump();
    Check(vm.Markdown == original && source.Text == original, "Source shortcut restores shared document history after filter undo");
    foreach (var editorLayout in new[] { "source", "instant" })
    {
        wb.SelectLayout(editorLayout); Pump();
        if (editorLayout == "source")
        {
            source.Editor.Select(vm.Markdown.IndexOf("方案正文", StringComparison.Ordinal), 2);
            source.FocusEditor();
        }
        else
        {
            instant.Editor.NavigateToSourceLine(1 + vm.Markdown[..vm.Markdown.IndexOf("方案正文", StringComparison.Ordinal)].Count(c => c == '\n'), beginEdit: true);
            Pump();
            var paragraphBox = instant.GetVisualDescendants().OfType<TextBox>().Single(text => text.IsEffectivelyVisible);
            paragraphBox.SelectionStart = 0; paragraphBox.SelectionEnd = 2;
            fileFilter.Focus(); // 菜单夺走焦点后也要保留正文选区。
        }
        Pump();
        var tableBefore = vm.Markdown;
        var tableSelection = editorLayout == "source" ? source.Selection : instant.Editor.Selection;
        vm.EditorActions.InsertAction(EditorActionKind.Table);
        PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellInsertTableOverlayView>().Any(view => view.IsEffectivelyVisible));
        var tableDialog = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellInsertTableOverlayView>().Single(view => view.IsEffectivelyVisible);
        var tableRows = tableDialog.FindControl<NumericUpDown>("TableRows")!;
        var tableColumns = tableDialog.FindControl<NumericUpDown>("TableColumns")!;
        Check(tableRows.Value == 3 && tableColumns.Value == 3 && vm.Markdown == tableBefore,
            $"{editorLayout} table command opens dimensions without modifying the document: rows={tableRows.Value}, columns={tableColumns.Value}, unchanged={vm.Markdown == tableBefore}");
        var tableModel = (Vex.Modules.Shell.Views.ShellInsertTableModel)tableDialog.DataContext!;
        tableModel.Rows = null; Pump();
        Check(!tableDialog.FindControl<Button>("ConfirmTable")!.IsEnabled, "Missing row count disables table insertion: " + editorLayout);
        tableModel.Rows = 3;
        tableDialog.FindControl<Button>("CancelTable")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(vm.Markdown == tableBefore && (editorLayout == "source" ? source.Selection : instant.Editor.Selection) == tableSelection,
            editorLayout + " cancel preserves document and original selection");
        vm.EditorActions.InsertAction(EditorActionKind.Table);
        PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellInsertTableOverlayView>().Any(view => view.IsEffectivelyVisible));
        tableDialog = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellInsertTableOverlayView>().Single(view => view.IsEffectivelyVisible);
        tableDialog.FindControl<NumericUpDown>("TableRows")!.Value = 4;
        tableDialog.FindControl<NumericUpDown>("TableColumns")!.Value = 3;
        Capture(window, "insert-table-" + editorLayout);
        tableDialog.FindControl<Button>("ConfirmTable")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
        var expectedTable = MarkdownTableInsertion.Create(new TableDimensions(4, 3), n => "列 " + n);
        var expectedInsertion = MarkdownTableInsertion.AtSelection(tableBefore, tableSelection.Start, tableSelection.Length, expectedTable);
        Check(vm.Markdown == tableBefore.Remove(tableSelection.Start, tableSelection.Length).Insert(tableSelection.Start, expectedInsertion),
            editorLayout + " inserts exactly header plus four content rows and three columns at the saved selection");
        Check(instant.Editor.GetVisualDescendants().OfType<Grid>().Any(grid => grid.GetType().Name == "MarkdownTableGrid" && grid.RowDefinitions.Count == 5 && grid.ColumnDefinitions.Count == 3) || editorLayout == "source",
            editorLayout + " table is rendered as an editable table");
        wb.UndoDocument(); Pump();
        Check(vm.Markdown == tableBefore, editorLayout + " table insertion is a single shared undo step");
    }
    wb.SelectLayout("map"); SelectChapter("研究");
    var beforeDrag = vm.Markdown;
    map.Fit(.5); Pump();
    var draggedNode = wb.SelectedNode!;
    var targetNode = Flatten(wb.Root).Single(node => node.Title == "方案");
    var fromBounds = map.GetNodeBounds(draggedNode.Id)!.Value;
    var targetBounds = map.GetNodeBounds(targetNode.Id)!.Value;
    var dragFrom = map.TranslatePoint(fromBounds.Center, window)!.Value;
    var dragTo = map.TranslatePoint(new Point(targetBounds.Center.X, targetBounds.Bottom - targetBounds.Height * .1), window)!.Value;
    window.MouseDown(dragFrom, MouseButton.Left); window.MouseMove(dragTo); window.MouseUp(dragTo, MouseButton.Left); Pump();
    Check(vm.Markdown.IndexOf("## 研究", StringComparison.Ordinal) > vm.Markdown.IndexOf("## 方案", StringComparison.Ordinal)
        && vm.Markdown.Contains("研究正文") && vm.Markdown.Contains("### 资料"), "Pointer drag moves the whole chapter after its sibling");
    wb.UndoDocument(); PumpUntil(() => vm.Markdown == beforeDrag && Flatten(wb.Root).Any(node => node.Title == "研究")); SelectChapter("研究");
    map.Focus(); window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null); Pump();
    Check(mapHost.FindControl<Border>("ChapterDeleteLayer")!.IsVisible && vm.Markdown == original, "Delete opens confirmation before changing a chapter");
    mapHost.FindControl<CheckBox>("KeepChapterContent")!.IsChecked = true;
    mapHost.FindControl<Button>("ConfirmDeleteChapter")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
    Check(!vm.Markdown.Contains("## 研究") && vm.Markdown.Contains("研究正文") && vm.Markdown.Contains("### 资料"), "Deleting only the title preserves its body and descendant headings");
    wb.UndoDocument(); PumpUntil(() => Flatten(wb.Root).Any(node => node.Title == "研究")); SelectChapter("研究");
    map.RevealNode(wb.SelectedNode!.Id); Pump(); map.Focus(); window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, null); Pump();
    var rename = mapHost.FindControl<TextBox>("InlineChapterTitle")!;
    Check(rename.IsEffectivelyVisible && rename.IsFocused, "F2 places the native title editor over the selected map node");
    rename.Text = "不应提交"; window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump();
    Check(!rename.IsEffectivelyVisible && vm.Markdown == original, "Escape cancels inline chapter rename before global shortcuts");
    map.Focus(); window.KeyPress(Key.F2, RawInputModifiers.None, PhysicalKey.F2, null); Pump(); rename.Text = "键盘改名"; window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Pump();
    Check(vm.Markdown.Contains("## 键盘改名"), "Native map keyboard rename writes Markdown");
    wb.UndoDocument(); PumpUntil(() => Flatten(wb.Root).Any(n => n.Title == "研究")); SelectChapter("研究");
    var beforeFastEdit = vm.Markdown;
    vm.Markdown = "前置正文。\n\n" + vm.Markdown;
    wb.CommitChapterTitle("快速调研");
    Check(vm.Markdown.Contains("## 快速调研") && vm.Markdown.StartsWith("前置正文。"), "Map edit reconciles source offsets before the parse debounce completes");
    vm.Markdown = beforeFastEdit; PumpUntil(() => Flatten(wb.Root).Any(n => n.Title == "研究")); SelectChapter("研究");
    wb.CommitChapterTitle("调研"); PumpUntil(() => wb.SelectedNode?.Title == "调研");
    Check(vm.Markdown.Contains("## 调研") && vm.DocumentInfo.IsModified, "Map rename changes real Markdown and marks the document modified");
    wb.CommitChapterBody("新的调研正文。\n\n- 保留 Markdown"); PumpUntil(() => wb.ChapterBody.Contains("新的调研正文"));
    Check(vm.Markdown.Contains("### 资料") && vm.Markdown.Contains("# 围栏中的标题"), "Editing chapter body preserves children and fenced code");
    wb.ExecuteChapter("down"); PumpUntil(() => wb.SelectedNode?.Title == "调研" && wb.SelectedNode.SourceStart > vm.Markdown.IndexOf("## 方案", StringComparison.Ordinal));
    Check(vm.Markdown.IndexOf("## 方案", StringComparison.Ordinal) < vm.Markdown.IndexOf("## 调研", StringComparison.Ordinal)
        && vm.Markdown.Contains("新的调研正文") && vm.Markdown.Contains("### 资料"), "Reordering moves the entire chapter with body and children");
    wb.ExecuteChapter("demote"); PumpUntil(() => wb.SelectedNode?.HeadingLevel == 3);
    Check(vm.Markdown.Contains("#### 资料"), "Demoting adjusts all descendant heading levels");
    wb.ExecuteChapter("promote"); PumpUntil(() => wb.SelectedNode?.HeadingLevel == 2);
    wb.ExecuteChapter("child"); PumpUntil(() => wb.SelectedNode?.Title == WorkbenchText.Get("WSNewChapter"));
    Check(wb.SelectedNode!.HeadingLevel == 3, "Map creates an actual child heading");
    var withNew = vm.Markdown; wb.DeleteChapter(false); Pump(); wb.UndoDocument(); Pump();
    Check(vm.Markdown == withNew, "Chapter deletion can be undone across the shared history");
    wb.SelectLayout("three"); window.WorkspaceViews.Separate(); Pump();
    Check(window.WorkspaceViews.FloatingWindows.Count == 2, "Render and map detach into native windows");
    window.WorkspaceViews.Float("editor"); Pump();
    Check(window.WorkspaceViews.FloatingWindows.Count == 3 && ReferenceEquals(window.WorkspaceViews.FloatingWindows["editor"].DataContext, vm), "Three native windows share one document model");
    var edits = vm.Markdown + "\n多窗口同步。\n"; vm.Markdown = edits; Pump();
    Check(source.Text == edits, "Detached source follows document edits");
    Pump(vm.SaveAsync()); Pump(vm.OpenPathAsync(second)); PumpUntil(() => wb.Root?.Title == "第二篇");
    Check(source.Text.Contains("第二篇") && window.WorkspaceViews.FloatingWindows.Values.All(floating => floating.Title.Contains("另一篇文档")), "Switching files replaces every detached view and its window title");
    var floats = window.WorkspaceViews.FloatingWindows.ToDictionary(item => item.Key, item => item.Value);
    var history = wb.History.Capture(); var layout = wb.WorkspaceLayout;
    window.BeginOnboardingGuide(); Pump();
    var guide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    Check(window.WorkspaceViews.FloatingWindows.Count == 0 && floats.Values.All(floating => !floating.IsVisible), "Guide suspends detached windows without destroying them");
    for (var step = 0; step < 6; step++) { guide.ShowStep(step); Pump(); SettleRendering(); Capture(window, "guide-" + (step + 1)); }
    guide.Finish(); Pump();
    Check(vm.Markdown.Contains("第二篇") && wb.WorkspaceLayout == layout && wb.History.Capture().Current == history.Current
        && floats.All(pair => ReferenceEquals(window.WorkspaceViews.FloatingWindows[pair.Key], pair.Value)), "Guide restores the document, layout, shared history and original windows");
    foreach (var id in window.WorkspaceViews.FloatingWindows.Keys.ToArray()) window.WorkspaceViews.Return(id);
    Pump();
    VerifyChapterRanges();
    var aboutTask = app.Resolve<IShellOverlayService>().ShowAboutAsync();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Any());
    Capture(window, "about-light");
    var about = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Single();
    var aboutModel = (ShellAboutViewModel)about.DataContext!;
    Check(aboutModel.PlatformText == ".NET 10，Avalonia" && aboutModel.CopyrightText == "Copyright © 2026 CodeWF · 维刻 Vex"
        && about.GetVisualDescendants().OfType<Button>().Count(button => button.Command == aboutModel.ReleaseCommand) == 1
        && about.GetVisualDescendants().OfType<Button>().Count(button => button.Command == aboutModel.WebsiteCommand || button.Command == aboutModel.RepositoryCommand) == 2,
        "About uses accepted metadata, one release badge and both product links");
    vm.Appearance.SelectThemeByKey("dark"); Pump(); Capture(window, "about-dark");
    aboutModel.Close(); Pump(aboutTask); vm.Appearance.SelectThemeByKey("light");
    Pump(vm.OpenStartupDocumentAsync([BundledDocuments.GetPath("鸣谢.md")]));
    vm.Workbench.ShowRead(); Pump(); SettleRendering(); Capture(window, "thanks");
    Check(vm.DocumentInfo.CurrentFilePath == BundledDocuments.GetPath("鸣谢.md"), "Thanks uses the ordinary workspace document flow");
    var performanceFile = args.ElementAtOrDefault(2) ?? @"D:\github\company\shxskj\S-FlowC\docs\S-FlowC建模调试客户端_RELAP技术实现方案v1.3.md";
    if (File.Exists(performanceFile))
    {
        var sampleHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(performanceFile));
        var watch = System.Diagnostics.Stopwatch.StartNew(); Pump(vm.OpenPathAsync(performanceFile));
        PumpUntil(() => wb.ImageBasePath == performanceFile && wb.Root?.Title != "第二篇");
        var openMs = watch.Elapsed.TotalMilliseconds; Console.WriteLine($"PERFORMANCE OPEN {openMs:F1} ms");
        var measurements = new List<object>();
        foreach (var option in WorkbenchLayoutOption.All)
        {
            var values = new List<double>();
            for (var repeat = 0; repeat < 3; repeat++)
            {
                wb.SelectLayout(option.Id == "read" ? "source" : "read"); Pump();
                watch.Restart(); wb.SelectLayout(option.Id); Pump(); values.Add(watch.Elapsed.TotalMilliseconds);
            }
            Console.WriteLine($"PERFORMANCE {option.Id}: {string.Join(",", values.Select(value => value.ToString("F1")))} ms");
            measurements.Add(new { layout = option.Id, firstMs = values[0], medianMs = values.Order().ElementAt(1), maximumMs = values.Max() });
        }
        var report = new { document = performanceFile, bytes = new FileInfo(performanceFile).Length, characters = vm.Markdown.Length,
            openMs, memoryMiB = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 / 1048576d,
            environment = "Windows, Release net10.0, Avalonia.Headless+Skia; synchronous layout through next rendered frame", layouts = measurements };
        File.WriteAllText(Path.Combine(output, "performance.json"), System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PERFORMANCE " + System.Text.Json.JsonSerializer.Serialize(report));
        Capture(window, "relap-three");
        Check(sampleHash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(performanceFile))), "RELAP performance sample remains unchanged");
    }
    void SelectChapter(string title) { PumpUntil(() => Flatten(wb.Root).Any(node => node.Title == title)); wb.SelectedNode = Flatten(wb.Root).First(node => node.Title == title); Pump(); }
    static IEnumerable<CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode> Flatten(CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNode? node)
    { if (node is null) yield break; yield return node; foreach (var child in node.Children) foreach (var item in Flatten(child)) yield return item; }
    void VerifyChapterRanges()
    {
        const string crlf = "---\r\ntitle: demo\r\n---\r\n\r\n根标题\r\n======\r\n\r\n## 第一章\r\n\r\n正文 A\r\n\r\n### 子节\r\n\r\n~~~md\r\n## 代码标题\r\n~~~\r\n\r\n## 第二章\r\n\r\n正文 B\r\n";
        var doc = new MarkdownChapterDocument(crlf);
        Check(doc.Chapters.Count == 4, "Chapter AST supports Setext and excludes YAML and fenced headings");
        var moved = doc.Move(doc.Chapters[1], doc.Chapters[3], "after").Text;
        Check(moved.Contains("~~~md\r\n## 代码标题\r\n~~~") && moved.Contains("### 子节\r\n") && !moved.Replace("\r\n", "").Contains('\n'), "Moving chapters preserves CRLF and code byte content");
        var kept = doc.Delete(doc.Chapters[1], true);
        Check(!kept.Contains("## 第一章") && kept.Contains("正文 A") && kept.Contains("### 子节"), "Delete-title-only preserves body and descendants");
        Check(doc.Move(doc.Chapters[1], doc.Chapters[2], "child").Text == crlf, "A chapter cannot be moved into its own subtree");
        var deep = new MarkdownChapterDocument("# A\n\n##### B\n\n###### C\n");
        var rejected = false; try { deep.Move(deep.Chapters[1], deep.Chapters[2], "after"); } catch (InvalidOperationException) { rejected = true; }
        // 自身子树目标不进行移动；超出 H6 另用非子树目标校验。
        deep = new MarkdownChapterDocument("# A\n\n##### B\n\n###### C\n\n## Other\n\n###### D\n");
        try { deep.Move(deep.Chapters[1], deep.Chapters[4], "child"); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Chapter movement refuses to truncate heading depth beyond H6");
    }
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
    var typographyGallery = window.FindControl<Vex.Modules.Shell.Views.ShellTypographyGallery>("TypographyGallery")!;
    var simpleCard = typographyGallery.GetVisualDescendants().OfType<Button>().SingleOrDefault(b => b.Name == "Typography_Simple");
    Check(simpleCard is not null
        && simpleCard.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
            .Any(viewer => viewer.Markdown.Contains(app.Resolve<IAppLocalizer>().Get(Vex.VexL.TypographySimple))),
        "Typography cards use the same localized names as the menu");
    vm.Workbench.ShowTypography = false;
    var preferences = app.Resolve<IAppSettingsStore>().Current;
    var sourceDocument = window.GetVisualDescendants().OfType<MarkdownEditorView>().Single().Editor.Document;
    var mapControl = map.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
    mapControl.SetZoom(.85);
    ((Vex.Modules.Shell.Views.MainWindow)window).BeginOnboardingGuide(); Pump();
    var guide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    // 六幕演示引导：开场卡 →「继续」进入第一幕（打字机运行中）→ 跳幕到 Mermaid 幕（基线文档确定性铺设）→ 结束还原。
    Check(guide.IsVisible && guide.StepIndex == 0, "Interactive guide opens on the intro card");
    guide.NextStep(); Pump();
    Check(guide.IsVisible && guide.StepIndex == 0, "Continuing from the intro starts the first act");
    guide.ShowStep(3); Pump();
    PumpUntil(() => vm.Workbench.Root?.Title == "成都，慢慢走");
    Capture(window, "vex-native-guide-mermaid-act");
    guide.Finish(); Pump();
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

void VerifyPathIdentity(VerificationVexApp app, Window window, MainWindowViewModel vm, string sampleFolder)
{
    var state = app.Resolve<IWorkspaceDocumentState>();
    var markdown = state.Markdown;
    var originalPath = vm.DocumentInfo.CurrentFilePath!;
    var lowerDirectory = Path.Combine(sampleFolder, "case-path");
    var upperDirectory = Path.Combine(sampleFolder, "Case-Path");
    var lowerPath = Path.Combine(lowerDirectory, "same.md");
    var upperPath = Path.Combine(upperDirectory, "same.md");
    Directory.CreateDirectory(lowerDirectory);
    File.WriteAllText(lowerPath, markdown);
    var caseSensitive = !File.Exists(upperPath);
    Directory.CreateDirectory(upperDirectory);
    File.WriteAllText(upperPath, markdown);

    vm.Workbench.ShowInstant(); Pump();
    var live = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
    state.UpdateDocument(markdown, lowerPath); Pump();
    var selection = live.Selection;
    state.UpdateDocument(markdown, upperPath); Pump();
    Check(state.FilePath == upperPath, "Workspace state preserves a path spelling change with identical Markdown");
    Check(live.ImageBasePath == upperPath && live.Text == markdown,
        "Instant synchronization retains case-sensitive image paths for identical Markdown");
    Check(live.Selection == selection, "Updating only the image base path preserves the instant selection");

    if (caseSensitive)
    {
        // 仅在输出文件系统确实支持大小写区分时验证两个真实文件的打开链路。
        vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
            new DocumentFile(lowerPath, "same.md", lowerDirectory, string.Empty, string.Empty), null));
        PumpUntil(() => vm.DocumentInfo.CurrentFilePath == lowerPath);
        vm.ApplyDocumentFileOpenRequested(new DocumentFileOpenRequestedCommand(
            new DocumentFile(upperPath, "same.md", upperDirectory, string.Empty, string.Empty), null));
        PumpUntil(() => vm.DocumentInfo.CurrentFilePath == upperPath);
        Check(state.FilePath == upperPath && live.ImageBasePath == upperPath,
            "Opening real case-distinct files with equal Markdown updates the document and image base path");
        Check(vm.BuildQuickOpenItems().Count(item => item.Path == lowerPath || item.Path == upperPath) == 2,
            "Quick open retains both real case-distinct document paths");
        Pump(vm.OpenPathAsync(originalPath));
    }
    else
    {
        Console.WriteLine("SKIP Real case-distinct file opening: output filesystem is case-insensitive; state and live path spelling checks passed.");
    }
    state.UpdateDocument(markdown, originalPath); Pump();
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
    wb.SelectLayout("instant"); Pump();
    Check(!source.IsEffectivelyVisible && !preview.IsEffectivelyVisible && instant.IsEffectivelyVisible, "Instant mode hides the source and read views");
    Check(instant.Editor.DocumentMaxWidth == 1280 && instant.Editor.EditorFontSize >= 15, "Instant document uses the prototype document width and base font");
    Check(window.FindControl<Vex.Modules.Shell.Views.ShellEditorOptionsView>("EditorOptions")!.Bounds.Height == 42,
        "Editing context row is 42 px above the workspace");
    wb.ShowSource(); Pump();
    Check(source.IsEffectivelyVisible && !preview.IsEffectivelyVisible && Math.Abs(source.Bounds.Width - window.FindControl<Grid>("ViewSurface")!.Bounds.Width) < 2,
        $"The source layout fills the current workspace width: source={source.IsEffectivelyVisible}/{source.Bounds}, preview={preview.IsEffectivelyVisible}, surface={window.FindControl<Grid>("ViewSurface")!.Bounds}, layout={wb.WorkspaceLayout}");
    Check(window.FindControl<Vex.Modules.Shell.Views.ShellEditorOptionsView>("EditorOptions")!.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "TypographyTarget"),
        "Editing options retain the prototype formatting and typography controls");
    wb.ShowRead(); Pump();
    Check(preview.IsEffectivelyVisible && preview.IsReadingMode && !source.IsEffectivelyVisible && !instant.IsEffectivelyVisible
        && !window.FindControl<Vex.Modules.Shell.Views.ShellEditorOptionsView>("EditorOptions")!.IsEffectivelyVisible,
        "Read mode has one read only document without editing toolbar or duplicate preview header");
    app.Resolve<MarkdownEditorViewModel>().ApplySearch(new EditorSearchCommand(EditorSearchAction.Count, "Markdown")); Pump();
    Check(wb.IsReading && vm.Markdown == document, "Counting search matches keeps the reading view and document unchanged");
    wb.ShowInstant(); wb.ShowTypography = true; Pump();
    var gallery = window.FindControl<Control>("TypographyGallery")!;
    Check(gallery.Bounds.Width > 0 && vm.Appearance.TypographyOptions.Count == 12,
        $"Typography gallery exposes all 12 prototype themes: width={gallery.Bounds.Width}, count={vm.Appearance.TypographyOptions.Count}");
    var typography = vm.Appearance.SelectedTypography;
    foreach (var option in vm.Appearance.TypographyOptions)
    {
        vm.Appearance.SelectedTypography = option; Pump();
        Check(instant.Editor.TypographyTheme == option.Key && app.Resolve<MarkdownPreviewViewModel>().TypographyTheme == option.Key && vm.Markdown == document,
            "Typography stays consistent across instant and reading views: " + option.Key);
    }
    // 渲染密度（data-mdsize）才是排版尺寸档来源；界面密度 IsCompactLayout 不再驱动编辑器字号档。
    vm.Appearance.MarkdownDensity = "compact";
    PumpUntil(() => instant.Editor.TypographySize == "Compact");
    Check(instant.Editor.TypographySize == "Compact", "Compact typography size reaches the live editor");
    Check(vm.Markdown == document, "Compact typography applies without rewriting Markdown");
    vm.Appearance.MarkdownDensity = "normal";
    PumpUntil(() => instant.Editor.TypographySize == "Normal");
    Check(instant.Editor.TypographySize == "Normal", "Normal typography size returns to the live editor");
    vm.Appearance.SelectedTypography = typography; wb.ShowTypography = false;
    wb.ShowMapSource = false; wb.ShowMindMap(); Pump(); SettleRendering();
    var oldRoot = wb.Root;
    wb.ShowParagraphs = wb.ShowLists = wb.ShowCode = wb.ShowTables = false;
    PumpUntil(() => !ReferenceEquals(oldRoot, wb.Root));
    Check(Flatten(wb.Root!).All(n => n.Kind is CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Heading or CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Document), "Turning off summaries leaves the heading structure intact");
    wb.ShowRichBlocks = true; Check(wb.ShowCode && wb.ShowTables, "Combined summaries option enables both code and tables");
    wb.ShowRichBlocks = false; Check(!wb.ShowCode && !wb.ShowTables, "Combined summaries option disables both code and tables");
    wb.SummaryLength = 999; Check(wb.SummaryLength == 120, "Summary length is capped at 120 graphemes");
    wb.SummaryLength = -10; Check(wb.SummaryLength == 1, "Summary length is at least one grapheme");
    oldRoot = wb.Root; wb.ResetMapSettings();
    PumpUntil(() => !ReferenceEquals(oldRoot, wb.Root)); Pump();
    Check(wb.SummaryLength == 30 && !wb.ShowParagraphs && !wb.ShowLists && !wb.ShowCode && !wb.ShowTables, "Reset restores every map display default");
    Check(wb.VisibleNodeCount == map.VisibleNodeCount && wb.SummaryPreview.Length > 0, "Map count and summary preview update from the real tree");
    map.CollapseAll(); Pump();
    var heading = Flatten(wb.Root!).LastOrDefault(n => n.Kind == CodeWF.Avalonia.MindView.Markdown.MarkdownMindMapNodeKind.Heading) ?? wb.Root!;
    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(heading.SourceLine)); Pump();
    Check(wb.IsMindMap && map.SelectedNode?.Id == heading.Id && wb.SelectedNode?.Id == heading.Id, "Outline navigation reveals and selects the corresponding map node");
    wb.CloseInspector(); Pump(); Check(map.SelectedNode is null && !host.FindControl<Border>("NodeInspector")!.IsEffectivelyVisible, "Closing full text also clears canvas selection");
    map.SetZoom(.01); Check(Math.Abs(map.Zoom - .15) < .001, "Map zoom respects the lower bound");
    map.SetZoom(50); Check(Math.Abs(map.Zoom - 2.5) < .001, "Map zoom respects the upper bound"); map.Fit();
    wb.ShowMapSource = true; Pump();
    Check(!source.FindControl<MarkdownEditorView>("MarkdownEditor")!.Editor.IsReadOnly, "Source and map allow synchronized editing in both views");
    wb.ShowMapSource = false;
    wb.ShowInstant();
    CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new NavigateToLineCommand(heading.SourceLine)); Pump();
    Check(wb.IsInstantEditing && !source.IsEffectivelyVisible && vm.Markdown == document, "Outline navigation in instant mode scrolls without activating hidden source");
    // ShowStatisticsAsync 的任务在浮层关闭后才完成，这里只触发并泵渲染。
    _ = app.Resolve<ShellStatusBarViewModel>().ShowStatisticsAsync(); Pump();
    Check(window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellStatisticsOverlayView>().Any(v => v.IsVisible),
        "Status bar character count opens the statistics overlay");
    window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump();
    vm.Layout.IsOutlineVisible = false; Pump();
    var shell = (Vex.Modules.Shell.Views.MainWindow)window;
    shell.BeginOnboardingGuide(); Pump();
    var guide = window.FindControl<Vex.Modules.Shell.Views.WorkbenchGuideView>("WorkbenchGuide")!;
    guide.ShowStep(1); Pump();
    Check(guide.SpotlightTargets.Count >= 1, "Guide acts highlight one focus target at a time");
    var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Source = source.FindControl<MarkdownEditorView>("MarkdownEditor")!.Editor.TextArea, Key = Key.Right };
    Check(!guide.HandleKey(key) && guide.StepIndex == 1, "Guide leaves editor arrow keys available for typing");
    vm.Appearance.SelectTypographyByKey("OrangeHeart"); guide.ShowStep(3); Pump();
    Check(guide.SpotlightTargets.Count >= 1, "Mermaid act highlights the source editor");
    guide.Finish(); Pump();
    Check(vm.Appearance.SelectedTypography == typography && vm.Markdown == document, "Guide restores typography and original document after real interactions");
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
    Check(live.DocumentPadding.Bottom == 80 && vm.Markdown == text,
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
        for (var step = 0; step < 6; step++)
        {
            guide.ShowStep(step); Pump(); SettleRendering();
            if (step == 1) Check(guide.SpotlightTargets[0].Top >= 0 && guide.SpotlightTargets[0].Bottom <= guide.Bounds.Height,
                "Guide exposes the file and outline navigation within the window: " + culture);
            var card = guide.GetVisualDescendants().OfType<Border>().Single(b => b.Width == 320);
            if (step == 0) Check(vm.Layout.EnableMotion || card.Transitions is null || card.Transitions.Count == 0,
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
    var menu = window.FindControl<Vex.Modules.Shell.Views.ShellAppearanceMenuView>("AppearanceMenu")!;
    foreach (var palette in appearance.PaletteOptions)
    {
        appearance.SelectedPalette = palette; appearance.ThemeMode = StudioThemeMode.System; Pump();
        foreach (var expected in new[] { StudioThemeMode.Light, StudioThemeMode.Dark, StudioThemeMode.System })
        {
            var content = (Control)((Flyout)menu.AppearanceButton.Flyout!).Content!;
            // 外观菜单已按原型改为 appearance-pill；模式行 = 紧凑密度按钮所在行。
            var modes = content.GetLogicalDescendants().OfType<Button>().Where(b => b.Classes.Contains("appearance-pill")).ToArray();
            Check(modes.Length == 6, "Appearance menu exposes three density and three mode pills");
            var index = expected switch { StudioThemeMode.Light => 3, StudioThemeMode.Dark => 4, _ => 5 };
            modes[index].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
            Check(appearance.ThemeMode == expected && ReferenceEquals(appearance.SelectedPalette, palette), "Title bar cycles mode and preserves palette: " + palette.Key + "/" + expected);
            var settings = ((VerificationVexApp)Application.Current!).Resolve<IAppSettingsStore>().Current;
            Check(settings.ThemeKey == appearance.SelectedTheme!.Key && settings.PaletteKey == palette.Key, "Title bar persists the shared appearance state");
        }
    }
    foreach (var width in new[] { 980, 1280 })
    foreach (var option in appearance.LanguageOptions)
    {
        window.Width = width; menu.LanguageButton.Flyout!.ShowAt(menu.LanguageButton); Pump();
        var languages = (StackPanel)((Flyout)menu.LanguageButton.Flyout).Content!;
        Check(languages.Children.Count == appearance.LanguageOptions.Count, "Language icon opens the complete language menu");
        languages.Children.OfType<Button>().ElementAt(appearance.LanguageOptions.ToList().IndexOf(option))
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Pump();
        Check(CodeWF.Toolkit.Lang.I18nManager.Instance.Culture.Name == option.CultureName, "Title bar language follows the single global manager");
        var left = (Control)((CodeWF.Avalonia.Controls.Controls.CodeWFWindow)window).LeftContent!;
        var right = window.FindControl<StackPanel>("TitleBarAppearance")!;
        Check(left.TranslatePoint(new Point(left.Bounds.Width, 0), window)!.Value.X <= right.TranslatePoint(default, window)!.Value.X,
            "Title bar menus and appearance controls do not overlap: " + width + "/" + option.CultureName);
    }
    appearance.SelectedTheme = originalTheme; appearance.SelectedPalette = originalPalette; appearance.SelectedLanguage = originalLanguage;
    window.Width = 1280; Pump();
}

void VerifyWorkspaceLayout(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    app.Resolve<IWorkspaceDocumentState>().UpdateDocument("""
        # Layout sample

        A readable paragraph and a compact table.

        | API | Description |
        | --- | --- |
        | Read | A longer description that wraps naturally in a narrow window. |

        ```text
        project/
          src/
          tests/
        ```
        """, null);
    var sidebar = window.FindControl<Border>("SidebarGuideTarget")!;
    var columns = window.FindControl<Grid>("WorkspaceColumns")!;
    var splitter = window.FindControl<GridSplitter>("SidebarSplitter")!;
    var instant = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!;
    var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
    var titleMenu = window.FindControl<Vex.Modules.Shell.Views.ShellTitleMenuView>("TitleMenuView")!;
    var insert = titleMenu.FindControl<MenuItem>("InsertMenuItem")!;
    Check(insert.GetLogicalDescendants().OfType<MenuItem>().Count(item => item.CommandParameter is string key && key is not "footnote") == 22,
        "All 22 Mermaid templates remain available in grouped insertion menus");
    var view = titleMenu.FindControl<MenuItem>("ViewMenuItem")!;
    foreach (var width in new[] { 1280, 980, 1920 })
    {
        window.Width = width; window.Height = width == 980 ? 640 : 900; vm.Layout.IsSidebarVisible = true;
        vm.Layout.SidebarWidth = ShellWindowLayoutViewModel.DefaultSidebarWidth; vm.Workbench.ShowInstant(); Pump();
        foreach (var delta in new[] { 90d, -40d, 220d, -400d, 70d })
        {
            var settings = (MemorySettings)app.Resolve<IAppSettingsStore>();
            var writesBefore = settings.UpdateCount;
            var before = sidebar.Bounds.Width;
            var origin = splitter.TranslatePoint(new Point(splitter.Bounds.Width / 2, splitter.Bounds.Height / 2), window)!.Value;
            window.MouseMove(origin); window.MouseDown(origin, MouseButton.Left);
            for (var step = 1; step <= 5; step++) { window.MouseMove(origin + new Vector(delta * step / 5, 0), RawInputModifiers.LeftMouseButton); Pump(); }
            Check(settings.UpdateCount == writesBefore, "Dragging updates layout without writing settings for every pixel");
            window.MouseUp(origin + new Vector(delta, 0), MouseButton.Left); Pump();
            Check(settings.UpdateCount <= writesBefore + 1 && settings.Current.SidebarWidth == vm.Layout.SidebarWidth,
                "Finishing a drag saves its final width once");
            Check(Math.Abs(before - sidebar.Bounds.Width) > 1 || sidebar.Bounds.Width <= 200 || sidebar.Bounds.Width >= 560,
                $"Sidebar responds to actual pointer dragging at width {width}: {before} -> {sidebar.Bounds.Width}");
            Check(Math.Abs(sidebar.Bounds.Width - columns.ColumnDefinitions[0].ActualWidth) < 1
                && Math.Abs(sidebar.Bounds.Width - vm.Layout.SidebarWidth) < 1,
                "One sidebar width is shared by the column, visual and persisted state");
            var contentLeft = instant.TranslatePoint(default, window)!.Value.X;
            Check(Math.Abs(contentLeft - sidebar.Bounds.Width - splitter.Bounds.Width) < 1 && instant.Bounds.Width >= 360
                && contentLeft + instant.Bounds.Width <= window.Bounds.Width + 1,
                "Sidebar drag leaves no phantom column or clipped document");
        }
        var saved = vm.Layout.SidebarWidth;
        vm.Layout.ShowOutline(); Pump(); vm.Layout.ShowFiles(); Pump();
        Check(Math.Abs(sidebar.Bounds.Width - saved) < 1, "Files and outline retain the dragged width");
        vm.Layout.IsSidebarVisible = false; Pump();
        Check(sidebar.Bounds.Width == 0 && instant.Bounds.Width >= columns.Bounds.Width - vm.Layout.OutlineColumnWidth.Value - vm.Layout.OutlineSplitterWidth.Value - 2,
            "Collapsing file navigation releases its entire column while preserving the independent auxiliary panel");
        vm.Layout.IsSidebarVisible = true; Pump();
        Check(Math.Abs(sidebar.Bounds.Width - saved) < 1 && app.Resolve<IAppSettingsStore>().Current.SidebarWidth == saved,
            "Reopening restores the actual dragged and persisted width");
        vm.Layout.SidebarWidth = 226; vm.Workbench.ShowInstant(); Pump();
        var scroll = instant.Editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        var document = (Border)scroll.Content!;
        if (width == 1920) Check(document.Bounds.Width >= 1200, "Wide instant document uses more than 1100 pixels for content and padding");
        vm.Workbench.ShowRead(); Pump();
        var rendered = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
        Check(rendered.Bounds.Width + rendered.Margin.Left + rendered.Margin.Right <= preview.Bounds.Width + 1,
            "Reading document fits the available width");
        if (width == 1920) Check(rendered.Bounds.Width <= 724 && rendered.Bounds.Width >= 720, "Wide reading document keeps the prototype's 820px paper width including padding");
        Capture(window, $"layout-{width}-read");
    }
    window.Width = 1280; window.Height = 900; vm.Workbench.ShowRead(); Pump();
    var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
    foreach (var palette in vm.Appearance.PaletteOptions)
    foreach (var theme in new[] { "light", "dark" })
    {
        vm.Appearance.SelectedPalette = palette; vm.Appearance.SelectThemeByKey(theme); Pump(); SettleRendering();
        var code = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().Single(block => block.Classes.Contains("MdCodeBlockText"));
        var frame = code.GetVisualAncestors().OfType<Border>().First(border => border.Classes.Contains("MdCodeBlock"));
        Check(frame.Background is ISolidColorBrush, "Code background resolves for " + palette.Key + "/" + theme);
        var brush = (ISolidColorBrush)frame.Background!;
        Application.Current!.TryGetResource("VexPreviewBackgroundBrush", window.ActualThemeVariant, out var pageBackground);
        var canvas = ((ISolidColorBrush)pageBackground!).Color;
        var alpha = brush.Color.A / 255d * brush.Opacity;
        byte Blend(byte front, byte back) => (byte)Math.Round(front * alpha + back * (1 - alpha));
        var visibleBackground = Color.FromRgb(Blend(brush.Color.R, canvas.R), Blend(brush.Color.G, canvas.G), Blend(brush.Color.B, canvas.B));
        Check(code.Inlines!.OfType<Avalonia.Controls.Documents.Run>().All(run => Contrast(((ISolidColorBrush)run.Foreground!).Color, visibleBackground) >= 4.5),
            $"Plain code remains readable on the actual Semi background: {palette.Key}/{theme}");
        Check(code.FontSize >= 12.5 && viewer.ParagraphFontSize >= 15,
            "Rendered code and paragraph retain readable native font sizes");
        Check(!frame.GetVisualDescendants().OfType<Control>().Any(control => control.Classes.Contains("MdCodeBlockHeader") && control.IsEffectivelyVisible),
            "Reading code shows neither a language toolbar nor a copy button");
        Capture(window, $"code-{palette.Key}-{theme}");
    }
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
        $"Vex sidebar and workspace start immediately below the title divider: y={contentY}");
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
    var files = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellFilesView>().Single();
    var outline = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellOutlineView>().Single();
    Check(files.IsEffectivelyVisible && outline.IsEffectivelyVisible,
        "Vex independent Files and Outline panels remain visible under " + theme);
    Check(outline.TranslatePoint(default, window)!.Value.X > files.TranslatePoint(default, window)!.Value.X + files.Bounds.Width,
        "Vex Outline remains to the right of the document under " + theme);
    var heading = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("MdH1Border"));
    var shellVm = (MainWindowViewModel)window.DataContext!;
    var type = CodeWF.Avalonia.Markdown.Themes.ArticleTypographyCatalog.Find(shellVm.Appearance.SelectedTypography?.Key)!;
    // 尺寸档按 baseSize/BodySize 等比缩放目录实测值（Compact 13 / Normal 15 / Relaxed 17）。
    var scale = shellVm.Appearance.MarkdownDensity switch { "compact" => 13d, "relaxed" => 17d, _ => 15d } / type.BodySize;
    var scaledHeading = Thickness.Parse(type.Headings[0].Border) * scale;
    Check(Approx(heading.BorderThickness, scaledHeading),
        "Vex H1 follows the selected article decoration under " + theme + $" (actual {heading.BorderThickness}, scaled {scaledHeading})");
    var quote = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("MdQuote"));
    var scaledQuote = Thickness.Parse(type.Quote.Border) * scale;
    Check(Approx(quote.BorderThickness, scaledQuote),
        "Vex quote follows the selected article border under " + theme + $" (actual {quote.BorderThickness}, scaled {scaledQuote})");
}

static bool Approx(Thickness a, Thickness b) =>
    Math.Abs(a.Left - b.Left) < 0.01 && Math.Abs(a.Top - b.Top) < 0.01
    && Math.Abs(a.Right - b.Right) < 0.01 && Math.Abs(a.Bottom - b.Bottom) < 0.01;

bool SameBrush(IBrush? first, IBrush? second) => first is ISolidColorBrush a && second is ISolidColorBrush b && a.Color == b.Color;

void VerifySourceLineHeight(Window window, MarkdownEditorView source, double expected, string name)
{
    var textView = source.Editor.TextArea.TextView;
    Check(Math.Abs(textView.DefaultLineHeight - expected) < 0.01, $"{name} source line height matches {expected} px (actual {textView.DefaultLineHeight}, factor {textView.Options.LineHeightFactor})");
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

void VerifyEditingTools(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    var wb = vm.Workbench;
    var options = window.FindControl<Vex.Modules.Shell.Views.ShellEditorOptionsView>("EditorOptions")!;
    var toolbar = options.FindControl<Ursa.Controls.ToolBar>("FormattingToolbar")!;
    var source = window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!.FindControl<MarkdownEditorView>("MarkdownEditor")!;
    var live = window.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
    var doc = app.Resolve<IWorkspaceDocumentState>();
    window.Width = 1280; window.Height = 820; wb.ShowSource(); Pump();
    doc.UpdateDocument("alpha beta", null); Pump();
    source.Editor.Select(0, 5); source.Editor.Focus(); Pump();
    Click(options.FindControl<Button>("FormatBold")!);
    Check(vm.Markdown == "**alpha** beta", "Formatting click preserves source selection");
    doc.UpdateDocument("alpha beta", null); Pump();
    wb.ShowInstant(); Pump(); live.NavigateToSourceLine(1, true); Pump();
    var input = live.GetVisualDescendants().OfType<TextBox>().Single(box => box.IsEffectivelyVisible);
    input.SelectionStart = 0; input.SelectionEnd = 5; input.Focus(); Pump();
    Click(options.FindControl<Button>("FormatItalic")!);
    Check(vm.Markdown == "*alpha* beta", "Formatting acts on the current live block selection");
    var originalSettings = app.Resolve<IAppSettingsStore>().Current;
    var originalState = live.CaptureState();
    foreach (var prior in new[] { WindowState.Normal, WindowState.Maximized, WindowState.FullScreen })
    {
        vm.Layout.CurrentWindowState = prior; Pump(); wb.StartPresentation(); Pump(); SettleRendering();
        Check(wb.IsPresenting && wb.IsReading && !toolbar.IsEffectivelyVisible
            && !((CodeWF.Avalonia.Controls.Controls.CodeWFWindow)window).IsTitleBarVisible && vm.Layout.SidebarColumnWidth.Value == 0 && !vm.Layout.ShowStatusBar,
            "Presentation hides chrome and editing controls without replacing the document: " + prior);
        Check(vm.Layout.CurrentWindowState == WindowState.FullScreen && vm.Markdown == "*alpha* beta", "Presentation uses the current Markdown in full screen");
        for (var i = 0; i < 30; i++) wb.IncreasePresentationText();
        Check(wb.PresentationZoom == 2.5 && !wb.CanIncreasePresentationText, "Presentation text caps at 250 percent");
        for (var i = 0; i < 30; i++) wb.DecreasePresentationText();
        Check(wb.PresentationZoom == 1 && !wb.CanDecreasePresentationText, "Presentation text has a readable lower bound");
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump();
        Check(!wb.IsPresenting && wb.IsInstantEditing && vm.Layout.CurrentWindowState == prior && live.CaptureState().Current == originalState.Current,
            "Escape restores mode, active block, selection and prior window state: " + prior);
        Check(app.Resolve<IAppSettingsStore>().Current == originalSettings, "Presentation does not persist layout or typography preferences");
    }
    vm.Layout.CurrentWindowState = WindowState.Normal;
    foreach (var width in new[] { 980, 1280 })
    {
        window.Width = width; vm.Layout.SidebarWidth = width == 980 ? 560 : 226; wb.ShowSource(); Pump(); SettleRendering();
        var origin = toolbar.TranslatePoint(default, options)!.Value;
        Check(toolbar.Bounds.Height <= 30 && origin.X >= 0 && origin.X + toolbar.Bounds.Width <= options.Bounds.Width,
            "Formatting remains one compact row inside the available editor width: " + width);
        var more = toolbar.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>().Single(button => button.Name == "button");
        Check(more.IsEffectivelyVisible && more.Bounds.Width > 0, "More formatting remains reachable with automatic overflow: " + width);
        if (width == 980) Check(toolbar.Classes.Contains(":overflow"), "Narrow document moves formatting commands into Ursa overflow");
        Capture(window, "editing-tools-" + width);
    }
    window.Width = 1280; vm.Layout.SidebarWidth = 226;
    wb.ShowRead(); Pump();
    Check(!options.IsEffectivelyVisible, "Read view hides editing formats");
    wb.ShowMindMap(); Pump(); Check(!options.IsEffectivelyVisible, "Mind map keeps the editing row hidden");
    foreach (var culture in new[] { "zh-CN", "zh-Hant", "en-US", "ja-JP" })
    {
        app.Resolve<IAppLocalizer>().SetCulture(culture); wb.ShowSource(); Pump();
        Check((string?)ToolTip.GetTip(options.FindControl<Button>("FormatBold")!) == CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(Vex.VexL.Bold), "Formatting tooltip follows language: " + culture);
    }
    void Click(Button button)
    {
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Pump();
    }
}

void VerifyDialogButtonStates(VerificationVexApp app, Window window, MainWindowViewModel vm, string kind, bool auditOnly = false)
{
    Task dialogTask;
    Control dialog;
    Action close;
    if (kind == "primary")
    {
        dialogTask = app.Resolve<IShellOverlayService>().ShowInsertTableAsync();
        PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellInsertTableOverlayView>().Any());
        dialog = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellInsertTableOverlayView>().Single();
        close = ((Vex.Modules.Shell.Views.ShellInsertTableModel)dialog.DataContext!).Close;
    }
    else
    {
        dialogTask = app.Resolve<IShellOverlayService>().ConfirmDeleteAsync("Delete verification", "Verification only", Path.Combine(output, "not-a-real-file.md"));
        PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellDeleteConfirmationOverlayView>().Any());
        dialog = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellDeleteConfirmationOverlayView>().Single();
        close = ((Vex.Modules.Shell.Views.ShellDeleteConfirmationModel)dialog.DataContext!).Close;
    }
    var button = dialog.GetVisualDescendants().OfType<Button>().Single(control => control.Classes.Contains(kind));
    var originalCommand = button.Command;
    // 只检查实际按钮样式；指针释放在按钮外，避免执行文档操作。
    button.Command = null;
    var originalPalette = vm.Appearance.SelectedPalette;
    var originalTheme = vm.Appearance.SelectedTheme;
    var issues = new List<string>();
    try
    {
        foreach (var palette in vm.Appearance.PaletteOptions)
        foreach (var theme in vm.Appearance.ThemeOptions.Where(option => option.Mode != StudioThemeMode.System))
        {
            vm.Appearance.SelectedPalette = palette;
            vm.Appearance.SelectedTheme = theme;
            Pump();
            var scroller = button.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
            if (scroller is not null) scroller.Offset = default;
            Pump();
            var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
            var outside = new Point(1, 1);
            window.MouseMove(outside); Pump();
            Inspect("normal");
            window.MouseMove(center); Pump();
            Inspect("hover");
            window.MouseDown(center, MouseButton.Left); Pump();
            Inspect("pressed");
            window.MouseMove(outside);
            window.MouseUp(outside, MouseButton.Left); Pump();

            void Inspect(string state)
            {
                var expectedState = state switch
                {
                    "normal" => !button.IsPointerOver && !button.IsPressed,
                    "hover" => button.IsPointerOver && !button.IsPressed,
                    _ => button.IsPointerOver && button.IsPressed
                };
                if (!expectedState) throw new InvalidOperationException($"The actual {kind} button did not enter {state} state.");
                var presenter = button.GetVisualDescendants().OfType<ContentPresenter>()
                    .Single(control => control.Name == "PART_ContentPresenter");
                var text = presenter.GetVisualDescendants().OfType<TextBlock>().First();
                var foreground = ((ISolidColorBrush)text.Foreground!).Color;
                var background = ((ISolidColorBrush)presenter.Background!).Color;
                if (background.A == 0) background = ((ISolidColorBrush)dialog.GetVisualDescendants().OfType<Border>().First(control => control.Classes.Contains("dialog-card")).Background!).Color;
                var ratio = Contrast(foreground, background);
                var description = $"{kind} button {palette.Key}/{theme.Key}/{state}: {foreground} on {background}, contrast {ratio:F2}";
                Console.WriteLine("AUDIT " + description);
                if (ratio < 4.5) issues.Add(description);
                if (auditOnly || state == "hover") Capture(window, $"vex-{kind}-{palette.Key}-{theme.Key}-{state}");
                if (!auditOnly) Check(ratio >= 4.5, description);
            }
        }
        if (auditOnly) Console.WriteLine($"AUDIT {kind} button states below 4.5 contrast: {issues.Count}");
    }
    finally
    {
        window.MouseMove(new Point(1, 1));
        window.MouseUp(new Point(1, 1), MouseButton.Left);
        button.Command = originalCommand;
        close();
        Pump(dialogTask);
        vm.Appearance.SelectedPalette = originalPalette;
        vm.Appearance.SelectedTheme = originalTheme;
        Pump();
    }
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

void VerifyUpdates(VerificationVexApp app, Window window, MainWindowViewModel vm)
{
    window.Width = 980; window.Height = 640; Pump();
    var service = (VerificationUpdates)VerificationVexApp.Updates!;
    var overlay = app.Resolve<IShellOverlayService>();
    var menu = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellTitleMenuView>().Single();
    var menuItem = menu.FindControl<MenuItem>("CheckUpdatesMenuItem")!;
    Check(menuItem.CommandParameter?.ToString() == "update", "Help menu routes to the shared update action");
    var menuTask = vm.Help.OpenHelpTopic("update");
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUpdateOverlayView>().Any());
    var view = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUpdateOverlayView>().Single();
    var model = (ShellUpdateViewModel)view.DataContext!;
    Check(service.Checks == 1 && model.State == UpdateState.Available, "Menu opens one update view and starts one check");
    SettleRendering();
    var updateCard = view.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("dialog-card"));
    Check(updateCard.Bounds.Width == 620 && ((Grid)updateCard.Child!).Children[0].Bounds.Height == 36, "Update uses the unified 620px dialog frame and fixed 36px title bar");
    Check(!view.FindControl<Expander>("UpdateNotes")!.IsExpanded, "Available update keeps release notes collapsed");
    Capture(window, "updates-available");
    var download = model.AdvanceAsync(); Pump();
    Check(model.IsDownloading && service.Downloads == 1, "Shared view starts one download");
    Check(!view.FindControl<Button>("UpdateAction")!.IsVisible, "Download state exposes cancellation without a disabled primary action");
    Capture(window, "updates-downloading");
    model.Close(); Pump(menuTask);
    Check(model.IsDownloading, "Closing the view keeps the download active");
    var aboutTask = overlay.ShowAboutAsync();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Any());
    var about = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellAboutOverlayView>().Single();
    ((ShellAboutViewModel)about.DataContext!).UpdateCommand.Execute().Subscribe(); Pump();
    PumpUntil(() => window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUpdateOverlayView>().Any());
    view = window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUpdateOverlayView>().Single();
    Check(ReferenceEquals(model, view.DataContext) && model.IsDownloading && service.Checks == 1 && service.Downloads == 1,
        "About reopens the same update session and download");
    var duplicate = overlay.ShowUpdatesAsync();
    Check(ReferenceEquals(duplicate, overlay.ShowUpdatesAsync()) && window.GetVisualDescendants().OfType<Vex.Modules.Shell.Views.ShellUpdateOverlayView>().Count() == 1,
        "Repeated entry requests reuse one visible dialog");
    service.CompleteDownload(); Pump(download);
    Check(model.State == UpdateState.Downloaded && model.HasDownload, "Reopened view receives the completed download");
    Check(view.FindControl<Expander>("UpdateNotes")!.IsExpanded, "Downloaded update expands the release notes");
    Capture(window, "updates-downloaded");
    Pump(model.AdvanceAsync());
    Check(model.State == UpdateState.Failed && model.HasDownload && !aboutTask.IsCompleted, "Failed installation keeps Vex and the update view open");
    foreach (var culture in new[] { "zh-CN", "en-US", "zh-Hant", "ja-JP" })
    foreach (var theme in new[] { "light", "dark" })
    {
        vm.Appearance.SelectLanguageByCulture(culture); vm.Appearance.SelectThemeByKey(theme); Pump(); SettleRendering();
        var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Single(); scroll.ScrollToEnd(); Pump();
        VerifyInside(scroll, window, "Update viewport fits: " + culture + "/" + theme);
        VerifyInside(view.FindControl<Button>("UpdateAction")!, scroll, "Install retry remains reachable: " + culture + "/" + theme);
        Check(!model.StatusText.StartsWith("Vex.VexL."), "Update text is localized: " + culture);
        Capture(window, "updates-" + culture + "-" + theme);
    }
    model.Close(); Pump(aboutTask); Pump(duplicate);
    var reopen = overlay.ShowUpdatesAsync(); Pump();
    Check(model.HasDownload && model.State == UpdateState.Failed && service.Downloads == 1, "Installer failure can be reopened without downloading again");
    model.Close(); Pump(reopen);
}

public sealed class VerificationVexApp : Vex.App
{
    public static bool ProbeFileTree { get; set; }
    public static bool UseRealSettings { get; set; }
    public static IUpdateService? Updates { get; set; }
    public static IMarkdownExportService? Exports { get; set; }
    public Window? Shell { get; private set; }
    public T Resolve<T>() => Container.Resolve<T>();
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        base.RegisterTypes(registry);
        if (ProbeFileTree) registry.RegisterSingleton<IDocumentFileFactory, ProbeDocumentFileFactory>();
        if (Updates is not null) registry.RegisterInstance<IUpdateService>(Updates);
        if (Exports is not null) registry.RegisterInstance<IMarkdownExportService>(Exports);
        registry.RegisterInstance<IAppSettingsStore>(UseRealSettings ? new AppSettingsStore() : new MemorySettings());
        registry.RegisterInstance<IAutoSaveDraftService>(new MemoryDrafts());
        registry.RegisterInstance<IRecentDocumentStore>(new MemoryRecent());
        registry.RegisterInstance<IDocumentWindowLauncher>(new VerificationDocumentWindows());
    }
    protected override AvaloniaObject CreateShell()
    {
        Shell = (Window)base.CreateShell();
        return Shell;
    }
}

public sealed class VerificationDocumentWindows : IDocumentWindowLauncher
{
    public List<string> Paths { get; } = [];
    public void Open(string path) => Paths.Add(path);
}

public sealed class MemorySettings : IAppSettingsStore
{
    public int UpdateCount { get; private set; }
    public AppSettings Current { get; private set; } = new()
    {
        ThemeKey = "light", HasSeenOnboardingGuide = true, EnableMotion = false,
        IsMcpServerEnabled = false, IsSidebarVisible = true
    };
    public AppSettings Update(Func<AppSettings, AppSettings> update)
    {
        UpdateCount++;
        return Current = update(Current);
    }
}
public sealed class MemoryDrafts : IAutoSaveDraftService
{
    public string? LastError { get; set; }
    public event EventHandler? StateChanged;
    public bool FailFlush { get; set; }
    public string? RestoredMarkdown { get; set; }
    public DocumentSnapshot? TryRestore(DocumentSnapshot document) => RestoredMarkdown is null ? null : document with { Markdown = RestoredMarkdown };
    public void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown) { }
    public void Clear(DocumentSnapshot document) { }
    public bool Flush() { LastError = FailFlush ? "Draft storage unavailable" : null; StateChanged?.Invoke(this, EventArgs.Empty); return !FailFlush; }
    public void ClearRestored(DocumentSnapshot document) => RestoredMarkdown = null;
}
public sealed class MemoryRecent : IRecentDocumentStore
{
    public IReadOnlyList<RecentDocument> Load(int maxCount) => [];
    public void Save(IEnumerable<RecentDocument> documents) { }
}
