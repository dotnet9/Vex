using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown;
using CodeWF.Avalonia.Markdown.Themes;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class PrototypeAppearanceVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        FontManager.Current.TryMatchCharacter('文', FontStyle.Normal, FontWeight.Normal, FontStretch.Normal,
            vm.Workbench.Preferences.ContentFontFamily, System.Globalization.CultureInfo.GetCultureInfo("zh-CN"), out var chineseTypeface);
        Console.WriteLine("CJK BODY FONT " + chineseTypeface.FontFamily.Name);
        void Check(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text);
            Console.WriteLine("PASS " + text);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render(); }
        if (OperatingSystem.IsWindows()) Check(chineseTypeface.FontFamily.Name == "Microsoft YaHei UI", "Chinese body text uses the system sans-serif fallback");
        Check(vm.Appearance.PaletteOptions.Count == 9 && vm.Appearance.TypographyOptions.Count == 12, "Native catalog matches 9 palettes and 12 typography styles");
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Appearance.SelectPaletteByKey("indigo"); vm.Appearance.SelectThemeByKey("light");
        Render();
        var original = app.Resolve<IWorkspaceDocumentState>().Markdown;
        vm.Workbench.ShowTypography = true; Render();
        var gallery = window.FindControl<ShellTypographyGallery>("TypographyGallery")!;
        var cards = gallery.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Typography_") == true).ToArray();
        Check(cards.Length == 12 && cards.All(b => b.IsVisible), "All twelve typography cards are shown without a filter (prototype redesign)");
        Check(cards.All(card => card.Content is Panel panel && panel.Children.OfType<Border>().First().Child is MarkdownViewer preview
            && preview.TypographyTheme == card.Name![11..]), "Each card renders its own typography key");
        var simple = cards.First(b => b.Name == "Typography_Simple"); simple.Focus();
        var selected = vm.Appearance.SelectedTypography;
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
        Check(vm.Appearance.SelectedTypography == selected, "Arrow navigation moves focus without applying typography");
        var book = cards.First(b => b.Name == "Typography_InkBlack"); Click(book);
        Check(vm.Appearance.SelectedTypography?.Key == "InkBlack" && vm.Workbench.ShowTypography, "Click applies typography and keeps comparison panel open");
        Check(gallery.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("appearance-pill")) == 3, "Render density pills offer the three prototype sizes");
        var startWidth = window.Width; var startHeight = window.Height;
        window.Width = 1440; window.Height = 1000; Render();
        var addStrip = gallery.GetVisualDescendants().OfType<Button>().First(b => b.Name == "AddCustomTypography");
        // 原型 a1a9013：弹层尺寸不变，卡片区改 4 列 3 行（预览固定 13px 基准，卡高不随密度变），新增条降到 44px 整行。
        foreach (var density in new[] { "compact", "normal", "relaxed" })
        {
            vm.Appearance.MarkdownDensity = density; Render();
            const double height = 162;
            var scroll = gallery.GetVisualDescendants().OfType<ScrollViewer>().First();
            var tops = cards.Select(c => Math.Round(c.TranslatePoint(default, scroll)!.Value.Y)).Distinct().ToArray();
            var lefts = cards.Select(c => Math.Round(c.TranslatePoint(default, scroll)!.Value.X)).Distinct().ToArray();
            Check(tops.Length == 3 && lefts.Length == 4, $"Cards form 4 columns x 3 rows at {density}: {lefts.Length}x{tops.Length}");
            Check(cards.All(card => Math.Abs(card.Bounds.Height - height) < 1), $"Cards are {height:0}px tall at {density}: " + string.Join(",", cards.Select(c => c.Bounds.Height.ToString("0.#"))));
            Check(cards.Average(card => card.Bounds.Width) is > 150 and < 175, $"Narrow cards keep the prototype's ~161px width at {density}: " + cards[0].Bounds.Width.ToString("0.#"));
            Check(scroll.Extent.Height <= scroll.Viewport.Height + 1, $"Card grid does not scroll at {density}: extent={scroll.Extent.Height:0.#}, viewport={scroll.Viewport.Height:0.#}");
            var stripBottom = addStrip.TranslatePoint(new Point(0, addStrip.Bounds.Height), gallery)!.Value.Y;
            var scrollBottom = scroll.TranslatePoint(new Point(0, scroll.Bounds.Height), gallery)!.Value.Y;
            Check(addStrip.Bounds.Width > cards[0].Bounds.Width * 3 && stripBottom <= scrollBottom + 1,
                $"Add strip spans the grid and stays fully visible at {density}: {addStrip.Bounds.Width:0.#}x{addStrip.Bounds.Height:0.#}, bottom={stripBottom:0.#}, scrollBottom={scrollBottom:0.#}, extent={scroll.Extent.Height:0.#}");
            // 原型 line-clamp：标题与说明各 1 行、引用 2 行，引用整行落在卡片内（不截断就会被挤出卡片）。
            bool InQuote(TextBlock t) => t.GetVisualAncestors().OfType<StyledElement>().Any(e => e.Classes.Contains("MdQuote"));
            Check(cards.All(card =>
            {
                var blocks = card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.GetType() != typeof(TextBlock)).ToArray();
                var quote = blocks.FirstOrDefault(InQuote);
                return quote is { MaxLines: 2 } && blocks.Where(t => !InQuote(t)).All(t => t is { MaxLines: 1 })
                    && quote.TranslatePoint(new Point(0, quote.Bounds.Height), card)!.Value.Y <= card.Bounds.Height + .5;
            }), $"Preview lines stay clamped and the quote fits the card at {density}");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "typography-" + density + ".png"));
        }
        vm.Appearance.MarkdownDensity = "normal"; Render();
        foreach (var size in new[] { new Size(800, 480), new Size(1024, 768) })
        {
            window.Width = size.Width; window.Height = size.Height; Render();
            Check(gallery.Bounds.Width <= window.ClientSize.Width && gallery.Bounds.Bottom <= window.ClientSize.Height - 11,
                $"Typography panel fits {size.Width}: gallery={gallery.Bounds}, margin={gallery.Margin}, client={window.ClientSize}");
            var scroll = gallery.GetVisualDescendants().OfType<ScrollViewer>().First();
            if (size.Width <= 800) Check(scroll.Viewport.Height > 50 && scroll.Extent.Height > scroll.Viewport.Height, $"Card area scrolls independently in a short window at {size.Width}: viewport={scroll.Viewport.Height:0.#}, extent={scroll.Extent.Height:0.#}, gallery={gallery.Bounds}");
            else Check(scroll.Viewport.Height > 500, $"A laptop-height window shows nearly all three rows at {size.Width}: viewport={scroll.Viewport.Height:0.#}, extent={scroll.Extent.Height:0.#}, gallery={gallery.Bounds}");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "typography-" + size.Width + "x" + size.Height + ".png"));
        }
        window.Width = startWidth; window.Height = startHeight; Render();
        foreach (var language in vm.Appearance.LanguageOptions)
        {
            vm.Appearance.SelectedLanguage = language; Render();
            Check(gallery.GetVisualDescendants().OfType<TextBlock>().All(t => t.Text?.StartsWith("Vex.VexL.") != true), "Typography labels resolve in " + language.CultureName);
        }
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Workbench.ShowTypography = false;
        var combinations = 0;
        foreach (var palette in vm.Appearance.PaletteOptions)
        foreach (var mode in new[] { "light", "dark" })
        {
            vm.Appearance.SelectedPalette = palette; vm.Appearance.SelectThemeByKey(mode); Render();
            var variant = mode == "light" ? ThemeVariant.Light : ThemeVariant.Dark;
            Application.Current!.TryGetResource("VexDocumentPaperBrush", window.ActualThemeVariant, out var paperResource);
            var paper = ((ISolidColorBrush)paperResource!).Color;
            Application.Current.TryGetResource("StudioSelected", window.ActualThemeVariant, out var selectedBrush);
            Application.Current.TryGetResource("VexAccentSoftBrush", window.ActualThemeVariant, out var applicationSelected);
            Check(((ISolidColorBrush)selectedBrush!).Color == ((ISolidColorBrush)applicationSelected!).Color,
                "Selected surface follows " + palette.Key + "/" + mode);
            foreach (var article in ArticleTypographyCatalog.All)
            {
                var resources = (ResourceDictionary)article.CreateResources().ThemeDictionaries[variant];
                Color ColorOf(string key) => ((ISolidColorBrush)resources[key]!).Color;
                void Readable(Color foreground, Color background, string role)
                {
                    if (Contrast(foreground, background) < 4.5)
                        throw new InvalidOperationException($"Contrast {palette.Key}/{mode}/{article.Key}/{role}: {Contrast(foreground, background):F2}");
                }
                Readable(ColorOf(MarkdownStyleKeys.TextBrushResource), paper, "body");
                Readable(ColorOf(MarkdownStyleKeys.AccentBrushResource), paper, "link");
                Readable(ColorOf(MarkdownStyleKeys.MutedTextBrushResource), ColorOf(MarkdownStyleKeys.QuoteBackgroundBrushResource), "quote");
                for (var level = 1; level <= 6; level++)
                {
                    var background = ColorOf("CodeWFMarkdownHeading" + level + "Background");
                    Readable(ColorOf("CodeWFMarkdownHeading" + level + "Foreground"), background.A == 0 ? paper : background, "H" + level);
                }
                combinations++;
            }
        }
        Check(combinations == 216, "216 native palette/mode/typography combinations retain readable body, links, quotes and headings");
        vm.Appearance.SelectPaletteByKey("indigo"); vm.Appearance.SelectThemeByKey("light");
        var current = app.Resolve<IWorkspaceDocumentState>().Markdown;
        if (current != original)
        {
            var index = 0;
            while (index < Math.Min(current.Length, original.Length) && current[index] == original[index]) index++;
            Console.WriteLine($"MARKDOWN DRIFT at {index}: original={original.Length} current={current.Length}");
            Console.WriteLine("ORIGINAL: " + original[Math.Max(0, index - 60)..Math.Min(original.Length, index + 60)]);
            Console.WriteLine("CURRENT : " + current[Math.Max(0, index - 60)..Math.Min(current.Length, index + 60)]);
        }
        Check(current == original, "Appearance and layout changes preserve Markdown exactly");
        Console.WriteLine("PASS Native comparison interaction and size checks complete");
    }

    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte v) => v / 255d <= .04045 ? v / 255d / 12.92 : Math.Pow((v / 255d + .055) / 1.055, 2.4);
        static double Luminance(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        return (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
    }
}
