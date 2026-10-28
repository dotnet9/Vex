using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

        var scroller = gallery.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scroller is not null) Line("SCROLL", scroller, $"padding={scroller.Padding}");
    }
}
