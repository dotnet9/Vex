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
using Vex.Modules.Shell.Views;

/// <summary>
/// 一次性探针：键盘焦点可见性对照原型 design-system.css:3/16（2px 强调色外环 offset 2）与
/// prototype.css:51（输入 offset 3）。Tab 走到的控件画外环，鼠标点焦点不画；search-shell 一族不叠第二圈。
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
                && (b.Margin == new Thickness(-4) || b.Margin == new Thickness(-5)));
        window.Width = 1280; window.Height = 800; Render();
        Application.Current!.TryGetResource("VexAccentBrush", window.ActualThemeVariant, out var accentResource);
        var accent = ((ISolidColorBrush)accentResource!).Color;
        void PixelCheck(string label, Control target)
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
            // 环带占 [-(offset), -(offset-2)]，取带中心采样，别踩抗锯齿的外沿。
            var band = -Rings().Single().Margin.Left - 1;
            var hits = new[] { (X: box.X - band, Y: midY), (X: box.Right + band, Y: midY), (X: midX, Y: box.Y - band), (X: midX, Y: box.Bottom + band) }
                .Count(p => Accent(p.X, p.Y));
            Check(hits == 4 && !Accent(box.X - band - 5, midY),
                $"{label}: ring pixels paint on all four edges {band}px outside the box and stop there (hits={hits}/4)");
            var area = new SKRectI((int)Math.Max(0, box.X - 14), (int)Math.Max(0, box.Y - 14),
                (int)Math.Min(raster.Width, box.Right + 14), (int)Math.Min(raster.Height, box.Bottom + 14));
            using var crop = new SKBitmap();
            raster.ExtractSubset(crop, area);
            // 最近邻放大 6 倍，肉眼能数清 2px 环带，又不引入插值色。
            using var zoomed = new SKBitmap(new SKImageInfo(crop.Width * 6, crop.Height * 6));
            for (var y = 0; y < crop.Height; y++)
            for (var x = 0; x < crop.Width; x++)
            {
                var pixel = crop.GetPixel(x, y);
                for (var dy = 0; dy < 6; dy++)
                for (var dx = 0; dx < 6; dx++) zoomed.SetPixel(x * 6 + dx, y * 6 + dy, pixel);
            }
            using var encoded = zoomed.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(output, label + ".png"), encoded.ToArray());
            Console.WriteLine($"CAPTURE {label}: {area} zoomed 6x nearest-neighbour");
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
        Console.WriteLine("PASS focus ring probe complete");
    }
}