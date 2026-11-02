using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Views;

/// <summary>同源样例正文的布局探针，与浏览器实际边框盒进行比较。</summary>
internal static class RenderGeometryProbe
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); LayoutSettle.Run(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
        window.Width = 1440; window.Height = 900;
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light"); vm.Appearance.SelectPaletteByKey("indigo");
        vm.Appearance.SelectTypographyByKey("Simple");
        vm.Layout.IsSidebarVisible = true; vm.Layout.IsOutlineVisible = false;
        var text = VisualParityCapture.SampleDocument;
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Vex.Core.Messaging.MarkdownTextChangedCommand(text, 1, 1, text.Count(c => c == '\n') + 1));
        Render();
        foreach (var layout in new[] { "read", "source-render", "instant" })
        {
            vm.Workbench.SelectLayout(layout); Render();
            var host = window.Workspace.FindControl<Control>(layout == "instant" ? "InstantGuideTarget" : "PreviewGuideTarget")!;
            foreach (var scroll in host.GetVisualDescendants().OfType<ScrollViewer>()) scroll.Offset = default;
            Render();
            Console.WriteLine("### " + layout);
            var surface = window.Workspace.FindControl<Grid>("ViewSurface")!;
            Console.WriteLine($"Surface origin={surface.TranslatePoint(default, window)} size={surface.Bounds.Size} columns={string.Join(',', surface.ColumnDefinitions.Select(column => column.ActualWidth))}");
            Console.WriteLine($"Document host origin={host.TranslatePoint(default, window)} size={host.Bounds.Size}");
            if (layout == "source-render" && (surface.ColumnDefinitions[1].ActualWidth != 0
                || Math.Abs(host.Bounds.Width - surface.Bounds.Width / 2) > .01
                || Math.Abs(host.TranslatePoint(default, surface)!.Value.X - surface.Bounds.Width / 2) > .01))
                throw new InvalidOperationException("Split documents share the full width without rounding or an extra gap.");
            var table = host.GetVisualDescendants().OfType<Border>().Single(c => c.Classes.Contains("MdTableContainer"));
            var tableGrid = table.GetVisualDescendants().OfType<Grid>().Single(c => c.Classes.Contains("MdTable"));
            var instant = layout == "instant";
            var expectedHeight = instant ? 110.4 : 136.4;
            if (CodeWF.Avalonia.Markdown.Rendering.MarkdownTableLayout.GetCollapseBorders(tableGrid)
                || table.BorderThickness != new Thickness(instant ? 0 : 1)
                || table.CornerRadius != new CornerRadius(instant ? 8 : 10)
                || !table.ClipToBounds
                || Math.Abs(table.Bounds.Height - expectedHeight) > .1
                || Math.Abs(table.Bounds.Width - tableGrid.Bounds.Width - (instant ? 0 : 2)) > .1)
                throw new InvalidOperationException($"{layout} table keeps separate cell borders and its prototype frame: size={table.Bounds.Size}, border={table.BorderThickness}, radius={table.CornerRadius}");
            Console.WriteLine($"PASS {layout} separate table borders, outer frame and height {expectedHeight:0.##}");
            var quote = host.GetVisualDescendants().OfType<Border>().Single(c => c.Classes.Contains("MdQuote"));
            var followingHeading = host.GetVisualDescendants().OfType<Border>().Last(c => c.Classes.Contains("MdH2Border"));
            var quoteBottom = quote.TranslatePoint(new Point(0, quote.Bounds.Height), window)!.Value.Y;
            var headingTop = followingHeading.TranslatePoint(default, window)!.Value.Y;
            if (Math.Abs(headingTop - quoteBottom - 40.32) > .1)
                throw new InvalidOperationException($"{layout} quote-to-heading gap follows collapsed prototype margins: gap={headingTop - quoteBottom:0.##}");
            Console.WriteLine($"PASS {layout} quote-to-heading gap 40.32");
            if (layout == "source-render")
            {
                var source = window.Workspace.FindControl<MarkdownEditorView>("EditorGuideTarget")!
                    .FindControl<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>("MarkdownEditor")!;
                var textView = source.Editor.TextArea.TextView;
                var origin = textView.TranslatePoint(default, window)!.Value;
                var sourceOrigin = source.TranslatePoint(default, window)!.Value;
                var inset = origin.X - sourceOrigin.X;
                var sourceLineHeight = textView.DefaultLineHeight;
                var visualLineHeight = textView.VisualLines.FirstOrDefault()?.Height ?? 0;
                Console.WriteLine($"SourceTextView x={origin.X:0.##} y={origin.Y:0.##} inset={inset:0.##} margin={textView.Margin} font={source.Editor.FontFamily} size={source.Editor.FontSize} defaultLineHeight={sourceLineHeight:0.##} visualLineHeight={visualLineHeight:0.##}");
                if (Math.Abs(inset - 54) >= .1)
                    throw new InvalidOperationException($"Source text starts 54px into the pane to match the prototype: inset={inset:0.##}");
                if (Math.Abs(visualLineHeight - 26.25) >= .1)
                    throw new InvalidOperationException($"Visible source text and line numbers use 26.25px rows to match the prototype: row={visualLineHeight:0.##}");
                foreach (var margin in source.Editor.TextArea.LeftMargins)
                    Console.WriteLine($"SourceMargin {margin.GetType().Name} bounds={margin.Bounds} margin={margin.Margin}");
            }
            foreach (var cls in new[] { "MdH1Border", "MdH2Border", "MdH3Border", "MdParagraph", "MdList", "MdListItem", "MdTaskMarkerBox", "MdQuote", "MdTableBlock", "MdTableContainer", "MdTableHeaderCell" })
            {
                var controls = host.GetVisualDescendants().OfType<Control>().Where(c => c.Classes.Contains(cls)).ToArray();
                for (var i = 0; i < controls.Length; i++)
                {
                    var c = controls[i]; var point = c.TranslatePoint(default, window)!.Value;
                    Console.WriteLine($"{cls}[{i}] x={point.X:0.##} y={point.Y:0.##} w={c.Bounds.Width:0.##} h={c.Bounds.Height:0.##} margin={c.Margin} padding={(c as Border)?.Padding}");
                    if (c is SelectableTextBlock textBlock)
                        Console.WriteLine($"TEXT {cls}[{i}] text=<{string.Concat(textBlock.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? [])}> weight={textBlock.FontWeight} font={textBlock.FontFamily} size={textBlock.FontSize}");
                }
            }
        }
    }
}
