using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using CodeWF.Avalonia.Markdown.Highlighting;
using Prism.Ioc;
using Vex.Core.Models;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Workspace.Views;
using Vex.Modules.Workspace.ViewModels;

var output = Path.GetFullPath(args.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), "VexPerformance"));
Directory.CreateDirectory(output);
// 只读写独立配置和文档，不使用用户的设置、草稿、最近记录或网络资源。
AppContext.SetData("APP_CONFIG_FILE", Path.Combine(output, "performance.config"));
File.WriteAllText((string)AppContext.GetData("APP_CONFIG_FILE")!, """
    <configuration><appSettings>
      <add key="ThemeKey" value="light" />
      <add key="HasSeenOnboardingGuide" value="True" />
      <add key="IsMcpServerEnabled" value="False" />
    </appSettings></configuration>
    """);
var timings = new List<Measurement>();
Measure("markdown.highlight.register", CodeWFMarkdownHighlightingExtensions.UseHighlighting);
Measure("settings.load.20", () => { for (var i = 0; i < 20; i++) _ = new AppSettingsStore().Current; });
var settings = new AppSettingsStore();
Measure("settings.update.20", () => { for (var i = 0; i < 20; i++) settings.Update(s => s with { PaletteKey = i % 2 == 0 ? "dotnet" : "aurora" }); });

var startup = Stopwatch.StartNew();
AppBuilder.Configure<PerformanceApp>().UseSkia().WithInterFont()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
var app = (PerformanceApp)Application.Current!;
var window = app.Shell!;
var vm = (MainWindowViewModel)window.DataContext!;
timings.Add(new Measurement("startup.setup", startup.Elapsed.TotalMilliseconds));
Measure("startup.firstFrame", () => { window.Show(); Pump(); Capture("startup"); });
var live = window.FindControl<MarkdownInstantView>("InstantGuideTarget")!.Editor;
var preview = window.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!.FindControl<MarkdownViewer>("PreviewMarkdownViewer")!;
if (args.Skip(1).Contains("--profile"))
{
    var document = CreateDocument(100);
    var path = Path.Combine(output, "profile-100.md");
    File.WriteAllText(path, document);
    vm.Workbench.ShowInstant(); Pump();
    var host = (Panel)live.Parent!;
    var position = host.Children.IndexOf(live);
    host.Children.RemoveAt(position);
    var layout = new LayoutProbe { Child = live };
    host.Children.Insert(position, layout);
    Pump(); layout.Reset();
    Measure("profile.parse.100", () => _ = MarkdownBlockParser.Parse(document));
    if (args.Skip(1).Contains("--atomic"))
    {
        Measure("profile.controlsAndImagePath.100", () => live.SetText(document, path));
    }
    else
    {
        Measure("profile.controls.100", () => live.SetText(document));
        Measure("profile.imagePath.100", () => live.ImageBasePath = path);
    }
    Measure("profile.renderAndLayout.100", () => { window.UpdateLayout(); Pump(); });
    timings.Add(new Measurement("profile.layout.measure.100", layout.MeasureMilliseconds));
    timings.Add(new Measurement("profile.layout.arrange.100", layout.ArrangeMilliseconds));
    Check(live.Text == document, "Profile keeps the complete document");
    Capture("profile-100");
    window.Close();
    WriteTimings();
    return;
}
foreach (var sections in new[] { 12, 100 })
{
    var document = CreateDocument(sections);
    var path = Path.Combine(output, $"sample-{sections}.md");
    File.WriteAllText(path, document);
    vm.Workbench.ShowInstant(); Pump();
    Measure($"document.{sections}.instant.open", () => Pump(vm.OpenPathAsync(path)));
    Check(vm.Markdown == document && live.Text == vm.Markdown, "Instant editor displays the opened document");
    Console.WriteLine($"Document {sections}: {document.Length} characters, hidden preview {preview.Markdown?.Length ?? 0} characters, {preview.GetVisualDescendants().Count()} preview controls");
    Measure($"document.{sections}.read.firstFrame", () => { vm.Workbench.ShowRead(); Pump(); Capture($"sample-{sections}-read"); });
    Check(preview.Markdown == vm.Markdown, "Read mode displays the latest document");
    Measure($"document.{sections}.source.firstFrame", () => { vm.Workbench.ShowSource(); Pump(); });
    Measure($"document.{sections}.instant.return", () => { vm.Workbench.ShowInstant(); Pump(); });
}

