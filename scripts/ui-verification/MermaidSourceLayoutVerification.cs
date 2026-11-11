using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

internal static class MermaidSourceLayoutVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var sources = global::Markdig.Markdown.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
                "docs", "演示文档", "Mermaid 图表图鉴.md")))
            .OfType<Markdig.Syntax.FencedCodeBlock>()
            .Where(block => block.Info == "mermaid").Select(block => block.Lines.ToString().Replace("\r\n", "\n").TrimEnd('\n')).ToArray();
        Check(sources.Length == 24, "All 24 bundled Mermaid types are covered by source layout regression");
        window.Width = 1400; window.Height = 1000;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        void Click(Control control)
        {
            control.BringIntoView(); Render();
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point); Render();
            point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Render();
        }
        // 阅读态放大对话框是库内 internal 类型，挂在窗口 OverlayLayer 上，按样式类从视觉树识别。
        bool OverlayOpen() => window.GetVisualDescendants().OfType<Panel>().Any(panel => panel.Classes.Contains("MdMediaZoomDialog"));
        foreach (var theme in new[] { "light", "dark" })
        foreach (var layout in new[] { "read", "instant" })
        foreach (var source in sources)
        {
            vm.Appearance.SelectThemeByKey(theme); vm.Workbench.SelectLayout(layout);
            vm.Markdown = "# Diagram editing\n\n```mermaid\n" + source + "\n```\n\nAfter diagram.\n";
            Render();
            var root = layout == "read" ? window.Workspace.FindControl<Control>("PreviewGuideTarget")!
                : window.Workspace.FindControl<Control>("InstantGuideTarget")!;
            var scroll = root.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroll.ScrollToHome();
            Image? image = null;
            var deadline = DateTime.UtcNow.AddSeconds(15);
            do
            {
                Render();
                if (layout == "read" && root.GetVisualDescendants().OfType<MarkdownViewer>().First().CurrentModel.Source != vm.Markdown)
                {
                    Thread.Sleep(10);
                    continue;
                }
                image = root.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("MdMermaidPreview"))
                    .SelectMany(border => border.GetVisualDescendants().OfType<Image>()).FirstOrDefault(item => item.Source is not null);
                if (image is not null) break;
                Thread.Sleep(10);
            } while (DateTime.UtcNow < deadline);
            var kind = source.Split('\n')[0];
            Check(image is not null, $"Diagram renders before source click: {layout}/{theme}/{kind}");
            if (layout == "read")
            {
                // 阅读页签是纯浏览：图表照常出图，点击不再展开源码框而是弹放大查看对话框
                // （MdMediaZoomDialog 遮罩），mermaid 徽标在交互包装里按 IsDiagramEditingEnabled=false 隐藏。
                var documentBeforeClick = vm.Markdown;
                Click(image!);
                Check(!root.GetVisualDescendants().OfType<TextBox>().Any(input => input.IsEffectivelyVisible),
                    $"Read tab keeps diagrams browse-only without a source editor: {theme}/{kind}");
                Check(vm.Markdown == documentBeforeClick,
                    $"Read tab diagram click never rewrites the document: {theme}/{kind}");
                var browseDiagram = image!.GetVisualAncestors().OfType<Border>().FirstOrDefault(border => border.Classes.Contains("MdMermaid"));
                var badges = browseDiagram?.GetVisualDescendants().OfType<Button>().Where(b => Equals(b.Content, "mermaid")).ToArray() ?? [];
                // 徽标仍留在视觉树里（交互包装承担放大点击），只按「不可见」断言；与上方源码框的 IsEffectivelyVisible 口径一致。
                Check(badges.All(badge => !badge.IsEffectivelyVisible),
                    $"Read tab hides the mermaid badge: {theme}/{kind}; badges={badges.Length}, visible={badges.Count(badge => badge.IsEffectivelyVisible)}");
                // 点击应弹出放大对话框；断言其出现后按 Esc 收起，避免遮罩残留挡住后续迭代与布局切换后的点击。
                var zoomDeadline = DateTime.UtcNow.AddSeconds(5);
                while (!OverlayOpen() && DateTime.UtcNow < zoomDeadline) { Render(); Thread.Sleep(10); }
                Check(OverlayOpen(), $"Read tab diagram click opens the media zoom dialog: {theme}/{kind}");
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                zoomDeadline = DateTime.UtcNow.AddSeconds(5);
                while (OverlayOpen() && DateTime.UtcNow < zoomDeadline) { Render(); Thread.Sleep(10); }
                Check(!OverlayOpen(), $"Read tab zoom dialog closes on Escape: {theme}/{kind}");
                continue;
            }
            Click(image!);
            var editor = root.GetVisualDescendants().OfType<TextBox>().SingleOrDefault(input =>
                input.Classes.Contains("MdMermaidSourceInput") && input.IsEffectivelyVisible)
                ?? throw new InvalidOperationException($"Mermaid source editor did not open: {layout}/{theme}/{kind}");
            Check(editor.IsKeyboardFocusWithin && editor.Text == source,
                $"One click focuses complete source: {layout}/{theme}/{kind}; focus={editor.IsKeyboardFocusWithin}, lengths={editor.Text?.Length}/{source.Length}");
            var preview = root.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("MdMermaidPreview"));
            var origin = editor.TranslatePoint(default, root)!.Value;
            var previewOrigin = preview.TranslatePoint(default, root)!.Value;
            Check(previewOrigin.Y >= origin.Y + editor.Bounds.Height + 9 && Math.Abs(preview.Bounds.Width - editor.Bounds.Width) < 2,
                $"Source has a separate full-width row above the diagram: {layout}/{theme}/{kind}");
            Check(editor.Padding == new Thickness(14, 12) && editor.CornerRadius == new CornerRadius(8)
                && editor.LineHeight == 24, $"Source keeps the established code card style: {layout}/{theme}/{kind}");
            if (kind == "journey") window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"journey-source-{layout}-{theme}.png"));
            editor.SelectAll(); window.KeyTextInput(source + "\n%% Source edit verification"); Render();
            var paragraph = root.GetVisualDescendants().OfType<SelectableTextBlock>()
                .First(text => (text.Inlines?.Text ?? text.Text) == "After diagram.");
            Click(paragraph);
            deadline = DateTime.UtcNow.AddSeconds(10);
            do { Render(); if (vm.Markdown.Contains("%% Source edit verification")) break; Thread.Sleep(10); }
            while (DateTime.UtcNow < deadline);
            if (!vm.Markdown.Contains("%% Source edit verification") || editor.IsEffectivelyVisible)
            {
                Console.WriteLine($"OUTSIDE CLICK sourceSaved={vm.Markdown.Contains("%% Source edit verification")}, editorVisible={editor.IsEffectivelyVisible}, paragraph={paragraph.TranslatePoint(default, window)}, paragraphSize={paragraph.Bounds.Size}, scroll={scroll.Offset}");
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, "diagram-outside-click-failure.png"));
            }
            Check(vm.Markdown.Replace("\r\n", "\n").Contains(source.Replace("\r\n", "\n") + "\n%% Source edit verification")
                && !root.GetVisualDescendants().OfType<TextBox>().Any(input => input.Classes.Contains("MdMermaidSourceInput") && input.IsEffectivelyVisible),
                $"Outside click commits source and hides its card: {layout}/{theme}/{kind}");
        }
    }

    private static void Check([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
