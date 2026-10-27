using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Workspace.Views;

/// <summary>
/// 阅读缩放到最大档时正文会不会被截断：虚拟化面板把每个块的高度缓成一份测量值，
/// 字号变了却没人让缓存作废，块就按旧高度排位，正文最后一行被裁掉、图表挤进占位高度。
/// 这里按「实测高度 vs 排布高度」逐块比对，并把内容总高度是否随缩放长上去一起钉住。
/// 用法：dotnet <harness>.dll zoom-truncate artifacts/zoom
/// </summary>
internal static class ZoomTruncationProbe
{
    private const string Document = """
        # 缩放截断验证

        这是一段足够长的正文，用来在缩放放大后迫使文本换到更多行，从而暴露块高缓存失效的问题。中文字符在窄栏里换行位置对字号很敏感，因此把句子写得长一些、连着写好几句，让同一段落在 100% 与 200% 下的行数差得足够远，任何按旧高度排布的实现都会在这里露出来。再加一句、再加一句、再加一句，直到这段文字在两档缩放下明显不是一样高。

        ## 二级标题

        - 列表项一：列表也走同一套块缓存，缩放后同样要重新测量。
        - 列表项二：如果只有段落被重测，列表会在最大档被裁掉最后一行。

        > 引用块里的文字同样按段落字号缩放，引用条与文字底线的相对位置不该因为缩放而改变。

        ```mermaid
        flowchart LR
          A[记录想法] --> B[梳理结构]
          B --> C{再次阅读}
          C -->|调整| B
          C -->|完成| D[导出分享]
        ```

        | 列一 | 列二 | 列三 |
        | --- | --- | --- |
        | 甲 | 乙 | 丙 |
        | 缩放后表格单元不应溢出边框 | 单元格文字仍然会被裁 | 三 |

        ```csharp
        var message = "代码块按 CodeBlockFontSize 缩放，行高与滚动条留白要一起跟上，否则最后一行会被裁掉。";
        Console.WriteLine(message);
        ```

        收尾段落：最后一段最容易被裁，因为它排在所有缓存高度之后，任何一档少算一点都会把它整体上移或截断。这里再补一句让它足够长，直到在两档缩放下都占满好几行位置为止。
        """;

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var previewView = window.FindControl<MarkdownPreviewView>("PreviewGuideTarget")!;
        var viewer = previewView.FindControl<Control>("PreviewMarkdownViewer")!;
        var preferences = ((MarkdownPreviewViewModel)previewView.DataContext!).Preferences;
        void Render()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            LayoutSettle.Run();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        void Shot(string name)
        {
            Directory.CreateDirectory(output);
            var file = Path.Combine(output, name + ".png");
            window.CaptureRenderedFrame()?.Save(file);
            Console.WriteLine("SHOT " + file);
        }
        // 只认文档面板里的块：弹层、大纲、状态栏里的文本也继承 TextBlock，混进来会误报。
        static Control[] Host(Control viewer) => viewer.GetVisualDescendants()
            .OfType<Control>()
            .First(control => control.GetType().Name == "WindowedStackPanel")
            .GetVisualChildren().OfType<Control>().ToArray();
        static double Height(string label, Control[] blocks)
        {
            var host = blocks[0].GetVisualParent() as Control ?? throw new InvalidOperationException(label + " 找不到块宿主");
            return host.Bounds.Height;
        }
        void Dump(string label, Control[] blocks)
        {
            foreach (var block in blocks.Where(block => block.Bounds.Height > 1))
            {
                var font = block is TextBlock text ? $" fs={text.FontSize:F1} lh={text.LineHeight:F1}" : string.Empty;
                Console.WriteLine($"BLOCK {label} {block.GetType().Name}[{string.Join(',', block.Classes)}]" +
                    $" bounds={block.Bounds.Width:F1}x{block.Bounds.Height:F1} desired={block.DesiredSize.Width:F1}x{block.DesiredSize.Height:F1}" +
                    $" margin={block.Margin}{font}");
            }
        }
        // DesiredSize 含外边距、Bounds 不含，直接相减每块都会差出一个 margin（100% 下 13/16/46 就是它），
        // 于是同口径比较：槽位 = 实排高 + 上下边距。
        static double Slot(Control block) => block.Bounds.Height + block.Margin.Top + block.Margin.Bottom;
        static double NeededAt(Control block, double width)
        {
            block.Measure(new Size(width, double.PositiveInfinity));
            return block.DesiredSize.Height;
        }
        // 面板自己量出来的高度（不改布局，截图前后都能读）：量到的高度大于排出的槽位，说明排位用了
        // 别的（缓存的）高度，正文最后一行就被裁。
        static Control[] Stale(Control[] blocks) => blocks.Where(block =>
            block.Bounds.Height > 1 && block.DesiredSize.Height - Slot(block) > 0.5).ToArray();
        // 地面真值：按实排宽重问一次需要多高。注意这次 Measure 会改写块内的文字排版，
        // 所以必须在截完静息图之后再调，否则图里的裁字可能是探针自己造出来的。
        static Control[] Truncated(Control[] blocks) => blocks.Where(block =>
        {
            if (block.Bounds.Height <= 1 || block.Bounds.Width <= 1) return false;
            return NeededAt(block, block.Bounds.Width) - Slot(block) > 0.5;
        }).ToArray();
        static string Why(Control block) =>
            $"{block.GetType().Name}[{string.Join(',', block.Classes)}] 实排宽={block.Bounds.Width:F0}" +
            $" 槽位={Slot(block):F1}(margin {block.Margin.Top:F1}+{block.Margin.Bottom:F1})" +
            $" 面板量得={block.DesiredSize.Height:F1} 重测需高={NeededAt(block, block.Bounds.Width):F1}";
        // 截图里每块的下沿都被下一块盖掉，说明面板推进 y 用的不是这块实际排出的高度。
        // 逐对算「下一块顶 - 本块底」，负数即重叠（也就是肉眼看到的截断）。
        static (int Count, double Worst) Overlap(Control[] blocks)
        {
            var ordered = blocks.Where(block => block.Bounds.Height > 1)
                .OrderBy(block => block.Bounds.Y).ToArray();
            var worst = double.MaxValue;
            var hits = 0;
            for (var i = 1; i < ordered.Length; i++)
            {
                var gap = ordered[i].Bounds.Y - ordered[i - 1].Bounds.Bottom;
                if (gap < -0.5)
                {
                    hits++;
                    worst = Math.Min(worst, gap);
                    Console.WriteLine($"OVERLAP {ordered[i - 1].GetType().Name}[{string.Join(',', ordered[i - 1].Classes)}]" +
                        $" 底={ordered[i - 1].Bounds.Bottom:F1} 被 {ordered[i].GetType().Name}[{string.Join(',', ordered[i].Classes)}]" +
                        $" 顶={ordered[i].Bounds.Y:F1} 压住 {-gap:F1}px");
                }
            }
            return (hits, worst == double.MaxValue ? 0 : worst);
        }

