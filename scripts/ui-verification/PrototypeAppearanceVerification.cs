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
using Vex;
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
        VerifyCustomizationDialogs(app, Check, Render);
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
        Check(gallery.GetVisualDescendants().OfType<Button>().Count(b => b.Classes.Contains("density-pill")) == 3, "Render density pills offer the three prototype sizes");
        var startWidth = window.Width; var startHeight = window.Height;
        window.Width = 1440; window.Height = 1000; Render();
        var addStrip = gallery.GetVisualDescendants().OfType<Button>().First(b => b.Name == "AddCustomTypography");
        // 原型 a1a9013：弹层尺寸不变，卡片区改 4 列 3 行（预览固定 13px 基准，卡高不随密度变），新增条降到 44px 整行。
        foreach (var density in new[] { "compact", "normal", "relaxed" })
        {
            vm.Appearance.MarkdownDensity = density; Render();
            // 原型 #typography-panel 卡片实测 160.5x150.5（Playwright 探针，卡高不随密度变）。
            const double height = 150.5;
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

    /// <summary>
    /// 自定义配色/排版浮层的四语与回写断言。原型 theme.js / typography.js 里这两个对话框只有中文，
    /// 应用侧的标签全部走 i18n；下拉必须展示译文而不是 sans/1.7 这类内部键，且选择要回写内部值。
    /// </summary>
    private static void VerifyCustomizationDialogs(VerificationVexApp app, Action<bool, string> Check, Action Render)
    {
        static string L(string key) => CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(key) ?? string.Empty;
        var localizer = app.Resolve<IAppLocalizer>();

        // 配色浮层（中文）：分组/色标标签解析、非法色报错、空名回落本地化默认名。
        localizer.SetCulture("zh-CN"); Render();
        var paletteModel = new ShellPaletteEditModel(null);
        var paletteWindow = new Window { Content = new ShellPaletteEditView(paletteModel), Width = 380, Height = 620 };
        paletteWindow.Show(); Render();
        var paletteBoxes = paletteWindow.GetVisualDescendants().OfType<TextBox>().ToArray();
        var hexBoxes = paletteBoxes.Where(t => t.Text?.StartsWith('#') == true).ToArray();
        Check(hexBoxes.Length == 8, "Palette dialog shows the eight prototype color inputs");
        Check(paletteWindow.GetVisualDescendants().OfType<TextBlock>()
            .Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))
            .All(t => !t.StartsWith("Vex.") && !t.Contains("VexL.")), "Palette dialog labels resolve in zh-CN");
        Check(ShellPaletteEditModel.ColorLabels.Distinct().Count() == 4 && ShellPaletteEditModel.GroupLabels.Length == 2,
            "Palette dialog reuses the four color labels across light and dark groups");
        var applyPalette = paletteWindow.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("primary"));
        hexBoxes[0].Text = "#GGGGGG"; Render();
        applyPalette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        Check(paletteModel.Result is null && paletteModel.Report == L(VexL.CustomColorInvalid),
            "Invalid hex blocks the palette dialog with the localized report");
        hexBoxes[0].Text = "#A1B2C3"; Render();
        applyPalette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Render();
        Check(paletteModel.Result?.Colors[0] == "#A1B2C3",
            "Edited hex reaches the applied colors: " + (paletteModel.Result?.Colors[0] ?? "<null>"));
        Check(paletteModel.Result?.Name == L(VexL.CustomDefaultName),
            "Blank palette name falls back to the localized default: " + paletteModel.Result?.Name);
        Check(paletteWindow.GetVisualDescendants().OfType<Button>().Count(b => b.Content is string) == 2,
            "Palette dialog offers the prototype's cancel and save-and-apply pair");
        paletteWindow.Close(); Render();

        // 排版浮层（切到英文后构造）：证明标签随语言变化，而不是硬编码中文。
        localizer.SetCulture("en-US"); Render();
        var typographyModel = new ShellTypographyEditModel("user:seed", "seed");
        var typographyWindow = new Window { Content = new ShellTypographyEditView(typographyModel), Width = 380, Height = 620 };
        typographyWindow.Show(); Render();
        var combos = typographyWindow.GetVisualDescendants().OfType<ComboBox>().ToArray();
        Check(combos.Length == 2, "Typography dialog keeps the font and leading selectors");
        Check(combos[0].ItemsSource is string[] fonts && fonts.SequenceEqual(["Sans-serif", "Serif", "Monospace"])
            && combos[0].SelectedIndex == 0, "Font selector shows localized names without leaking internal keys: " + string.Join("/", (string[])combos[0].ItemsSource));
        combos[0].SelectedIndex = 2; Render();
        Check(typographyModel.Font == "mono", "Font selection writes the internal key back");
        combos[1].SelectedIndex = 0; Render();
        Check(Math.Abs(typographyModel.Leading - 1.7) < 0.001, "Leading selection writes the numeric line height back");
        Check(typographyModel.Title == L(VexL.WBTypographyEdit), "Editing an existing typography uses the localized title");
        var typoBoxes = typographyWindow.GetVisualDescendants().OfType<TextBox>().ToArray();
        // 按内容识别输入框：ComboBox 模板里也有 PART TextBox，用计数会把模板部件一起算进来。
        Check(typoBoxes.Any(t => t.Text == "seed"), "Typography dialog pre-fills the name field");
        var colorInputs = typoBoxes.Where(t => t.Text?.StartsWith('#') == true).ToArray();
        Check(colorInputs.Length == 4, "Typography dialog edits the four accent and decoration colors");
        colorInputs[1].Text = "#001122"; Render();
        Check(typographyModel.AccentDark == "#001122", "Color input writes back without reflection (AOT-safe)");
        typographyModel.Apply();
        Check(typographyModel.Result?.Name == "seed", "Existing typography name is preserved on apply");
        typographyWindow.Close(); Render();
        localizer.SetCulture("zh-CN"); Render();
    }

    private static double Contrast(Color a, Color b)
    {
        static double Linear(byte v) => v / 255d <= .04045 ? v / 255d / 12.92 : Math.Pow((v / 255d + .055) / 1.055, 2.4);
        static double Luminance(Color c) => .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B);
        return (Math.Max(Luminance(a), Luminance(b)) + .05) / (Math.Min(Luminance(a), Luminance(b)) + .05);
    }
}
