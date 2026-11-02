using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class ReadingPointerVerification
{
    public static void Run(MainWindow window, string output, bool menus = true)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1280; window.Height = 820;
        vm.Workbench.SelectLayout("read");
        vm.Markdown = "# Pointer verification\n\nVex - 写下想法，看见脉络。\n\n```mermaid\nflowchart LR\n A[Start] --> B[End]\n```\n\nAfter diagram.\n";
        var viewer = window.Workspace.FindControl<Control>("PreviewGuideTarget")!
            .FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        Render();
        if (menus)
        foreach (var scaling in new[] { 1d, 1.25, 1.5, 2 })
        foreach (var theme in new[] { "light", "dark" })
        foreach (var culture in new[] { "zh-CN", "en-US", "ja-JP", "zh-Hant" })
        {
            window.SetRenderScaling(scaling);
            vm.Appearance.SelectThemeByKey(theme); vm.Appearance.SelectLanguageByCulture(culture); Render();
            var text = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
                .First(t => (t.Inlines?.Text ?? t.Text ?? "").StartsWith("Vex -"));
            var menu = text.ContextMenu!;
            var point = text.TranslatePoint(new Point(30, 10), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right);
            for (var i = 0; i < 8; i++) Render();
            Check(menu.IsOpen, "Reading text opens its menu through the actual right pointer button");
            var scroll = menu.GetVisualDescendants().OfType<ScrollViewer>().First();
            var rows = menu.Items.OfType<MenuItem>().ToArray();
            foreach (var label in menu.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
                Check(Math.Abs(label.LineHeight - 22.4) < .01, "Reading menu resets inherited document line height");
            Console.WriteLine($"READING MENU {scaling}/{theme}/{culture} bounds={menu.Bounds}, viewport={scroll.Viewport}, extent={scroll.Extent}");
            if (scaling == 1) window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"reading-menu-{theme}-{culture}.png"));
            Check(rows.Length == 2 && scroll.Extent.Height <= scroll.Viewport.Height + .5,
                "Two reading actions fit without scrolling: " + theme + "/" + culture);
            foreach (var row in rows)
            {
                var origin = row.TranslatePoint(default, scroll)!.Value;
                Check(origin.Y >= 0 && origin.Y + row.Bounds.Height <= scroll.Bounds.Height + .5,
                    "Reading menu row is fully visible: " + row.Header);
            }
            menu.Close(); Render();
        }
        window.SetRenderScaling(1);
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        var deadline = DateTime.UtcNow.AddSeconds(12);
        Image? image;
        do
        {
            Render();
            image = viewer.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("MdMermaid"))?
                .GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Source is not null);
            if (image is not null) break;
            Thread.Sleep(10);
        } while (DateTime.UtcNow < deadline);
        Check(image is not null, "Mermaid renders before pointer interaction");
        var diagramPoint = image!.TranslatePoint(new Point(image.Bounds.Width / 2, image.Bounds.Height / 2), window)!.Value;
        window.MouseMove(diagramPoint);
        window.MouseDown(diagramPoint, MouseButton.Left); window.MouseUp(diagramPoint, MouseButton.Left); Render();
        Check(!viewer.HasSelection, "Clicking a diagram leaves no phantom text selection");
        Check(vm.Markdown.Contains("A[Start] --> B[End]"), "Diagram click preserves source");
        var diagram = image.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("MdMermaid"));
        var sourceCard = diagram.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("diagram-source"));
        var editor = sourceCard.GetVisualDescendants().OfType<TextBox>().Single();
        Check(sourceCard.IsVisible && editor.Text == "flowchart LR\n A[Start] --> B[End]" && editor.IsKeyboardFocusWithin,
            "One diagram click immediately focuses its complete editable source");
        Check(!diagram.GetVisualDescendants().OfType<Button>().Any(b => Equals(b.Content, "编辑"))
            && !diagram.GetVisualDescendants().OfType<TextBlock>().Any(b => (b.Text ?? "").Contains("Ctrl+Enter")),
            "Diagram editing has no extra Edit button or bottom instruction text");
        var previewCard = diagram.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("MdMermaidPreview"));
        var sourceOrigin = sourceCard.TranslatePoint(default, diagram)!.Value;
        var previewOrigin = previewCard.TranslatePoint(default, diagram)!.Value;
        Check(sourceCard.Bounds.Height > 0 && previewOrigin.Y >= sourceOrigin.Y + sourceCard.Bounds.Height - .5,
            "Diagram source occupies its own row above the preview");
        Check(editor.VerticalContentAlignment == global::Avalonia.Layout.VerticalAlignment.Top
            && Math.Abs(previewCard.Bounds.Width - sourceCard.Bounds.Width) < 2,
            $"Source starts at the top and the preview card fills the same content width: align={editor.VerticalContentAlignment}, preview={previewCard.Bounds}, source={sourceCard.Bounds}");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "diagram-source-open.png"));
        var badge = diagram.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "mermaid"));
        void Click(Control control)
        {
            control.BringIntoView(); Render();
            var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left); Render();
        }
        Click(editor);
        Check(sourceCard.IsVisible && editor.IsKeyboardFocusWithin, "Clicking editable source keeps it open and focused");
        Click(badge);
        Check(sourceCard.IsVisible && editor.IsKeyboardFocusWithin, "Clicking the same diagram badge retains the active edit");
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Render();
        Check(!editor.IsEffectivelyVisible, "Escape returns from diagram editing to the preview");
        Click(badge);
        editor.SelectAll(); window.KeyTextInput("flowchart LR\n A[Start] --> C[Updated]"); Render();
        Check(editor.Text!.Contains("C[Updated]"), "Direct source editor accepts keyboard text input");
        var afterText = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
            .Single(t => (t.Inlines?.Text ?? t.Text) == "After diagram.");
        Click(afterText);
        var commitDeadline = DateTime.UtcNow.AddSeconds(10);
        while (viewer.CurrentModel.Source != vm.Markdown && DateTime.UtcNow < commitDeadline) { Render(); Thread.Sleep(10); }
        Check(vm.Markdown.Contains("C[Updated]") && !viewer.GetVisualDescendants().OfType<TextBox>().Any(t => t.IsEffectivelyVisible),
            "Clicking another paragraph commits the diagram and automatically hides the source");
        vm.Workbench.UndoDocument(); Render();
        Check(vm.Markdown.Contains("A[Start] --> B[End]"), "One undo restores the diagram before the edit");
        vm.Workbench.RedoDocument(); Render();
        Check(vm.Markdown.Contains("C[Updated]"), "One redo restores the diagram edit");
        void WaitDocument()
        {
            var until = DateTime.UtcNow.AddSeconds(12);
            do { Render(); Thread.Sleep(10); } while (viewer.CurrentModel.Source != vm.Markdown && DateTime.UtcNow < until);
            Check(viewer.CurrentModel.Source == vm.Markdown, "Diagram fixture finishes rendering the current document");
        }
        Image CurrentImage()
        {
            var until = DateTime.UtcNow.AddSeconds(12);
            do
            {
                Render();
                var current = viewer.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("MdMermaid"))
                    .SelectMany(b => b.GetVisualDescendants().OfType<Image>()).FirstOrDefault(i => i.Source is not null);
                if (current is not null) return current;
                Thread.Sleep(10);
            } while (DateTime.UtcNow < until);
            throw new TimeoutException("Mermaid image did not render.");
        }
        WaitDocument();
        vm.Markdown = "# Inserted ahead\n\n" + vm.Markdown; WaitDocument();
        Click(CurrentImage());
        var rebasedEditor = viewer.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsEffectivelyVisible);
        Check(rebasedEditor.Text!.Contains("C[Updated]"), "Reopening after preceding text changes uses the current source model");
        rebasedEditor.SelectAll(); window.KeyTextInput("flowchart LR\n A --> SAVED"); Render();
        CodeWF.Avalonia.Markdown.Mermaid.MermaidEditing.CommitPendingEdits(viewer); WaitDocument();
        Check(vm.Markdown.StartsWith("# Inserted ahead") && vm.Markdown.Contains("A --> SAVED"),
            "Saving with focus in diagram source commits at its rebased document offset");
        var unchanged = vm.Markdown;
        Click(CurrentImage());
        CodeWF.Avalonia.Markdown.Mermaid.MermaidEditing.CommitPendingEdits(viewer); Render();
        Check(vm.Markdown == unchanged, "Opening and closing unchanged source leaves document text untouched");
        Click(CurrentImage());
        var oldEditor = viewer.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsEffectivelyVisible);
        oldEditor.Text = "flowchart LR\n STALE --> OLD";
        vm.Markdown = "# A different document\n\nThis must never be replaced by an old diagram edit.";
        WaitDocument();
        CodeWF.Avalonia.Markdown.Mermaid.MermaidEditing.CommitPendingEdits(viewer); Render();
        Check(vm.Markdown.StartsWith("# A different document") && !vm.Markdown.Contains("STALE"),
            "An outdated diagram editor cannot overwrite a newly loaded document");
        foreach (var theme in new[] { "light", "dark" })
        {
            const string firstSource = "flowchart LR\n FIRST --> ONE";
            const string lastSource = "flowchart LR\n LAST --> TWO";
            var document = "```mermaid\n" + firstSource + "\n```\n\n"
                + string.Join("\n\n", Enumerable.Range(0, 160).Select(i => "Paragraph " + i + " separates the two diagrams."))
                + "\n\n```mermaid\n" + lastSource + "\n```\n";
            vm.Appearance.SelectThemeByKey(theme);
            vm.Markdown = document;
            var scrollHost = viewer.GetVisualAncestors().OfType<ScrollViewer>().First();
            Image? lastImage = null;
            var limit = DateTime.UtcNow.AddSeconds(15);
            do
            {
                Render(); scrollHost.ScrollToEnd(); Render();
                lastImage = viewer.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("MdMermaid"))
                    .SelectMany(b => b.GetVisualDescendants().OfType<Image>()).LastOrDefault(i => i.Source is not null);
                if (viewer.CurrentModel?.Source == document && lastImage is not null
                    && lastImage.TranslatePoint(default, scrollHost) is { } position && position.Y >= 0
                    && position.Y + lastImage.Bounds.Height <= scrollHost.Bounds.Height) break;
                Thread.Sleep(10);
            } while (DateTime.UtcNow < limit);
            Check(lastImage is not null && viewer.CurrentModel?.Source == document, "Long document renders its tail diagram: " + theme);
            var blocks = viewer.GetVisualDescendants().OfType<WindowedStackPanel>().First();
            Check(blocks.IsVirtualizing, "Diagram regression runs with block virtualization: " + theme);
            Console.WriteLine($"DIAGRAM VIRTUAL {theme}: firstRealized={blocks.GetRealizedItem(0) is not null}, realized={blocks.RealizedItemCount}/{blocks.ItemCount}");
            Click(lastImage!);
            var tail = lastImage!.GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("MdMermaid"));
            var tailSource = tail.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("diagram-source"));
            var tailEditor = tailSource.GetVisualDescendants().OfType<TextBox>().Single();
            Check(tailSource.IsVisible && tailEditor.Text == lastSource && tailEditor.IsKeyboardFocusWithin,
                "A diagram after virtualized blocks immediately edits its own source: " + theme);
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"diagram-source-tail-{theme}.png"));
            tailEditor.SelectAll(); window.KeyTextInput("flowchart LR\n LAST --> EDITED"); Render();
            var before = scrollHost.Offset.Y;
            // 点 viewer 外、窗体内的已处理工具条区域，不能依赖 viewer 的冒泡事件。
            var toolbar = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible
                && !b.GetVisualAncestors().Contains(viewer) && b.Name == "EditorModeButton");
            var outside = toolbar.TranslatePoint(new Point(toolbar.Bounds.Width / 2, toolbar.Bounds.Height / 2), window)!.Value;
            window.MouseMove(outside); window.MouseDown(outside, MouseButton.Left); window.MouseUp(outside, MouseButton.Left);
            limit = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < limit)
            {
                Render();
                if (viewer.CurrentModel.Source == vm.Markdown && vm.Markdown.Contains("LAST --> EDITED")) break;
                Thread.Sleep(10);
            }
            for (var settle = 0; settle < 25; settle++) { Render(); Thread.Sleep(10); }
            Check(vm.Markdown.Contains(firstSource) && vm.Markdown.Contains("LAST --> EDITED"),
                "Only the virtualized tail fence is committed: " + theme);
            Check(!viewer.GetVisualDescendants().OfType<TextBox>().Any(t => t.IsEffectivelyVisible), "Outside toolbar click hides the source: " + theme);
            Check(scrollHost.Offset.Y > 1000 && scrollHost.Offset.Y > before - 600,
                "Editing deep in the document preserves its viewport instead of jumping to the start: " + theme);
            vm.Workbench.ToggleLayouts(); Render();
        }
        if (menus) ContextMenuLayoutVerification.Run(window, output);
    }
}
