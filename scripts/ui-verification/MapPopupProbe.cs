using CodeWF.Avalonia.Controls.Controls;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>临时探针：打开导图样式弹层截图，供与 design 原型逐项目视对照（结构与风格两排同屏）。</summary>
internal static class MapPopupProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        Directory.CreateDirectory(output);
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            LayoutSettle.Run();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        window.Width = 1440; window.Height = 900; Render();
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectPaletteByKey("indigo");
        const char newline = (char)10;
        var sample = VisualParityCapture.SampleDocument;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(sample, 1, 1, sample.Count(ch => ch == newline) + 1));
        Render();
        Thread.Sleep(900);
        vm.Workbench.SelectLayout("map");
        Render();

        var trigger = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Classes.Contains("map-tool") && b.Flyout is not null);
        Console.WriteLine($"TRIGGER_FOUND={trigger != null}");
        if (trigger is null) return;
        trigger.Flyout!.ShowAt(trigger);
        Render();
        var content = (Control)((Flyout)trigger.Flyout).Content!;
        var presenter = content.GetVisualAncestors().OfType<FlyoutPresenter>().First();
        void Capture(string name)
        {
            Render();
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)presenter.Bounds.Width, (int)presenter.Bounds.Height));
            bitmap.Render(presenter);
            bitmap.Save(Path.Combine(output, name + ".png"));
            Console.WriteLine($"CAPTURE {name}: {presenter.Bounds}");
        }
        Capture("map-popup-both-rows");
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        static Size Miniature(Control host, string name) => host.GetVisualDescendants().OfType<Button>()
            .First(b => b.Name == name).GetVisualDescendants().OfType<Canvas>().First().Bounds.Size;
        var steps = content.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Classes.Contains("map-step-label")).ToArray();
        Check(steps.Length == 2, $"弹层内两排同屏，只剩两个步骤标签（实得 {steps.Length}）");
        var stepY = steps.Select(s => s.TranslatePoint(default, presenter)!.Value.Y).ToArray();
        Check(stepY[1] > stepY[0], $"①结构在上、②风格在下（y={stepY[0]:0.##} → {stepY[1]:0.##}）");
        Check(!content.GetVisualDescendants().OfType<SegmentedPanel>().Any(), "分段页签已删除，两排不再互相隐藏");
        var choices = content.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("map-choice")).ToArray();
        Check(choices.Length == 12 && choices.All(b => b.IsVisible), $"两组共 12 张卡（实得 {choices.Length}）");
        Check(choices.All(b => Math.Abs(b.Bounds.Height - 108) <= 1 && b.BorderThickness == new Thickness(1)),
            $"卡片高度108px，选中环不改变边框布局（实得 {string.Join(",", choices.Select(b => b.Bounds.Height))}）");
        var initialScroll = content.GetVisualDescendants().OfType<ScrollViewer>().First();
        var initialFrame = presenter.GetVisualDescendants().OfType<Border>().First(b => b.Name == "LayoutRoot");
        Check(Math.Abs(initialFrame.Bounds.Height - 602) <= 1, $"标准导图弹层602px高（实得 {initialFrame.Bounds.Height}）");
        Check(choices.All(b => b.TranslatePoint(new Point(0, b.Bounds.Height), initialFrame)!.Value.Y <= initialFrame.Bounds.Height - 15 + .5)
            && initialScroll.Extent.Height <= initialScroll.Viewport.Height + .5,
            "标准桌面高度下最后一排卡片完整位于滚动视口内");
        // 原型 map-appearance.js:54-55 的卡片只有 miniature + <strong>名称</strong>：用途行早已退役，
        // 说明职能留在无障碍名字里（AddCard 把 purpose 拼进 AutomationProperties.Name）。
        var styleChoices = choices.Where(b => b.Name!.StartsWith("MapChoiceStyle_", StringComparison.Ordinal)).ToArray();
        Check(styleChoices.Length == 6
            && styleChoices.All(b => b.GetVisualDescendants().OfType<TextBlock>().Count() == 1)
            && styleChoices.All(b => Avalonia.Automation.AutomationProperties.GetName(b).Length > b.GetVisualDescendants().OfType<TextBlock>().First().Text!.Length),
            "六张风格卡只留名称，说明留在无障碍名字里而不是卡面用途行");
        var colorfulNodes = styleChoices.Single(b => b.Name == "MapChoiceStyle_colorful").GetVisualDescendants()
            .OfType<Avalonia.Controls.Shapes.Rectangle>().Skip(1).ToArray();
        Check(colorfulNodes.Select(n => n.Fill?.ToString()).Distinct().Count() == 6
            && colorfulNodes.All(n => n.StrokeThickness == 1.5), "缤纷预览沿用真实导图色表，六个子节点各自实色填充");
        var before = ShellWorkbenchViewModel.MindMapStyleKeys.Select(key => Miniature(content, "MapChoiceStyle_" + key)).ToArray();
        // 改选「中心放射」：风格排的线稿要跟着换形（双向联动），并把「② 风格」点亮引路。
        content.GetVisualDescendants().OfType<Button>().First(b => b.Name == "MapChoiceLayout_radial")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Render();
        var after = ShellWorkbenchViewModel.MindMapStyleKeys.Select(key => Miniature(content, "MapChoiceStyle_" + key)).ToArray();
        var radial = Miniature(content, "MapChoiceLayout_radial");
        Check(after.All(size => size == radial) && !after.SequenceEqual(before), "风格线稿随所选结构整体重绘");
        // 点卡片会重建两排步骤标签，旧实例已不在树上，只能重新按位置取。
        var litSteps = content.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Classes.Contains("map-step-label"))
            .Select(p => (p, p.TranslatePoint(default, presenter)!.Value.Y)).ToArray();
        Check(litSteps.Length == 2 && litSteps.Single(s => s.p.Classes.Contains("map-step-nudge")).Y == litSteps.Max(s => s.Y),
            "选完结构后点亮的是下面那排「风格」");
        Capture("map-popup-after-radial");
        trigger.Flyout.Hide();
        var map = trigger.GetVisualAncestors().OfType<Vex.Modules.Workspace.Views.MarkdownMindMapHost>().First()
            .FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
        foreach (var theme in new[] { "light", "dark" })
        foreach (var size in new[] { new Size(800, 480), new Size(1280, 820) })
        {
            vm.Appearance.SelectThemeByKey(theme);
            window.Width = size.Width; window.Height = size.Height; Render();
            var viewportBefore = map.CaptureViewport();
            trigger.Flyout.ShowAt(trigger); Render();
            Check(map.CaptureViewport().Y > viewportBefore.Y, "导图视口下移避让弹层");
            Check(map.TryFindResource("CodeWFMindViewDefaultPalette", map.ActualThemeVariant, out var palette) && palette is string,
                "导图主题包含宿主定义的分支色表");
            var expectedColors = ((string)palette!).Split(';').Skip(1).Take(6).Select(Avalonia.Media.Color.Parse).ToArray();
            var previewColors = content.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "MapChoiceStyle_colorful")
                .GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Rectangle>().Skip(1)
                .Select(n => ((Avalonia.Media.ISolidColorBrush)n.Fill!).Color).ToArray();
            Check(previewColors.SequenceEqual(expectedColors), "预览读取当前明暗主题色表，不回退到库内默认饱和色");
            var frame = presenter.GetVisualDescendants().OfType<Border>().First(b => b.Name == "LayoutRoot");
            var origin = frame.TranslatePoint(default, window)!.Value;
            Console.WriteLine($"MAP ANCHOR {trigger.TranslatePoint(default, window)} {trigger.Bounds.Size}");
            Console.WriteLine($"MAP POPUP {theme}/{size}: frame={origin}, {frame.Bounds.Size}, shadow={frame.BoxShadow}");
            Check(Math.Abs(trigger.Bounds.Height - 32) <= .5 && trigger.FontSize == 12
                && trigger.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any(p => p.Classes.Contains("wb-icon") && p.Data is not null && p.Stroke is not null),
                "导图样式入口为32px高、12号字，图标有可见描线");
            Check(Math.Abs(frame.Bounds.Width - 528) <= 1 && frame.Margin == new Thickness(32, 20, 32, 44)
                && frame.BoxShadow.Count == 2, "导图弹层528px宽，完整保留两层阴影的外圈空间");
            Check(origin.X >= 0 && origin.Y >= 0
                && origin.X + frame.Bounds.Width <= window.ClientSize.Width + .5
                && origin.Y + frame.Bounds.Height <= window.ClientSize.Height + .5,
                $"导图样式可见边框完整位于 {size}/{theme} 窗口内");
            var bodyScroll = content.GetVisualDescendants().OfType<ScrollViewer>().First();
            if (bodyScroll.Extent.Height > bodyScroll.Viewport.Height + .5)
            {
                var point = bodyScroll.TranslatePoint(new Point(bodyScroll.Bounds.Width / 2, bodyScroll.Bounds.Height / 2), window)!.Value;
                window.MouseWheel(point, new Vector(0, -100)); Render();
                Check(bodyScroll.Offset.Y + bodyScroll.Viewport.Height >= bodyScroll.Extent.Height - .5,
                    "窄窗导图样式能够滚到最后一排卡片");
            }
            Capture($"map-popup-{theme}-{size.Width}");
            var close = content.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("vex-dismiss"));
            var closePoint = close.TranslatePoint(new Point(close.Bounds.Width / 2, close.Bounds.Height / 2), window)!.Value;
            window.MouseDown(closePoint, Avalonia.Input.MouseButton.Left);
            window.MouseUp(closePoint, Avalonia.Input.MouseButton.Left); Render();
            Check(!trigger.Flyout.IsOpen, "导图样式关闭钮在窄窗仍可点击");
            Check(map.CaptureViewport() == viewportBefore, "关闭样式弹层精确还原导图位置和缩放");
        }
    }
}
