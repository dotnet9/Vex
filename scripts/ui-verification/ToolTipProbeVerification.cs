using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

// ToolTip 真实渲染探针：以真实 ToolTip 控件（默认主题模板 + 字符串内容）在自有 Popup 中
// 打开并整幅截图，明暗两变体各两张（短文案 / 长文案）——核对默认 ToolTip 的真实观感
// （配色 / 圆角 / 内边距 / 320px 折行是否生效）。
internal static class ToolTipProbeVerification
{
    public static void Run(Window window, string output)
    {
        // 排版采样：与原型 probe-typography 同位对照（字号/字重/高度/内距）。
        void Sample(string label, Avalonia.Visual root, params string[] names)
        {
            foreach (var name in names)
            {
                var ctl = window.FindControl<Control>(name);
                if (ctl is null) continue;
                var text = ctl.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(b => b.IsVisible) ?? ctl as TextBlock;
                var tb = window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);
                if (tb is null) continue;
                var fs = tb.GetValue(Avalonia.Controls.TextBlock.FontSizeProperty);
                var h = tb.Bounds.Height;
                Console.WriteLine($"VEX-TYPO {label}/{name}: fs={fs} h={h:F0}");
            }
        }
        void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        var shell = (Vex.Modules.Shell.ViewModels.MainWindowViewModel)window.DataContext!;
        var longTipHost = new Button { Content = "probe", Width = 0, Height = 0 };
        if ((window.Content as Panel) is { } probePanel) probePanel.Children.Add(longTipHost);
        var samples = new[]
        {
            "清空搜索",
            "这条提示是一段很长的中文文案，用来核对默认 ToolTip 在超过 320 逻辑像素时是否正确折行，而不是被屏幕边缘直接裁切掉。"
        };
        foreach (var theme in new[] { "dark", "light" })
        {
            shell.Appearance.SelectThemeByKey(theme);
            Pump();
            for (var i = 0; i < samples.Length; i++)
            {
                ToolTip.SetTip(longTipHost, samples[i]);
                ToolTip.SetIsOpen(longTipHost, true);
                Pump();
                Pump();
                Console.WriteLine($"PROBE {theme} sample={i} isOpen={ToolTip.GetIsOpen(longTipHost)}");
                var resolvedFg = Application.Current!.TryGetResource("ToolTipForeground", window.ActualThemeVariant, out var fgResource)
                    ? fgResource : null;
                var resolvedBg = Application.Current!.TryGetResource("ToolTipBackground", window.ActualThemeVariant, out var bgResource)
                    ? bgResource : null;
                Console.WriteLine($"TOOLTIP-COLORS {theme} sample={i}: tokenFg={resolvedFg} tokenBg={resolvedBg}");
                // 弹层内真实解析值：ToolTip 自身与内容 TextBlock 的实际前景（含 DynamicResource 求值结果）。
                foreach (var tooltip in window.GetVisualDescendants().OfType<ToolTip>())
                {
                    var tipFg = tooltip.Foreground as Avalonia.Media.ISolidColorBrush;
                    var tipBg = tooltip.Background as Avalonia.Media.ISolidColorBrush;
                    var blocks = tooltip.GetVisualDescendants().OfType<TextBlock>().ToList();
                    var blockInfo = string.Join(",", blocks.Select(b => (b.Foreground as Avalonia.Media.ISolidColorBrush)?.Color.ToString() ?? b.Foreground?.GetType().Name ?? "inherit"));
                    var presenter = tooltip.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().FirstOrDefault();
                    Console.WriteLine($"TOOLTIP-LIVE {theme} sample={i}: tipFg={tipFg?.Color} tipBg={tipBg?.Color} presenter={presenter?.Name ?? "-"} textFg=[{blockInfo}]");
                }
                ToolTip.SetIsOpen(longTipHost, false);
                Pump();
            }
        }
    }
}
