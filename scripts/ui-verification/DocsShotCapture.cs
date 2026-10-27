using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
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

        window.Width = 1440; window.Height = 900;
        vm.Appearance.SelectThemeByKey("dark");
        vm.Layout.ShowOutline();
        vm.Workbench.SelectLayout("instant");
        Render();

        // §2 开始写作：文件列表 + 即时编辑 + 右侧大纲。
        Shot("工作区与大纲");

        // §2 查找与替换：替换行展开，徽章按真实命中计数。
        vm.FindBar.ShowReplacePanel();
        vm.FindBar.SearchText = "想法";
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

        // §3 结构：导图视图，等节点树解析完成。
        vm.Workbench.SelectLayout("map");
        var deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 15;
        while (vm.Workbench.Root is null && Stopwatch.GetTimestamp() < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        Shot("思维导图");

        // §3 演示：F6 进入演示态，右侧同时展开文档大纲。
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

        // §4 外观：正文排版的十二套效果卡片。
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
    }
}
