using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.ViewModels;

internal static class PreferencesVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var preferences = app.Resolve<EditorPreferences>();
        var model = app.Resolve<ShellPreferencesViewModel>();
        var localizer = app.Resolve<IAppLocalizer>();
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Pump(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(10); }
            Check(task.IsCompleted, "Async operation completes"); task.GetAwaiter().GetResult(); Render();
        }
        window.Width = 1280; window.Height = 820;
        vm.Markdown = "# Preferences\n\nText ==highlight== and ~sub~.\n";
        vm.Workbench.ShowSource(); Render();
        var original = vm.Markdown;
        // 复刻真实菜单触发路径：经事件总线由协调层打开，而不仅直调浮层服务。
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.ShowPreferences));
        Render(); Render();
        // 复刻真实菜单触发路径：总线 → 协调层 → 浮层，验证偏好设置能弹出。
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.ShowPreferences));
        var menuDeadline = DateTime.UtcNow.AddSeconds(10);
        while (!window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Any())
        {
            Render();
            if (DateTime.UtcNow > menuDeadline) throw new TimeoutException("Menu-triggered preferences did not open");
            Thread.Sleep(20);
        }
        Check(true, "Preferences opens through the menu action bus path");
        foreach (var stale in window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().ToArray()) ((ShellPreferencesViewModel)stale.DataContext!).Close();
        Render();
        var dialog = app.Resolve<IShellOverlayService>().ShowPreferencesAsync(model);
        Render();
        var view = window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Single();
        T Control<T>(string name) where T : Control => view.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
        Check(view.GetVisualDescendants().OfType<ListBox>().Single().ItemCount == 6, "Preferences has six prototype categories");
        Check(Control<ComboBox>(nameof(preferences.DefaultEncoding)).SelectedIndex == 0, "Default encoding has a visible initial choice");
        Check(Control<ComboBox>(nameof(preferences.DefaultNewline)).SelectedIndex == 0, "Default line ending has a visible initial choice");
        Control<ComboBox>(nameof(preferences.RestoreWorkspace)).SelectedIndex = 0; Render();
        Check(!preferences.RestoreWorkspace, "Startup choice updates the typed workspace preference");
        Control<ComboBox>(nameof(preferences.DefaultNewline)).SelectedIndex = 2; Render();
        Check(preferences.DefaultNewline == "CRLF", "Choice selection updates preference");
        view.SelectCategory("editor"); Render();
        // 对齐原型 editor 页：缩进改为 2/4/8 空格下拉，页面只有 字体/缩进/自动换行/源码行号 四项。
        Control<ComboBox>(nameof(preferences.IndentSize)).SelectedIndex = 1; Render();
        Check(preferences.IndentSize == 4, "Indent choice updates the typed preference");
        Control<ToggleSwitch>(nameof(preferences.CodeWrap)).IsChecked = true; Render();
        var source = window.GetVisualDescendants().OfType<MarkdownEditorView>().Single();
        Check(source.Editor.WordWrap, "Editor settings update the attached source editor");
        view.SelectCategory("images"); Render();
        Control<TextBox>(nameof(preferences.ImageFolder)).Text = "media"; Render();
        Check(preferences.ImageFolder == "media", "Image directory edits persist");
        foreach (var theme in new[] { "light", "dark" })
        {
            vm.Appearance.SelectThemeByKey(theme);
            foreach (var category in new[] { "general", "editor", "images", "appearance", "export", "mcp" })
            {
                view.SelectCategory(category); Render();
                Check(view.Bounds.Width <= window.Bounds.Width && view.Bounds.Height <= window.Bounds.Height, "Settings fits window: " + category);
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"settings-{theme}-{category}.png"));
            }
        }
        window.Width = 800; window.Height = 480;
        foreach (var culture in new[] { "en-US", "zh-Hant", "ja-JP", "zh-CN" })
        {
            localizer.SetCulture(culture); view.SelectCategory("general"); Render();
            Check(view.SelectedCategory == "general" && view.Bounds.Height <= window.Bounds.Height, "Localized settings fits narrow window: " + culture);
            Check(Control<ComboBox>(nameof(preferences.DefaultNewline)).SelectedIndex == 2, "Locale switch preserves selected preference");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-narrow-" + culture + ".png"));
        }
        Check(vm.Markdown == original, "Settings and locale changes preserve document text");
        view.SelectCategory("mcp"); Render();
        // MCP 页：分段式标签（服务/客户端/审计），客户端页配置为已保存端点的合法 JSON。
        Check(!view.GetVisualDescendants().OfType<TabControl>().Any(), "MCP page uses segmented tabs instead of a classic TabControl");
        Control<Button>("McpTabClients").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        using (var config = System.Text.Json.JsonDocument.Parse(Control<TextBox>("McpClientConfiguration").Text!))
            Check(config.RootElement.GetProperty("mcpServers").GetProperty("vex").GetProperty("url").GetString() == "http://127.0.0.1:17891/mcp/", "MCP client configuration is valid JSON using saved endpoint");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-clients.png"));
        Control<Button>("McpTabAudit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-audit.png"));
        model.Close(); Pump(dialog);
        window.Width = 1280; window.Height = 820; vm.Appearance.SelectThemeByKey("light"); Render();
        source.Editor.Select(0, 0);
        app.Resolve<MarkdownEditorViewModel>().InsertSnippet(new EditorSnippetCommand("flowchart")); Render();
        Check(vm.Markdown.Contains("```mermaid\nflowchart LR"), "Menu snippet inserts actual Mermaid source");
        vm.Workbench.UndoDocument(); Render();
        Check(vm.Markdown == original, "Snippet is a single undo operation");
        vm.Workbench.RedoDocument(); Render();
        Check(vm.Markdown.Contains("flowchart LR"), "Snippet supports redo");

        var directory = Path.Combine(output, "files"); Directory.CreateDirectory(directory);
        var documentPath = Path.Combine(directory, "note.md");
        var imagePath = Path.Combine(directory, "sample image.png"); File.WriteAllBytes(imagePath, [1, 2, 3]);
        var images = app.Resolve<IShellDocumentUtilityActions>();
        preferences.ImageCopy = true; preferences.ImageRelative = true;
        Check(images.CopyImageToAssets(documentPath, imagePath) == "media/sample%20image.png", "Image copy uses configured folder and escaped relative path");
        File.WriteAllBytes(imagePath, [4, 5, 6]);
        Check(images.CopyImageToAssets(documentPath, imagePath) == "media/sample%20image-1.png", "Different image with same name preserves existing resource");
        Check(File.ReadAllBytes(Path.Combine(directory, "media", "sample image.png")).SequenceEqual(new byte[] { 1, 2, 3 }), "Original resource remains intact");
        preferences.ImageCopy = false; preferences.ImageRelative = false;
        Check(images.CopyImageToAssets(null, imagePath) == new Uri(imagePath).AbsoluteUri, "Unsaved document and absolute path preference use file URI");
        preferences.ImageCopy = true; preferences.ImageFolder = "../outside";
        try { images.CopyImageToAssets(documentPath, imagePath); throw new InvalidOperationException("Unsafe image folder was accepted"); }
        catch (IOException) { Console.WriteLine("PASS Image resource directory stays within document folder"); }
        preferences.ImageFolder = "assets";

        vm.Workbench.ShowInstant(); preferences.WatermarkPreview = true; preferences.WatermarkText = "Private 维刻"; Render();
        Check(window.GetVisualDescendants().OfType<Vex.Modules.Workspace.Views.WatermarkOverlay>().Any(x => x.IsEffectivelyVisible && x.Text == preferences.WatermarkText), "Instant document shows configured watermark without changing text");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-watermark.png"));
        Check(vm.Workbench.LiveEditor!.CodeFontSize == preferences.CodeFontSize, "Instant code font uses independent code preference");
        preferences.WatermarkPreview = false;

        var exporter = app.Resolve<IMarkdownExportService>();
        var buildHtml = exporter.GetType().GetMethod("BuildHtml", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        string Html(DocumentSnapshot document) => (string)buildHtml.Invoke(exporter, new object?[] { document }.Concat(buildHtml.GetParameters().Skip(1).Select(x => x.DefaultValue)).ToArray())!;
        var htmlDocument = new DocumentSnapshot(null, "export.md", "==marked==", Encoding.UTF8, true);
        preferences.WatermarkPreview = false; preferences.SyntaxHighlight = false;
        var plainHtml = Html(htmlDocument);
        Check(plainHtml.Contains("==marked==") && !plainHtml.Contains("article::after"), "HTML respects disabled syntax and watermark settings");
        preferences.WatermarkPreview = true; preferences.SyntaxHighlight = true;
        var markedHtml = Html(htmlDocument);
        Check(markedHtml.Contains("<mark>marked</mark>") && markedHtml.Contains("article::after"), "HTML enables document syntax and embedded watermark");
        preferences.WatermarkPreview = false;

        var documents = app.Resolve<IDocumentService>();
        preferences.DefaultEncoding = "utf-8-bom";
        var fresh = documents.CreateNew();
        Check(fresh.Encoding.GetPreamble().Length == 3 && fresh.Markdown.Contains("\r\n"), "New document honors default encoding and newline");
        var baseline = "# Disk\r\n";
        File.WriteAllText(documentPath, baseline, new UTF8Encoding(true));
        var opened = documents.OpenPathAsync(documentPath); Pump(opened);
        Check(opened.Result.Encoding.GetPreamble().Length == 3, "Opening an existing BOM document retains its actual encoding");
        var snapshot = opened.Result with { Markdown = "# Changed\r\n" };
        var saved = documents.TryAutoSaveAsync(snapshot, baseline); Pump(saved);
        Check(saved.Result && File.ReadAllText(documentPath) == snapshot.Markdown, "Auto save writes an unchanged disk document");
        Check(File.ReadAllBytes(documentPath).Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "Auto save retains BOM encoding");
        File.WriteAllText(documentPath, "# External\n");
        var conflict = documents.TryAutoSaveAsync(snapshot, baseline); Pump(conflict);
        Check(!conflict.Result && File.ReadAllText(documentPath) == "# External\n", "Auto save preserves external changes even without relying on timestamps");
        using (var writer = new FileStream(documentPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            try { Pump(documents.TryAutoSaveAsync(snapshot, baseline)); throw new InvalidOperationException("Locked file was written"); }
            catch (IOException) { Console.WriteLine("PASS Auto save respects an external file lock"); }
        }
        // 走真实调度器，确认用户停止输入后写回当前具名文件。
        File.WriteAllText(documentPath, baseline);
        var open = vm.OpenPathAsync(documentPath); Render();
        foreach (var confirmation in window.GetVisualDescendants().OfType<ShellUnsavedDialogView>())
            ((ShellUnsavedDialogViewModel)confirmation.DataContext!).Discard();
        Pump(open);
        preferences.AutoSave = true; preferences.AutoSaveDelay = 2;
        vm.Markdown = "# Timer saved\n";
        var deadline = DateTime.UtcNow.AddSeconds(6);
        while (vm.DocumentInfo.IsModified && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(20); }
        Check(!vm.DocumentInfo.IsModified && File.ReadAllText(documentPath) == vm.Markdown, "Idle timer auto saves the named document and clears dirty state");
        preferences.AutoSave = false;
        Console.WriteLine("Preferences verification complete: " + output);
    }
}
