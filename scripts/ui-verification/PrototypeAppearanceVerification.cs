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
        void Check(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text);
            Console.WriteLine("PASS " + text);
        }
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Click(Button button) { button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render(); }
        Check(vm.Appearance.PaletteOptions.Count == 16 && vm.Appearance.TypographyOptions.Count == 27, "Native catalog matches 16 palettes and 27 typography styles");
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Appearance.SelectPaletteByKey("weft"); vm.Appearance.SelectThemeByKey("light");
        var original = app.Resolve<IWorkspaceDocumentState>().Markdown;
        vm.Workbench.ShowTypography = true; Render();
        var gallery = window.FindControl<ShellTypographyGallery>("TypographyGallery")!;
        var cards = gallery.GetVisualDescendants().OfType<Button>().Where(b => b.Name?.StartsWith("Typography_") == true).ToArray();
        Check(cards.Length == 27 && cards.Count(b => b.IsVisible) == 6, "All independent cards exist and six recommended cards are shown");
        Check(cards.All(card => card.Content is StackPanel stack && stack.Children.OfType<Border>().Single().Child is MarkdownViewer preview
            && preview.TypographyTheme == card.Name![11..]), "Each card renders its own typography key");
        var simple = cards.First(b => b.Name == "Typography_Simple"); simple.Focus();
        var selected = vm.Appearance.SelectedTypography;
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
        Check(vm.Appearance.SelectedTypography == selected, "Arrow navigation moves focus without applying typography");
        var book = cards.First(b => b.Name == "Typography_InkBlack"); Click(book);
        Check(vm.Appearance.SelectedTypography?.Key == "InkBlack" && vm.Workbench.ShowTypography, "Click applies typography and keeps comparison panel open");
        Click(gallery.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "全部 27 套"));
        Check(cards.All(b => b.IsVisible), "All 27 typography styles are one click away");
        foreach (var size in new[] { new Size(800,480), new Size(1024,768), new Size(1440,1000) })
        {
            window.Width = size.Width; window.Height = size.Height; Render();
            Check(gallery.Bounds.Width <= window.ClientSize.Width && gallery.Bounds.Height <= window.ClientSize.Height - 90, "Typography panel fits " + size.Width);
            var scroll = gallery.GetVisualDescendants().OfType<ScrollViewer>().First();
            Check(scroll.Viewport.Height > 50 && scroll.Extent.Height > scroll.Viewport.Height, "Card area scrolls independently at " + size.Width);
            Check(cards.All(card => card.Bounds.Height >= 332), "Cards retain readable natural height at " + size.Width);
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "typography-" + size.Width + ".png"));
        }
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
        Check(combinations == 864, "864 native palette/mode/typography combinations retain readable body, links, quotes and headings");
        vm.Appearance.SelectPaletteByKey("weft"); vm.Appearance.SelectThemeByKey("light");
        Check(app.Resolve<IWorkspaceDocumentState>().Markdown == original, "Appearance and layout changes preserve Markdown exactly");
        Console.WriteLine("PASS Native comparison interaction and size checks complete");
    }

    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte v) => v / 255d <= .04045 ? v / 255d / 12.92 : Math.Pow((v / 255d + .055) / 1.055, 2.4);
        static double Luminance(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        return (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
    }
}
