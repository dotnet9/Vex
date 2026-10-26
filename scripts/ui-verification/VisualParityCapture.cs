using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 与 design 原型逐场景离屏对比的截图器：同尺寸(1440x900)、同语言、同明暗、
/// 同配色与排版、同文档内容。原型侧由 design/tests/capture-parity.cjs 产出同名单。
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
            LayoutSettle.Run();
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
        // 与输入通道一致地写正文（事件总线会刷新统计、文件列表与预览），避免状态栏残留旧文档字数。
        const char cNewline = (char)10;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(SampleDocument, 1, 1, SampleDocument.Count(character => character == cNewline) + 1));
        Render();
        // 统计与派生状态是防抖刷新：等待真实时间窗口再继续，保证状态栏字数为样例文档的。
        Thread.Sleep(900);
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
            // 原型偏好页「界面动效」默认开启（motion:true），截图需呈现同样的开态。
            vm.Layout.EnableMotion = true;
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
            ResetScrolls();
            Render();
            // 布局切换会异步恢复光标并再滚动一次：截图前最后再归位一遍。
            ResetScrolls();
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine("CAPTURED " + name);
        }

        // 原型截图器注入样例后各面板都停在文档顶部；原生侧文本注入会把光标滚到首行之后，
        // 逐面板回滚到顶，保证与原型同一视口起点。
        void ResetScrolls()
        {
            if (window.FindControl<Vex.Modules.Workspace.Views.MarkdownEditorView>("EditorGuideTarget")
                    ?.FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor") is { } source)
            {
                source.Editor.CaretOffset = 0;
                source.Editor.ScrollToHome();
                foreach (var scroll in source.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
            }
            foreach (Control? host in new Control?[]
            {
                window.FindControl<Control>("PreviewGuideTarget"),
                window.FindControl<Control>("InstantGuideTarget"),
            })
            {
                if (host is null) continue;
                foreach (var scroll in host.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
            }
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