if (!args.Skip(1).Contains("--timings-only")) VerifyPreviewSynchronization();
window.Close();
WriteTimings();

void WriteTimings()
{
    var report = new PerformanceReport(
        System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported
            ? "managed Avalonia Headless + Skia" : "NativeAOT Avalonia Headless + Skia",
        Environment.OSVersion.ToString(), false,
        typeof(MarkdownViewer).Assembly.GetName().Version?.ToString(), timings);
    File.WriteAllText(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(report, PerformanceJsonContext.Default.PerformanceReport));
    foreach (var timing in timings) Console.WriteLine($"{timing.Name}: {timing.Milliseconds:F1} ms");
}

static string CreateDocument(int sections)
{
    var document = new StringBuilder("# Performance sample\n\n");
    for (var i = 0; i < sections; i++)
        document.Append($"## Section {i + 1}\n\nA local paragraph with **bold**, *emphasis* and a [local link](#section).\n\n- First item\n- Second item\n\n```csharp\nvar value = {i};\nConsole.WriteLine(value);\n```\n\n| Name | Value | Description |\n| --- | --- | --- |\n| A | {i} | A short local row |\n\n");
    return document.ToString();
}

void VerifyPreviewSynchronization()
{
    var previewViewModel = (MarkdownPreviewViewModel)window.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!.DataContext!;
    vm.Workbench.ShowInstant(); Pump();
    var refreshVersion = previewViewModel.RemoteImageRefreshVersion;
    var text = "# Synchronized preview\n\n- [ ] Task item\n";
    vm.Markdown = text; Pump();
    Check(preview.Markdown == text, "Hidden preview stays synchronized before entering read mode");
    Check(live.Text == text, "Instant editor displays the latest document");

    previewViewModel.ApplyMarkdownPreviewRefresh(new MarkdownPreviewRefreshCommand(text, vm.DocumentInfo.CurrentFilePath, 1));
    Check(previewViewModel.RemoteImageRefreshVersion == refreshVersion + 1, "Remote image refresh stays immediate while preview is hidden");
    vm.Workbench.ShowRead(); Pump();
    Check(preview.Markdown == text && preview.ImageBasePath == vm.DocumentInfo.CurrentFilePath, "Read mode synchronizes the latest text and image path");
    Check(previewViewModel.RemoteImageRefreshVersion == refreshVersion + 1, "Entering read mode does not duplicate remote image refresh");
    Check(!previewViewModel.IsTaskEditingEnabled, "Reading mode preserves disabled task editing");

    vm.Workbench.ShowSource(); Pump();
    Check(previewViewModel.IsTaskEditingEnabled, "Source preview retains task editing");
    vm.Workbench.SourcePreviewEnabled = false; Pump();
    vm.Markdown = text + "\nUpdated while source preview is hidden.\n"; Pump();
    Check(preview.Markdown == vm.Markdown, "Hidden source preview retains the latest document");
    vm.Workbench.SourcePreviewEnabled = true; Pump();
    Check(preview.Markdown == vm.Markdown, "Re-enabling source preview displays the latest text");

    refreshVersion = previewViewModel.RemoteImageRefreshVersion;
    previewViewModel.ApplyMarkdownPreviewRefresh(new MarkdownPreviewRefreshCommand(vm.Markdown, vm.DocumentInfo.CurrentFilePath, 2));
    Check(previewViewModel.RemoteImageRefreshVersion == refreshVersion + 1, "Visible remote image refresh remains immediate");
    Pump(vm.SaveAsync());
    vm.Workbench.ShowInstant(); Pump();
    var alternateDirectory = Path.Combine(output, "another-directory");
    Directory.CreateDirectory(alternateDirectory);
    var sameTextPath = Path.Combine(alternateDirectory, "same-content.md");
    File.WriteAllText(sameTextPath, vm.Markdown);
    Pump(vm.OpenPathAsync(sameTextPath));
    Check(live.ImageBasePath == sameTextPath, "Opening equal text from another directory updates the instant editor image base path");
    vm.Workbench.ShowRead(); Pump();
    Check(preview.ImageBasePath == sameTextPath && preview.Markdown == vm.Markdown, "Reading follows the new file path even when its text is identical");
    Console.WriteLine("PASS: preview synchronizes current text, image path, remote image refresh and source task editing.");
}

