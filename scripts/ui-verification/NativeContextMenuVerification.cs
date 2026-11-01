using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

internal static class NativeContextMenuVerification
{
    public static void Run(string output)
    {
        AppBuilder.Configure<VerificationVexApp>().UsePlatformDetect().With(VexFonts.Options)
            .UseReactiveUI().SetupWithoutStarting();
        var window = ((VerificationVexApp)Application.Current!).CreateShell();
        window.Width = 1280; window.Height = 820;
        using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        Exception? failure = null;
        var completed = false;
        window.Show();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                var vm = (MainWindowViewModel)window.DataContext!;
                vm.Workbench.SelectLayout("read");
                const string markdown = "# Heading menu\n\nParagraph menu\n\n```csharp\nConsole.WriteLine(1);\n```\n\n| Header |\n| --- |\n| Cell menu |\n";
                vm.Markdown = markdown;
                var viewer = window.Workspace.FindControl<Control>("PreviewGuideTarget")!
                    .FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
                for (var i = 0; i < 100 && viewer.CurrentModel?.Source != markdown; i++)
                    await Task.Delay(20);
                Check(viewer.CurrentModel?.Source == markdown, "Native viewer renders the requested document");
                foreach (var theme in new[] { "light", "dark" })
                foreach (var language in new[] { "zh-CN", "en-US", "ja-JP", "zh-Hant" })
                {
                    vm.Appearance.SelectThemeByKey(theme);
                    vm.Appearance.SelectLanguageByCulture(language);
                    await Task.Delay(100);
                    foreach (var prefix in new[] { "Heading menu", "Paragraph menu", "Console.WriteLine", "Cell menu" })
                    {
                        var text = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
                            .First(t => (t.Inlines?.Text ?? t.Text ?? "").StartsWith(prefix, StringComparison.Ordinal));
                        text.BringIntoView();
                        await Task.Delay(30);
                        var menu = text.ContextMenu!;
                        menu.Placement = PlacementMode.Bottom;
                        menu.Open(text);
                        await Task.Delay(80);
                        var root = TopLevel.GetTopLevel(menu);
                        Check(root is PopupRoot, "Context menu uses an actual native popup window");
                        var scroll = menu.GetVisualDescendants().OfType<ScrollViewer>().First();
                        var label = $"{theme}/{language}/{prefix}";
                        Console.WriteLine($"NATIVE MENU {label}: root={root!.ClientSize}, menu={menu.Bounds.Size}, viewport={scroll.Viewport}, extent={scroll.Extent}");
                        Check(scroll.Extent.Height <= scroll.Viewport.Height + .5,
                            "Native reading actions fit without scrolling: " + label);
                        Check(!menu.GetVisualDescendants().OfType<RepeatButton>().Any(b => b.IsEffectivelyVisible),
                            "Short native menu has no scroll arrows: " + label);
                        foreach (var row in menu.Items.OfType<MenuItem>())
                        {
                            var origin = row.TranslatePoint(default, scroll)!.Value;
                            Check(origin.Y >= -.5 && origin.Y + row.Bounds.Height <= scroll.Bounds.Height + .5,
                                "Native row is fully inside its viewport: " + row.Header);
                        }
                        foreach (var title in menu.GetVisualDescendants().OfType<TextBlock>()
                            .Where(t => !string.IsNullOrEmpty(t.Text)))
                            Check(title.TextLayout.Height <= title.Bounds.Height + .5
                                && title.TextLayout.WidthIncludingTrailingWhitespace <= title.Bounds.Width + .5,
                                "Native menu text has a complete line box: " + title.Text);
                        if (prefix == "Paragraph menu")
                        {
                            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(menu.Bounds.Width), (int)Math.Ceiling(menu.Bounds.Height)));
                            bitmap.Render(menu);
                            bitmap.Save(Path.Combine(output, $"native-menu-{theme}-{language}.png"));
                        }
                        menu.Close();
                    }
                }
                completed = true;
            }
            catch (Exception ex) { failure = ex; }
            finally { window.Close(); stop.Cancel(); }
        });
        Dispatcher.UIThread.MainLoop(stop.Token);
        if (failure is not null) throw failure;
        Check(completed, "Native popup verification completes before its timeout");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
