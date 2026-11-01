using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

internal static class TablePreviewVerification
{
    public static void Run(MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var original = vm.Markdown;
        void Render()
        {
            for (var i = 0; i < 5; i++) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        }
        void SetMarkdown(string markdown)
        {
            CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Vex.Core.Messaging.MarkdownTextChangedCommand(
                markdown, 1, 1, markdown.Count(c => c == '\n') + 1));
            Render();
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Click(Control control)
        {
            var point = control.TranslatePoint(new Rect(control.Bounds.Size).Center, window)!.Value;
            window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); Render();
        }
        void Settle()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 350)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Render(); Thread.Sleep(8);
            }
        }
        void WaitForClose()
        {
            var deadline = DateTime.UtcNow.AddSeconds(3);
            while (window.GetVisualDescendants().OfType<TablePreviewView>().Any() && DateTime.UtcNow < deadline)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Render(); Thread.Sleep(8);
            }
            Check(!window.GetVisualDescendants().OfType<TablePreviewView>().Any(), "Preview closes through Escape or the actual close button");
        }
        void CheckFrame(TablePreviewView view)
        {
            var card = view.FindControl<Border>("PreviewCard")!;
            var origin = card.TranslatePoint(default, window)!.Value;
            Console.WriteLine($"TABLE PREVIEW {window.ClientSize}: {origin}, {card.Bounds.Size}");
            Check(Math.Abs(card.Bounds.Width - Math.Min(1200, window.ClientSize.Width - 64)) < 1
                && origin.X >= 31 && origin.Y >= 31
                && origin.X + card.Bounds.Width <= window.ClientSize.Width - 31
                && origin.Y + card.Bounds.Height <= window.ClientSize.Height - 31
                && Math.Abs(origin.Y + card.Bounds.Height / 2 - window.ClientSize.Height / 2) < 1,
                "Table preview uses the desktop width and stays inside the window");
            var close = view.FindControl<Button>("ClosePreview")!;
            foreach (var text in close.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)))
                Check(text.TextLayout.Height <= text.Bounds.Height + .5
                    && text.TextLayout.WidthIncludingTrailingWhitespace <= text.Bounds.Width + .5, "Preview close label is complete");
        }
        vm.Workbench.SelectLayout("read");
        const string table = "| 时间 | 地点 | 说明 |\n| --- | --- | --- |\n| 09:00 | 人民公园 | 完整内容 |\n| 14:00 | 宽窄巷子 | 第二行 |\n";
        SetMarkdown(table);
        foreach (var size in new[] { new Size(800, 480), new Size(1440, 900) })
        foreach (var theme in new[] { "light", "dark" })
        foreach (var culture in new[] { "zh-CN", "en-US", "ja-JP", "zh-Hant" })
        {
            window.Width = size.Width; window.Height = size.Height;
            vm.Appearance.SelectThemeByKey(theme); vm.Appearance.SelectLanguageByCulture(culture); Render();
            var viewer = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
            var deadline = DateTime.UtcNow.AddSeconds(5);
            Button[] actions;
            do
            {
                Render();
                actions = viewer.GetVisualDescendants().OfType<Button>()
                    .Where(b => b.IsEffectivelyVisible && b.Classes.Contains("md-table-tool")).ToArray();
                if (actions.Length == 3 && viewer.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!.CurrentModel.Source == table) break;
                Thread.Sleep(10);
            } while (DateTime.UtcNow < deadline);
            Check(viewer.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!.CurrentModel.Source == table,
                "Preview waits for the current document to finish rendering");
            if (actions.Length != 3)
            {
                Console.WriteLine($"TABLE ACTIONS count={actions.Length}, layout={vm.Workbench.WorkspaceLayout}, viewer={viewer.Bounds}, visible={viewer.IsEffectivelyVisible}");
                window.CaptureRenderedFrame()?.Save(Path.Combine(output, "table-actions-failure.png"));
            }
            Check(actions.Length == 3, "Reading table exposes all three actions");
            Click(actions[2]);
            Settle();
            var view = window.GetVisualDescendants().OfType<TablePreviewView>().Single();
            CheckFrame(view);
            var grid = view.FindControl<DataGrid>("PreviewTable")!;
            if (grid.Columns.Count != 3 || grid.ItemsSource.Cast<object>().Count() != 2)
                Console.WriteLine($"PREVIEW DATA columns={grid.Columns.Count}, rows={System.Text.Json.JsonSerializer.Serialize(grid.ItemsSource.Cast<string[]>())}");
            Check(grid.Columns.Count == 3 && grid.ItemsSource.Cast<object>().Count() == 2,
                "Table preview preserves its header, columns and every data row");
            Check(view.FindControl<Button>("ClosePreview")!.IsKeyboardFocusWithin, "Preview initially focuses its close action");
            window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, null);
            window.KeyRelease(Key.F6, RawInputModifiers.None, PhysicalKey.F6, null); Render();
            Check(!vm.Workbench.IsPresenting, "Modal preview prevents presentation shortcuts reaching the workspace");
            if (culture == "ja-JP") window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"table-preview-{size.Width}-{theme}.png"));
            if (culture == "zh-CN")
            {
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Render();
            }
            else Click(view.FindControl<Button>("ClosePreview")!);
            WaitForClose();
            Check(vm.FindBar.IsVisible, "Closing preview does not consume Escape in the find bar behind it");
            Check(vm.Markdown == table, "Opening and closing table preview preserves the document");
        }
        window.Width = 800; window.Height = 480; Render();
        var rows = Enumerable.Range(0, 1001).Select(row => (IReadOnlyList<string>)Enumerable.Range(0, 15)
            .Select(column => row == 0 ? $"Column {column + 1}" : $"Row {row} / {column + 1}").ToArray()).ToArray();
        var task = TablePreviewView.Show(rows, window); Render(); Settle();
        var largeView = window.GetVisualDescendants().OfType<TablePreviewView>().Single();
        CheckFrame(largeView);
        var largeGrid = largeView.FindControl<DataGrid>("PreviewTable")!;
        Check(largeGrid.GetVisualDescendants().OfType<DataGridRow>().Count() < 100,
            "A thousand-row table realizes only the visible desktop rows");
        var vertical = largeGrid.GetVisualDescendants().OfType<ScrollBar>().Single(s => s.Name == "PART_VerticalScrollbar");
        var horizontal = largeGrid.GetVisualDescendants().OfType<ScrollBar>().Single(s => s.Name == "PART_HorizontalScrollbar");
        var presenter = largeGrid.GetVisualDescendants().OfType<Control>().Single(c => c.Name == "PART_RowsPresenter");
        var point = presenter.TranslatePoint(new Rect(presenter.Bounds.Size).Center, window)!.Value;
        window.MouseMove(point); window.MouseWheel(point, new Vector(0, -10000)); Render();
        Check(vertical.Maximum > 0 && vertical.Value >= vertical.Maximum - 1, "Real wheel reaches the last preview row");
        window.MouseWheel(point, new Vector(-10000, 0)); Render();
        Check(horizontal.Maximum > 0 && horizontal.Value >= horizontal.Maximum - 1, "Real horizontal wheel reaches the final preview column");
        Check(largeGrid.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Row 1000 / 15"), "The last cell of a large table remains readable");
        window.Width = 1440; window.Height = 900; Render(); CheckFrame(largeView);
        window.Width = 800; window.Height = 480; Render(); CheckFrame(largeView);
        Click(largeView.FindControl<Button>("ClosePreview")!);
        WaitForClose();
        Check(task.IsCompletedSuccessfully, "The resized preview completes its dialog lifecycle");
        task = TablePreviewView.Show(new IReadOnlyList<string>[] { new[] { "First", "Second" }, new[] { "Third" } }, window, false);
        Render(); Settle();
        var headerless = window.GetVisualDescendants().OfType<TablePreviewView>().Single();
        var headerlessGrid = headerless.FindControl<DataGrid>("PreviewTable")!;
        var headerlessRows = headerlessGrid.ItemsSource.Cast<string[]>().ToArray();
        Check(headerlessGrid.Columns.Count == 2 && headerlessRows.Length == 2
            && headerlessRows[0][0] == "First" && headerlessRows[1][1] == string.Empty,
            "Headerless and uneven tables retain the first row and pad missing cells");
        var outside = new Point(16, window.ClientSize.Height / 2);
        window.MouseMove(outside); window.MouseDown(outside, MouseButton.Left); window.MouseUp(outside, MouseButton.Left);
        WaitForClose();
        Check(task.IsCompletedSuccessfully, "Outside click dismisses the table preview");
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        foreach (var label in new[] { "First document", "Replacement document", "First document" })
        {
            SetMarkdown($"| Header |\n| --- |\n| {label} |\n"); Settle();
            var preview = window.GetVisualDescendants().OfType<Button>()
                .Where(b => b.IsEffectivelyVisible && b.Classes.Contains("md-table-tool")).Last();
            Click(preview); Settle();
            var current = window.GetVisualDescendants().OfType<TablePreviewView>().Single();
            Check(current.FindControl<DataGrid>("PreviewTable")!.ItemsSource.Cast<string[]>().Single()[0] == label,
                "Replacing the document opens the current table, not the previous toolbar closure");
            Click(current.FindControl<Button>("ClosePreview")!); WaitForClose();
        }
        SetMarkdown("A document without a table"); Settle();
        Check(!window.GetVisualDescendants().OfType<Button>().Any(b => b.IsEffectivelyVisible && b.Classes.Contains("md-table-tool")),
            "Removing the final table removes its actions");
        SetMarkdown(original);
    }
}