void Measure(string name, Action action)
{
    var clock = Stopwatch.StartNew();
    var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
    action();
    timings.Add(new Measurement(name, clock.Elapsed.TotalMilliseconds, GC.GetAllocatedBytesForCurrentThread() - allocatedBefore));
}

void Pump(Task? task = null)
{
    var clock = Stopwatch.StartNew();
    do
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        if (task is null || task.IsCompleted) break;
        if (clock.Elapsed > TimeSpan.FromSeconds(30)) throw new TimeoutException("Document operation timed out.");
        Thread.Sleep(1);
    } while (true);
    task?.GetAwaiter().GetResult();
    Dispatcher.UIThread.RunJobs();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick();
}

void Capture(string name)
{
    using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
    frame.Save(Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
}

static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}

internal sealed record Measurement(string Name, double Milliseconds, long AllocatedBytes = 0);
internal sealed record PerformanceReport(string Runtime, string OperatingSystem, bool StartupIncludesUserConfiguration,
    string? MarkdownLibraryVersion, List<Measurement> Measurements);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PerformanceReport))]
internal partial class PerformanceJsonContext : JsonSerializerContext;

// 在验证宿主内统计真实布局回调，不向产品控件增加诊断状态。
internal sealed class LayoutProbe : Decorator
{
    public double MeasureMilliseconds { get; private set; }
    public double ArrangeMilliseconds { get; private set; }
    public void Reset() => MeasureMilliseconds = ArrangeMilliseconds = 0;
    protected override Size MeasureOverride(Size availableSize)
    {
        var start = Stopwatch.GetTimestamp();
        var result = base.MeasureOverride(availableSize);
        MeasureMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return result;
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        var start = Stopwatch.GetTimestamp();
        var result = base.ArrangeOverride(finalSize);
        ArrangeMilliseconds += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        return result;
    }
}

public sealed class PerformanceApp : Vex.App
{
    public Window? Shell { get; private set; }
    protected override void RegisterTypes(IContainerRegistry registry)
    {
        base.RegisterTypes(registry);
        registry.RegisterInstance<IAutoSaveDraftService>(new PerformanceDrafts());
        registry.RegisterInstance<IRecentDocumentStore>(new PerformanceRecent());
        // 显式工厂保留验证器自身的构造调用，避免 NativeAOT 仅保留反射元数据。
        registry.Register<IShellExternalPathResolver>(provider => new PerformancePathResolver(provider.Resolve<IDocumentService>()));
    }
    protected override AvaloniaObject CreateShell()
    {
        Shell = (Window)base.CreateShell();
        return Shell;
    }
}

internal sealed class PerformanceDrafts : IAutoSaveDraftService
{
    public DocumentSnapshot? TryRestore(DocumentSnapshot document) => null;
    public void QueueSave(DocumentSnapshot document, string markdown, string lastSavedMarkdown) { }
    public void Clear(DocumentSnapshot document) { }
    public void Flush() { }
}

internal sealed class PerformanceRecent : IRecentDocumentStore
{
    public IReadOnlyList<RecentDocument> Load(int maxCount) => [];
    public void Save(IEnumerable<RecentDocument> documents) { }
}

public sealed class PerformancePathResolver(IDocumentService documents) : IShellExternalPathResolver
{
    private readonly ShellExternalPathResolver _paths = new(documents);
    // 输出目录是验证器参数，不应被真实主窗口再次作为启动工作区打开。
    public ShellExternalPathResult ResolveStartupArgument(IEnumerable<string> arguments) => ShellExternalPathResult.None;
    public ShellExternalPathResult ResolveDroppedPath(string? path) => _paths.ResolveDroppedPath(path);
    public ShellExternalPathResult ResolveRecentPath(string? path) => _paths.ResolveRecentPath(path);
}
