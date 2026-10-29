using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 一次性探针：悬停与按压态对照原型。
/// workspace-chrome.css:176/408 实测悬停是两件事——0,3,0 的灰底规则压住 accent-soft，
/// 但同条规则的 color 无人竞争，所以底取 --hover、描线取强调色；:177 按压换 --pressed；
/// prototype.css:439 所有按钮 :active 整枚下沉 1px。底色由 Semi 画在模板 ContentPresenter 上，
/// 因此结构断言只看模板 presenter，颜色与位移一律回到位图上数像素。
/// </summary>
internal static class ButtonStateProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        Color Token(string key)
        {
            if (!Application.Current!.TryGetResource(key, window.ActualThemeVariant, out var resource))
                throw new InvalidOperationException("Missing token resource: " + key);
            return ((ISolidColorBrush)resource!).Color;
        }
        bool Close(SKColor pixel, Color want, int tolerance = 10)
            => Math.Abs(pixel.Red - want.R) <= tolerance && Math.Abs(pixel.Green - want.G) <= tolerance && Math.Abs(pixel.Blue - want.B) <= tolerance;

        var hover = Token("VexHoverBrush");
        var pressed = Token("VexPressedBrush");
        var accent = Token("VexAccentBrush");
        var value = Token("VexValueBrush");
        window.Width = 1280; window.Height = 800; Render();

        SKBitmap Raster()
        {
            var frame = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
            frame.Render(window);
            using var stream = new MemoryStream();
            frame.Save(stream);
            return SKBitmap.Decode(stream.ToArray())!;
        }
        Rect Box(Control target)
        {
            var origin = target.TranslatePoint(default, window)!.Value;
            return new Rect(origin, target.Bounds.Size);
        }
        // 盒内像素统计：强调色描线像素个数与它们的最高行，用来同时证明「前景翻色」与「整枚下沉 1px」。
        (int AccentPixels, int TopRow, int BottomRow) AccentRowes(SKBitmap raster, Rect box)
        {
            var count = 0;
            var top = int.MaxValue;
            var bottom = int.MinValue;
            for (var y = (int)box.Y; y < box.Bottom; y++)
            for (var x = (int)box.X; x < box.Right; x++)
            {
                if (!Close(raster.GetPixel(x, y), accent, 26)) continue;
                count++;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
            return (count, top, bottom);
        }
        int CountColor(SKBitmap raster, Rect box, Color want, int tolerance = 6)
        {
            var count = 0;
            for (var y = (int)box.Y; y < box.Bottom; y++)
            for (var x = (int)box.X; x < box.Right; x++)
                if (Close(raster.GetPixel(x, y), want, tolerance)) count++;
            return count;
        }
        bool Same(Color pixel, Color want, int tolerance = 10)
            => Math.Abs(pixel.R - want.R) <= tolerance && Math.Abs(pixel.G - want.G) <= tolerance && Math.Abs(pixel.B - want.B) <= tolerance;
        ContentPresenter? Presenter(Control target) => target.GetVisualDescendants().OfType<ContentPresenter>()
            .FirstOrDefault(c => c.Name == "PART_ContentPresenter");
        Color PresenterColor(Button target) => ((ISolidColorBrush)Presenter(target)!.Background!).Color;
        void MoveAway() { window.MouseMove(new Point(6, 6), RawInputModifiers.None); Render(); }

        var vm = (MainWindowViewModel)window.DataContext!;

        // ===== 图标钮一族（原型 .icon-button / .format-button）：灰底 + 强调描线 + 按压底 + 1px 下沉 =====
        var filesTool = window.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && b.Classes.Contains("files-tool"));
        var filesBox = Box(filesTool);
        MoveAway();
        var idleRaster = Raster();
        var idleAccent = AccentRowes(idleRaster, filesBox);
        var idleBackground = PresenterColor(filesTool);
        Check(!filesTool.Classes.Contains(":pointerover") && idleAccent.AccentPixels == 0,
            $"Idle icon button paints no accent glyph and no hover plate (accent pixels={idleAccent.AccentPixels}, plate=#{idleBackground.ToString()})");

        var center = new Point(filesBox.X + filesBox.Width / 2, filesBox.Y + filesBox.Height / 2);
        window.MouseMove(center, RawInputModifiers.None); Render();
        using var hoverRaster = Raster();
        var hoverAccent = AccentRowes(hoverRaster, filesBox);
        Check(filesTool.Classes.Contains(":pointerover")
            && Same(PresenterColor(filesTool), hover)
            && Same(((ISolidColorBrush)filesTool.Foreground!).Color, accent)
            && hoverAccent.AccentPixels > 20,
            $"Hover takes the prototype grey plate plus accent stroke: plate=#{PresenterColor(filesTool)} (want #{hover}), foreground=#{((ISolidColorBrush)filesTool.Foreground!).Color} (want #{accent}), accent pixels={hoverAccent.AccentPixels}");
        Check(CountColor(hoverRaster, filesBox, pressed) < 8,
            "Hover plate stays clear of the pressed tint");

        window.MouseDown(center, MouseButton.Left); Render();
        using var pressRaster = Raster();
        var pressAccent = AccentRowes(pressRaster, filesBox);
        Check(filesTool.Classes.Contains(":pressed") && Same(PresenterColor(filesTool), pressed),
            $"Press swaps the plate to the accent-derived pressed tint: #{PresenterColor(filesTool)} (want #{pressed})");
        Check(filesTool.RenderTransform is TranslateTransform lift && Math.Abs(lift.Y - 1) < 0.01,
            "Press carries the prototype 1px translate on the button itself");
        Check(pressAccent.TopRow == hoverAccent.TopRow + 1 && pressAccent.BottomRow == hoverAccent.BottomRow + 1,
            $"Pressed glyph really sits one pixel lower on the raster: rows {hoverAccent.TopRow}-{hoverAccent.BottomRow} to {pressAccent.TopRow}-{pressAccent.BottomRow}");
        window.MouseUp(center, MouseButton.Left);
        MoveAway();

        // ===== 文字工具钮（原型 .toolbar-button）：悬停前景从 muted 翻成正文色，按压实心下沉 =====
        var quiet = window.GetVisualDescendants().OfType<Button>()
            .First(b => b.IsEffectivelyVisible && b.Classes.Contains("wb-quiet"));
        var quietBox = Box(quiet);
        var quietCenter = new Point(quietBox.X + quietBox.Width / 2, quietBox.Y + quietBox.Height / 2);
        MoveAway();
        var quietIdle = ((ISolidColorBrush)quiet.Foreground!).Color;
        window.MouseMove(quietCenter, RawInputModifiers.None); Render();
        var quietHover = ((ISolidColorBrush)quiet.Foreground!).Color;
        Check(quietHover == value && quietIdle != value,
            $"Toolbar button text brightens from muted to body colour on hover: #{quietIdle} to #{quietHover}");
        window.MouseDown(quietCenter, MouseButton.Left); Render();
        Check(quiet.Classes.Contains(":pressed") && quiet.RenderTransform is TranslateTransform,
            "Toolbar button presses with the same 1px lift");
        window.MouseUp(quietCenter, MouseButton.Left);
        MoveAway();

        // ===== 选中态不能被按压规则改写（原型 .toolbar-button.on 是 accent-soft，按下也不该变灰）=====
        var checkedToggle = window.GetVisualDescendants().OfType<ToggleButton>()
            .FirstOrDefault(t => t.IsEffectivelyVisible && t.IsChecked == true && t.Classes.Contains("wb-quiet"));
        if (checkedToggle is not null)
        {
            var checkedBox = Box(checkedToggle);
            var checkedCenter = new Point(checkedBox.X + checkedBox.Width / 2, checkedBox.Y + checkedBox.Height / 2);
            window.MouseMove(checkedCenter, RawInputModifiers.None);
            window.MouseDown(checkedCenter, MouseButton.Left); Render();
            var plate = ((ISolidColorBrush)Presenter(checkedToggle)!.Background!).Color;
            var soft = Token("VexAccentSoftBrush");
            Check(Same(plate, soft, 8),
                $"Checked tool stays on the accent-soft plate while pressed: #{plate} (want #{soft})");
            window.MouseUp(checkedCenter, MouseButton.Left);
            MoveAway();
        }

        // ===== 主按钮族的位移同样到位（原型 button:active 全局规则）=====
        var overlayTask = app.Resolve<Vex.Modules.Shell.Services.IShellOverlayService>()
            .ShowMcpSettingsAsync(app.Resolve<McpSettingsViewModel>());
        var deadline = DateTime.UtcNow.AddSeconds(10);
        McpSettingsOverlayView? mcp = null;
        while (mcp is null && DateTime.UtcNow <= deadline)
        {
            Render();
            mcp = window.GetVisualDescendants().OfType<McpSettingsOverlayView>().FirstOrDefault(v => v.IsEffectivelyVisible);
            if (mcp is null) Thread.Sleep(20);
        }
        var primary = mcp!.GetVisualDescendants().OfType<Button>().First(b => b.IsEffectivelyVisible && b.Classes.Contains("primary"));
        var primaryBox = Box(primary);
        var primaryCenter = new Point(primaryBox.X + primaryBox.Width / 2, primaryBox.Y + primaryBox.Height / 2);
        window.MouseDown(primaryCenter, MouseButton.Left); Render();
        using var primaryRaster = Raster();
        Check(primary.Classes.Contains(":pressed") && primary.RenderTransform is TranslateTransform
            && CountColor(primaryRaster, primaryBox, accent, 30) > 100,
            $"Primary action presses with the lift and keeps its accent plate ({CountColor(primaryRaster, primaryBox, accent, 30)} accent pixels)");
        var cropArea = new SKRectI((int)primaryBox.X - 6, (int)primaryBox.Y - 8, (int)primaryBox.Right + 6, (int)primaryBox.Bottom + 8);
        using var crop = new SKBitmap();
        primaryRaster.ExtractSubset(crop, cropArea);
        using var encoded = crop.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(output, "button-state-primary-pressed.png"), encoded.ToArray());
        Console.WriteLine("CAPTURE button-state-primary-pressed: " + cropArea);
        window.MouseMove(new Point(6, 6), RawInputModifiers.None);
        window.MouseUp(new Point(6, 6), MouseButton.Left);
        Render();
        if (mcp.DataContext is McpSettingsViewModel mcpVm) mcpVm.Cancel();
        for (var i = 0; i < 50 && window.GetVisualDescendants().OfType<McpSettingsOverlayView>().Any(); i++) { Render(); Thread.Sleep(20); }

        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "button-state.png"));
        Check(vm.DocumentFiles.Count > 0, "Demo document still loaded through the press probes");
        Console.WriteLine("PASS button state probe complete");
    }
}
