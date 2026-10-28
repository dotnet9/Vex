using CodeWF.Avalonia.Controls.Controls;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Controls;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
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
        Control<Avalonia.Controls.Primitives.ToggleButton>(nameof(preferences.CodeWrap)).IsChecked = true; Render();
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
        // 元素级对齐原型：.vex-pref-row 53 间距（32 控件 + 10×2 内距 + 1 分隔线）、input 180x32
        //（字段与按钮同高，--control-h-md 档）、控件右边缘与正文列对齐。
        var portField = view.GetVisualDescendants().OfType<TextBox>().First(t => t.Classes.Contains("pref-field"));
        var portRow = portField.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("pref-row"));
        Check(Math.Abs(portField.Bounds.Width - 180) < 0.6 && Math.Abs(portField.Bounds.Height - 32) < 0.6,
            "MCP form field keeps the prototype 180x32 box");
        Check(Math.Abs(portRow.Bounds.Height - 53) < 1.5, "MCP form row keeps the prototype 53px pitch");
        // TranslatePoint 的入参是控件自身坐标系里的点，取右上角要用 Width 而不是 Bounds.Right。
        var portRight = portField.TranslatePoint(new Point(portField.Bounds.Width, 0), view)!.Value.X;
        var rowRight = portRow.TranslatePoint(new Point(portRow.Bounds.Width, 0), view)!.Value.X;
        Check(Math.Abs(portRight - rowRight) < 0.6, "MCP form controls sit flush with the content column");
        Control<Button>("McpTabClients").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        // 高亮后正文只剩 inlines，复制钮读的也是这份文本，所以断言先从 inlines 还原 JSON 再核对 token 色。
        static (string Text, List<Color> Colors, SelectableTextBlock Runs, Border Box, Border Inner) Highlighted(MarkdownViewer viewer)
        {
            var code = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().Single(t => t.Classes.Contains("MdCodeBlockText"));
            var text = new StringBuilder();
            foreach (var inline in code.Inlines ?? [])
                text.Append(inline is Avalonia.Controls.Documents.Run run ? run.Text
                    : inline is Avalonia.Controls.Documents.LineBreak ? "\n" : null);
            return (text.ToString(),
                (code.Inlines ?? []).OfType<Avalonia.Controls.Documents.Run>()
                    .Select(run => (run.Foreground as ISolidColorBrush)?.Color).Where(c => c.HasValue).Select(c => c!.Value).ToList(),
                code,
                code.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("mcp-code")),
                code.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("MdCodeBlock")));
        }
        // 原型 .vex-code.json 用的就是这两个主题的取值：库按代码块底色亮度挑 LightPlus / DarkPlus，所以跟着 data-theme 走。
        foreach (var (theme, keyToken, stringToken) in new[]
                 { ("light", "#267f99", "#a31515"), ("dark", "#4ec9b0", "#ce9178") })
        {
            vm.Appearance.SelectThemeByKey(theme); Render();
            var http = Highlighted(Control<MarkdownViewer>("McpClientConfiguration"));
            using (var config = System.Text.Json.JsonDocument.Parse(http.Text))
                Check(config.RootElement.GetProperty("mcpServers").GetProperty("vex").GetProperty("url").GetString() == "http://127.0.0.1:17891/mcp/",
                    "Highlighted client config still parses with the saved endpoint: " + theme);
            Check(http.Colors.Contains(Color.Parse(keyToken)) && http.Colors.Contains(Color.Parse(stringToken)),
                $"Client config keys and strings carry the prototype token colours ({theme}): {string.Join("/", http.Colors.Distinct())}");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-clients-" + theme + ".png"));
            Control<Button>("McpSubStdio").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
            var stdio = Highlighted(Control<MarkdownViewer>("McpStdioConfiguration"));
            using (var config = System.Text.Json.JsonDocument.Parse(stdio.Text))
            {
                var server = config.RootElement.GetProperty("mcpServers").GetProperty("vex");
                Check(server.GetProperty("command").GetString() == "vex"
                      && server.GetProperty("args").EnumerateArray().Select(arg => arg.GetString()).Contains("--endpoint")
                      && server.GetProperty("env").TryGetProperty("VEX_MCP_TOKEN", out _),
                    "Highlighted stdio bridge config still parses into a runnable command: " + theme);
            }
            Check(stdio.Colors.Contains(Color.Parse(keyToken)) && stdio.Colors.Contains(Color.Parse(stringToken)),
                $"Stdio bridge keys and strings carry the prototype token colours ({theme}): {string.Join("/", stdio.Colors.Distinct())}");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-stdio-" + theme + ".png"));
            Control<Button>("McpSubHttp").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        }
        // 块面归 .mcp-code：库自带的第二层盒要压平，行号槽要收掉，正文只留 16 内距 + 1 描边 = 17 的缩进。
        var block = Highlighted(Control<MarkdownViewer>("McpClientConfiguration"));
        var inset = block.Runs.TranslatePoint(default, block.Box)!.Value;
        Check(block.Inner.Padding == new Thickness(0) && block.Inner.Margin == new Thickness(0)
              && block.Inner.BorderThickness == new Thickness(0) && block.Inner.CornerRadius == default
              && !block.Box.GetVisualDescendants().OfType<SelectableTextBlock>().Any(t => t.Tag as string == "CodeLineNumbers" && t.IsVisible)
              && Math.Abs(inset.X - 17) < .6 && Math.Abs(inset.Y - 17) < .6
              && block.Runs.TextWrapping == TextWrapping.Wrap
              && Math.Abs(block.Runs.FontSize - 12) < .01 && Math.Abs(block.Runs.LineHeight - 21.6) < .01,
            $"Highlighted block keeps the prototype box: inset={inset}, font={block.Runs.FontSize}/{block.Runs.LineHeight}, wrap={block.Runs.TextWrapping}");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-clients.png"));
        Control<Button>("McpSubStdio").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-stdio.png"));
        Control<Button>("McpTabAudit").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        // 原型 table.vex-audit：表头与每行都是 11+18+11+1 = 41 的行程，只画横线不画竖线，
        // 空态是贴在表头下面的一整行；有记录时最近一次调用排在第一行。
        var audit = Control<DataGrid>("AuditGrid");
        var auditEmpty = Control<Border>("EmptyRow");
        // 表头里还有模板自带的左上角占位与末列填充，只有带列标题的那几个才是原型意义上的 th。
        List<DataGridColumnHeader> AuditHeaders() => audit.GetVisualDescendants()
            .OfType<DataGridColumnHeader>().Where(header => header.Content is not null).ToList();
        var auditHeaders = AuditHeaders();
        Check(auditHeaders.Count == 3 && auditHeaders.All(header => Math.Abs(header.Bounds.Height - 41) < 0.6),
            "Audit header keeps the prototype 41px pitch: " + string.Join("/", auditHeaders.Select(header => header.Bounds.Height.ToString("0.#"))));
        Check(auditHeaders.Select(header => header.Content?.ToString()).SequenceEqual(new[] { "时间", "操作", "结果" }),
            "Audit columns carry the prototype header captions");
        Check(audit.GridLinesVisibility == DataGridGridLinesVisibility.Horizontal,
            "Audit table draws horizontal rules only: " + audit.GridLinesVisibility);
        Check(auditEmpty.IsVisible
            && Math.Abs(auditEmpty.TranslatePoint(new Point(0, 0), audit)!.Value.Y - 41) < 0.6,
            "Audit empty row sits directly under the header");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "settings-mcp-audit-empty.png"));
        model.Audit.Record(new McpOperationRecord(new DateTime(2026, 10, 27, 9, 8, 7, DateTimeKind.Utc), "vex_replace_text", "文档.md", "edit", true, true, false, "未找到匹配文本"));
        model.Audit.Record(new McpOperationRecord(new DateTime(2026, 10, 27, 9, 12, 3, DateTimeKind.Utc), "vex_read_document", "文档.md", "read", false, false, true, null));
        // 审计服务不推事件，表只按「刷新」重新拉取——这正是 Vex 比原型多出来的那颗刷新钮的用途。
        Control<Button>("RefreshButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        var auditRows = audit.GetVisualDescendants().OfType<DataGridRow>().ToArray();
        Check(!auditEmpty.IsVisible, "Audit empty row hides once records arrive");
        Check(auditRows.Length == 2 && auditRows.All(row => Math.Abs(row.Bounds.Height - 41) < 0.6),
            "Audit rows keep the prototype 41px pitch: " + string.Join("/", auditRows.Select(row => row.Bounds.Height.ToString("0.#"))));
        // 主题的竖线是表头竖分隔与单元格右格线两种 Rectangle；原型只有横线，所以竖着的那根必须不存在。
        Check(!audit.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>()
                .Any(line => line.IsVisible && line.Bounds.Width is > 0 and < 2 && line.Bounds.Height > 2),
            "Audit table draws no vertical rules");
        // 视觉树里的行不一定按上下顺序枚举，取表顺序要按行的 Y 排。
        List<string> AuditOrder() => audit.GetVisualDescendants().OfType<DataGridRow>().OrderBy(row => row.Bounds.Y)
            .Select(row => ((McpOperationRecord)row.DataContext!).ToolName).ToList();
        var auditOrder = AuditOrder();
        Check(auditOrder.SequenceEqual(new[] { "vex_read_document", "vex_replace_text" }),
            "Audit lists the newest call first, like the prototype record() unshift: " + string.Join("/", auditOrder));
        // 换 DataGrid 换来的能力：三列表头都能点着排序（原型没有），时间列点一次升、再点一次降。
        // 排序入口在 DataGridColumnHeader.OnClick 里，手工 RaiseEvent 点不动，只能走真指针事件。
        void ClickAuditHeader(string caption)
        {
            var header = AuditHeaders().First(x => x.Content?.ToString() == caption);
            var at = header.TranslatePoint(new Point(header.Bounds.Width / 2, header.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left);
            window.MouseUp(at, Avalonia.Input.MouseButton.Left);
            Render();
        }
        ClickAuditHeader("时间");
        Check(AuditOrder().SequenceEqual(new[] { "vex_replace_text", "vex_read_document" }),
            "Audit sorts ascending by the time column on the first header click: " + string.Join("/", AuditOrder()));
        ClickAuditHeader("时间");
        Check(AuditOrder().SequenceEqual(new[] { "vex_read_document", "vex_replace_text" }),
            "Audit sorts descending by the time column on the second header click: " + string.Join("/", AuditOrder()));
        localizer.SetCulture("en-US"); Render();
        Check(AuditHeaders().Select(header => header.Content?.ToString())
            .SequenceEqual(new[] { "Time", "Action", "Result" }), "Audit headers re-localize with the app language");
        localizer.SetCulture("zh-CN"); Render();
        // 整页会随语言重建，页签选择属于用户状态：换完语言审计页必须还在。
        Check(SegmentedPanel.GetIsActive(Control<Button>("McpTabAudit")), "Audit tab stays selected across a locale switch");
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
        if (app.Resolve<IAppSettingsStore>() is AppSettingsStore)
        {
            // 三处缩放坞的读数要跨会话保留：写盘后换成全新实例重读，确认 App.config 里的键真的落地。
            preferences.MapZoom = 137; preferences.PresentationScale = 2.2; preferences.ContentZoom = 145;
            var reread = new AppSettingsStore().Current;
            Check(reread.MapZoom == 137 && reread.ContentZoom == 145
                && Math.Abs((reread.PresentationScale ?? 0) - 2.2) < .001,
                "Map, presentation and content zoom survive a real settings-file reload");
        }
        Console.WriteLine("Preferences verification complete: " + output);
    }
}
