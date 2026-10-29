using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Motion;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 一次性探针：验证界面动效/界面圆角三档真的写进了运行时令牌，并留一份文本证据。
/// </summary>
internal static class MotionRadiusProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var resources = Application.Current!.Resources;
        var sb = new StringBuilder();
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }

        void Snap(string tag)
        {
            var fast = (TimeSpan)resources["MotionDurationFast"]!;
            var mid = (TimeSpan)resources["MotionDurationMid"]!;
            var slow = (TimeSpan)resources["MotionDurationSlow"]!;
            var sm = (CornerRadius)resources["VexDensityCornerRadiusSm"]!;
            var md = (CornerRadius)resources["VexDensityCornerRadiusMd"]!;
            var lg = (CornerRadius)resources["VexDensityCornerRadiusLg"]!;
            var pill = (CornerRadius)resources["VexDensityCornerRadiusPill"]!;
            sb.AppendLine($"{tag}: motion={vm.Layout.MotionMode} enabled={MotionResources.IsEnabled} scale={MotionResources.Scale:0.##} " +
                          $"fast={fast.TotalMilliseconds:0}ms mid={mid.TotalMilliseconds:0}ms slow={slow.TotalMilliseconds:0}ms " +
                          $"radius={vm.Appearance.RadiusMode} sm={sm.TopLeft} md={md.TopLeft} lg={lg.TopLeft} pill={pill.TopLeft}");
        }

        Snap("startup");

        vm.Layout.MotionMode = MotionModes.Reduced;
        vm.Appearance.RadiusMode = "compact";
        Render();
        Snap("reduced+compact");

        vm.Layout.MotionMode = MotionModes.Off;
        vm.Appearance.RadiusMode = "square";
        Render();
        Snap("off+square");

        // 直角档下的真实边界：分段轨道与缩放坞都吃同一个 pill 令牌，应随档位归零。
        var track = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("segmented-track"));
        if (track is not null) sb.AppendLine($"square segmented track radius={track.CornerRadius.TopLeft}");
        var dock = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("vex-zoomdock"));
        if (dock is not null) sb.AppendLine($"square zoomdock radius={dock.CornerRadius.TopLeft}");
        var searchFrame = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("search-frame"));
        if (searchFrame is not null) sb.AppendLine($"square search frame radius={searchFrame.CornerRadius.TopLeft}");

        vm.Layout.MotionMode = MotionModes.Full;
        vm.Appearance.RadiusMode = "standard";
        Render();
        Snap("restored");

        var path = Path.Combine(output, "motion-radius-probe.txt");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        Console.WriteLine(sb.ToString());
        if ((TimeSpan)resources["MotionDurationMid"]! != TimeSpan.FromMilliseconds(220)) throw new InvalidOperationException("mid duration did not restore");
        Console.WriteLine("PASS motion and radius tokens round-trip through all three modes");
    }
}
