using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

internal static class OutlineNavigationVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var source = string.Join("\n", Enumerable.Range(1, 50).Select(index =>
            $"# {(index is 35 or 40 ? "同名章节" : "章节 " + index)}\n\n" +
            string.Concat(Enumerable.Range(1, 8).Select(paragraph => $"正文 {index}.{paragraph}。阅读位置跟随，不修改源码。\n\n"))));
        var path = Path.Combine(output, "outline-navigation.md");
        File.WriteAllText(path, source);
        vm.Workbench.SelectLayout("read"); vm.Layout.IsOutlineVisible = true;
        var preview = window.Workspace.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = preview.FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
        var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
        var outlineView = window.GetVisualDescendants().OfType<ShellOutlineView>().Single();
        // 大纲 UI 已下沉到库 MarkdownOutlinePanel（行模型 OutlineRow），旧 ListBox「OutlineList」由模板内部提供。
        var outlinePanel = outlineView.FindControl<MarkdownOutlinePanel>("OutlinePanel")!;
        var list = outlinePanel.GetVisualDescendants().OfType<ListBox>().First();
        var outline = (ShellOutlineViewModel)outlineView.DataContext!;
        var opening = vm.OpenPathAsync(path);
        Wait(() => opening.IsCompleted, "Document opens"); opening.GetAwaiter().GetResult();
        Wait(() => viewer.CurrentModel.Source == source && outline.OutlineItems.Count == 50, "Long document and outline render");
        var target = outline.OutlineItems[39];
        Click(target);
        Wait(() => scroll.Offset.Y > 5000, "Click reaches an offscreen heading");
        Wait(() => Landed(target.Line), "Offscreen heading settles at the reading inset");
        Check(ReferenceEquals(outline.CurrentOutlineItem, target), "Duplicate heading identity uses its source line");
        // 不泵采样队列：维持同一选中行，重现原来的重复点击失效。
        scroll.Offset = default;
        Click(target);
        Wait(() => Landed(target.Line), "Clicking the already selected row navigates again");
        scroll.Focus();
        var earlier = outline.OutlineItems[10];
        ScrollTo(earlier.Line);
        Wait(() => ReferenceEquals(outline.CurrentOutlineItem, earlier), "Reading scroll follows the current chapter");
        Check(scroll.IsFocused, "Reading highlight does not take keyboard focus");
        var held = scroll.Offset;
        Settle(250);
        Check(Math.Abs(held.Y - scroll.Offset.Y) < 1, "Highlight synchronization does not navigate the document");
        list.SelectedItem = target;
        list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Wait(() => Landed(target.Line), "Enter activates the selected heading");
        vm.Workbench.Preferences.ContentZoom = 150;
        list.SelectedItem = earlier;
        list.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Wait(() => Landed(earlier.Line), "Explicit navigation overrides zoom-position restoration");
        Check(vm.Markdown == source, "Outline interactions preserve Markdown");
        using var bitmap = new RenderTargetBitmap(new PixelSize(1280, 820));
        bitmap.Render(window); bitmap.Save(Path.Combine(output, "outline-reading.png"));
        Console.WriteLine("Outline navigation verification passed.");

        void Click(OutlineRow item)
        {
            list.ScrollIntoView(item); Render();
            var row = list.GetVisualDescendants().OfType<ListBoxItem>().Single(control => ReferenceEquals(control.DataContext, item));
            var listScroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
            if (row.TranslatePoint(default, listScroll) is { } rowOrigin)
                listScroll.Offset = new Vector(0, Math.Max(0, listScroll.Offset.Y + rowOrigin.Y - listScroll.Viewport.Height / 2));
            Render();
            var point = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)!.Value;
            Console.WriteLine($"OUTLINE CLICK {item.Line}: row={row.Bounds}; point={point}; hit={window.InputHitTest(point)}; current={outline.CurrentOutlineItem?.Line}; visible={row.IsEffectivelyVisible}");
            window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
            Render();
            Console.WriteLine($"OUTLINE AFTER current={outline.CurrentOutlineItem?.Line}; offset={scroll.Offset}; extent={scroll.Extent}; viewport={scroll.Viewport}");
        }
        bool Landed(int line)
        {
            return viewer.TryGetSourceLineBounds(line, out var bounds)
                && viewer.TranslatePoint(new Point(0, bounds.Top), scroll) is { } point
                && Math.Abs(point.Y - 14) < 2;
        }
        void ScrollTo(int line)
        {
            if (!viewer.TryGetSourceLineBounds(line, out var bounds)) throw new InvalidOperationException("Missing source bounds");
            var point = viewer.TranslatePoint(new Point(0, bounds.Top), scroll)!.Value;
            scroll.Offset = new Vector(0, Math.Max(0, scroll.Offset.Y + point.Y - 14));
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
        void Settle(int milliseconds)
        {
            Render();
            var frame = new DispatcherFrame();
            using var timer = DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
            Dispatcher.UIThread.PushFrame(frame);
            Render();
        }
        void Wait(Func<bool> predicate, string message)
        {
            var until = Environment.TickCount64 + 12000;
            while (!predicate()) { if (Environment.TickCount64 >= until) throw new InvalidOperationException(message + $"; current={outline.CurrentOutlineItem?.Line}; offset={scroll.Offset}; extent={scroll.Extent}"); Settle(20); }
            Settle(200); Check(predicate(), message);
        }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
    }
}