        window.Width = 1280; window.Height = 820;
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectPaletteByKey("indigo");
        vm.Workbench.SelectLayout("read");
        Render();
        preferences.ContentZoom = 100;
        vm.Markdown = Document;
        Render();
        Thread.Sleep(600);
        Render();

        var scroller = viewer.GetSelfAndVisualAncestors().OfType<ScrollViewer>().First();
        void Extent(string label)
        {
            var host = (Control)Host(viewer)[0].GetVisualParent()!;
            Console.WriteLine($"SCROLL {label} extent={scroller.Extent.Height:F0} viewport={scroller.Viewport.Height:F0}" +
                $" 横向 extent={scroller.Extent.Width:F0} viewport={scroller.Viewport.Width:F0}" +
                $" 面板 bounds={host.Bounds.Width:F0}x{host.Bounds.Height:F0} desired={host.DesiredSize}" +
                $" viewer={viewer.Bounds.Width:F0}x{viewer.Bounds.Height:F0}" +
                $" 窗口={window.Bounds.Width:F0}x{window.Bounds.Height:F0} 缩放={window.RenderScaling:F2}");
        }
        // 面板自己的账本：每项缓存的槽位高、是否已量测、以及实排顶边。三行对不上就是截断的位置。
        void Ledger(string label)
        {
            var panel = (CodeWF.Avalonia.Markdown.Controls.WindowedStackPanel)(Control)Host(viewer)[0].GetVisualParent()!;
            var width = panel.Bounds.Width;
            Console.WriteLine($"LEDGER {label} 项数={panel.ItemCount} 已物化={panel.RealizedItemCount}" +
                $" Spacing={panel.Spacing:F1} 估算高={panel.EstimatedItemHeight:F1} 虚拟化={panel.IsVirtualizing} 面板宽={width:F1}");
            for (var i = 0; i < panel.ItemCount; i++)
            {
                var child = panel.GetRealizedItem(i);
                var need = child is null ? double.NaN : NeededAt(child, width);
                Console.WriteLine($"  [{i}] 缓存槽位={panel.GetItemHeight(i):F1} 已量测={panel.IsItemMeasured(i)}" +
                    $" 顶边={(child?.Bounds.Y ?? double.NaN):F1} 实排高={(child?.Bounds.Height ?? double.NaN):F1}" +
                    $" 重测需高={need:F1} 类型={child?.GetType().Name ?? "未物化"}[{string.Join(',', child?.Classes ?? [])}]");
            }

            // 登记项与 Children 必须一一对应，否则排位循环里的 childIndex 会串位。
            var registered = Enumerable.Range(0, panel.ItemCount).Select(panel.GetRealizedItem).ToHashSet();
            var index = 0;
            foreach (var child in ((Panel)panel).Children)
            {
                var tag = registered.Contains(child) ? "已登记" : "未登记";
                Console.WriteLine($"  子项[{index}] {tag} {child.GetType().Name}[{string.Join(',', child.Classes)}]" +
                    $" bounds={child.Bounds.Width:F0}x{child.Bounds.Height:F0} 顶={child.Bounds.Y:F0}" +
                    $" 首段文字={child.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text?.Substring(0, Math.Min(14, child.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault()?.Text?.Length ?? 0))}");
                index++;
            }
        }

