using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class BlogFrontMatterVerification
{
    public const string Sample = "---\ntitle: 博客 YAML 验证\nslug: blog-front-matter\ndescription: \"可空类型、C# 与 Avalonia\"\ndate: 2026-11-08 12:00:00\ncategories:\n  - Avalonia UI\ntags:\n  - CSharp\n  - Markdown\ndraft: false\n---\n\n# 博文正文\n\n元数据与正文各自保留。\n";

    public static void Run(MainWindow window, string output, string? repository)
    {
        var model = (MainWindowViewModel)window.DataContext!;
        var fixture = Path.Combine(output, "blog-front-matter.md");
        File.WriteAllText(fixture, Sample);
        var paths = new List<string> { fixture };
        if (repository is not null)
            paths.AddRange(Directory.EnumerateFiles(repository, "*.md", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).StartsWith("---", StringComparison.Ordinal))
                .OrderDescending(StringComparer.OrdinalIgnoreCase).Take(3));
        var renders = 0;
        foreach (var path in paths)
        {
            var source = File.ReadAllText(path);
            var blocks = MarkdownBlockParser.Parse(source);
            if (blocks[0].Kind != MarkdownBlockKind.Raw || MarkdownBlockParser.Write(blocks) != source)
                throw new InvalidOperationException("Blog YAML/source round trip failed: " + path);
            var opening = model.OpenPathAsync(path);
            WaitUntil(() => opening.IsCompleted);
            opening.GetAwaiter().GetResult();
            foreach (var theme in new[] { "light", "dark" })
            foreach (var layout in new[] { "read", "instant" })
            {
                model.Appearance.SelectThemeByKey(theme);
                model.Workbench.SelectLayout(layout);
                var rootName = layout == "read" ? "PreviewGuideTarget" : "InstantGuideTarget";
                var root = window.Workspace.FindControl<Control>(rootName)
                    ?? throw new InvalidOperationException("Missing blog view: " + layout);
                WaitUntil(() => root.GetVisualDescendants().OfType<Border>()
                    .Any(border => border.Classes.Contains("MdFrontMatter")));
                if (model.Markdown != source)
                    throw new InvalidOperationException("Opening a blog changed its source: " + path);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
                bitmap.Render(window);
                bitmap.Save(Path.Combine(output, $"blog-{paths.IndexOf(path)}-{layout}-{theme}.png"));
                Console.WriteLine($"PASS Blog YAML metadata card/source preserved: {Path.GetFileName(path)}/{layout}/{theme}");
                renders++;
            }
        }
        File.WriteAllText(Path.Combine(output, "blog-verification.txt"),
            $"Blog metadata verification passed; documents={paths.Count}; renders={renders}; source=preserved");

        void WaitUntil(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            do
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Dispatcher.UIThread.RunJobs();
                if (condition()) return;
                Thread.Sleep(10);
            } while (DateTime.UtcNow < deadline);
            throw new TimeoutException("Blog front matter did not finish loading/rendering.");
        }
    }
}
