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
using Vex.Controls;
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
        Check(cards.All(card => card.Content is Panel panel && panel.Children.OfType<Border>().First().Child is StackPanel stack
            && stack.Children.OfType<MarkdownViewer>().FirstOrDefault() is { } preview
            && preview.TypographyTheme == card.Name![11..]), "Each card renders its own typography key");
        var simple = cards.First(b => b.Name == "Typography_Simple"); simple.Focus();
        var selected = vm.Appearance.SelectedTypography;
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
        Check(vm.Appearance.SelectedTypography == selected, "Arrow navigation moves focus without applying typography");
        var book = cards.First(b => b.Name == "Typography_InkBlack"); Click(book);
        Check(vm.Appearance.SelectedTypography?.Key == "InkBlack" && vm.Workbench.ShowTypography, "Click applies typography and keeps comparison panel open");
        Check(gallery.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("appearance-pill")) == 3, "Render density pills offer the three prototype sizes");
        foreach (var size in new[] { new Size(800,480), new Size(1024,768), new Size(1440,1000) })
        {
            window.Width = size.Width; window.Height = size.Height; Render();
            Check(gallery.Bounds.Width <= window.ClientSize.Width && gallery.Bounds.Bottom <= window.ClientSize.Height - 11,
                $"Typography panel fits {size.Width}: gallery={gallery.Bounds}, margin={gallery.Margin}, client={window.ClientSize}");
            var scroll = gallery.GetVisualDescendants().OfType<ScrollViewer>().First();
            Check(scroll.Viewport.Height > 50 && scroll.Extent.Height > scroll.Viewport.Height, $"Card area scrolls independently at {size.Width}: viewport={scroll.Viewport.Height:0.#}, extent={scroll.Extent.Height:0.#}, gallery={gallery.Bounds}");
            Check(cards.All(card => card.Bounds.Height >= 191 && card.Bounds.Height <= 193), "Typography cards keep the compact 192px height at " + size.Width + ": " + string.Join(",", cards.Select(c => c.Bounds.Height.ToString("0.#"))));
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "typography-" + size.Width + ".png"));
        }
        foreach (var language in vm.Appearance.LanguageOptions)
        {
            vm.Appearance.SelectedLanguage = language; Render();
            Check(gallery.GetVisualDescendants().OfType<TextBlock>().All(t => t.Text?.StartsWith("Vex.VexL.") != true), "Typography labels resolve in " + language.CultureName);
        }
        vm.Appearance.SelectLanguageByCulture("zh-CN"); vm.Workbench.ShowTypography = false;
        var appearanceMenu = window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")!;
        var appearanceIcon = appearanceMenu.AppearanceButton.Content as Avalonia.Controls.Shapes.Path;
        Check(appearanceIcon is not null && appearanceIcon.Data?.ToString() == MenuIconCatalog.Get("palette").ToString(),
            "Appearance button uses the shared Lucide palette outline instead of a filled PathIcon");
        foreach (var theme in new[] { "light", "dark" })
        {
            vm.Appearance.SelectThemeByKey(theme); Render();
            window.MouseMove(new Point(1, window.ClientSize.Height - 1)); Render();
            Application.Current!.TryGetResource("VexMutedBrush", window.ActualThemeVariant, out var normalIconBrush);
            Check(appearanceIcon!.Bounds.Size == new Size(15, 15)
                && appearanceIcon.Stroke is ISolidColorBrush normalStroke && normalIconBrush is ISolidColorBrush normalBrush
                && normalStroke.Color == normalBrush.Color, "Palette icon keeps its 15px size and theme stroke under " + theme);
            var button = appearanceMenu.AppearanceButton;
            window.MouseMove(button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value); Render();
            Application.Current.TryGetResource("VexAccentBrush", window.ActualThemeVariant, out var hoverIconBrush);
            Check(button.IsPointerOver && appearanceIcon.Stroke is ISolidColorBrush hoverStroke
                && hoverIconBrush is ISolidColorBrush hoverBrush && hoverStroke.Color == hoverBrush.Color,
                "Palette icon follows the title bar theme hover color under " + theme);
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "appearance-icon-hover-" + theme + ".png"));
        }
        window.MouseMove(new Point(1, window.ClientSize.Height - 1));
        vm.Appearance.SelectThemeByKey("light"); Render();
        appearanceMenu.AppearanceButton.Flyout!.ShowAt(appearanceMenu.AppearanceButton); Render();
        var paletteContent = (Control)((Flyout)appearanceMenu.AppearanceButton.Flyout).Content!;
        var paletteCards = paletteContent.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("palette-card") || button.Classes.Contains("palette-add-card")).ToArray();
        Check(paletteCards.Length == vm.Appearance.PaletteOptions.Count + 1, "Appearance menu renders every palette and the add card");
        var regularPalette = paletteCards.First(button => button.Classes.Contains("palette-card"));
        var addPalette = paletteCards.Single(button => button.Classes.Contains("palette-add-card"));
        Check(Math.Abs(regularPalette.Bounds.Height - addPalette.Bounds.Height) < 0.1
            && Math.Abs(regularPalette.Bounds.Width - addPalette.Bounds.Width) < 0.1,
            $"Add palette card matches palette card size: regular={regularPalette.Bounds}, add={addPalette.Bounds}");
        var addDash = addPalette.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().Single();
        Check(Math.Abs(addDash.Bounds.Height - addPalette.Bounds.Height) < 0.1
            && Math.Abs(addDash.Bounds.Width - addPalette.Bounds.Width) < 0.1,
            $"Add palette dashed frame fills the card: dash={addDash.Bounds}, card={addPalette.Bounds}");
        var regularTop = regularPalette.TranslatePoint(default, paletteContent)!.Value.Y;
        var addTop = addPalette.TranslatePoint(default, paletteContent)!.Value.Y;
        Check(Math.Abs((addTop - regularTop) - 3 * (92 + 6)) < 0.1,
            $"Add palette card occupies the next fixed-height grid row: regularTop={regularTop:0.#}, addTop={addTop:0.#}");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "appearance-palettes.png"));
        foreach (var theme in new[] { "light", "dark" })
        {
            vm.Appearance.SelectThemeByKey(theme); Render();
            var hoverPoint = addPalette.TranslatePoint(new Point(addPalette.Bounds.Width / 2, addPalette.Bounds.Height / 2), window)!.Value;
            window.MouseMove(hoverPoint); Render();
            Check(addPalette.IsPointerOver, "Pointer reaches the add palette card under " + theme);
            Check(Math.Abs(addDash.Bounds.Height - addPalette.Bounds.Height) < 0.1
                && Math.Abs(addDash.Bounds.Width - addPalette.Bounds.Width) < 0.1,
                $"Hovered dashed frame keeps the full card size under {theme}: dash={addDash.Bounds}, card={addPalette.Bounds}");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "appearance-palettes-hover-" + theme + ".png"));
            window.MouseMove(new Point(1, window.ClientSize.Height - 1)); Render();
        }
        appearanceMenu.AppearanceButton.Flyout.Hide(); Render();
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