        Check(preferences.ContentZoom == 100, "缩放起点为 100%");
        var baselineBlocks = Host(viewer);
        Check(baselineBlocks.Length > 3, $"阅读面板已排布出块（{baselineBlocks.Length}）");
        var baselineHeight = Height("100%", baselineBlocks);
        var baselineExtent = scroller.Extent.Height;
        var baselineFontSize = FirstFontSize(baselineBlocks);
        Check(baselineHeight > 0, $"100% 内容总高度（{baselineHeight:F0}）");
        Dump("100%", baselineBlocks);
        Extent("100%");
        Shot("zoom-100");
        var baselineRealised = baselineBlocks.Where(block => block.Bounds.Height > 1).ToArray();
        var baselineStale = Stale(baselineRealised);
        foreach (var block in baselineStale)
            Console.WriteLine($"STALE 100% {Why(block)}");
        var baselineOverlap = Overlap(baselineRealised);
        Ledger("100%");
        var baselineTruncated = Truncated(baselineRealised);
        foreach (var block in baselineTruncated)
            Console.WriteLine($"TRUNCATED 100% {Why(block)}");
        Check(baselineOverlap.Count == 0, $"100% 基准块与块之间不重叠（最大压叠 {baselineOverlap.Worst:F1}px）");
        Check(baselineStale.Length == 0, $"100% 基准没有块按旧高度排位（{baselineRealised.Length} 块实测）");
        Check(baselineTruncated.Length == 0, $"100% 基准没有块在实排宽度下被截断（{baselineRealised.Length} 块实测）");

        static double FirstFontSize(Control[] blocks)
        {
            foreach (var block in blocks)
            {
                if (block is TextBlock self) return self.FontSize;
                var inner = block.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
                if (inner is not null) return inner.FontSize;
            }
            return double.NaN;
        }

