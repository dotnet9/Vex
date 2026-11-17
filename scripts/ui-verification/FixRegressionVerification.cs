using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 三处修复的回归探针（2026-11-17）：
/// 1. 图鉴缩略图：Flyout 打开后 24 张 Image 应有源（原从按钮视觉树找 Flyout 内容恒为空）。
/// 2. tooltip I18n 绑定：工具带按钮 ToolTip 应解析为文案而非键名原文（库程序集 json 未入全局池）。
/// 3. 即时编辑回车：段落块内发 Enter 应拆块换行。
/// </summary>
internal static class FixRegressionVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Pump(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(10); }
            task.GetAwaiter().GetResult(); Render();
        }
        void Shot(string name)
        {
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(System.IO.Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1440; window.Height = 948; Render();
        // ---- 2. tooltip I18n 绑定：读工具带按钮的附加属性文案 ----
        var ruleButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "FormatHorizontalRule");
        Check(ruleButton is not null, "Toolbar horizontal-rule button found");
        var tipText = CodeWF.Avalonia.Controls.Helpers.ToolTipHelper.GetText(ruleButton!);
        Console.WriteLine("tooltip text: " + tipText);
        Check(tipText is { } tip && tip.Length > 0 && !tip.Contains("MarkdownL.") && !tip.Contains("VexL."),
            "Tooltip resolves to localized text, not raw key");

        // ---- 1. 图鉴缩略图：打开 Flyout 等填充，24 张 Image 应有源 ----
        var galleryButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "FormatMermaidGallery");
        Check(galleryButton is not null, "Gallery button found");
        if (galleryButton!.Flyout is not Flyout flyout) throw new InvalidOperationException("Gallery flyout missing");
        flyout.ShowAt(galleryButton);
        Render();
        var deadline = DateTime.UtcNow.AddSeconds(25);
        int filled = 0, total = 0;
        while (DateTime.UtcNow < deadline)
        {
            Render();
            var images = flyout.Content is Visual root
                ? root.GetVisualDescendants().OfType<Image>().ToList()
                : [];
            total = images.Count;
            filled = images.Count(i => i.Source is not null);
            if (total >= 24 && filled >= 24) break;
            Thread.Sleep(250);
        }
        Console.WriteLine($"gallery thumbs: {filled}/{total} filled");
        // 分组 tab + 大卡（260x110）：4 个 tab，默认组 0 可见 4 卡、其余隐藏。
        var tabs = flyout.Content is Visual tabRoot
            ? tabRoot.GetVisualDescendants().OfType<RadioButton>().Where(r => r.GroupName == "MermaidGallery").ToList()
            : [];
        var cards0 = flyout.Content is Visual cardRoot
            ? cardRoot.GetVisualDescendants().OfType<Button>().Where(b => b is not RadioButton && int.TryParse(b.Tag?.ToString(), out _) && b.IsVisible).ToList()
            : [];
        Console.WriteLine($"gallery tabs: {tabs.Count}, visible cards (group0): {cards0.Count}");
        // 切到组 2 应见 8 卡。
        tabs.LastOrDefault()?.IsChecked = true;
        Render();
        var cards2 = flyout.Content is Visual cardRoot2
            ? cardRoot2.GetVisualDescendants().OfType<Button>().Where(b => b is not RadioButton && int.TryParse(b.Tag?.ToString(), out _) && b.IsVisible).ToList()
            : [];
        Console.WriteLine("visible cards (group2): " + cards2.Count);
        var bigThumb = cards2.SelectMany(b => b.GetVisualDescendants().OfType<Image>()).FirstOrDefault();
        Console.WriteLine($"thumb size: {bigThumb?.Width}x{bigThumb?.Height}");
        Shot("gallery-thumbs");
        Check(total >= 24 && filled >= 24, "All 24 gallery thumbnails rendered: " + filled);
        Check(tabs.Count == 4, "Gallery group tabs present: " + tabs.Count);
        Check(cards0.Count == 4, "Group 0 shows 4 cards: " + cards0.Count);
        Check(cards2.Count == 8, "Group 2 shows 8 cards after switch: " + cards2.Count);
        Check(bigThumb is { Width: >= 260, Height: >= 110 }, "Thumbnails enlarged to at least 260x110");
        flyout.Hide();
        Render();

        // ---- 3. 即时编辑回车：真实点击块进入编辑态，再回车应拆块换行 ----
        var probeDoc = System.IO.Path.Combine(output, "instant-enter-probe.md");
        System.IO.File.WriteAllText(probeDoc, "# 探针标题\n\n第一段内容，光标在末尾回车应拆块。\n");
        Pump(vm.OpenStartupDocumentAsync([probeDoc]));
        vm.Workbench.SelectLayout("instant");
        Render(); Thread.Sleep(300); Render();
        var live = window.Workspace.GetVisualDescendants()
            .FirstOrDefault(control => control.GetType().Name == "MarkdownInstantView");
        Check(live is not null, "Instant view found");
        var blocks = live!.GetVisualDescendants()
            .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
        Console.WriteLine("block views: " + blocks.Count);
        Check(blocks.Count >= 2, "Paragraph block views found: " + blocks.Count);
        var paragraph = blocks.OrderByDescending(b => b.Bounds.Width * b.Bounds.Height).Last();
        var center = paragraph.TranslatePoint(new Point(paragraph.Bounds.Width / 2, paragraph.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Block center unmapped");
        // 真实点击进入编辑态（OnPointerPressed → BeginEdit），再打字确认 caret 激活。
        window.MouseMove(center, RawInputModifiers.None); Render();
        window.MouseDown(center, MouseButton.Left); Render();
        window.MouseUp(center, MouseButton.Left); Render();
        Thread.Sleep(200); Render();
        var editor = paragraph.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Console.WriteLine("editor state: visible=" + editor?.IsVisible + " focused=" + editor?.IsFocused + " text=" + (editor?.Text ?? "null"));
        Check(editor is { IsVisible: true }, "Click enters block editing");
        window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null); Render();
        // headless 焦点链路不可靠：直接对块编辑器派发 KeyDown（tunnel handler 挂在它自己上必收）。
        editor!.CaretIndex = (editor.Text ?? string.Empty).Length;
        var textBefore = editor.Text ?? string.Empty;
        Console.WriteLine("editor text: " + textBefore);
        // Typora 语义：末尾连续回车，每敲一次拆出一个新（空）段落块，块数递增。
        var counts = new List<int> { blocks.Count };
        for (var attempt = 0; attempt < 3; attempt++)
        {
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = editor });
            Render(); Thread.Sleep(300); Render();
            var viewsNow = live!.GetVisualDescendants()
                .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
            counts.Add(viewsNow.Count);
            Console.WriteLine($"enter #{attempt + 1}: blocks={viewsNow.Count}");
            Check(viewsNow.Count == blocks.Count + attempt + 1,
                $"Enter #{attempt + 1} appends a new empty paragraph: {viewsNow.Count}");
        }
        Shot("instant-enter");
        Check(counts[^1] > counts[0], "Consecutive enters append empty paragraphs: " + string.Join(" -> ", counts));
        Console.WriteLine("Fix regression verification PASS");
    }
}
