using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 临时探针：打开排版弹层，逐项打印与原型一致的几何/字号指标（原型侧由 design/tests/probe-typography.cjs 读数）。
/// 用法：dotnet run --project scripts/ui-verification/UiVerification.csproj -c Release -- typography-probe artifacts/typo-probe
/// </summary>
internal static class TypographyProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            LayoutSettle.Run();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        window.Width = VisualParityCapture.Width; window.Height = VisualParityCapture.Height; Render();
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectPaletteByKey("indigo");
        vm.Appearance.SelectedTypography = vm.Appearance.TypographyOptions.First(option => option.Key == "Simple");
        vm.Layout.IsSidebarVisible = true;
        vm.Layout.IsOutlineVisible = false;
        const char newline = (char)10;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(VisualParityCapture.SampleDocument, 1, 1,
                VisualParityCapture.SampleDocument.Count(c => c == newline) + 1));
        Render();
        Thread.Sleep(600);
        Render();
        app.Resolve<ShellPreferencesViewModel>().Close();
        Render();
        vm.Workbench.ShowTypography = true;
        Render();
        Thread.Sleep(400);
        Render();

        var gallery = window.GetVisualDescendants().OfType<ShellTypographyGallery>().FirstOrDefault();
        if (gallery is null) { Console.WriteLine("GALLERY not found"); return; }

        var windowOrigin = window.TranslatePoint(default, window) ?? default;
        Point? Pos(Visual visual) => visual.TranslatePoint(default, window);

        void Line(string label, Visual visual, params string[] extras)
        {
            var p = Pos(visual);
            var bounds = (visual as Control)?.Bounds ?? default;
            Console.WriteLine($"{label}\tx={p?.X - windowOrigin.X:F2}\ty={p?.Y - windowOrigin.Y:F2}\tw={bounds.Width:F2}\th={bounds.Height:F2}"
                + (extras.Length > 0 ? "\t" + string.Join("\t", extras) : ""));
        }

        // 弹层外框：gallery 自身即 wb-popover 的宿主角（内容 Border 是它的 Content）。
        Line("POPOVER", gallery);
        if (gallery.Content is Control border) Line("POPOVER-INNER", border);
        if (gallery.Content is Border outer) Console.WriteLine($"POPOVER-BG\t{outer.Background}\tpadding={outer.Padding}");

        // 密度分段条
        foreach (var track in gallery.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("segmented-track")))
        {
            Line("SEG-TRACK", track, $"bg={track.Background}\tpadding={track.Padding}\tradius={track.CornerRadius}");
            var panel = track.Child;
            if (panel is Control panelControl) Line("SEG-PANEL", panelControl, $"desired={panelControl.DesiredSize}");
            if (panel is Panel host)
            {
                foreach (var child in host.Children.OfType<Control>())
                {
                    var text = child is Button { Content: string s } ? s : child.GetType().Name;
                    var thumb = child.Classes.Contains("segmented-thumb");
                    Line(thumb ? "SEG-THUMB" : "SEG-PILL", child,
                        $"text={text}\tbg={(child as Border)?.Background}\tfs={(child as TemplatedControl)?.FontSize}");
                }
            }
            var label = gallery.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "渲染密度");
            if (label is not null) Line("DENSITY-LABEL", label, $"fs={label.FontSize}\tfg={label.Foreground}");
        }

        // 网格与卡片
        var cards = gallery.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Typography_") == true).ToList();
        Console.WriteLine($"CARD-COUNT\t{cards.Count}");
        foreach (var card in cards.Take(3))
        {
            Line("CARD", card, $"bg={card.Background}\tradius={card.CornerRadius}");
            var preview = card.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>().FirstOrDefault();
            if (preview is not null)
                Line("PREVIEW", preview,
                    $"margin={preview.Margin}\tsize={preview.TypographySize}\tparaFS={preview.ParagraphFontSize}\tparaLH={preview.ParagraphLineHeight}\th2FS={preview.Heading2FontSize}\thMargin={preview.HeadingMargin}");
            foreach (var text in card.GetVisualDescendants().OfType<TextBlock>()
                         .Where(t => t.Text is { Length: > 0 } && t.GetType() == typeof(TextBlock)).Take(4))
            {
                var cls = string.Join(",", text.Classes);
                Console.WriteLine($"CARDTEXT\t{cls}\ttext={text.Text}\tfs={text.FontSize}\tlh={text.LineHeight}\tmax={text.MaxLines}");
            }
            var quote = card.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("MdQuote"));
            if (quote is not null) Console.WriteLine($"QUOTE\tbg={quote.Background}\tborder={quote.BorderThickness}\tpadding={quote.Padding}\tmargin={quote.Margin}");
        }

        var add = gallery.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "AddCustomTypography");
        if (add is not null) Line("ADD", add);

        // 卡片外观契约（原型实测：probe-typography.cjs 读 .typography-card 计算样式 =
        // 1px 描边 / radius-md(8) / surface 底 / box-shadow:none，hover 只把描边换成强调色）。
        // 结构断言吃前两档，描边颜色与「没有脏光」一律回到位图上数像素。
        if (cards.Count > 0)
        {
            void Check(bool value, string message)
            {
                if (!value) throw new InvalidOperationException(message);
            }

            var card = cards[0];
            var accent = (app.TryGetResource("VexAccentBrush", app.ActualThemeVariant, out var accentResource)
                && accentResource is ISolidColorBrush accentBrush) ? accentBrush.Color : default;
            Check(card.BorderThickness == new Thickness(1), $"Typography card carries the prototype 1px hairline: {card.BorderThickness}");
            var cardRadius = (app.TryGetResource("VexDensityCornerRadiusMd", app.ActualThemeVariant, out var radiusResource)
                && radiusResource is CornerRadius radius) ? radius : default;
            Check(cardRadius.TopLeft > 0 && card.CornerRadius == cardRadius,
                $"Typography card hugs the radius-md token like the prototype (8px): card={card.CornerRadius} token={cardRadius}");

            var origin = card.TranslatePoint(default, window)!.Value;
            var cardBox = new Rect(origin, card.Bounds.Size);
            using var frame = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
            frame.Render(window);
            using var stream = new MemoryStream();
            frame.Save(stream);
            using var raster = SkiaSharp.SKBitmap.Decode(stream.ToArray())!;
            int Distance(SkiaSharp.SKColor p) => Math.Abs(p.Red - accent.R) + Math.Abs(p.Green - accent.G) + Math.Abs(p.Blue - accent.B);
            // 描边落在卡片左缘那两三像素里，抗锯齿会把它摊开，取这一小片里最贴强调色的一枚。
            int EdgeDistance(SkiaSharp.SKBitmap bmp)
            {
                var best = int.MaxValue;
                for (var x = (int)cardBox.X; x < cardBox.X + 3; x++)
                    for (var y = (int)(cardBox.Y + cardBox.Height / 2) - 1; y < cardBox.Y + cardBox.Height / 2 + 2; y++)
                        best = Math.Min(best, Distance(bmp.GetPixel(x, y)));
                return best;
            }
            // 卡底下方三行：原型 box-shadow:none，落出来的必须是弹层原本的底，多一个暗像素都算投影没退干净。
            var baseline = raster.GetPixel((int)cardBox.X - 12, (int)cardBox.Bottom + 1);
            var leaked = 0;
            for (var y = (int)cardBox.Bottom + 1; y < cardBox.Bottom + 4; y++)
                for (var x = (int)cardBox.X + 4; x < cardBox.Right - 4; x++)
                {
                    var p = raster.GetPixel(x, y);
                    if (Math.Abs(p.Red - baseline.Red) > 2 || Math.Abs(p.Green - baseline.Green) > 2 || Math.Abs(p.Blue - baseline.Blue) > 2) leaked++;
                }
            var idle = EdgeDistance(raster);
            Console.WriteLine($"CARD\tbox={cardBox}\tleaked={leaked}\tedgeDistance={idle}");
            Check(leaked == 0, $"Prototype paints no shadow under the typography card; {leaked} pixels differ from the popover field {baseline.Red:X2}{baseline.Green:X2}{baseline.Blue:X2}");
            Check(idle > 60, $"Idle card edge is the hairline, not the accent: distance to accent {idle}");

            window.MouseMove(cardBox.Center, RawInputModifiers.None);
            frame.Render(window);
            using var hoverStream = new MemoryStream();
            frame.Save(hoverStream);
            using var hovered = SkiaSharp.SKBitmap.Decode(hoverStream.ToArray())!;
            var hover = EdgeDistance(hovered);
            Console.WriteLine($"HOVER\tedgeDistance={hover}");
            Check(hover < 60 && hover < idle - 40, $"Hover swaps the hairline to the accent like the prototype: distance {idle} to {hover}");
        }

        var scroller = gallery.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroller is not null) Line("SCROLL", scroller, $"padding={scroller.Padding}");
    }
}