        var diagramBaseline = 0d;
        foreach (var zoom in new[] { 100, 150, 200, 50 })
        {
            preferences.ContentZoom = zoom;
            Render();
            Thread.Sleep(400);
            Render();
            var scale = zoom / 100.0;
            var blocks = Host(viewer);
            var realised = blocks.Where(block => block.Bounds.Height > 1).ToArray();
            Check(realised.Length > 3, $"{zoom}% 仍有块在视口内排布（{realised.Length}）");
            Dump($"{zoom}%", realised);
            Extent($"{zoom}%");
            var stale = Stale(realised);
            foreach (var block in stale)
                Console.WriteLine($"STALE {zoom}% {Why(block)}");
            var overlap = Overlap(realised);
            Shot($"zoom-{zoom}");
            var truncated = Truncated(realised);
            foreach (var block in truncated)
                Console.WriteLine($"TRUNCATED {zoom}% {Why(block)}");
            // 诊断：先让块宿主与窗口整体重测一遍。若截断随之消失，缺的是「字号变了没人触发布局失效」；
            // 若仍在，那是排位本身按旧缓存高度算，得改面板。
            if (stale.Length > 0 || truncated.Length > 0 || overlap.Count > 0)
            {
                var panel = (Control)realised[0].GetVisualParent()!;
                panel.InvalidateMeasure();
                window.InvalidateMeasure();
                Render();
                var after = Host(viewer).Where(block => block.Bounds.Height > 1).ToArray();
                var afterTruncated = Truncated(after);
                var afterOverlap = Overlap(after);
                Console.WriteLine($"DIAG {zoom}% 手工重测后仍截断 {afterTruncated.Length}/{truncated.Length} 块，" +
                    $"仍按旧高排位 {Stale(after).Length}/{stale.Length} 块，仍重叠 {afterOverlap.Count}/{overlap.Count} 处");
                foreach (var block in afterTruncated)
                    Console.WriteLine($"STILL {zoom}% {Why(block)}");
                Shot($"zoom-{zoom}-remeasured");
            }
            Check(overlap.Count == 0, $"{zoom}% 块与块之间不重叠（最大压叠 {overlap.Worst:F1}px）");
            Check(stale.Length == 0, $"{zoom}% 没有块按旧高度排位（{realised.Length} 块实测）");
            Check(truncated.Length == 0, $"{zoom}% 没有块在实排宽度下被截断（{realised.Length} 块实测）");

            // 原型 --vex-content-zoom 是整篇布局缩放：字号与行高停在排版档基准值，放大体现在物理像素上。
            var fontSize = FirstFontSize(realised);
            Check(Math.Abs(fontSize - baselineFontSize) < .01,
                $"{zoom}% 字号仍是排版档基准 {fontSize:F1}（缩放不进字号，行高/边距才不会与字脱钩）");
            var physicalWidth = viewer.Bounds.Width * scale;
            Check(physicalWidth <= scroller.Viewport.Width + 1,
                $"{zoom}% 正文物理宽 {physicalWidth:F0} 不超出窗格 {scroller.Viewport.Width:F0}（横向不溢出）");
            var column = viewer.Bounds.Width + viewer.Margin.Left + viewer.Margin.Right;
            var expected = Math.Min(Math.Max(820, window.ClientSize.Width * .5), scroller.Viewport.Width / scale);
            Check(Math.Abs(column - expected) < 2,
                $"{zoom}% 正文列宽按缩放回算：含留白 own={column:F1} 期望 {expected:F1}");
            var extentRatio = scroller.Extent.Height / baselineExtent;
            var ownHeight = Height($"{zoom}%", blocks);
            var ownRatio = ownHeight / baselineHeight;
            var physicalRatio = ownRatio * scale;
            Console.WriteLine($"EXTENT {zoom}% 滚动高度={scroller.Extent.Height:F0}（相对 100% 为 {extentRatio:F2}×，" +
                $"小倍率时被视口高度兜住）；面板 own {ownRatio:F2}×＝列宽变窄多出的行数，×缩放 = {physicalRatio:F2}×");
            Check(physicalRatio >= scale * .97 && physicalRatio <= scale * 1.35,
                $"{zoom}% 正文物理总高跟上缩放（{physicalRatio:F2}×，允许带 {scale * .97:F2}–{scale * 1.35:F2}）");
            Check(scroller.Extent.Height >= ownHeight * scale - 1,
                $"{zoom}% 滚动范围覆盖缩放后的正文（{scroller.Extent.Height:F0} ≥ {ownHeight * scale:F0}）");

            // 滚到底再验一次：代码块最后一行与收尾段落最容易露出「按旧槽位排位」的破绽。
            scroller.Offset = new Vector(0, Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height));
            Render();
            Shot($"zoom-{zoom}-bottom");
            var tail = Host(viewer).Where(block => block.Bounds.Height > 1).ToArray();
            var tailOverlap = Overlap(tail);
            Check(tailOverlap.Count == 0, $"{zoom}% 滚到底后块与块之间不重叠（最大压叠 {tailOverlap.Worst:F1}px）");
            var tailTruncated = Truncated(tail);
            foreach (var block in tailTruncated)
                Console.WriteLine($"TAIL-TRUNCATED {zoom}% {Why(block)}");
            Check(tailTruncated.Length == 0, $"{zoom}% 滚到底后没有块被截断（{tail.Length} 块实测）");

