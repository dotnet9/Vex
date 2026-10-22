using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 与 design 原型逐场景离屏对比的截图器：同尺寸(1440x900)、同语言、同明暗、
/// 同配色与排版、同文档内容。原型侧由 design/scripts/capture-parity.cjs 产出同名单。
/// </summary>
internal static class VisualParityCapture
{
    public const int Width = 1440;
    public const int Height = 900;

    /// <summary>两侧共享的对比样例：覆盖标题、段落、任务、引用、表格、小节标题。</summary>
    public const string SampleDocument =
        "# 成都，慢慢走\n\n" +
        "这个周末，去成都。把脚步放慢一点，给茶香和街巷多留些时间。\n\n" +
        "## 出发清单\n\n" +
        "- [ ] 带上相机\n- [ ] 预订车票\n- [x] 收拾行李\n\n" +
        "> 慢一点，才能看见城市的细节。\n\n" +
        "## 第一天行程\n\n" +
        "| 时间 | 地点 | 说明 |\n| --- | --- | --- |\n| 09:00 | 人民公园 | 喝一碗盖碗茶 |\n| 14:00 | 宽窄巷子 | 沿青砖小巷慢慢走 |\n\n" +
        "### 人民公园\n\n" +
        "坐在茶社里，看本地人摆龙门阵，一下午就过去了。\n";

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        window.Width = Width; window.Height = Height; Render();
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectPaletteByKey("indigo");
        vm.Appearance.SelectedTypography = vm.Appearance.TypographyOptions.First(option => option.Key == "Simple");
        vm.Layout.IsSidebarVisible = true;
        vm.Layout.IsOutlineVisible = false;
        app.Resolve<IWorkspaceDocumentState>().UpdateDocument(SampleDocument, null);
        Render();

        Capture("01-instant", () => Select(vm, "instant"));
        Capture("02-source", () => Select(vm, "source"));
        Capture("03-read", () => Select(vm, "read"));
        Capture("04-map", () => { Select(vm, "map"); DelayMap(); });
        Capture("05-source-render", () => Select(vm, "source-render"));
        Capture("06-three", () => { Select(vm, "three"); DelayMap(); });
        Capture("07-appearance-menu", () =>
        {
            Select(vm, "instant");
            var appearance = window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")!;
            appearance.AppearanceButton.Flyout!.ShowAt(appearance.AppearanceButton);
            Render();
        });
        Capture("08-preferences", () =>
        {
            CloseFlyout(window);
            _ = app.Resolve<IShellOverlayService>().ShowPreferencesAsync(app.Resolve<ShellPreferencesViewModel>(), "appearance");
            Render();
        });
        Capture("09-typography-gallery", () =>
        {
            app.Resolve<ShellPreferencesViewModel>().Close();
            Render();
            vm.Workbench.ShowTypography = true;
            Render();
        });
        Capture("10-presentation", () =>
        {
            vm.Workbench.ShowTypography = false;
            vm.Workbench.StartPresentation();
            Render();
        });
        vm.Workbench.StopPresentation(); Render();

        void Capture(string name, Action setup)
        {
            setup();
            Render();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine("CAPTURED " + name);
        }

        static void Select(MainWindowViewModel vm, string layout)
        {
            vm.Workbench.ShowTypography = false;
            vm.Workbench.SelectLayout(layout);
        }

        // 导图布局是异步排版：多泵几轮布局任务再截图。
        void DelayMap()
        {
            for (var i = 0; i < 6; i++)
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
            }
        }

        static void CloseFlyout(MainWindow window)
        {
            var menu = window.FindControl<ShellAppearanceMenuView>("AppearanceMenu")!;
            if (menu.AppearanceButton.Flyout?.IsOpen == true) menu.AppearanceButton.Flyout.Hide();
        }
    }
}
