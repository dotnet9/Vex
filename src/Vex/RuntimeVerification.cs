using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;
using CodeWF.Toolkit.Lang;
using CodeWF.Toolkit.Lang.Json;
using Mermaider;
using Mermaider.Models;
using Point = Avalonia.Point;
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

    public static void RunUi(string directory, string? articlePath = null)
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
                if (layout.Id is WorkbenchLayoutKeys.Read or WorkbenchLayoutKeys.Instant)
                {
                    var view = window.Workspace.FindControl<Control>(layout.Id == WorkbenchLayoutKeys.Read ? "PreviewGuideTarget" : "InstantGuideTarget")!;
                    var rendered = DateTime.UtcNow.AddSeconds(15);
                    while (!view.GetVisualDescendants().OfType<Border>().Any(border => border.Classes.Contains("MdFrontMatter")))
                    {
                        if (DateTime.UtcNow >= rendered) throw new InvalidOperationException("Native blog YAML metadata card is missing: " + layout.Id);
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        Thread.Sleep(10);
                    }
                }
                if (model.Protection.HasErrorNotice)
                    throw new InvalidOperationException("Native document UI reported an operation error: "
                        + model.Protection.DraftError + model.Protection.SaveError + model.Protection.FileReadError);
                using var bitmap = new RenderTargetBitmap(new PixelSize(1280, 820));
                bitmap.Render(window);
                if (culture == "zh-CN" && theme == "light")
                    bitmap.Save(Path.Combine(_uiDirectory, "layout-" + layout.Id + ".png"));
                renders++;
            }
            VerifyOutline(window);
            if (articlePath is not null) VerifyArticleTableImages(window, articlePath);
            var result = $"NativeAOT UI verification passed; languages=4; themes=2; renders={renders}; operationErrors=0; outlineNavigation=passed; readingChapter=passed; preferences=isolated";
            File.WriteAllText(Path.Combine(_uiDirectory, "ui-verification.txt"), result);
            Console.WriteLine(result);
        }
        finally { window.Close(); }
    }

    private static void VerifyArticleTableImages(MainWindow window, string articlePath)
    {
        var model = (MainWindowViewModel)window.DataContext!;
        var source = File.ReadAllText(articlePath);
        var opening = model.OpenPathAsync(articlePath);
        Wait(() => opening.IsCompleted); opening.GetAwaiter().GetResult();
        foreach (var layout in new[] { WorkbenchLayoutKeys.Read, WorkbenchLayoutKeys.Instant })
        {
            model.Workbench.SelectLayout(layout);
            var view = window.Workspace.FindControl<Control>(layout == WorkbenchLayoutKeys.Read ? "PreviewGuideTarget" : "InstantGuideTarget")!;
            var ready = Environment.TickCount64 + 300; Wait(() => Environment.TickCount64 >= ready);
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().OrderByDescending(control => control.Extent.Height - control.Viewport.Height).First();
            scroll.ScrollToEnd();
            CodeWF.Avalonia.Markdown.Controls.MarkdownImage[] Images() => view.GetVisualDescendants()
                .OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownImage>()
                .Where(image => image.GetVisualAncestors().OfType<Control>().Any(control => control.Classes.Contains("MdTableCell") || control.Classes.Contains("MdTableHeaderCell")))
                .ToArray();
            Wait(() => Images().Length >= 2 && Images().All(image => image.Bounds.Height > 30
                && image.GetVisualDescendants().OfType<Image>().Any(bitmap => bitmap.Source is not null && bitmap.Bounds.Height > 20)));
            var images = Images();
            images[0].BringIntoView();
            var settle = Environment.TickCount64 + 300; Wait(() => Environment.TickCount64 >= settle);
            using var bitmap = new RenderTargetBitmap(new PixelSize(1280, 820));
            bitmap.Render(window); bitmap.Save(Path.Combine(_uiDirectory, "article-table-images-" + layout + ".png"));
            if (model.Markdown != source || File.ReadAllText(articlePath) != source)
                throw new InvalidOperationException("Article image verification changed its source.");
            Console.WriteLine($"Article table images passed: layout={layout}; count={images.Length}; {string.Join("; ", images.Select(image => image.Bounds.Size))}");
        }

        void Wait(Func<bool> condition)
        {
            var deadline = Environment.TickCount64 + 30000;
            var frame = new DispatcherFrame();
            var passed = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) => { window.UpdateLayout(); passed = condition(); if (passed || Environment.TickCount64 >= deadline) frame.Continue = false; };
            timer.Start();
            try { Dispatcher.UIThread.PushFrame(frame); }
            finally { timer.Stop(); }
            if (!passed) throw new TimeoutException("Article table images did not finish rendering: " + articlePath);
        }
    }

    private static void VerifyOutline(MainWindow window)
    {
        var model = (MainWindowViewModel)window.DataContext!;
        var source = string.Join("\n", Enumerable.Range(1, 50).Select(index =>
            $"# {(index is 35 or 40 ? "同名章节" : "章节 " + index)}\n\n" +
            string.Concat(Enumerable.Range(1, 8).Select(paragraph => $"正文 {index}.{paragraph}。阅读位置跟随。\n\n"))));
        var path = Path.Combine(_uiDirectory, "outline-navigation.md");
        File.WriteAllText(path, source);
        var opening = model.OpenPathAsync(path);
        Wait(() => opening.IsCompleted); opening.GetAwaiter().GetResult();
        model.Workbench.SelectLayout(WorkbenchLayoutKeys.Read); model.Layout.IsOutlineVisible = true;
        var preview = window.Workspace.FindControl<Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
        var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
        var outlineView = window.GetVisualDescendants().OfType<ShellOutlineView>().Single();
        var outlinePanel = outlineView.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownOutlinePanel>("OutlinePanel")!;
        var outline = (ShellOutlineViewModel)outlineView.DataContext!;
        Wait(() => viewer.CurrentModel.Source == source && outline.OutlineItems.Count == 50);
        var target = outline.OutlineItems[39];
        Activate(39);
        Wait(() => scroll.Offset.Y > 5000);
        Wait(() => Landed(target.Line)); Settle();
        scroll.Offset = default;
        Activate(39);
        Wait(() => Landed(target.Line)); Settle();
        if (!ReferenceEquals(outline.CurrentOutlineItem, target)) throw new InvalidOperationException("Native outline lost source identity.");
        scroll.Focus();
        var focused = window.FocusManager?.GetFocusedElement();
        var earlier = outline.OutlineItems[10];
        if (!viewer.TryGetSourceLineBounds(earlier.Line, out var bounds)) throw new InvalidOperationException("Native source bounds missing.");
        var point = viewer.TranslatePoint(new Point(0, bounds.Top), scroll)!.Value;
        scroll.Offset = new Vector(0, Math.Max(0, scroll.Offset.Y + point.Y - 14));
        Wait(() => ReferenceEquals(outline.CurrentOutlineItem, earlier));
        if (!ReferenceEquals(focused, window.FocusManager?.GetFocusedElement()) || model.Markdown != source)
            throw new InvalidOperationException("Native reading follow changed focus or source.");
        using var bitmap = new RenderTargetBitmap(new PixelSize(1280, 820));
        bitmap.Render(window); bitmap.Save(Path.Combine(_uiDirectory, "outline-reading.png"));
        Console.WriteLine("Native outline activation, repeated navigation and reading follow passed.");

        void Activate(int index)
        {
            // 大纲行 UI 已下沉到库面板：按同序索引换算面板行后走面板导航入口。
            outlinePanel.Navigate(outlinePanel.Rows[index]);
        }
        bool Landed(int line) => viewer.TryGetSourceLineBounds(line, out var sourceBounds)
            && viewer.TranslatePoint(new Point(0, sourceBounds.Top), scroll) is { } targetPoint
            && Math.Abs(targetPoint.Y - 14) < 2;
        void Settle() { var until = Environment.TickCount64 + 250; Wait(() => Environment.TickCount64 >= until); }
        void Wait(Func<bool> condition)
        {
            var deadline = Environment.TickCount64 + 15000;
            var frame = new DispatcherFrame();
            var passed = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            timer.Tick += (_, _) =>
            {
                window.UpdateLayout(); passed = condition();
                if (passed || Environment.TickCount64 >= deadline) frame.Continue = false;
            };
            timer.Start();
            try { Dispatcher.UIThread.PushFrame(frame); }
            finally { timer.Stop(); }
            if (!passed) throw new TimeoutException("Native outline navigation or reading follow timed out.");
        }
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

    /// <summary>git 执行模式：Vex 程序作为本仓库（或指定仓库）git 操作的统一入口——
    /// commit/tag/push 全走面板同一套 GitService（SSH 推送内部转系统 git，属于 Vex 的封装行为）。</summary>
    public static void RunGitExecute(string repositoryPath, string action, string? argument, string? pathsCsv = null)
    {
        AppBuilder.Configure<NativeVerificationApp>().UsePlatformDetect().With(VexFonts.Options)
            .UseReactiveUI(_ => { }).SetupWithoutStarting();
        var app = (NativeVerificationApp)Application.Current!;
        var window = app.CreateShell();
        window.Show();
        try
        {
            var model = (MainWindowViewModel)window.DataContext!;
            var opened = model.OpenStartupDocumentAsync([repositoryPath]);
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!opened.IsCompleted)
            {
                if (DateTime.UtcNow >= deadline) throw new TimeoutException("Open repository timed out.");
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            opened.GetAwaiter().GetResult();
            model.EnterGitMode();
            var git = (Modules.Git.ViewModels.ShellGitViewModel)window.GetVisualDescendants()
                .OfType<Modules.Git.Views.ShellGitView>().Single().DataContext!;
            var refresh = git.RefreshAsync();
            var refreshDeadline = DateTime.UtcNow.AddSeconds(60);
            while (!refresh.IsCompleted)
            {
                if (DateTime.UtcNow >= refreshDeadline) throw new TimeoutException("Git snapshot timed out.");
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            refresh.GetAwaiter().GetResult();
            switch (action)
            {
                case "commit":
                    git.Summary = argument ?? throw new ArgumentException("commit 需要提交信息。");
                    // pathsCsv 非空时按路径部分提交（分批提交历史用），否则全勾提交。
                    var commitPaths = string.IsNullOrWhiteSpace(pathsCsv)
                        ? null
                        : pathsCsv.Split(',').Select(p => p.Trim().Replace('/', System.IO.Path.DirectorySeparatorChar)).ToArray();
                    if (commitPaths is { Length: > 0 })
                    {
                        var service = new Vex.Git.GitService(repositoryPath);
                        var committed = service.CommitPaths(commitPaths, git.Summary + (git.Description.Length > 0 ? "\n\n" + git.Description : string.Empty));
                        Console.WriteLine("GIT-EXEC committed " + committed.Sha[..7] + " files=" + commitPaths.Length);
                        break;
                    }
                    git.AllChecked = true;
                    var commit = git.CommitAsync();
                    var commitDeadline = DateTime.UtcNow.AddSeconds(60);
                    while (!commit.IsCompleted)
                    {
                        if (DateTime.UtcNow >= commitDeadline) throw new TimeoutException("Commit timed out.");
                        Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(10);
                    }
                    commit.GetAwaiter().GetResult();
                    Console.WriteLine("GIT-EXEC committed");
                    break;
                case "tag":
                    {
                        // async-void 命令不可等待：无头通道必须等真实完成，失败透出而非无条件报成功。
                        var tagTask = git.CreateTagHeadlessAsync(argument ?? throw new ArgumentException("tag 需要名称。"));
                        var tagDeadline = DateTime.UtcNow.AddSeconds(60);
                        while (!tagTask.IsCompleted)
                        {
                            if (DateTime.UtcNow >= tagDeadline) throw new TimeoutException("Tag timed out.");
                            Dispatcher.UIThread.RunJobs();
                            Thread.Sleep(10);
                        }
                        tagTask.GetAwaiter().GetResult();
                        Console.WriteLine("GIT-EXEC tagged " + argument);
                        break;
                    }
                case "push":
                    var push = git.PushAsync();
                    var pushDeadline = DateTime.UtcNow.AddSeconds(180);
                    while (!push.IsCompleted)
                    {
                        if (DateTime.UtcNow >= pushDeadline) throw new TimeoutException("Push timed out.");
                        Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(100);
                    }
                    push.GetAwaiter().GetResult();
                    Console.WriteLine("GIT-EXEC pushed");
                    break;
                case "push-tag":
                    var pushTag = git.PushTagAsync(argument ?? throw new ArgumentException("push-tag 需要名称。"));
                    var pushTagDeadline = DateTime.UtcNow.AddSeconds(180);
                    while (!pushTag.IsCompleted)
                    {
                        if (DateTime.UtcNow >= pushTagDeadline) throw new TimeoutException("Push tag timed out.");
                        Dispatcher.UIThread.RunJobs();
                        Thread.Sleep(100);
                    }
                    pushTag.GetAwaiter().GetResult();
                    Console.WriteLine("GIT-EXEC pushed-tag " + argument);
                    break;
                default:
                    throw new ArgumentException("未知 git 动作：" + action);
            }
        }
        finally
        {
            window.Close();
        }
    }

    public static void RunGit(string repositoryPath)
    {
        AppBuilder.Configure<NativeVerificationApp>().UsePlatformDetect().With(VexFonts.Options)
            .UseReactiveUI(_ => { }).SetupWithoutStarting();
        var app = (NativeVerificationApp)Application.Current!;
        var window = app.CreateShell();
        window.Width = 1440;
        window.Height = 948;
        window.Show();
        try
        {
            var model = (MainWindowViewModel)window.DataContext!;
            var opened = model.OpenStartupDocumentAsync([repositoryPath]);
            var openDeadline = DateTime.UtcNow.AddSeconds(60);
            while (!opened.IsCompleted)
            {
                if (DateTime.UtcNow >= openDeadline) throw new TimeoutException("Open repository timed out.");
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            opened.GetAwaiter().GetResult();
            model.EnterGitMode();
            var panel = window.GetVisualDescendants().OfType<Modules.Git.Views.ShellGitView>().Single();
            var git = (Modules.Git.ViewModels.ShellGitViewModel)panel.DataContext!;
            var refresh = git.RefreshAsync();
            var refreshDeadline = DateTime.UtcNow.AddSeconds(60);
            while (!refresh.IsCompleted)
            {
                if (DateTime.UtcNow >= refreshDeadline) throw new TimeoutException("Git snapshot timed out.");
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            refresh.GetAwaiter().GetResult();
            Console.WriteLine($"GIT-VERIFY repo={repositoryPath} branch={git.BranchName} history={git.History.Count} changes={git.Changes.Count}");
            for (var i = 0; i < git.History.Count; i++)
            {
                git.SelectedCommit = git.History[i];
                Dispatcher.UIThread.RunJobs();
                Console.WriteLine($"commit[{i}] files={git.CommitFiles.Count} subject={git.CommitSubject}");
                foreach (var file in git.CommitFiles)
                {
                    git.SelectedCommitFile = file;
                    Dispatcher.UIThread.RunJobs();
                }
            }
            Console.WriteLine("GIT-VERIFY PASS");
        }
        finally
        {
            window.Hide(); // Close 会触发 headless 渲染竞态（CompositingRenderer），进程退出即回收
        }
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
        foreach (Match image in Regex.Matches(File.ReadAllText(BundledDocuments.GetPath("快速开始.md")), @"!\[[^\]]*\]\(([^)]+\.(?:png|gif))\)"))
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
            "gitGraph", "kanban", "classDiagram", "erDiagram", "requirementDiagram", WorkbenchLayoutKeys.ViewMindMap, "block-beta",
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
        // 语言 json 已内嵌程序集（不再拷贝输出目录），验证走与 App 相同的 AddResource 嵌入注册。
        var manager = I18nManager.Instance;
        manager.Register(new JsonLangPlugin(), CultureInfo.GetCultureInfo("en-US"));
        manager.AddResource(typeof(VexL).Assembly, typeof(Vex.Core.Messaging.ShellActionKind).Assembly);
        var labels = new HashSet<string>();
        foreach (var culture in new[] { "en-US", "zh-CN", "zh-Hant", "ja-JP" })
        {
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

    /// <summary>
    /// 布局真值探针：大纲悬浮/坞设切换、正文列贴底对齐、阅读长文虚拟化空白。
    /// 三类问题都以运行时几何为准（容器底边差、视口内未物化条目带），并留整窗截图。
    /// </summary>
    public static void RunLayout(string directory)
    {
        _uiDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_uiDirectory);
        new AppSettingsStore().Update(settings => settings with
        {
            HasSeenOnboardingGuide = true, IsMcpServerEnabled = false
        });
        AppBuilder.Configure<NativeVerificationApp>().UsePlatformDetect().With(VexFonts.Options)
            .UseReactiveUI(_ => { }).SetupWithoutStarting();
        var app = (NativeVerificationApp)Application.Current!;
        var window = app.CreateShell();
        window.Width = 1280;
        window.Height = 820;
        window.Show();
        var report = new List<string>();
        try
        {
            var model = (MainWindowViewModel)window.DataContext!;
            // 用户的验收文档形态：多级标题 + 操作/预期/实际段落 + XAML 代码栅栏。
            // 16 章起复刻 5.1C 家族的两个特征：反引号内大量空格对齐的超宽行内代码、
            // 括号里成对裸反引号（2.0`2`），行尾超宽在源码视图里横向溢出。
            var sections = new StringBuilder();
            for (var section = 1; section <= 40; section++)
            {
                var wide = section >= 16;
                sections.AppendLine($"## {section}. 验收章节 {section}").AppendLine();
                sections.AppendLine("**准备**：版本=3.2、算例类型=restart。").AppendLine();
                sections.AppendLine(wide
                    ? "**操作**：运行选项切 inp-chk。"
                    : $"**操作**：执行第 {section} 章的验收步骤，观察画布刷新。").AppendLine();
                sections.AppendLine(wide
                    ? "**预期**：101 行为 `101     inp-chk`。"
                    : $"**预期**：第 {section} 章的画布网格与吸附线同时刷新。").AppendLine();
                sections.AppendLine("**实际**：").AppendLine();
                for (var sub = 1; sub <= 3; sub++)
                {
                    sections.AppendLine($"### {section}.{sub} 头卡 {section}{sub} 联动").AppendLine();
                    sections.AppendLine(wide
                        ? "**操作**：输入单位/输出单位 切 british。"
                        : $"**操作**：头卡 {section}{sub} 改编号 7、勾选压缩标记。").AppendLine();
                    if (sub == 2 && wide)
                    {
                        sections.AppendLine("**预期**：新增 `103   7   cmpress   rstplt2`（压缩未勾选时为 ncmpress）；切回类型=new 后 103 行消失。").AppendLine();
                    }
                    else if (sub == 3 && wide)
                    {
                        sections.AppendLine("**预期**：新增 `105    1.5    2`（V1 同款：限值为 double 最短形态 2.0`2`，分配时间 `/` 渲染成空不留痕；三值任一非缺省即整行回滚并弹提示，若任一值超上限则整族拒绝提交。）").AppendLine();
                    }
                    else
                    {
                        sections.AppendLine($"**预期**：新增 `{section}{sub}` 与 `7`（压缩未勾选时为 `{section}{sub}` 原值）。").AppendLine();
                    }
                    sections.AppendLine("```xml").AppendLine("<Setter Property=\"VerticalAlignment\" Value=\"Center\" />")
                        .AppendLine("<Setter Property=\"Width\" Value=\"12\" />").AppendLine("```").AppendLine();
                    if (sub == 1 && wide && section % 4 == 0)
                    {
                        // 表格（重块 + 工具条装饰）与 mermaid（异步出图、量测后变高）穿插在宽行内码区。
                        sections.AppendLine("| 头卡 | 字段 | V1 行为 |").AppendLine("| --- | --- | --- |")
                            .AppendLine("| 101 | 运行选项 | `inp-chk` |").AppendLine("| 102 | 单位制 | `british` |")
                            .AppendLine("| 105 | CPU 预留 | `1.5` |").AppendLine();
                        sections.AppendLine("```mermaid").AppendLine("flowchart LR").AppendLine(" A[头卡] --> B[画布]")
                            .AppendLine(" B --> C{校验}").AppendLine(" C -->|通过| D[提交]").AppendLine("```").AppendLine();
                    }
                    sections.AppendLine(wide
                        ? "> [!WARNING]\n> 三值任一非缺省即整行回滚；清单与引用混排时注意压缩标记的联动。\n"
                        : string.Empty);
                    sections.AppendLine("**实际**：").AppendLine();
                }
            }
            var source = sections.ToString();
            var documentPath = Path.Combine(_uiDirectory, "layout-probe.md");
            File.WriteAllText(documentPath, source);
            var opening = model.OpenPathAsync(documentPath);
            Wait(window, () => opening.IsCompleted);
            opening.GetAwaiter().GetResult();
            // 对齐用户实机环境：SoftSage 衬线排版主题（其验收文档即在主题下阅读）。
            model.Appearance.SelectTypographyByKey("SoftSage");
            SettleStable(window, 800);
            model.Layout.IsOutlineVisible = true;

            var workspaceColumns = window.Workspace.FindControl<Grid>("WorkspaceColumns")!;
            var documentColumn = window.Workspace.FindControl<Grid>("DocumentColumn")!;
            var viewSurface = window.Workspace.FindControl<Grid>("ViewSurface")!;
            var outlineTarget = window.Workspace.FindControl<Border>("OutlineGuideTarget")!;
            var preview = window.Workspace.FindControl<Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;

            double Bottom(Avalonia.Visual element) =>
                element.TranslatePoint(new Point(0, element.Bounds.Height), window)?.Y ?? -1;

            void Probe(string tag)
            {
                window.UpdateLayout();
                var layout = model.Layout;
                // 悬浮形态已移除：任何窗幅、任何布局，大纲展开即坞设列（outlineCol>0），不得再出现悬浮卡。
                var docked = layout.ShowOutlineSidebar && layout.OutlineColumnWidth.Value > 0
                    && !outlineTarget.Classes.Contains("overlay");
                report.Add($"[{tag}] size={window.Width:0}x{window.Height:0} layout={model.Workbench.WorkspaceLayout} " +
                    $"docked={docked} outlineCol={(layout.ShowOutlineSidebar ? layout.OutlineColumnWidth.Value : 0):0}" +
                    (docked ? string.Empty : "  <-- DOCK VIOLATION"));
            }

            void Screenshot(string tag)
            {
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
                bitmap.Render(window);
                bitmap.Save(Path.Combine(_uiDirectory, tag + ".png"));
            }

            // 一、大纲悬浮与正文贴底：三档窗幅 × 四种布局全扫。
            foreach (var (width, height) in new[] { (1280, 820), (1000, 700), (900, 640) })
            {
                window.Width = width;
                window.Height = height;
                foreach (var layoutId in new[] { WorkbenchLayoutKeys.Source, WorkbenchLayoutKeys.Map, WorkbenchLayoutKeys.Read, WorkbenchLayoutKeys.Three })
                {
                    model.Workbench.SelectLayout(layoutId);
                    SettleStable(window, 600);
                    Probe($"dock-{width}x{height}-{layoutId}");
                    if (layoutId is WorkbenchLayoutKeys.Map or WorkbenchLayoutKeys.Source) Screenshot($"dock-{width}x{height}-{layoutId}");
                }
            }

            // 二、阅读空白：dock 几何使用小合成稿；以下全部改用仓库内的真实长文档，
            // 避免合成文本遗漏图片、公式、脚注、HTML、Grid Table 与异步图表。
            var fixturePath = FindRepositoryDocument("docs/Markdown功能示例.md");
            var openingFixture = model.OpenPathAsync(fixturePath);
            Wait(window, () => openingFixture.IsCompleted);
            openingFixture.GetAwaiter().GetResult();
            model.Appearance.SelectTypographyByKey("SoftSage");
            SettleStable(window, 1200);
            model.Layout.IsOutlineVisible = true;
            report.Add($"[fixture] path={fixturePath} bytes={new FileInfo(fixturePath).Length}");

            // 按用户手势序列——源码编辑态缩窗 → 切阅读 → 大纲跳到宽行内代码区（5.1C 形态）→
            // 逐屏下滚（每步探一次视口内未物化条目）→ 放大窗幅，捕捉空白首次出现的位置与当时条目尺寸。
            window.Width = 1280;
            window.Height = 820;
            model.Workbench.SelectLayout(WorkbenchLayoutKeys.Source);
            SettleStable(window, 800);
            window.Width = 1000;
            window.Height = 700;
            SettleStable(window, 600);
            model.Workbench.SelectLayout(WorkbenchLayoutKeys.Read);
            SettleStable(window, 1200);
            var viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
            var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
            var panel = viewer.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel>().Single();
            var outlineView = window.GetVisualDescendants().OfType<ShellOutlineView>().Single();
            var outlinePanel = outlineView.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownOutlinePanel>("OutlinePanel")!;
            var outline = (ShellOutlineViewModel)outlineView.DataContext!;
            Wait(window, () => viewer.CurrentModel.Blocks.Count > 0 && outline.OutlineItems.Count > 0);
            report.Add($"[read] blocks={viewer.CurrentModel.Blocks.Count} items={panel.ItemCount} outlineRows={outline.OutlineItems.Count}");

            var blanks = new List<string>();
            var stableFailures = new List<string>();
            void ProbeBlank(string tag)
            {
                window.UpdateLayout();
                blanks.Clear();
                double blankHeight = 0;
                var viewportTop = scroll.Offset.Y;
                var viewportBottom = scroll.Offset.Y + scroll.Viewport.Height;
                for (var index = 0; index < panel.ItemCount; index++)
                {
                    if (!panel.TryGetItemTop(index, out var top)) continue;
                    var bandHeight = panel.GetItemHeight(index);
                    if (top + bandHeight <= viewportTop || top >= viewportBottom) continue;
                    var realized = panel.GetRealizedItem(index);
                    var deferredEmpty = realized?.GetType().Name == "DeferredMarkdownBlock"
                        && realized is Avalonia.Controls.Decorator { Child: null };
                    if (realized is null || deferredEmpty)
                    {
                        blanks.Add($"#{index}@{top:0}+{bandHeight:0}{(deferredEmpty ? ":empty-child" : string.Empty)}");
                        blankHeight += bandHeight;
                    }
                }
                report.Add($"[{tag}] offset={scroll.Offset.Y:0} extent={scroll.Extent.Height:0} viewport={scroll.Viewport.Height:0} " +
                    $"compensations={panel.AnchorCompensationCount} viewportBlankBands={blanks.Count} blankHeight={blankHeight:0}" +
                    (blanks.Count > 0 ? " " + string.Join(",", blanks.Take(12)) : string.Empty));
            }

            void DumpNeighbors(string tag, int aroundIndex)
            {
                for (var index = Math.Max(0, aroundIndex - 3); index <= Math.Min(panel.ItemCount - 1, aroundIndex + 3); index++)
                {
                    panel.TryGetItemTop(index, out var top);
                    var realized = panel.GetRealizedItem(index);
                    report.Add($"[{tag}] item#{index} top={top:0} height={panel.GetItemHeight(index):0} measured={panel.IsItemMeasured(index)} " +
                        $"realized={realized is not null} control={(realized?.GetType().Name ?? "-")} childEmpty={(realized is Avalonia.Controls.Decorator d && d.Child is null ? "yes" : "no")}");
                }
            }

            // 大纲跳到宽行内代码区开头（复刻「从 5.1C 开始」的落点）。
            var wideRow = outlinePanel.Rows[Math.Min(48, outlinePanel.Rows.Count - 1)];
            outlinePanel.Navigate(wideRow);
            SettleStable(window, 1200);
            ProbeBlank("read-jump");
            if (blanks.Count > 0)
            {
                stableFailures.Add("read-jump");
                DumpNeighbors("read-jump", int.Parse(blanks[0].Split('@')[0][1..]));
                Screenshot("read-jump");
            }

            // 逐屏下滚：捕捉空白首次出现与最严重一步。
            string? worstStep = null;
            double worstBlank = 0;
            for (var step = 0; step < 24; step++)
            {
                scroll.Offset += new Vector(0, 260);
                SettleStable(window, 350);
                ProbeBlank($"read-scroll-{step}");
                var stepBlank = blanks.Count;
                if (stepBlank > 0 && worstStep is null) Screenshot($"read-onset-{step}");
                if (stepBlank > worstBlank)
                {
                    worstBlank = stepBlank;
                    worstStep = $"read-scroll-{step}";
                    Screenshot("read-worst");
                    DumpNeighbors("read-worst", int.Parse(blanks[0].Split('@')[0][1..]));
                }
            }
            if (worstStep is not null) report.Add($"[read] worstStep={worstStep} worstBlankBands={worstBlank}");

            // 放大窗幅（用户「调整窗体大小」路径），复探一次。
            window.Width = 1400;
            window.Height = 940;
            SettleStable(window, 1200);
            ProbeBlank("read-enlarged");
            if (blanks.Count > 0) stableFailures.Add("read-enlarged");
            Screenshot("read-enlarged");

            // 三、渲染进行中的滚动风暴：阅读态在场重开文档，不等渲染静默就开始逐屏下滚，
            // 复刻「渲染时底部逐渐抬升」的观察时序；捕到空白立即截图保鲜。
            window.Width = 1000;
            window.Height = 700;
            SettleStable(window, 500);
            var reopened = model.OpenPathAsync(fixturePath);
            Wait(window, () => reopened.IsCompleted);
            reopened.GetAwaiter().GetResult();
            for (var step = 0; step < 40; step++)
            {
                scroll.Offset += new Vector(0, 320);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                ProbeBlank($"live-{step}");
                if (blanks.Count > 0)
                {
                    Screenshot($"live-blank-{step}");
                    DumpNeighbors($"live-{step}", int.Parse(blanks[0].Split('@')[0][1..]));
                    break;
                }
                Thread.Sleep(120);
                Dispatcher.UIThread.RunJobs();
            }
            // 风暴后再静默复探：区分「瞬时未收敛」与「稳定坏态」。
            SettleStable(window, 1200);
            ProbeBlank("live-settled");
            if (blanks.Count > 0) stableFailures.Add("live-settled");
            if (model.Markdown != File.ReadAllText(fixturePath))
            {
                throw new InvalidOperationException("Reading layout verification changed the comprehensive repository document.");
            }

            // 四、真实手工测试长文：跨整篇比例跳滚，并在窄窗/宽窗下复探。
            // 这份文档没有重复生成，可直接发现与合成压力样本不同的块高组合。
            var manualDocumentPath = FindRepositoryDocument("docs/测试文档.md");
            var openingManual = model.OpenPathAsync(manualDocumentPath);
            Wait(window, () => openingManual.IsCompleted);
            openingManual.GetAwaiter().GetResult();
            SettleStable(window, 1500);
            viewer = preview.FindControl<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>("PreviewMarkdownViewer")!;
            scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
            panel = viewer.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel>().Single();
            Wait(window, () => viewer.CurrentModel.Blocks.Count > 0 && panel.ItemCount > 0);
            report.Add($"[manual] path={manualDocumentPath} blocks={viewer.CurrentModel.Blocks.Count} items={panel.ItemCount} bytes={new FileInfo(manualDocumentPath).Length}");
            foreach (var ratio in new[] { 0d, .03d, .11d, .23d, .37d, .49d, .61d, .73d, .84d, .93d, 1d })
            {
                var maxOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
                scroll.Offset = new Vector(0, maxOffset * ratio);
                SettleStable(window, 600);
                ProbeBlank($"manual-jump-{ratio:0.00}");
                if (blanks.Count > 0)
                {
                    stableFailures.Add($"manual-jump-{ratio:0.00}");
                    DumpNeighbors($"manual-{ratio:0.00}", int.Parse(blanks[0].Split('@')[0][1..]));
                    Screenshot($"manual-blank-{ratio:0.00}");
                }
            }

            foreach (var (width, height) in new[] { (860, 620), (1520, 960) })
            {
                window.Width = width;
                window.Height = height;
                SettleStable(window, 1000);
                ProbeBlank($"manual-resize-{width}x{height}");
                if (blanks.Count > 0) stableFailures.Add($"manual-resize-{width}x{height}");
            }
            if (model.Markdown != File.ReadAllText(manualDocumentPath))
            {
                throw new InvalidOperationException("Reading layout verification changed the manual repository document.");
            }

            var finalResult = string.Join(Environment.NewLine, report);
            File.WriteAllText(Path.Combine(_uiDirectory, "layout-verification.txt"), finalResult);
            Console.WriteLine(finalResult);
            if (stableFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Reading viewport still contains unrealized bands after layout settled: {string.Join(",", stableFailures)}.");
            }
            Console.WriteLine("LAYOUT-VERIFY PASS");
        }
        finally
        {
            window.Close();
        }

        void SettleStable(Window target, int quietMilliseconds)
        {
            // 等几何三要素（偏移、范围、面板总高）连续静默后再补一拍后台队列，锚点补偿链路才算收完。
            var stable = 0d;
            double lastOffset = -1, lastExtent = -1, lastDesired = -1;
            Wait(target, () =>
            {
                var panelQuiet = true;
                var previewPanel = target.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel>().FirstOrDefault();
                var desired = previewPanel?.DesiredSize.Height ?? -1;
                var scrollNow = target.GetVisualDescendants().OfType<ScrollViewer>()
                    .OrderByDescending(control => control.Extent.Height - control.Viewport.Height).FirstOrDefault();
                var offset = scrollNow?.Offset.Y ?? -1;
                var extent = scrollNow?.Extent.Height ?? -1;
                if (Math.Abs(offset - lastOffset) < .5 && Math.Abs(extent - lastExtent) < .5 && Math.Abs(desired - lastDesired) < .5)
                {
                    stable += 30;
                    panelQuiet = stable >= quietMilliseconds;
                }
                else
                {
                    stable = 0;
                    panelQuiet = false;
                }
                lastOffset = offset;
                lastExtent = extent;
                lastDesired = desired;
                return panelQuiet;
            });
        }
    }

    private static string FindRepositoryDocument(string relativePath)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Repository verification document was not found: {relativePath}");
    }

    private static void Wait(Window window, Func<bool> condition)
    {
        var deadline = Environment.TickCount64 + 30000;
        var frame = new DispatcherFrame();
        var passed = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) => { window.UpdateLayout(); passed = condition(); if (passed || Environment.TickCount64 >= deadline) frame.Continue = false; };
        timer.Start();
        try { Dispatcher.UIThread.PushFrame(frame); }
        finally { timer.Stop(); }
        if (!passed) throw new TimeoutException("Layout probe condition timed out.");
    }
}
