using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Themes;
using Vex;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class DocumentTypographyVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1440; window.Height = 900;
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        const string markdown = "# 一级标题\n\n## 二级标题\n\n正文颜色回归。\n\n> 引用颜色回归。\n";
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(markdown, 1, 1, 8));
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); LayoutSettle.Run();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
        Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color
            : throw new InvalidOperationException("Expected an opaque document color: " + brush);
        void Equal(IBrush? actual, Color expected, string role, string context)
        {
            if (ColorOf(actual) != expected)
                throw new InvalidOperationException($"{context}/{role}: actual={actual}, expected={expected}");
        }
        var count = 0;
        foreach (var layout in new[] { "read", "instant" })
        {
            vm.Workbench.SelectLayout(layout); Render();
            foreach (var palette in vm.Appearance.PaletteOptions)
            foreach (var mode in new[] { "light", "dark" })
            {
                vm.Appearance.SelectedPalette = palette; vm.Appearance.SelectThemeByKey(mode); Render();
                var variant = mode == "light" ? ThemeVariant.Light : ThemeVariant.Dark;
                var foreground = Color.Parse(mode == "light" ? palette.Definition.Light.Ink : palette.Definition.Dark.Ink);
                Application.Current!.TryGetResource("StudioMutedColor", window.ActualThemeVariant, out var muted);
                foreach (var article in ArticleTypographyCatalog.All)
                {
                    vm.Appearance.SelectTypographyByKey(article.Key); Render();
                    var host = window.Workspace.FindControl<Control>(layout == "read" ? "PreviewGuideTarget" : "InstantGuideTarget")!;
                    var text = host.GetVisualDescendants().OfType<SelectableTextBlock>().ToArray();
                    var context = $"{layout}/{palette.Key}/{mode}/{article.Key}";
                    var body = text.First(t => t.Classes.Contains("MdParagraph")
                        && !t.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("MdQuote")));
                    Equal(body.Foreground, foreground, "body", context);
                    var colors = (ResourceDictionary)article.CreateResources().ThemeDictionaries[variant];
                    for (var level = 1; level <= 2; level++)
                    {
                        var heading = article.Headings[level - 1];
                        var element = text.Single(t => t.GetVisualAncestors().OfType<Border>()
                            .Any(b => b.Classes.Contains($"MdH{level}Border")));
                        var expected = heading.Tint != 1 && heading.Foreground.Contains("--fg)", StringComparison.Ordinal)
                            || level == 2 && article.Key is "Simple" or "Basic" or "SoftSage"
                            ? foreground : ColorOf((IBrush)colors[$"CodeWFMarkdownHeading{level}Foreground"]!);
                        Equal(element.Foreground, expected, "H" + level, context);
                    }
                    var quote = text.Single(t => t.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("MdQuote")));
                    Equal(quote.Foreground, article.Quote.Foreground.Contains("--muted)", StringComparison.Ordinal)
                        ? (Color)muted! : ColorOf((IBrush)colors["CodeWFMarkdownQuoteForeground"]!), "quote", context);
                    if (palette.Key == "indigo" && article.Key == "Simple")
                        window.CaptureRenderedFrame()?.Save(Path.Combine(output, $"document-ink-{layout}-{mode}.png"));
                    count++;
                }
                Console.WriteLine($"PASS rendered body/H1/H2/quote colors {layout}/{palette.Key}/{mode}");
            }
        }
        Console.WriteLine($"PASS {count} rendered document palette/theme/typography/layout combinations");
    }
}
