using System.Text;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Controls.Controls;
using Vex.Core.Motion;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 悬浮层分段控件的原型保真探针：轨道内缩、指示块浮起、遗留样式是否真的让位给共享配方、
/// 动效档位是否驱动指示块过渡、悬浮件投影是否随明暗换层，逐项对着 design/ 原型的取值断言。
/// </summary>
internal static class SegmentedFidelityProbe
{
    private const char Nl = (char)10;

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var sb = new StringBuilder();
        var failures = new List<string>();
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool ok, string what)
        {
            sb.AppendLine($"{(ok ? "PASS" : "FAIL")} {what}");
            if (!ok) failures.Add(what);
        }

        window.Width = 1440; window.Height = 900;
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectPaletteByKey("indigo");
        vm.Appearance.SelectThemeByKey("light");
        vm.Layout.IsSidebarVisible = false;
        vm.Layout.IsOutlineVisible = false;
        vm.Workbench.SelectLayout("read");
        Render();

        Border? FindTrack() => window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("segmented-track") && b.GetVisualDescendants().OfType<SegmentedPanel>().Any());
        Border? FindThumb() => window.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("segmented-thumb"));

        // 1) 轨道内缩 = pad-sm 的一半（原型 segmented.css:6，density.css:2-4 三档 4/6/8）。
        var expectedInset = new Dictionary<string, double> { ["compact"] = 2, ["normal"] = 3, ["relaxed"] = 4 };
        foreach (var (density, inset) in expectedInset)
        {
            vm.Appearance.SelectedDensity = density;
            Render();
            var track = FindTrack();
            Check(track is not null, $"{density} 档找到分段轨道");
            if (track is null) continue;
            Check(track.Padding == new Thickness(inset), $"{density} 档轨道内缩 {track.Padding} 应为 {inset}（原型 pad-sm/2）");
        }
        vm.Appearance.SelectedDensity = "normal";
        Render();

        // 2) 轨道交给共享配方：胶囊圆角、底色取 Studio 分段轨道令牌而非悬停灰。
        //    原型这一代只剩密度/主题选择与演示坞还在用分段轨道，状态栏的 .view-switch 已退役，断言跟着落到现场仍在用的轨道上。
        var liveTrack = FindTrack();
        Check(liveTrack is not null, "找到分段轨道");
        if (liveTrack is not null)
        {
            Check(liveTrack.Classes.Contains("deck-modes") || liveTrack.BorderThickness == new Thickness(0),
                  $"轨道描边只在悬浮坞那一档出现（实测 {liveTrack.BorderThickness}）");
            Check(liveTrack.CornerRadius.TopLeft >= 499, $"轨道是胶囊圆角 {liveTrack.CornerRadius.TopLeft}");
            var expected = (liveTrack.Background as SolidColorBrush)?.Color;
            // 令牌在配色变体字典里，只能按当前变体查，读 Application.Resources 索引器只会拿到基表。
            window.TryFindResource("StudioSegmentedTrackColor", out var tokenValue);
            var token = tokenValue is Color color ? color : expected ?? default;
            Check(expected == token, $"轨道底色 {expected} 等于分段轨道令牌 {token}");
        }

        // 3) 指示块浮起随明暗换层：浅色 1px 4px 墨投影，深色投影清零改由 1px 淡描边成环（原型 prototype.css:24-25）。
        var lightThumb = FindThumb();
        Check(lightThumb is not null, "找到分段指示块");
        if (lightThumb is not null)
        {
            var shadow = lightThumb.BoxShadow;
            Check(shadow.Count == 1 && shadow[0].Blur == 4 && shadow[0].OffsetY == 1,
                  $"浅色指示块是 0 1 4 墨投影（实测 {shadow.Count} 层）");
        }
        vm.Appearance.SelectThemeByKey("dark");
        Render();
        var darkThumb = FindThumb();
        if (darkThumb is not null)
        {
            Check(darkThumb.BoxShadow.Count == 0, $"深色指示块不叠浅色投影（实测 {darkThumb.BoxShadow.Count} 层）");
            Check(darkThumb.BorderThickness == new Thickness(1), "深色指示块用 1px 描边成环");
        }

        // 4) 指示块过渡跟着「界面动效」档位：关闭档时长归零，标准档 260/140。
        // ITransition 的 Property/Duration 是受保护的，只能按具体过渡类型读回 Transition<T> 上的公开成员。
        static (string Property, int Ms)[] Shape(Transitions? transitions) => transitions is null ? [] :
            transitions.Select(transition => transition switch
            {
                Transition<double> wide => (wide.Property.Name, wide.Duration.Milliseconds),
                Transition<Thickness> thick => (thick.Property.Name, thick.Duration.Milliseconds),
                _ => (transition.GetType().Name, 0),
            }).ToArray();

        vm.Appearance.SelectThemeByKey("light");
        vm.Layout.MotionMode = MotionModes.Off;
        Render();
        var offShape = Shape(FindThumb()?.Transitions);
        Check(offShape.Length == 3 && offShape.All(entry => entry.Ms == 0),
              $"关闭动效档下指示块过渡时长归零（实测 {string.Join("|", offShape.Select(e => $"{e.Property}:{e.Ms}"))}）");
        vm.Layout.MotionMode = MotionModes.Full;
        Render();
        var fullShape = Shape(FindThumb()?.Transitions);
        Check(fullShape.Length == 3 &&
              fullShape.Any(entry => entry.Property == "Width" && entry.Ms == 260) &&
              fullShape.Any(entry => entry.Property == "Margin" && entry.Ms == 260) &&
              fullShape.Any(entry => entry.Property == "Opacity" && entry.Ms == 140),
              $"标准档指示块过渡回到 260/260/140（实测 {string.Join("|", fullShape.Select(e => $"{e.Property}:{e.Ms}"))}）");

        // 5) 页签内图标与文字的缝 = 6px、图标 14×14（原型 workspace-chrome.css:521/522）。
        // 按钮内容外面还套着模板里的 ContentPresenter，只能从按钮往下找，不能认直接父级。
        var tabHost = window.FindControl<ToggleButton>("SourceSegment")!;
        var tabLine = tabHost.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault();
        var tabIcon = tabHost.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().FirstOrDefault();
        Check(tabLine is not null && tabLine.Spacing == 6 && tabIcon is { Width: 14, Height: 14 },
              $"页签图标缝 {(tabLine?.Spacing.ToString() ?? "未找到")}、图标 {tabIcon?.Width:0.#}×{tabIcon?.Height:0.#} 应为 6 缝与 14×14");

        sb.AppendLine(failures.Count == 0 ? "PASS 悬浮层分段控件与原型配方逐项一致" : $"FAIL {failures.Count} 项不一致");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "segmented-fidelity.txt"), sb.ToString(), Encoding.UTF8);
        Console.Write(sb.ToString());
        if (failures.Count > 0) throw new InvalidOperationException($"悬浮层分段控件原型保真失败 {failures.Count} 项");
    }
}
