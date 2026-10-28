using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class FileIconVerification
{
    public static void Run(VerificationVexApp app, Window window, string output)
    {
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        }

        var iconRoot = Path.Combine(AppContext.BaseDirectory, "file-icons");
        Check(File.Exists(Path.Combine(iconRoot, "LICENSE.material-icon-theme.txt")), "Build output includes the icon license");
        using var theme = JsonDocument.Parse(File.ReadAllText(Path.Combine(iconRoot, "icon-theme.json")));
        foreach (var definition in theme.RootElement.GetProperty("iconDefinitions").EnumerateObject())
        {
            var relative = definition.Value.GetProperty("iconPath").GetString()!;
            if (!File.Exists(Path.Combine(iconRoot, relative))) throw new FileNotFoundException("Bundled icon is missing: " + definition.Name);
        }
        Check(theme.RootElement.GetProperty("iconDefinitions").EnumerateObject().Count() == 1245, "All 1,245 theme images are copied into build output");
        using var catalog = new FileIconCatalog(iconRoot);
        foreach (var (name, expected) in new[]
        {
            ("note.md", "markdown.png"), ("note.MARKDOWN", "markdown.png"), ("note.mdown", "markdown.png"),
            ("note.txt", "document.png"), ("README.MD", "readme.png"), ("Program.cs", "csharp.png"),
            ("view.axaml.cs", "csharp.png"), ("settings.json", "json.png"), ("export.pdf", "pdf.png")
        })
        {
            Check(Path.GetFileName(catalog.ResolveIconFilePath(name, false)) == expected, "Filename/extension mapping: " + name);
        }
        Check(catalog.Resolve("note.unknown-vex-extension", false) is null, "Unknown extension uses vector fallback");
        Check(!ReferenceEquals(catalog.Resolve("a.md", false), catalog.Resolve("a.txt", false)), "Markdown and text use distinct images");
        Check(ReferenceEquals(catalog.Resolve("a.md", false), catalog.Resolve("b.md", false)), "Files share the decoded bitmap cache");
        Check(Path.GetFileName(catalog.ResolveIconFilePath("docs", true)) == "folder-docs.png"
            && Path.GetFileName(catalog.ResolveIconFilePath("docs", true, expanded: true)) == "folder-docs-open.png",
            "Named folders switch between closed and expanded images");
        Check(Path.GetFileName(catalog.ResolveIconFilePath("workspace", true, isRoot: true)) == "folder-root.png", "Workspace root has its own icon");
        Check(Path.GetFileName(catalog.ResolveIconFilePath("settings.toml", false, light: true)) == "toml_light.png"
            && Path.GetFileName(catalog.ResolveIconFilePath("settings.toml", false)) == "toml.png", "Theme-specific light variants resolve independently");

        VerifyOverridesAndFailures(iconRoot, output, Check);
        var filesView = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        var productionFiles = filesView.DataContext;
        // ShellFilesViewModel 的 IDisposable 随按需加载波次的监控服务一起引入，本步先用普通局部变量。
        var files = new ShellFilesViewModel();
        filesView.DataContext = files;
        var samples = new[] { "note.md", "README.md", "plain.txt", "snippet.axaml.cs", "settings.toml", "unknown.vex-unmapped" }
            .Select(name => new DocumentFile(Path.Combine(output, "docs", name), name, "docs", "", "Icon verification")).ToArray();
        files.ApplyDocumentFilesChanged(new DocumentFilesChangedCommand(samples, samples[0], output));
        files.DocumentFileNodes[0].IsExpanded = true;
        files.DocumentFileNodes[0].Children.Single(node => node.IsFolder).IsExpanded = true;
        Render();
        var tree = window.GetVisualDescendants().OfType<TreeView>().Single(control => control.Classes.Contains("side-tree"));
        Image Icon(string name) => tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(item => item.DataContext is DocumentFileNode node && node.Name == name)
            .GetVisualDescendants().OfType<Image>().First(image => image.Name == "FileTypeIcon");
        Check(Icon("note.md").Source is Bitmap && Icon("plain.txt").Source is Bitmap, "Tree template binds decoded document images");
        Check(Icon("unknown.vex-unmapped").Source is null && tree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(item => item.DataContext is DocumentFileNode { Name: "unknown.vex-unmapped" })
            .GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any(path => path.Classes.Contains("tree-ico") && path.IsEffectivelyVisible),
            "Unknown tree item displays its original vector icon");
        var folder = files.DocumentFileNodes[0].Children.Single(node => node.IsFolder);
        var expandedIcon = Icon("docs").Source;
        folder.IsExpanded = false; Render();
        Check(!ReferenceEquals(expandedIcon, Icon("docs").Source), "Folder image updates when expansion changes");
        folder.IsExpanded = true; Render();
        var main = (MainWindowViewModel)window.DataContext!;
        foreach (var themeName in new[] { "dark", "light" })
        {
            main.Appearance.SelectThemeByKey(themeName); Render();
            Check(ReferenceEquals(Icon("settings.toml").Source, FileIconCatalog.Default.Resolve("settings.toml", false, light: themeName == "light")),
                "Live tree images follow the active theme: " + themeName);
            Check(tree.GetVisualDescendants().OfType<Image>().Where(image => image.IsEffectivelyVisible)
                .All(image => image.Bounds.Width == 12 && image.Bounds.Height == 12), "Themed icons preserve compact 12 px layout");
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "file-icons-" + themeName + ".png"), new PngBitmapEncoderOptions());
        }
        filesView.DataContext = productionFiles;
    }

    private static void VerifyOverridesAndFailures(string iconRoot, string output, Action<bool, string> check)
    {
        var root = Path.Combine(output, "catalog-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "icons"));
        File.Copy(Path.Combine(iconRoot, "icons", "markdown.png"), Path.Combine(root, "icons", "one.png"));
        File.Copy(Path.Combine(iconRoot, "icons", "document.png"), Path.Combine(root, "icons", "two.png"));
        File.WriteAllText(Path.Combine(root, "icons", "corrupt.png"), "invalid image");
        File.WriteAllText(Path.Combine(root, "icon-theme.json"), """
            {
              "iconDefinitions": {
                "one": { "iconPath": "icons/one.png" },
                "two": { "iconPath": "icons/two.png" },
                "missing": { "iconPath": "icons/missing.png" },
                "corrupt": { "iconPath": "icons/corrupt.png" },
                "outside": { "iconPath": "../outside.png" }
              },
              "fileNames": { "special.part.ext": "one" },
              "fileExtensions": { "ext": "one", "part.ext": "two", "override": "one", "missing": "missing", "bad": "corrupt", "outside": "outside" },
              "light": { "fileExtensions": { "override": "one" } }
            }
            """);
        File.WriteAllText(Path.Combine(root, "local-overrides.json"), """{ "fileExtensions": { "override": "two" } }""");
        File.Copy(Path.Combine(root, "icons", "one.png"), Path.Combine(output, "outside.png"), true);
        using (var catalog = new FileIconCatalog(root))
        {
            check(Path.GetFileName(catalog.ResolveIconFilePath("special.part.ext", false)) == "one.png", "Exact filename takes precedence over compound extension");
            check(Path.GetFileName(catalog.ResolveIconFilePath("ordinary.part.ext", false)) == "two.png", "Longest compound extension wins");
            check(Path.GetFileName(catalog.ResolveIconFilePath("ordinary.ext", false)) == "one.png", "Ordinary extension remains independent of compound extension");
            check(Path.GetFileName(catalog.ResolveIconFilePath("ordinary.override", false, light: true)) == "two.png", "Local override takes precedence in light themes too");
            check(catalog.Resolve("asset.missing", false) is null && catalog.Resolve("asset.bad", false) is null,
                "Missing and corrupt image resources fall back without crashing");
            check(catalog.Resolve("asset.outside", false) is null, "Theme paths cannot escape the icon directory");
        }
        File.WriteAllText(Path.Combine(root, "icon-theme.json"), "{ broken JSON");
        using var broken = new FileIconCatalog(root);
        check(broken.Resolve("note.md", false) is null, "Malformed themes fall back safely");
        using var absent = new FileIconCatalog(Path.Combine(output, "absent-icons"));
        check(absent.Resolve("note.md", false) is null, "Absent icon directory falls back safely");
    }
}
