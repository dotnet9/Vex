using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Vex.Core.Messaging;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 一次性探针：键盘焦点可见性对照原型 design-system.css:3/16（按钮/页签/菜单 2px offset 2）、
/// prototype.css:51（输入/下拉/数字框 offset 3）与 design-system.css:17 的 .source-scroll textarea（offset -2 内环）。
/// Tab 走到的控件画外环、鼠标点焦点不画；search-shell 一族（含并入它的快速打开）只有 3px 柔光，不叠第二圈。
/// 环不仅要在控制树里，还要真落在位图上：AdornerLayer 默认把 adorner 裁到被饰盒内，
/// 只查结构会把「只剩左上角」的残环当成功，故四边中点逐像素验收。
/// </summary>
internal static class FocusRingProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        IEnumerable<Border> Rings() => window.GetVisualDescendants().OfType<Border>()
            .Where(b => !b.IsHitTestVisible && b.BorderThickness == new Thickness(2)
                && (b.Margin == new Thickness(-4) || b.Margin == new Thickness(-5) || b.Margin == new Thickness(2)));
        window.Width = 1280; window.Height = 800; Render();
        Application.Current!.TryGetResource("VexAccentBrush", window.ActualThemeVariant, out var accentResource);
        var accent = ((ISolidColorBrush)accentResource!).Color;
        void PixelCheck(string label, Control target, bool inset = false)
        {
            var origin = target.TranslatePoint(default, window)!.Value;
            var box = new Rect(origin, target.Bounds.Size);
            using var frame = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
            frame.Render(window);
            using var stream = new MemoryStream();
            frame.Save(stream);
            using var raster = SKBitmap.Decode(stream.ToArray())!;
            bool Accent(double x, double y)
            {
                var pixel = raster.GetPixel((int)Math.Round(x), (int)Math.Round(y));
                return Math.Abs(pixel.Red - accent.R) <= 16 && Math.Abs(pixel.Green - accent.G) <= 16 && Math.Abs(pixel.Blue - accent.B) <= 16;
            }
            var midX = box.X + box.Width / 2;
            var midY = box.Y + box.Height / 2;
            // 环带占 2px：外环在 [-(offset), -(offset-2)]、内环在 [offset, offset+2]，取带中心采样，别踩抗锯齿外沿。
            var margin = Rings().Single().Margin.Left;
            var band = inset ? margin + 1 : -margin - 1;
            var sign = inset ? 1 : -1;
            var hits = new[] { (X: box.X + sign * band, Y: midY), (X: box.Right - sign * band, Y: midY), (X: midX, Y: box.Y + sign * band), (X: midX, Y: box.Bottom - sign * band) }
                .Count(p => Accent(p.X, p.Y));
            var cleanX = inset ? box.X - 4 : box.X - band - 5;
            Check(hits == 4 && !Accent(cleanX, midY),
                $"{label}: ring pixels paint on all four edges {(inset ? "inside" : "outside")} the box and stop there (hits={hits}/4)");
            var area = new SKRectI((int)Math.Max(0, box.X - 14), (int)Math.Max(0, box.Y - 14),
                (int)Math.Min(raster.Width, box.Right + 14), (int)Math.Min(raster.Height, box.Bottom + 14));
            using var crop = new SKBitmap();
            raster.ExtractSubset(crop, area);
            // 小目标最近邻放大 6 倍肉眼数环带；整面编辑器只存 1 倍，避免几十兆的放大图。
            var scale = crop.Width * crop.Height > 20000 ? 1 : 6;
            using var zoomed = new SKBitmap(new SKImageInfo(crop.Width * scale, crop.Height * scale));
            for (var y = 0; y < crop.Height; y++)
            for (var x = 0; x < crop.Width; x++)
            {
                var pixel = crop.GetPixel(x, y);
                for (var dy = 0; dy < scale; dy++)
                for (var dx = 0; dx < scale; dx++) zoomed.SetPixel(x * scale + dx, y * scale + dy, pixel);
            }
            using var encoded = zoomed.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(output, label + ".png"), encoded.ToArray());
            Console.WriteLine($"CAPTURE {label}: {area} zoomed {scale}x nearest-neighbour");
        }
        Control? focused = null;
        for (var i = 0; i < 60 && focused is null; i++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null); Render();
            focused = window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.IsFocused && c is Button or TextBox or ComboBox or ToggleButton);
        }
        Check(focused is not null && focused.Classes.Contains(":focus-visible"),
            "Tab lands on a control with the keyboard-focus pseudoclass: " + (focused?.GetType().Name ?? "none"));
        var ring = Rings().SingleOrDefault();
        Check(ring is not null && ring.BorderBrush is ISolidColorBrush ringBrush && ringBrush.Color == accent
            && ring.Margin == new Thickness(-5)
            && Math.Abs(ring.Bounds.Width - (focused!.Bounds.Width + 10)) < 1,
            $"Keyboard focus paints the prototype 2px accent ring 3px outside the box: margin={ring?.Margin}, width={ring?.Bounds.Width:0.#} vs {focused!.Bounds.Width + 10:0.#}");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "focus-ring.png"));
        PixelCheck("focus-ring-crop", focused!);
        // 鼠标点焦点不画环（原型 :focus-visible 语义）：点图标栏的大纲钮。
        var rail = window.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("rail-item") && b != focused);
        var railPoint = rail.TranslatePoint(new Point(rail.Bounds.Width / 2, rail.Bounds.Height / 2), window)!.Value;
        window.MouseDown(railPoint, MouseButton.Left); window.MouseUp(railPoint, MouseButton.Left); Render();
        Check(rail.IsFocused && !rail.Classes.Contains(":focus-visible") && !Rings().Any(),
            "Mouse-click focus stays ring-free like the prototype :focus-visible rule");
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null); Render();
        var keyboardAgain = window.GetVisualDescendants().OfType<Control>().First(c => c.IsFocused);
        Check(keyboardAgain.Classes.Contains(":focus-visible") && Rings().Any(),
            "Tabbing again brings the ring back: " + keyboardAgain.GetType().Name);
        PixelCheck("focus-ring-chrome", keyboardAgain);
        // search-shell 输入自带柔光，不叠外环：聚焦侧栏搜索框时只有 3px 柔光那一圈。
        var search = window.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && t.GetVisualAncestors().OfType<Grid>().Any(g => g.Classes.Contains("search-shell")));
        search.Focus(); Render();
        Check(!Rings().Any(), "Search-shell inputs keep their single glow without a second adorner ring");
        // 快速打开也是搜索输入：并入 search-shell 后只有柔光那一圈，不叠外环。
        var vm = (MainWindowViewModel)window.DataContext!;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(ShellActionKind.QuickOpen));
        var quickDeadline = DateTime.UtcNow.AddSeconds(10);
        ShellQuickOpenView? quick = null;
        while (quick is null && DateTime.UtcNow <= quickDeadline)
        {
            Render();
            quick = window.GetVisualDescendants().OfType<ShellQuickOpenView>().FirstOrDefault(v => v.IsEffectivelyVisible);
            if (quick is null) Thread.Sleep(20);
        }
        Check(quick is not null, "Quick open opens through the action bus");
        var queryBox = quick!.GetVisualDescendants().OfType<TextBox>().First();
        Check(queryBox.Classes.Contains("shell-input")
            && queryBox.GetVisualAncestors().OfType<Grid>().Any(g => g.Classes.Contains("search-shell"))
            && queryBox.IsFocused && !Rings().Any(),
            "Quick-open input shares the search-shell recipe with a single glow and no adorner ring");
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Render();
        Thread.Sleep(200); Render();
        // 偏好/MCP 里的普通输入（原型 prototype.css:51 input offset 3）：MCP 浮层里有实打实的 pref-field。
        var mcpTask = app.Resolve<Vex.Modules.Shell.Services.IShellOverlayService>().ShowMcpSettingsAsync(app.Resolve<McpSettingsViewModel>());
        var mcpDeadline = DateTime.UtcNow.AddSeconds(10);
        McpSettingsOverlayView? mcp = null;
        while (mcp is null && DateTime.UtcNow <= mcpDeadline)
        {
            Render();
            mcp = window.GetVisualDescendants().OfType<McpSettingsOverlayView>().FirstOrDefault(v => v.IsEffectivelyVisible);
            if (mcp is null) Thread.Sleep(20);
        }
        var prefField = mcp!.GetVisualDescendants().OfType<TextBox>().First(t => t.IsEffectivelyVisible && t.Classes.Contains("pref-field"));
        prefField.Focus(NavigationMethod.Tab); Render();
        Check(prefField.Classes.Contains(":focus-visible") && Rings().Single().Margin == new Thickness(-5),
            "Plain settings input takes the prototype offset-3 outer ring");
        PixelCheck("focus-ring-input", prefField);
        ((McpSettingsViewModel)mcp.DataContext!).Cancel();
        for (var i = 0; i < 50 && window.GetVisualDescendants().OfType<McpSettingsOverlayView>().Any(); i++) { Render(); Thread.Sleep(20); }
        // 源码编辑区（原型 .source-scroll textarea:focus-visible 的 outline-offset:-2）：环在盒内。
        vm.Workbench.ShowSource(); Render();
        var editor = window.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>().First(t => t.IsEffectivelyVisible).TextArea;
        editor.Focus(NavigationMethod.Tab); Render();
        Check(editor.Classes.Contains(":focus-visible") && Rings().Single().Margin == new Thickness(2),
            "Source editor takes the prototype inset 2px ring");
        PixelCheck("focus-ring-source", editor, inset: true);
        Console.WriteLine("PASS focus ring probe complete");
    }
}