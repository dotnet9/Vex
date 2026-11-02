using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class SelectionStyleVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var options = window.Workspace.FindControl<ShellEditorOptionsView>("EditorOptions")!;
        var toolbar = options.FindControl<Ursa.Controls.ToolBar>("FormattingToolbar")!;
        var source = window.Workspace.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")!
            .FindControl<MarkdownEditorView>("MarkdownEditor")!;
        var live = window.Workspace.FindControl<Vex.Modules.Workspace.Views.MarkdownInstantView>("InstantGuideTarget")!.Editor;
        const string text = "Target normal";
        void Render()
        {
            LayoutSettle.Run();
            for (var i = 0; i < 4; i++)
            {
                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Click(Control control)
        {
            var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point);
            var deadline = DateTime.UtcNow.AddSeconds(1);
            while (window.InputHitTest(point)?.GetType().Name == "LightDismissOverlayLayer" && DateTime.UtcNow < deadline)
            {
                Thread.Sleep(10); Render();
            }
            window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Render();
        }
        void Escape()
        {
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Render();
        }
        foreach (var theme in new[] { "light", "dark" })
        foreach (var culture in new[] { "zh-CN", "en-US", "zh-Hant", "ja-JP" })
        foreach (var layout in new[] { "source", "instant" })
        foreach (var width in new[] { 1280, 980 })
        {
            window.Width = width; window.Height = 820;
            vm.Layout.SidebarWidth = width == 980 ? 560 : 226;
            vm.Appearance.SelectThemeByKey(theme); vm.Appearance.SelectLanguageByCulture(culture);
            vm.Workbench.SelectLayout(layout); Render();
            var label = $"{theme}/{culture}/{layout}/{width}";
            void Select()
            {
                vm.Markdown = text; Render();
                if (layout == "source") { Check(source.Editor.TextArea.Focus(), "Source focus: " + label); source.Editor.TextArea.Caret.Offset = 0; }
                else
                {
                    live.FocusBlock(0); Render();
                    var box = live.GetVisualDescendants().OfType<TextBox>().Single(box => box.IsEffectivelyVisible);
                    Check(box.Focus(), "Live focus: " + label);
                    box.CaretIndex = box.SelectionStart = box.SelectionEnd = 0;
                }
                for (var i = 0; i < 6; i++) window.KeyPress(Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight, null);
                window.KeyRelease(Key.Right, RawInputModifiers.Shift, PhysicalKey.ArrowRight, null); Render();
                Check((layout == "source" ? source.SelectedText : live.SelectedText) == "Target", "Real keyboard selects target: " + label);
                Escape(); // Dismiss the selection bar before exercising the separate host toolbar.
            }
            MarkdownSpanStylePicker Open(string name)
            {
                var button = options.FindControl<Button>(name)!;
                if (width == 980) Click(toolbar.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name == "button"));
                Click(button);
                var picker = window.GetVisualDescendants().OfType<MarkdownSpanStylePicker>().SingleOrDefault();
                if (picker is null)
                {
                    window.CaptureRenderedFrame()?.Save(Path.Combine(output, "host-picker-failure.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                    throw new InvalidOperationException($"Host did not open {name}; flyout={button.Flyout?.IsOpen}, focus={window.FocusManager?.GetFocusedElement()?.GetType().Name}");
                }
                var bounds = new Rect(picker!.TranslatePoint(default, window)!.Value, picker.Bounds.Size);
                Check(bounds.Left >= 0 && bounds.Right <= window.Bounds.Width && bounds.Top >= 0 && bounds.Bottom <= window.Bounds.Height,
                    "Style picker stays inside client: " + label);
                Check(picker.DataContext is MarkdownSpanStylePickerViewModel, "Host binds shared style model: " + label);
                return picker;
            }
            Select();
            var picker = Open("SpanForeground");
            var hex = picker.FindControl<TextBox>("HexInput")!;
            Click(hex); hex.SelectAll(); window.KeyTextInput("#30953B"); Render();
            Check(vm.Markdown == text, "Typing a draft color leaves source unchanged: " + label);
            Escape();
            Check(vm.Markdown == text, "Escape cancels host color draft: " + label);
            Select(); picker = Open("SpanForeground");
            hex = picker.FindControl<TextBox>("HexInput")!;
            Click(hex); hex.SelectAll(); window.KeyTextInput("#30953B"); Render();
            Click(picker.FindControl<Button>("ApplyColor")!);
            Check(vm.Markdown.Contains("color:#30953B", StringComparison.OrdinalIgnoreCase) && vm.Markdown.Contains(">Target</span> normal"),
                "Apply formats only selected text through host routing: " + label);
            Check(!window.GetVisualDescendants().OfType<MarkdownSpanStylePicker>().Any(), "Apply closes picker: " + label);
            vm.EditorActions.Undo(); Render();
            Check(vm.Markdown == text, "Host Undo restores exact original: " + label);
            Select(); picker = Open("SpanFontSize");
            Click(picker.GetVisualDescendants().OfType<Button>().Single(button => button.Content is double value && value == 22));
            Check(vm.Markdown.Contains("font-size:22px") && vm.Markdown.Contains(">Target</span> normal"), "Size choice formats only selected text: " + label);
            vm.EditorActions.Undo(); Render(); Check(vm.Markdown == text, "Size change is undoable: " + label);
            Select(); picker = Open("SpanBackground");
            ((MarkdownSpanStylePickerViewModel)picker.DataContext!).HexValue = "#FFF3BF";
            Click(picker.FindControl<Button>("ApplyColor")!);
            Check(vm.Markdown.Contains("background-color:#FFF3BF", StringComparison.OrdinalIgnoreCase), "Background uses the same shared picker: " + label);
            vm.EditorActions.Undo(); Render(); Check(vm.Markdown == text, "Background change is undoable: " + label);
            if (width == 980 && culture == "zh-CN")
            {
                Select(); Open("SpanForeground");
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"host-picker-{theme}-{layout}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
                Escape();
            }
        }
    }
}
