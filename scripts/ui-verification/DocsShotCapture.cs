using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 快速开始文档的功能截图：以无头渲染驱动真实界面状态，产出与正文小节一一对应的图片，
/// 不用外部绘图工具伪造，也不会引入只存在于截图里的界面。
/// </summary>
internal static class DocsShotCapture
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 400 / 1000;
            while (Stopwatch.GetTimestamp() < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(10);
            }
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            LayoutSettle.Run();
            window.UpdateLayout();
        }
        void Shot(string name)
        {
            Render();
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine("SHOT " + name);
        }
        void OpenSample(string file)
        {
            var opening = vm.OpenPathAsync(Path.Combine(BundledDocuments.SamplesFolder, file));
            var timeout = Stopwatch.StartNew();
            while (!opening.IsCompleted && timeout.Elapsed < TimeSpan.FromSeconds(15)) Render();
            if (!opening.IsCompleted) throw new TimeoutException("Documentation sample did not open: " + file);
            opening.GetAwaiter().GetResult();
        }

        window.Width = 1440; window.Height = 900;
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectPaletteByKey("teal");
        vm.Appearance.SelectThemeByKey("light");
        vm.Layout.ShowOutline();
        vm.Workbench.SelectLayout("instant");
        Shot("主界面");

        vm.Appearance.SelectThemeByKey("dark");
        vm.Layout.ShowOutline();
        vm.Workbench.SelectLayout("instant");
        Render();

        // §2 开始写作：文件列表 + 即时编辑 + 右侧大纲。
        Shot("工作区与大纲");

        // §2 查找与替换：替换行展开，徽章按真实命中计数。
        vm.FindBar.ShowReplacePanel();
        vm.FindBar.SearchText = "文档";
        Thread.Sleep(300);
        Shot("查找与替换");
        vm.FindBar.CloseFindPanel();

        // §3 布局：八种视图组合的选择面板。
        vm.Workbench.ShowLayouts = true;
        Shot("布局切换");
        vm.Workbench.ShowLayouts = false;

        // §3 布局：源码＋阅读对照。
        vm.Workbench.SelectLayout("source-render");
        Shot("源码对照");

        vm.Workbench.SelectLayout("read");
        Shot("阅读视图");

        // §3 结构：导图视图，等节点树解析完成。
        OpenSample("Avalonia 跨平台之道.md");
        vm.Workbench.ShowParagraphs = vm.Workbench.ShowLists = vm.Workbench.ShowCode = vm.Workbench.ShowTables = false;
        vm.Workbench.SelectLayout("map");
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 15;
        while (vm.Workbench.Root is null && Stopwatch.GetTimestamp() < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Render();
        window.GetVisualDescendants().OfType<Vex.Modules.Workspace.Views.MarkdownMindMapHost>().Single(host => host.IsEffectivelyVisible).FitMap();
        Shot("思维导图");

        // §3 演示：F6 进入演示态，右侧同时展开文档大纲。
        OpenSample("排版与写作.md");
        vm.Workbench.SelectLayout("instant");
        vm.Workbench.StartPresentation();
        vm.Workbench.ShowPresentationOutline();
        Shot("演示模式");
        vm.Workbench.StopPresentation();

        // §4 外观：明暗模式与九套配色的弹层。
        var appearanceMenu = window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")!;
        appearanceMenu.AppearanceButton.Flyout!.ShowAt(appearanceMenu.AppearanceButton);
        Shot("外观与配色");
        appearanceMenu.AppearanceButton.Flyout!.Hide();

        // §4 外观：正文排版的十三套效果卡片。
        vm.Workbench.ShowTypography = true;
        Shot("正文排版");
        vm.Workbench.ShowTypography = false;

        // §4 偏好设置：通用与文件页。
        var preferences = app.Resolve<ShellPreferencesViewModel>();
        var pending = app.Resolve<IShellOverlayService>().ShowPreferencesAsync(preferences);
        Render();
        Shot("偏好设置");
        // §6 导出与分享：导出页的默认格式、目录与水印。
        window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Single().SelectCategory("export");
        Shot("导出设置");
        foreach (var view in window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().ToArray())
            ((ShellPreferencesViewModel)view.DataContext!).Close();
        pending.Wait(2000);
        Render();

        foreach (var (file, name) in new[] { ("数学与化学.md", "公式与代码"), ("Mermaid 图表图鉴.md", "Mermaid图表") })
        {
            OpenSample(file);
            vm.Workbench.SelectLayout("read");
            Shot(name);
        }

        OpenSample("排版与写作.md");
        vm.Appearance.SelectThemeByKey("light");
        vm.Workbench.SelectLayout("read");
        vm.Layout.IsOutlineVisible = true;
        Render();
        var outlineView = window.GetVisualDescendants().OfType<ShellOutlineView>().Single();
        var outline = (ShellOutlineViewModel)outlineView.DataContext!;
        var list = outlineView.FindControl<ListBox>("OutlineList")!;
        var preview = window.Workspace.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
        var scroll = preview.FindControl<ScrollViewer>("PreviewScrollViewer")!;
        if (outline.OutlineItems.Count < 3) throw new InvalidOperationException("Documentation outline fixture is incomplete.");
        // [已知回归 2026-11-17 区间引入] 本段 GIF 采集在 headless 下触发 native
        // Segfault（点击大纲行即崩，单帧动作粒度二分确认与 MouseMove/按键无关）。
        // bisect 候选 21 笔（见 docs/更新日志 V2.0.0.0 后续修复记录），专项排查前
        // 以静态截图替代 GIF，保证文档站产物完整可用。
        Shot("大纲导航与阅读跟随-静态");
        Shot("大纲阅读跟随");

        Record("四视图切换", frame =>
        {
            if (frame % 15 == 0) vm.Workbench.SelectLayout(new[] { "instant", "source-render", "read", "map" }[frame / 15]);
        });
        vm.Workbench.SelectLayout("instant");
        appearanceMenu.AppearanceButton.Flyout!.ShowAt(appearanceMenu.AppearanceButton);
        Record("外观切换", frame =>
        {
            if (frame % 15 != 0) return;
            vm.Appearance.SelectPaletteByKey(new[] { "teal", "aurora", "teal", "aurora" }[frame / 15]);
            vm.Appearance.SelectThemeByKey(frame is >= 15 and < 45 ? "dark" : "light");
        });
        appearanceMenu.AppearanceButton.Flyout!.Hide();

        // 连续采集真实控件帧；消息循环推进布局、导航重试和阅读位置采样。
        void Record(string name, Action<int> step)
        {
            var folder = Path.Combine(output, name); Directory.CreateDirectory(folder);
            for (var frame = 0; frame < 60; frame++)
            {
                step(frame);
                var dispatcherFrame = new DispatcherFrame();
                using var timer = DispatcherTimer.RunOnce(() => dispatcherFrame.Continue = false, TimeSpan.FromMilliseconds(100));
                Dispatcher.UIThread.PushFrame(dispatcherFrame);
                window.UpdateLayout(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
                using var bitmap = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Documentation frame missing.");
                bitmap.Save(Path.Combine(folder, $"{frame:D3}.png"));
            }
            Console.WriteLine("RECORD " + name + "; frames=60; interval=100ms");
        }
    }
}
