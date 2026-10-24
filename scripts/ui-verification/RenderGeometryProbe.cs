using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>临时探针：打印阅读窗格关键块与源码窗格首行的窗口坐标，供原型指标比对。</summary>
internal static class RenderGeometryProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render()
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            LayoutSettle.Run();
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        window.Width = 1440; window.Height = 900; Render();
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectPaletteByKey("indigo");
        vm.Appearance.SelectedTypography = vm.Appearance.TypographyOptions.First(option => option.Key == "Simple");
        vm.Layout.IsSidebarVisible = true;
        vm.Layout.IsOutlineVisible = false;
        const char nl = (char)10;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(
            new Vex.Core.Messaging.MarkdownTextChangedCommand(VisualParityCapture.SampleDocument, 1, 1,
                VisualParityCapture.SampleDocument.Count(c => c == nl) + 1));
        Render();
        Thread.Sleep(900);
        Render();
        vm.Workbench.SelectLayout("read");
        Render();
        var probeViewer = window.GetVisualDescendants()
            .OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
            .First(v => v.IsVisible && v.GetVisualAncestors().All(a => a.IsVisible));
        Console.WriteLine($"READ-MARGIN {probeViewer.Margin} parent={probeViewer.Parent?.GetType().Name}");
        vm.Workbench.SelectLayout("source-render");
        Render();
        Console.WriteLine($"SOURCERENDER-MARGIN {probeViewer.Margin} parent={probeViewer.Parent?.GetType().Name}");
        Thread.Sleep(700);
        Render();

        var viewer = window.GetVisualDescendants()
            .OfType<CodeWF.Avalonia.Markdown.Controls.MarkdownViewer>()
            .First(v => v.IsVisible && v.GetVisualAncestors().All(a => a.IsVisible));
        Dump("MdH1"); Dump("MdH2"); Dump("MdH3");
        Dump("MdList"); Dump("MdListItem"); Dump("MdTaskMarkerBox");
        Dump("MdQuote"); Dump("MdTableContainer"); Dump("MdTableHeaderCell"); Dump("MdTableCell");

        // 表格容器诊断：容器期望高度 vs 子网格期望高度（排查表体被裁剪）。
        var tableContainer = viewer.GetVisualDescendants().FirstOrDefault(x => x.Classes.Contains("MdTableContainer")) as Control;
        if (tableContainer is not null)
        {
            Console.WriteLine($"TABLE-CONTAINER desired={tableContainer.DesiredSize} bounds={tableContainer.Bounds} clip={(tableContainer as Border)?.ClipToBounds} maxH={(tableContainer as Border)?.MaxHeight}");
            if (tableContainer is Border tb && tb.Child is Control tchild)
                Console.WriteLine($"TABLE-GRID type={tchild.GetType().Name} desired={tchild.DesiredSize} bounds={tchild.Bounds} margin={tchild.Margin}");
            // 实验：强制祖先链重新测量，观察容器高度是否恢复（判定是否为面板尺寸缓存过期）。
            var chain = tableContainer.GetVisualAncestors().OfType<Control>().ToList();
            Console.WriteLine($"TABLE-CHAIN {string.Join(" < ", chain.Take(4).Select(c => c.GetType().Name))}");
            if (tableContainer.Parent is { } panel)
            {
                var entriesField = panel.GetType().GetField("_entries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (entriesField?.GetValue(panel) is System.Collections.IEnumerable entries)
                {
                    var children = (panel as Panel)?.Children;
                    var index = 0;
                    foreach (var entry in entries)
                    {
                        var t = entry.GetType();
                        var control = (Control?)t.GetProperty("Control")?.GetValue(entry);
                        var cls = string.Join(",", control?.Classes.Select(c => c.ToString()) ?? []);
                        var size = t.GetProperty("Size")?.GetValue(entry);
                        var measured = t.GetProperty("Measured")?.GetValue(entry);
                        var realized = t.GetProperty("Realized")?.GetValue(entry);
                        Console.WriteLine($"ENTRY[{index}] {control?.GetType().Name} {cls} size={size} measured={measured} realized={realized} sameAsChild={ReferenceEquals(control, index < (children?.Count ?? 0) ? children![index] : null)}");
                        index++;
                    }
                    Console.WriteLine($"PANEL-CHILDREN {children?.Count} entries={index} virtualizing={panel.GetType().GetProperty("IsVirtualizing")?.GetValue(panel)}");
                }
            }
            foreach (var ancestor in chain) ancestor.InvalidateMeasure();
            tableContainer.InvalidateMeasure();
            Render();
            Console.WriteLine($"TABLE-AFTER-FORCE bounds={tableContainer.Bounds} desired={tableContainer.DesiredSize}");
        }

        // 任务条目内部结构：条目 Grid 的子级高度分解。
        var taskItem = viewer.GetVisualDescendants().FirstOrDefault(x => x.Classes.Contains("MdListItem")
            && x.GetVisualDescendants().Any(d => d.Classes.Contains("MdTaskMarkerBox")));
        if (taskItem is not null)
        {
            foreach (var descendant in taskItem.GetSelfAndVisualDescendants().OfType<Control>())
            {
                var point = descendant.TranslatePoint(new Point(0, 0), window);
                Console.WriteLine($"TASK\t{descendant.GetType().Name}\t{string.Join(",", descendant.Classes.Select(c => c.ToString()))}\ty={point?.Y:F1}\th={descendant.Bounds.Height:F1}");
            }
        }

        // 段落行高抽样：第一个正文段落。
        var paragraph = viewer.GetVisualDescendants().OfType<SelectableTextBlock>()
            .FirstOrDefault(t => t.Text?.Contains("这个周末") == true);
        if (paragraph is not null)
        {
            var point = paragraph.TranslatePoint(new Point(0, 0), window);
            Console.WriteLine($"PARA\ty={point?.Y:F1}\th={paragraph.Bounds.Height:F1}\tlh={paragraph.LineHeight}");
        }

        // 源码窗格首行：取编辑器可视区域顶部文本坐标。
        var editor = window.GetVisualDescendants().OfType<AvaloniaEdit.TextEditor>()
            .FirstOrDefault(t => t.IsVisible && t.GetVisualAncestors().All(a => a.IsVisible));
        if (editor is not null)
        {
            var point = editor.TranslatePoint(new Point(0, 0), window);
            Console.WriteLine($"EDITOR\ty={point?.Y:F1}\th={editor.Bounds.Height:F1}\tscrollY={editor.VerticalOffset:F1}");
        }

        static void DumpNothing() { }
        // 阅读窗格根面板：直接子级逐个报坐标，定位顶部留白来源。
        Console.WriteLine($"VIEWER margin={viewer.Margin}");
        var vpoint = viewer.TranslatePoint(new Point(0, 0), window);
        Console.WriteLine($"VIEWER y={vpoint?.Y:F1} h={viewer.Bounds.Height:F1}");
        var rootPanel = viewer.GetVisualDescendants().OfType<Panel>()
            .FirstOrDefault(panel => panel.Children.Any(c => c.Classes.Contains("MdH1")));
        if (rootPanel is not null)
        {
            var rp = rootPanel.TranslatePoint(new Point(0, 0), window);
            Console.WriteLine($"PANEL {rootPanel.GetType().Name} y={rp?.Y:F1} margin={rootPanel.Margin}");
            foreach (var child in rootPanel.Children.Take(8))
            {
                var cp = child.TranslatePoint(new Point(0, 0), window);
                Console.WriteLine($"CHILD\t{child.GetType().Name}\t{string.Join(",", child.Classes.Select(c => c.ToString()))}\ty={cp?.Y:F1}\th={child.Bounds.Height:F1}\tmargin={child.Margin}");
            }
        }

        // 生效值回读：确认 Simple 资源是否真的落到控件属性上。
        if (viewer.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(p => p.Classes.Contains("MdList")) is { } listPanel)
        {
            Console.WriteLine($"LIST spacing={listPanel.Spacing} margin={listPanel.Margin}");
            if (listPanel.Children.OfType<Control>().FirstOrDefault() is { } firstItem)
                Console.WriteLine($"ITEM margin={firstItem.Margin}");
        }
        Console.WriteLine($"VIEWER blockSpacing={viewer.BlockSpacing} paraLH={viewer.ParagraphLineHeight} listFirstPara={viewer.ListFirstParagraphMargin}");
        var h1 = viewer.GetVisualDescendants().OfType<Control>().First(c => c.Classes.Contains("MdH1"));
        Console.WriteLine($"H1 {h1.GetType().Name} margin={h1.Margin} lh={(h1 as SelectableTextBlock)?.LineHeight}");
        var h1Border = viewer.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Classes.Contains("MdH1Border"));
        if (h1Border is not null) Console.WriteLine($"H1BORDER margin={h1Border.Margin} padding={((Avalonia.Controls.Border)h1Border).Padding}");
        var cell = viewer.GetVisualDescendants().OfType<SelectableTextBlock>().FirstOrDefault(t => t.Classes.Contains("MdParagraph") && t.Parent is StackPanel sp && sp.Classes.Contains("MdTableCellContent"));
        if (cell is not null) Console.WriteLine($"CELLTEXT fs={cell.FontSize} lh={cell.LineHeight}");

        void Dump(string cls)
        {
            foreach (var control in viewer.GetVisualDescendants().Where(x => x.Classes.Contains(cls)))
            {
                var point = control.TranslatePoint(new Point(0, 0), window);
                Console.WriteLine($"{cls}\ty={point?.Y:F1}\th={control.Bounds.Height:F1}");
            }
        }
    }
}
