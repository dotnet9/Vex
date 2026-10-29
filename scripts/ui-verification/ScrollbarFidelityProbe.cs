using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Services;
using Vex.Modules.Shell.Views;

/// <summary>
/// 一次性探针：滚动条对照原型 workspace-chrome.css:22-35 —— 9px 全透明轨道、2px 内缩的 5px 可见滑块、
/// muted 28% 常驻 / 48% 悬停、pill 圆角，以及点轨道翻页真的能滚（ScrollTrackPaging）。
/// </summary>
internal static class ScrollbarFidelityProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        window.Width = 1280; window.Height = 800; Render();
        // 种一篇足够长的正文，让当前视口真的出竖向滚动条（不绑定具体布局，哪个窗格在滚就量哪个）。
        var longDoc = "# 滚动条\n\n" + string.Join("\n\n", Enumerable.Repeat("一段足够长的正文，用来把阅读视口撑出滚动条，验证轨道与滑块的度量。", 160));
        app.Resolve<IWorkspaceDocumentState>().UpdateDocument(longDoc, null);
        Render();
        var scroll = window.GetVisualDescendants().OfType<ScrollViewer>()
            .First(s => s.IsEffectivelyVisible && s.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
                && s.Extent.Height > s.Viewport.Height + 1);
        var bar = scroll.GetVisualDescendants().OfType<ScrollBar>().First(b => b.Orientation == Orientation.Vertical);
        Check(Math.Abs(bar.Bounds.Width - 9) < 0.5, $"Vertical scrollbar track is the prototype 9px wide: {bar.Bounds.Width:0.#}");
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
        var inner = thumb.GetVisualDescendants().OfType<Border>().Single(b => b.Margin == new Thickness(2));
        Check(Math.Abs(thumb.Bounds.Width - 9) < 0.5 && Math.Abs(inner.Bounds.Width - 5) < 0.5,
            $"Thumb keeps a 9px hit area with a 5px visible bar after the 2px inset: hit={thumb.Bounds.Width:0.#}, visible={inner.Bounds.Width:0.#}");
        Check(inner.Background is ISolidColorBrush idleThumb && Math.Abs(idleThumb.Opacity - 0.28) < 0.001
            && inner.CornerRadius == (CornerRadius)Application.Current!.Resources["VexDensityCornerRadiusPill"]!,
            "Idle thumb paints muted at 28% with the density pill radius");
        Check(thumb.Bounds.Height > 32 && thumb.Bounds.Height < scroll.Viewport.Height,
            $"Thumb length tracks the viewport/extent ratio instead of collapsing: {thumb.Bounds.Height:0.#}");
        // 点滑块下方的轨道 = 朝下翻一页（原型浏览器行为，由 ScrollTrackPaging 接管）。
        var before = scroll.Offset.Y;
        var pagePoint = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, bar.Bounds.Height - 6), window)!.Value;
        window.MouseDown(pagePoint, MouseButton.Left); window.MouseUp(pagePoint, MouseButton.Left); Render();
        Check(scroll.Offset.Y > before, $"Clicking the track below the thumb pages the viewport down: {before:0.#} -> {scroll.Offset.Y:0.#}");
        var afterPage = scroll.Offset.Y;
        var upPoint = bar.TranslatePoint(new Point(bar.Bounds.Width / 2, 3), window)!.Value;
        window.MouseDown(upPoint, MouseButton.Left); window.MouseUp(upPoint, MouseButton.Left); Render();
        Check(scroll.Offset.Y < afterPage, $"Clicking the track above the thumb pages back up: {afterPage:0.#} -> {scroll.Offset.Y:0.#}");
        // 拖拽仍归 Thumb：按住滑块中部往下拖，视口跟着走且不被翻页行为拦截。
        var thumbCenter = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
        var beforeDrag = scroll.Offset.Y;
        window.MouseDown(thumbCenter, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        for (var step = 1; step <= 6; step++)
        {
            window.MouseMove(thumbCenter + new Vector(0, 10 * step), RawInputModifiers.LeftMouseButton);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }
        window.MouseUp(thumbCenter + new Vector(0, 60), MouseButton.Left); Render();
        Check(scroll.Offset.Y > beforeDrag, $"Dragging the thumb still scrolls the viewport: {beforeDrag:0.#} -> {scroll.Offset.Y:0.#}");
        var hoverPoint = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
        window.MouseMove(hoverPoint); Render();
        Check(inner.Background is ISolidColorBrush hoverThumb && Math.Abs(hoverThumb.Opacity - 0.48) < 0.001,
            "Hovering the thumb deepens it to muted 48%");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "scrollbar-fidelity.png"));
        Console.WriteLine("PASS scrollbar fidelity probe complete");
    }
}