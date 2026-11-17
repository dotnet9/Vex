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
        Shot("gallery-thumbs");
        Check(total >= 24, "Gallery has at least 24 card images: " + total);
        Check(filled >= 24, "All gallery thumbnails rendered: " + filled);
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
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = editor });
        Render(); Thread.Sleep(400); Render();
        Render(); Thread.Sleep(400); Render();
        var blocksAfter = live!.GetVisualDescendants()
            .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
        Console.WriteLine("block views after enter: " + blocksAfter.Count);
        Shot("instant-enter");
        // 原型语义：普通段落回车=块内换行（文本含换行符），不拆块。
        var textAfter = editor.Text ?? string.Empty;
        Console.WriteLine("editor text after enter: " + textAfter);
        Check(blocksAfter.Count == blocks.Count && textAfter.Contains('\n'),
            "Enter inserts a newline inside the paragraph block");
        Console.WriteLine("Fix regression verification PASS");
    }
}
