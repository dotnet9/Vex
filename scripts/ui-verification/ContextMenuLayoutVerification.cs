using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;
using SourceEditor = CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView;

internal static class ContextMenuLayoutVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var original = vm.Markdown;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Press(Key key, PhysicalKey physical)
        {
            window.KeyPress(key, RawInputModifiers.None, physical, null);
            window.KeyRelease(key, RawInputModifiers.None, physical, null);
            Render();
        }
        void Inspect(Control panel, string label)
        {
            var scroll = panel.GetVisualDescendants().OfType<ScrollViewer>().First();
            var origin = panel.TranslatePoint(default, window)!.Value;
            Console.WriteLine($"CONTEXT {label}: {origin}, {panel.Bounds.Size}, viewport={scroll.Viewport}, extent={scroll.Extent}");
            Check(origin.X >= -.5 && origin.Y >= -.5
                && origin.X + panel.Bounds.Width <= window.ClientSize.Width + .5
                && origin.Y + panel.Bounds.Height <= window.ClientSize.Height + .5,
                "Menu fits the client at the pointer: " + label);
            foreach (var header in panel.GetVisualDescendants().OfType<ContentPresenter>()
                .Where(p => p.Name == "PART_HeaderPresenter"))
            foreach (var text in header.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
            {
                Console.WriteLine($"CONTEXT TEXT {label}: {text.Text} bounds={text.Bounds.Size}, layout={text.TextLayout.WidthIncludingTrailingWhitespace}x{text.TextLayout.Height}");
                Check(text.TextLayout.Height <= text.Bounds.Height + .5
                    && text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + .5,
                    "Menu label has a complete line box: " + text.Text);
            }
            if (scroll.Extent.Height > scroll.Viewport.Height + .5)
            {
                var point = scroll.TranslatePoint(new Point(scroll.Bounds.Width / 2, scroll.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point); Render();
                Console.WriteLine($"CONTEXT WHEEL {label}: target={window.InputHitTest(point)}, before={scroll.Offset}, viewport={scroll.Viewport}, extent={scroll.Extent}");
                window.MouseWheel(point, new Vector(0, -100)); Render();
                if (scroll.Offset.Y + scroll.Viewport.Height < scroll.Extent.Height - .5)
                {
                    Console.WriteLine($"CONTEXT WHEEL FAILED {label}: after={scroll.Offset}, viewport={scroll.Viewport}, extent={scroll.Extent}");
                    window.CaptureRenderedFrame()?.Save(Path.Combine(output, "context-wheel-failure.png"));
                }
                Check(scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - .5,
                    "Long menu reaches its last action through the wheel: " + label);
                window.MouseWheel(point, new Vector(0, 100)); Render();
            }
        }
        void Open(Control host, Point point, string label)
        {
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right);
            for (var i = 0; i < 4; i++) Render();
            var menu = host.ContextMenu!;
            if (!menu.IsOpen)
            {
                Console.WriteLine($"MENU POINTER {label}: point={point}, host={host.Bounds}, origin={host.TranslatePoint(default, window)}, visible={host.IsEffectivelyVisible}");
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, "context-menu-pointer-failure.png"));
            }
            Check(menu.IsOpen, "Actual right click opens " + label);
            Inspect(menu, label);
            foreach (var branch in menu.Items.OfType<MenuItem>().Where(item => item.Items.Count > 0))
            {
                window.MouseMove(new Point(2, window.ClientSize.Height - 2));
                Check(menu.Focus(), "Root menu accepts focus before keyboard navigation");
                for (var step = 0; step <= menu.Items.Count && !ReferenceEquals(menu.SelectedItem, branch); step++)
                    Press(Key.Down, PhysicalKey.ArrowDown);
                Check(ReferenceEquals(menu.SelectedItem, branch), "Down arrow reaches the submenu row: " + branch.Header);
                Press(Key.Right, PhysicalKey.ArrowRight);
                Check(branch.IsSubMenuOpen, "Right arrow opens the selected submenu: " + branch.Header);
                var popup = branch.GetVisualDescendants().OfType<Popup>().First(p => p.IsOpen);
                Inspect(popup.Child!, label + "/" + branch.Header);
                Press(Key.Escape, PhysicalKey.Escape);
                if (branch.IsSubMenuOpen)
                    Console.WriteLine($"SUBMENU ESCAPE root={menu.IsOpen}, branch={branch.IsSubMenuOpen}, popup={popup.IsOpen}, focus={window.FocusManager?.GetFocusedElement()}, selected={menu.SelectedItem}, child={branch.SelectedItem}");
                Check(!branch.IsSubMenuOpen, "Escape closes the active submenu: " + branch.Header);
            }
            Check(menu.Focus(), "Context menu accepts keyboard focus after submenu inspection");
            Press(Key.Escape, PhysicalKey.Escape);
            Check(!menu.IsOpen, "Escape unwinds submenus and closes " + label);
        }

        foreach (var scaling in new[] { 1d, 1.5, 2 })
        foreach (var size in new[] { new Size(800, 480), new Size(1280, 820) })
        foreach (var theme in new[] { "light", "dark" })
        foreach (var language in new[] { "zh-CN", "en-US", "ja-JP", "zh-Hant" })
        {
            window.SetRenderScaling(scaling);
            window.Width = size.Width; window.Height = size.Height;
            vm.Appearance.SelectThemeByKey(theme); vm.Appearance.SelectLanguageByCulture(language);
            vm.Workbench.SelectLayout("source"); Render();
            var source = window.GetVisualDescendants().OfType<SourceEditor>().Single();
            foreach (var local in new[] { new Point(80, 25), new Point(source.Bounds.Width - 20, source.Bounds.Height - 20) })
                Open(source, source.TranslatePoint(local, window)!.Value, $"source/{scaling}/{size}/{theme}/{language}/{local}");
            if (scaling == 1 && size.Width == 800 && theme == "light" && language == "ja-JP")
            {
                var point = source.TranslatePoint(new Point(80, 25), window)!.Value;
                window.MouseDown(point, MouseButton.Right); window.MouseUp(point, MouseButton.Right); Render();
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, "source-menu-japanese.png"));
                source.ContextMenu!.Close(); Render();
            }
            vm.Workbench.SelectLayout("instant"); Render();
            var instant = window.GetVisualDescendants().OfType<MarkdownInstantView>().Single();
            Open(instant.Editor, instant.Editor.TranslatePoint(new Point(instant.Bounds.Width - 20, instant.Bounds.Height - 20), window)!.Value,
                $"instant/{scaling}/{size}/{theme}/{language}");
            var files = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
            var filesVm = (ShellFilesViewModel)files.DataContext!;
            foreach (var list in new[] { false, true })
            {
                if (filesVm.IsListView != list) filesVm.ToggleListView();
                Render();
                var host = list ? (Control)files.FindControl<ListBox>("FileList")! : files.FindControl<TreeView>("FileTree")!;
                Check(host.IsEffectivelyVisible, "File navigation menu target is visible");
                Open(host, host.TranslatePoint(new Point(30, host.Bounds.Height - 15), window)!.Value, $"files/{list}/{scaling}/{size}/{theme}/{language}");
            }
            Check(vm.Markdown == original, "Opening and dismissing menus preserves the document");
        }

        window.SetRenderScaling(1); window.Width = 1280; window.Height = 820;
        vm.Appearance.SelectThemeByKey("light"); vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Workbench.SelectLayout("read");
        vm.Markdown = "# Heading menu\n\nParagraph menu\n\n```csharp\nConsole.WriteLine(1);\n```\n\n| Header |\n| --- |\n| Cell menu |\n";
        Render();
        var viewer = window.Workspace.FindControl<Control>("PreviewGuideTarget")!.FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
        foreach (var prefix in new[] { "Heading menu", "Paragraph menu", "Console.WriteLine", "Cell menu" })
        {
            var text = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
                .First(t => (t.Inlines?.Text ?? t.Text ?? "").StartsWith(prefix, StringComparison.Ordinal));
            text.BringIntoView(); Render();
            Open(text, text.TranslatePoint(new Point(12, text.Bounds.Height / 2), window)!.Value, "reading/" + prefix);
        }
        vm.Markdown = original; Render();
    }
}
