using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Mermaider;
using Mermaider.Models;
using Vex.Core.Services;
using Vex.Core.Models;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

namespace Vex;

/// <summary>显式验证正式原生发布产物，只读输出资源，并在指定目录验证偏好持久化。</summary>
internal static class RuntimeVerification
{
    private static string _uiDirectory = string.Empty;

    public static void RunUi(string directory)
    {
        Run(directory);
        _uiDirectory = Path.GetFullPath(directory);
        new AppSettingsStore().Update(settings => settings with
        {
            HasSeenOnboardingGuide = true, IsMcpServerEnabled = false, MotionMode = "off"
        });
        AppBuilder.Configure<NativeVerificationApp>().UsePlatformDetect().With(VexFonts.Options)
            .UseReactiveUI(_ => { }).SetupWithoutStarting();
        var app = (NativeVerificationApp)Application.Current!;
        var window = app.CreateShell();
        window.Width = 1280;
        window.Height = 820;
        window.Show();
        try
        {
            var model = (MainWindowViewModel)window.DataContext!;
            var sample = Path.Combine(_uiDirectory, "native-smoke.md");
            File.WriteAllText(sample, "---\ntitle: NativeAOT 博客验证\ncategories:\n  - Avalonia UI\ntags: [CSharp, Markdown]\ndraft: false\n---\n\n# NativeAOT 验证\n\n可空类型与 C# 预览支持。\n\n```csharp\nstring? text = null;\n```\n\n```mermaid\nflowchart LR\n A --> B\n```\n");
            var loading = model.OpenPathAsync(sample);
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!loading.IsCompleted)
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("Native document loading timed out.");
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            loading.GetAwaiter().GetResult();
            var renders = 0;
            foreach (var culture in new[] { "zh-CN", "en-US", "zh-Hant", "ja-JP" })
            foreach (var theme in new[] { "light", "dark" })
            foreach (var layout in WorkbenchLayoutOption.All)
            {
                I18nManager.Instance.Culture = CultureInfo.GetCultureInfo(culture);
                model.Appearance.SelectThemeByKey(theme);
                model.Workbench.SelectLayout(layout.Id);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                if (layout.Id is "read" or "instant")
                {
                    var view = window.Workspace.FindControl<Control>(layout.Id == "read" ? "PreviewGuideTarget" : "InstantGuideTarget")!;
                    var rendered = DateTime.UtcNow.AddSeconds(15);
                    while (!view.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("MdFrontMatter")))
                    {
                        if (DateTime.UtcNow >= rendered) throw new InvalidOperationException("Native blog YAML metadata card is missing: " + layout.Id);
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        Thread.Sleep(10);
                    }
                }
                using var bitmap = new RenderTargetBitmap(new PixelSize(1280, 820));
                bitmap.Render(window);
                if (culture == "zh-CN" && theme == "light")
                    bitmap.Save(Path.Combine(_uiDirectory, "layout-" + layout.Id + ".png"));
                renders++;
            }
            var result = $"NativeAOT UI verification passed; languages=4; themes=2; renders={renders}; preferences=isolated";
            File.WriteAllText(Path.Combine(_uiDirectory, "ui-verification.txt"), result);
            Console.WriteLine(result);
        }
        finally { window.Close(); }
    }

    private sealed class NativeVerificationApp : App
    {
        public NativeVerificationApp() { }
        protected override void OnConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAutoSaveDraftService>(new AutoSaveDraftService
                { StorageDirectory = Path.Combine(_uiDirectory, "drafts") });
            services.AddSingleton<IRecentDocumentStore>(new EmptyRecentDocuments());
        }
        public MainWindow CreateShell()
        {
            Instance = this;
            ApplyInitialTheme();
            return Services.GetRequiredService<MainWindow>();
        }
    }

    private sealed class EmptyRecentDocuments : IRecentDocumentStore
    {
        public IReadOnlyList<RecentDocument> Load(int maxCount) => [];
        public void Save(IEnumerable<RecentDocument> documents) { }
    }

    public static void Run(string directory)
    {
        if (RuntimeFeature.IsDynamicCodeSupported)
            throw new InvalidOperationException("Runtime verification requires a NativeAOT executable.");
        if (!Uri.TryCreate(ProjectLinks.Website, UriKind.Absolute, out var website) || website.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Published website metadata is missing or invalid.");
        foreach (var file in new[] { "更新日志.md", "鸣谢.md", "快速开始.md" })
            _ = BundledDocuments.GetPath(file);
        foreach (var name in new[] { "note.md", "upper.MD", "note.markdown", "note.mdown", "README.md" })
        {
            var expected = name == "README.md" ? "readme.png" : "markdown.png";
            if (Path.GetFileName(FileIconCatalog.Default.ResolveIconFilePath(name, false)) != expected)
                throw new InvalidOperationException("Published Markdown file icon is missing: " + name);
        }
        // 快速开始按相对路径引用功能截图，应用内离线阅读要求图片随包，缺一张就少一幅画面。
        foreach (Match image in Regex.Matches(File.ReadAllText(BundledDocuments.GetPath("快速开始.md")), @"!\[[^\]]*\]\(([^)]+\.png)\)"))
        {
            var relative = image.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(Path.Combine(BundledDocuments.Folder, relative)))
                throw new InvalidOperationException("Published quick start image is missing: " + relative);
        }
        var samples = Directory.GetFiles(BundledDocuments.SamplesFolder, "*.md");
        if (samples.Length != 8 || !File.Exists(Path.Combine(BundledDocuments.SamplesFolder, "图片", "dotnet-avalonia-pipeline.svg")))
            throw new InvalidOperationException("Published sample documents or their offline image are missing.");
        var catalog = File.ReadAllText(Path.Combine(BundledDocuments.SamplesFolder, "Mermaid 图表图鉴.md"));
        var diagrams = Regex.Matches(catalog, @"(?m)^```mermaid\s*\r?\n([\s\S]*?)^```\s*$");
        string[] diagramTypes = ["flowchart", "sequenceDiagram", "stateDiagram-v2", "journey", "gantt", "timeline",
            "gitGraph", "kanban", "classDiagram", "erDiagram", "requirementDiagram", "mindmap", "block-beta",
            "C4Context", "architecture-beta", "packet-beta", "pie", "quadrantChart", "xychart-beta", "sankey-beta",
            "radar-beta", "treemap-beta", "venn-beta", "treeView-beta"];
        var actualTypes = diagrams.Select(diagram => diagram.Groups[1].Value.TrimStart().Split([' ', '\r', '\n'])[0]);
        if (diagrams.Count != diagramTypes.Length || !actualTypes.ToHashSet().SetEquals(diagramTypes))
            throw new InvalidOperationException("Published Mermaid catalog must contain all 24 distinct diagram types.");
        foreach (Match diagram in diagrams)
        {
            var svg = MermaidRenderer.RenderSvg(diagram.Groups[1].Value, new RenderOptions
            {
                Bg = "#FFFFFF", Fg = "#302C3C", Accent = "#8B7CF6", Transparent = true,
                DataPalette = ["#8B7CF6", "#D9AD53", "#70A98B", "#C583B5", "#62A5C2"]
            });
            if (XDocument.Parse(svg).Root?.Name.LocalName != "svg")
                throw new InvalidOperationException("Native Mermaid rendering did not produce an SVG diagram.");
        }
        // 走实际启动集成入口，确认 Windows 自检不会重新注册用户取消的打开方式。
        if (OperatingSystem.IsWindows()) DefaultFileOpeningService.Configure();
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var resources = Path.Combine(AppContext.BaseDirectory, "I18n");
        if (OperatingSystem.IsMacOS() && !Directory.Exists(resources))
            resources = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "I18n"));
        var manager = I18nManager.Instance;
        manager.Register(new JsonLangPlugin { ResourceFolder = resources }, CultureInfo.GetCultureInfo("en-US"));
        var labels = new HashSet<string>();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
        {
            if (!File.Exists(Path.Combine(resources, "Vex", culture + ".json")))
                throw new InvalidOperationException("Published language file is missing: " + culture);
            manager.Culture = CultureInfo.GetCultureInfo(culture);
            var text = manager.GetResource(VexL.MenuLanguage);
            if (string.IsNullOrWhiteSpace(text) || text == VexL.MenuLanguage)
                throw new InvalidOperationException("Published language resources are missing: " + culture);
            labels.Add(text);
            foreach (var key in new[] { VexL.InsertTable, VexL.TableRows, VexL.TableColumns, VexL.TableHeaderHint })
                if (manager.GetResource(key) is not { Length: > 0 } translated || translated == key)
                    throw new InvalidOperationException("Published table dialog translation is missing: " + culture + "/" + key);
        }
        if (labels.Count != 4) throw new InvalidOperationException("All four published languages must switch independently.");
        var config = Path.Combine(root, "runtime-" + Guid.NewGuid().ToString("N") + ".config");
        File.WriteAllText(config, "<configuration><appSettings /></configuration>");
        AppContext.SetData("APP_CONFIG_FILE", config);
        new AppSettingsStore().Update(settings => settings with { ThemeKey = "system", PaletteKey = "aurora", CultureName = "ja-JP" });
        var saved = new AppSettingsStore().Current;
        if (saved.ThemeKey != "system" || saved.PaletteKey != "aurora" || saved.CultureName != "ja-JP")
            throw new InvalidOperationException("Native appearance preferences did not round-trip.");
        var preferences = new EditorPreferences(new AppSettingsStore());
        if (preferences.DefaultEncoding != "utf-8" || preferences.DefaultNewline != "system" || preferences.BodyFont != "system"
            || preferences.ExportFormat != "HTML" || preferences.PdfSize != "A4")
            throw new InvalidOperationException("Native default preference choices did not survive real storage.");
        var result = $"NativeAOT runtime verification passed; version={typeof(Program).Assembly.GetName().Version}; website={ProjectLinks.Website}; languages=4; tableDialog=localized; samples={samples.Length}; mermaid={diagrams.Count}; fileIcons=5; preferences=isolated";
        File.WriteAllText(Path.Combine(root, "runtime-verification.txt"), result);
        Console.WriteLine(result);
    }
}