            // 图表：虚拟化下只有落进视口的块会物化，滚到中段再核对 SVG 块画全没有。
            scroller.Offset = new Vector(0, Math.Max(0, (scroller.Extent.Height - scroller.Viewport.Height) * .45));
            Render();
            Thread.Sleep(600);
            Render();
            Shot($"zoom-{zoom}-middle");
            // 图表块被延迟容器包着，面板直接子级看到的是 DeferredMarkdownBlock，得往下找 MdMermaid。
            var diagram = viewer.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(block => block.Classes.Contains("MdMermaid"));
            Console.WriteLine($"MIDDLE {zoom}% offset={scroller.Offset.Y:F0} extent={scroller.Extent.Height:F0} viewport={scroller.Viewport.Height:F0}" +
                $" 块={string.Join(" | ", Host(viewer).Select(block => block.GetType().Name + "[" + string.Join(',', block.Classes) + $"]{block.Bounds.Height:F0}"))}");
            Check(diagram is not null, $"{zoom}% 图表块进入视口后已物化");
            if (diagram is not null)
            {
                var picture = diagram.GetVisualDescendants().OfType<Image>().FirstOrDefault();
                // 原型 .diagram-preview：图嵌在 22 留白、最低 120 的卡里，右上角的 mermaid 徽标站在这条留白带。
                // 没有这张卡，缩放把图压扁后徽标就盖在节点上（200% 下块只有 80 高，徽标占掉一半）。
                var card = diagram.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(item => item.Classes.Contains("MdMermaidPreview"));
                var badge = diagram.GetVisualDescendants().OfType<Button>()
                    .FirstOrDefault(item => item.Content as string == "mermaid");
                Console.WriteLine($"DIAGRAM {zoom}% 块 own={diagram.Bounds.Width:F0}x{diagram.Bounds.Height:F0}" +
                    $" 卡片 own={(card?.Bounds.Width ?? 0):F0}x{(card?.Bounds.Height ?? 0):F0} 留白={(card?.Padding.Left ?? 0):F0}" +
                    $" 图片 own={(picture?.Bounds.Width ?? 0):F0}x{(picture?.Bounds.Height ?? 0):F0} 顶={(picture?.Bounds.Top ?? 0):F0}" +
                    $" 徽标 own={(badge?.Bounds.Width ?? 0):F0}x{(badge?.Bounds.Height ?? 0):F0} 底={(badge?.Bounds.Bottom ?? 0):F0}" +
                    $" 物理高={diagram.Bounds.Height * scale:F0}");
                Check(diagram.Bounds.Height > 1, $"{zoom}% 图表块量测出高度（own {diagram.Bounds.Height:F1}）");
                Check(picture is not null && picture.Bounds.Height > 1, $"{zoom}% 图表已绘制出内容");
                Check(picture is null || (picture.Bounds.Top >= -1
                    && picture.Bounds.Bottom <= diagram.Bounds.Height + 1),
                    $"{zoom}% 图表完整落在块内（没被占位高度裁掉）");
                Check(card is not null && card.Padding.Left >= 22 && card.Bounds.Height >= 120,
                    $"{zoom}% 图表卡留出徽标站位（own {card?.Bounds.Height:F0} ≥ 120，留白 {(card?.Padding.Left ?? 0):F0}）");
                // 徽标是悬浮件，原型靠卡片留白 + 120 最低高把它让在图形上方。图形按 Uniform 居中，
                // 所以「不压过画面纵向 35%」等价于「不压到居中的节点带」——修之前 200% 这里压到 52%。
                Check(badge is not null && picture is not null
                    && badge.Bounds.Bottom <= picture.Bounds.Top + picture.Bounds.Height * .35 + 1,
                    $"{zoom}% 徽标没压在图表节点带上（徽标底 {badge?.Bounds.Bottom:F0} ≤ 画面上沿 {picture?.Bounds.Top:F0}" +
                    $" + 35% 画面高 {picture?.Bounds.Height * .35:F0}）");
                if (scale > 1 && diagramBaseline > 0)
                    Check(diagram.Bounds.Height * scale >= diagramBaseline - 1,
                        $"{zoom}% 放大后图表物理高不缩水（{diagram.Bounds.Height * scale:F0} ≥ {diagramBaseline:F0}）");
                if (scale == 1) diagramBaseline = diagram.Bounds.Height;
            }
            scroller.Offset = new Vector(0, 0);
            Render();
        }
        preferences.ContentZoom = 100;
        Render();
        Check(Math.Abs(Height("回退", Host(viewer)) - baselineHeight) < 1, "缩放回 100% 后高度复原（缓存未污染）");

        // 暗色下图表卡的描边与徽标底色都走主题资源，留一张图证。
        vm.Appearance.SelectThemeByKey("dark");
        Render();
        scroller.Offset = new Vector(0, Math.Max(0, (scroller.Extent.Height - scroller.Viewport.Height) * .45));
        Render();
        Thread.Sleep(600);
        Render();
        Shot("zoom-100-dark-middle");
        vm.Appearance.SelectThemeByKey("light");
        Render();
    }
}
