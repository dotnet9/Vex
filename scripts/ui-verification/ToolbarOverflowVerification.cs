using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Ursa.Controls;

internal static class ToolbarOverflowVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var original = vm.Markdown;
        var options = window.Workspace.FindControl<ShellEditorOptionsView>("EditorOptions")!;
        var toolbar = options.FindControl<ToolBar>("FormattingToolbar")!;
        void Render()
        {
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            }
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Click(Control target)
        {
            var point = target.TranslatePoint(new Rect(target.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Render();
        }
        foreach (var scaling in new[] { 1d, 1.5, 2 })
        foreach (var size in new[] { new Size(800, 480), new Size(980, 640) })
        foreach (var theme in new[] { "light", "dark" })
        foreach (var culture in new[] { "zh-CN", "en-US", "ja-JP", "zh-Hant" })
        {
            window.SetRenderScaling(scaling); window.Width = size.Width; window.Height = size.Height;
            vm.Appearance.SelectThemeByKey(theme); vm.Appearance.SelectLanguageByCulture(culture);
            vm.Workbench.SelectLayout("source-render"); Render();
            var toggle = toolbar.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "button");
            var popup = toolbar.GetVisualDescendants().OfType<Popup>().Single();
            Click(toggle);
            var label = $"{scaling}/{size}/{theme}/{culture}";
            Check(popup.IsOpen, "Actual pointer opens labeled toolbar overflow: " + label);
            var frame = popup.Child!;
            var scroll = frame.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var panel = frame.GetVisualDescendants().OfType<FormattingOverflowPanel>().Single();
            var origin = frame.TranslatePoint(default, window)!.Value;
            Console.WriteLine($"OVERFLOW {label}: {origin}, {frame.Bounds.Size}, viewport={scroll.Viewport}, extent={scroll.Extent}");
            Check(Math.Abs(frame.Bounds.Width - 260) < .51
                && origin.X >= -.5 && origin.Y >= -.5
                && origin.X + frame.Bounds.Width <= window.ClientSize.Width + .5
                && origin.Y + frame.Bounds.Height <= window.ClientSize.Height + .5,
                "Two-column toolbar menu fits the client: " + label);
            var buttons = panel.Children.OfType<Button>().Where(b => b.IsVisible).ToArray();
            Check(buttons.Length > 0, "Toolbar overflow contains reachable commands");
            foreach (var button in buttons)
            {
                var text = button.GetVisualDescendants().OfType<TextBlock>()
                    .Single(t => t.Classes.Contains("format-overflow-label"));
                Console.WriteLine($"OVERFLOW LABEL {button.Name}: visible={text.IsVisible}, box={text.Bounds.Size}, layout={text.TextLayout.WidthIncludingTrailingWhitespace}x{text.TextLayout.Height}, action={button.Bounds.Size}");
                Check(!string.IsNullOrWhiteSpace(text.Text) && text.Text == AutomationProperties.GetName(button)
                    && text.TextLayout.Height <= text.Bounds.Height + .5
                    && text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + .5
                    && button.Bounds.Height >= 32,
                    "Complete localized toolbar label: " + text.Text);
            }
            var focusable = panel.Children.Where(c => c is Button or ComboBox && c.IsVisible && c.IsEnabled).ToArray();
            Check(focusable.All(c => c.Bounds.Width > 0 && c.Bounds.Height > 0), "Focusable overflow commands have a visible cell");
            Check(ReferenceEquals(window.FocusManager?.GetFocusedElement(), focusable[0]), "Opening overflow focuses its first command");
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
            window.KeyRelease(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null); Render();
            Check(ReferenceEquals(window.FocusManager?.GetFocusedElement(), focusable[1]), "Tab moves to the next overflow command");
            // 收紧视口以覆盖受限桌面工作区的滚动路径，而不是只检查内容恰好装得下的情况。
            var maxHeight = scroll.MaxHeight;
            scroll.MaxHeight = 100; Render();
            if (scroll.Extent.Height > scroll.Viewport.Height + .5)
            {
                var point = scroll.TranslatePoint(new Rect(scroll.Bounds.Size).Center, window)!.Value;
                window.MouseWheel(point, new Vector(0, -100)); Render();
                Check(scroll.Offset.Y + scroll.Viewport.Height >= scroll.Extent.Height - .5,
                    "Real wheel reaches the final overflow action: " + label);
            }
            scroll.MaxHeight = maxHeight; scroll.Offset = default; Render();
            if (scaling == 1 && culture == "ja-JP")
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"toolbar-overflow-{size.Width}-{theme}.png"));
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Render();
            Check(!popup.IsOpen && toggle.IsChecked != true && ReferenceEquals(window.FocusManager?.GetFocusedElement(), toggle),
                "Escape closes toolbar overflow, resets its toggle and restores focus: " + label);
            Click(toggle);
            var outside = new Point(30, window.ClientSize.Height / 2);
            window.MouseMove(outside);
            window.MouseDown(outside, MouseButton.Left); window.MouseUp(outside, MouseButton.Left); Render();
            if (popup.IsOpen) window.CaptureRenderedFrame()?.Save(Path.Combine(output, "toolbar-outside-failure.png"));
            Check(!popup.IsOpen, "Outside pointer dismisses toolbar overflow: " + label);
            Check(vm.Markdown == original, "Inspecting overflow preserves the document");
        }
        window.SetRenderScaling(1); vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Markdown = "overflow action"; Render();
        var source = window.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>().Single();
        source.Editor.Select(0, 8);
        var finalToggle = toolbar.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "button");
        var finalPopup = toolbar.GetVisualDescendants().OfType<Popup>().Single();
        Click(finalToggle);
        Check(source.Editor.SelectionLength == 8, "Opening overflow retains the editor selection");
        Click(options.FindControl<Button>("FormatInlineCode")!);
        Check(!finalPopup.IsOpen && vm.Markdown == "`overflow` action", "An actual overflow command formats the selection and closes the menu");
        var undo = options.FindControl<Button>("FormatUndo")!;
        var redo = options.FindControl<Button>("FormatRedo")!;
        Check(undo.Focusable && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(undo)),
            "Main formatting commands are keyboard focusable and named");
        undo.Focus(); Render();
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
        window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
        Check(ReferenceEquals(window.FocusManager?.GetFocusedElement(), redo), "ArrowRight moves between main formatting commands");
        Check(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(finalToggle)), "Toolbar overflow toggle has an accessible name");

        vm.Workbench.SelectLayout("instant"); Render();
        var instant = window.GetVisualDescendants().OfType<Vex.Modules.Workspace.Views.MarkdownInstantView>().Single();
        var hint = instant.FindControl<TextBlock>("InstantKeyboardHint")!;
        Check(hint.IsEffectivelyVisible && hint.Text!.Contains("Ctrl+Enter") && hint.Text.Contains("Esc")
            && hint.Text.Contains("Shift+Enter") && hint.Text.Contains("Ctrl+B") && hint.Text.Contains("Ctrl+I"),
            "Instant editor keeps its complete keyboard shortcut hint visible");

        vm.FindBar.IsVisible = true;
        vm.FindBar.IsRegex = true;
        vm.FindBar.SearchText = "["; Render();
        var findError = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(text => text.Classes.Contains("find-error"));
        Check(findError?.IsEffectivelyVisible == true && !string.IsNullOrWhiteSpace(findError.Text),
            "Invalid regular expression shows an inline actionable error");
        vm.FindBar.IsRegex = false; Render();
        Check(findError?.IsEffectivelyVisible != true, "Disabling regular expression clears the inline error");

        var background = options.FindControl<Button>("SpanBackground")!;
        var mark = background.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("span-background"));
        vm.Appearance.SelectThemeByKey("light"); Render();
        var lightMark = (mark.Background as ISolidColorBrush)?.Color;
        vm.Appearance.SelectThemeByKey("dark"); Render();
        var darkMark = (mark.Background as ISolidColorBrush)?.Color;
        Check(lightMark != darkMark, "Inline mark swatch follows light/dark semantic theme tokens");

        vm.Appearance.SelectThemeByKey("light");
        vm.FindBar.IsVisible = false;
        vm.Workbench.SelectLayout("source-render"); Render();
        vm.Markdown = original; Render();
    }
}
