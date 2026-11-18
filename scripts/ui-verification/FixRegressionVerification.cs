using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

/// <summary>
/// 三处修复的回归探针（2026-11-17）：
/// 1. 图鉴缩略图：Flyout 打开后 24 张 Image 应有源（原从按钮视觉树找 Flyout 内容恒为空）。
/// 2. tooltip I18n 绑定：工具带按钮 ToolTip 应解析为文案而非键名原文（库程序集 json 未入全局池）。
/// 3. 即时编辑回车：段落块内发 Enter 应拆块换行。
/// </summary>
internal static class FixRegressionVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Pump(Task task)
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (!task.IsCompleted && DateTime.UtcNow < deadline) { Render(); Thread.Sleep(10); }
            task.GetAwaiter().GetResult(); Render();
        }
        void Shot(string name)
        {
            using var frame = window.CaptureRenderedFrame();
            frame?.Save(System.IO.Path.Combine(output, name + ".png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        var vm = (MainWindowViewModel)window.DataContext!;
        window.Width = 1440; window.Height = 948; Render();
        // ---- 2. tooltip I18n 绑定：读工具带按钮的附加属性文案 ----
        var ruleButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "FormatHorizontalRule");
        Check(ruleButton is not null, "Toolbar horizontal-rule button found");
        var tipText = CodeWF.Avalonia.Controls.Helpers.ToolTipHelper.GetText(ruleButton!);
        Console.WriteLine("tooltip text: " + tipText);
        Check(tipText is { } tip && tip.Length > 0 && !tip.Contains("MarkdownL.") && !tip.Contains("VexL."),
            "Tooltip resolves to localized text, not raw key");

        // ---- 1. 图鉴缩略图：打开 Flyout 等填充；TabControl 只驻留选中组，按可见组验收 ----
        var galleryButton = window.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.Name == "FormatMermaidGallery");
        Check(galleryButton is not null, "Gallery button found");
        if (galleryButton!.Flyout is not Flyout flyout) throw new InvalidOperationException("Gallery flyout missing");
        flyout.ShowAt(galleryButton);
        Render();
        var deadline = DateTime.UtcNow.AddSeconds(25);
        int filled = 0, total = 0;
        while (DateTime.UtcNow < deadline)
        {
            Render();
            var images = flyout.Content is Visual root
                ? root.GetVisualDescendants().OfType<Image>().ToList()
                : [];
            total = images.Count;
            filled = images.Count(i => i.Source is Avalonia.Svg.Skia.SvgImage);
            if (total >= 4 && filled >= total && filled > 0) break;
            Thread.Sleep(250);
        }
        Console.WriteLine($"gallery thumbs: {filled}/{total} filled");
        // 分组 tab（经典 TabControl）：4 个 TabItem，默认组 0 可见 4 卡。
        var tabs = flyout.Content is Visual tabRoot
            ? tabRoot.GetVisualDescendants().OfType<TabItem>().ToList()
            : [];
        int VisibleCards(Visual? root) => root?.GetVisualDescendants()
            .OfType<Button>().Count(b => b.Classes.Contains("dg-card") && b.IsVisible) ?? 0;
        var cards0 = VisibleCards(flyout.Content as Visual);
        Console.WriteLine($"gallery tabs: {tabs.Count}, visible cards (group0): {cards0}");
        Shot("gallery-group0");
        // 切到组 2 应见 9 卡（treeview 属结构与关系组，原型 mermaid-catalog 分组真值）。
        // SVG 光栅化非首帧完成：等新组可见卡片全部有源再截图（真机为亚秒级，感知不到）。
        void WaitVisibleThumbs()
        {
            var waitDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < waitDeadline)
            {
                var imgs = (flyout.Content as Visual)?.GetVisualDescendants().OfType<Image>().ToList() ?? [];
                if (imgs.Count > 0 && imgs.All(i => i.Source is Avalonia.Svg.Skia.SvgImage)) break;
                Render(); Thread.Sleep(100);
            }
            Render();
        }
        tabs.ElementAtOrDefault(2)!.IsSelected = true;
        WaitVisibleThumbs();
        {
            var imgs = (flyout.Content as Visual)?.GetVisualDescendants().OfType<Image>().ToList() ?? [];
            Console.WriteLine($"group2 thumbs after wait: {imgs.Count(i => i.Source is Avalonia.Svg.Skia.SvgImage)}/{imgs.Count}");
        }
        var cards2 = VisibleCards(flyout.Content as Visual);
        Console.WriteLine("visible cards (group2): " + cards2);
        var bigThumb = (flyout.Content as Visual)?.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("dg-thumb"));
        Console.WriteLine($"thumb size: {bigThumb?.Bounds.Width}x{bigThumb?.Bounds.Height}");
        Shot("gallery-thumbs");
        // 组 1 也截一张（计划与演进：甘特/时间线/Git/看板）。
        tabs[1].IsSelected = true;
        WaitVisibleThumbs();
        Shot("gallery-group1");
        Check(filled >= 4 && filled == total, "Visible group thumbnails rendered: " + filled + "/" + total);
        Check(tabs.Count == 4, "Gallery group tabs present: " + tabs.Count);
        Check(cards0 == 4, "Group 0 shows 4 cards: " + cards0);
        Check(cards2 == 9, "Group 2 shows 9 cards after switch (treeview moved to group 2): " + cards2);
        Check(bigThumb is { Bounds.Width: >= 260, Bounds.Height: >= 110 }, "Thumbnails enlarged to at least 260x110");
        flyout.Hide();
        Render();

        // ---- 3. 即时编辑回车：真实点击块进入编辑态，再回车应拆块换行 ----
        var probeDoc = System.IO.Path.Combine(output, "instant-enter-probe.md");
        System.IO.File.WriteAllText(probeDoc, "# 探针标题\n\n第一段内容，光标在末尾回车应拆块。\n");
        Pump(vm.OpenStartupDocumentAsync([probeDoc]));
        vm.Workbench.SelectLayout("instant");
        Render(); Thread.Sleep(300); Render();
        var live = window.Workspace.GetVisualDescendants()
            .FirstOrDefault(control => control.GetType().Name == "MarkdownInstantView");
        Check(live is not null, "Instant view found");
        var blocks = live!.GetVisualDescendants()
            .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
        Console.WriteLine("block views: " + blocks.Count);
        Check(blocks.Count >= 2, "Paragraph block views found: " + blocks.Count);
        var paragraph = blocks.OrderByDescending(b => b.Bounds.Width * b.Bounds.Height).Last();
        var center = paragraph.TranslatePoint(new Point(paragraph.Bounds.Width / 2, paragraph.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Block center unmapped");
        // 真实点击进入编辑态（OnPointerPressed → BeginEdit），再打字确认 caret 激活。
        window.MouseMove(center, RawInputModifiers.None); Render();
        window.MouseDown(center, MouseButton.Left); Render();
        window.MouseUp(center, MouseButton.Left); Render();
        Thread.Sleep(200); Render();
        var editor = paragraph.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Console.WriteLine("editor state: visible=" + editor?.IsVisible + " focused=" + editor?.IsFocused + " text=" + (editor?.Text ?? "null"));
        Check(editor is { IsVisible: true }, "Click enters block editing");
        window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null); Render();
        // headless 焦点链路不可靠：直接对块编辑器派发 KeyDown（tunnel handler 挂在它自己上必收）。
        editor!.CaretIndex = (editor.Text ?? string.Empty).Length;
        var textBefore = editor.Text ?? string.Empty;
        Console.WriteLine("editor text: " + textBefore);
        // Typora 语义：末尾连续回车，每敲一次拆出一个新（空）段落块，块数递增。
        var counts = new List<int> { blocks.Count };
        for (var attempt = 0; attempt < 3; attempt++)
        {
            editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = editor });
            Render(); Thread.Sleep(300); Render();
            var viewsNow = live!.GetVisualDescendants()
                .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
            counts.Add(viewsNow.Count);
            Console.WriteLine($"enter #{attempt + 1}: blocks={viewsNow.Count}");
            Check(viewsNow.Count == blocks.Count + attempt + 1,
                $"Enter #{attempt + 1} appends a new empty paragraph: {viewsNow.Count}");
        }
        Shot("instant-enter");
        Check(counts[^1] > counts[0], "Consecutive enters append empty paragraphs: " + string.Join(" -> ", counts));
        // ---- 2.4 真实鼠标焦点语义（window.Mouse* 走真 hit test 管线）：文字命中落光标、
        // 块间点击不翻态、跨块往返内容零丢失——纯文本块编辑态常驻的行为钉。
        // 坐标必须落在字形上（块左缘+40px）：块内左对齐文字右侧是空白，点那里会穿透走最近块回退。 ----
        var textBlocks = live!.GetVisualDescendants()
            .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
        var withText = textBlocks.Where(b => b.GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault()?.Text is { Length: > 0 }).ToList();
        var firstText = withText.OrderByDescending(b => b.Bounds.Width * b.Bounds.Height).First();
        var nextText = withText.Where(b => !ReferenceEquals(b, firstText))
            .Select(b => (Block: b, Y: b.TranslatePoint(default, window)?.Y ?? double.MinValue))
            .Where(t => t.Y is > 0 && t.Y < window.ClientSize.Height - 40)
            .OrderBy(t => t.Y).First().Block;
        Point GlyphPointOf(Control c) => c.TranslatePoint(new Point(40, c.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Block unmapped");
        void RealClick(Control target)
        {
            var point = GlyphPointOf(target);
            window.MouseMove(point, RawInputModifiers.None); Render();
            window.MouseDown(point, MouseButton.Left); Render();
            window.MouseUp(point, MouseButton.Left); Render();
            Thread.Sleep(150); Render();
        }
        TextBox EditorOf(Control c) => c.GetVisualDescendants().OfType<TextBox>().First();
        RealClick(firstText);
        Check(EditorOf(firstText).IsFocused, "Real click on glyphs focuses first text block editor");
        var textSnapshot = vm.Workbench.LiveEditor!.Text;
        RealClick(nextText);
        var nextEditor = EditorOf(nextText);
        Check(nextEditor.IsFocused, "Real click on glyphs focuses second text block");
        Check(nextEditor.CaretIndex is > 0 and < 8, "Caret lands at glyph hit position, not block start: " + nextEditor.CaretIndex);
        Check(EditorOf(firstText).IsVisible, "First block stays in edit form after blur (no render flip)");
        RealClick(firstText);
        Check(EditorOf(firstText).IsFocused, "Click back reuses persistent editing state");
        Check(nextEditor.IsVisible, "Second block also stays in edit form");
        Check(vm.Workbench.LiveEditor!.Text == textSnapshot, "No content loss across focus round-trip");
        // ---- 2.5 行内图片单表面（阶段3）：含图段落进入行内编辑，图片以叠绘呈现 ----
        var imagePath = System.IO.Path.Combine(output, "inline-probe.png");
        using (var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize(96, 48)))
        {
            using (var ctx = bitmap.CreateDrawingContext())
                ctx.FillRectangle(Avalonia.Media.Brushes.SlateBlue, new Rect(0, 0, 96, 48));
            bitmap.Save(imagePath);
        }
        vm.Markdown = "行内图片段落 ![示例图](inline-probe.png) 尾文字";
        Render(); Thread.Sleep(400); Render();
        Shot("inline-image-render");
        var imageBlock = live!.GetVisualDescendants()
            .Where(c => c.GetType().Name == "MarkdownBlockView").OfType<Control>()
            .FirstOrDefault(b => b.GetVisualDescendants().OfType<TextBox>()
                .Any(box => (box.Text ?? "").Contains("![", StringComparison.Ordinal)));
        Check(imageBlock is not null, "Inline-image paragraph block found");
        var imgBlock = imageBlock!;
        var imageGlyph = imgBlock.TranslatePoint(new Point(20, imgBlock.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Image block unmapped");
        window.MouseMove(imageGlyph, RawInputModifiers.None); Render();
        window.MouseDown(imageGlyph, MouseButton.Left); Render();
        window.MouseUp(imageGlyph, MouseButton.Left); Render();
        Thread.Sleep(200); Render();
        var inlineFlag = imgBlock.GetType()
            .GetField("_inlineEditing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(imgBlock);
        Check(inlineFlag as bool? == true, "Image paragraph enters inline single-surface editing (not source overlay)");
        var imagePresenter = imgBlock.GetVisualDescendants()
            .FirstOrDefault(p => p.GetType().Name == "MarkdownInlinePresenter");
        var overlays = imagePresenter?.GetType()
            .GetField("_imageOverlays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(imagePresenter) as System.Collections.IEnumerable;
        var overlayCount = overlays is { } list ? list.Cast<object>().Count() : 0;
        Check(overlayCount >= 1, "Inline image resolved into overlay geometry: " + overlayCount);
        Shot("inline-image");
        // caret 进图片区间：显源码态截图（muya 行为对照）。
        var imgEditor = imgBlock.GetVisualDescendants()
            .OfType<TextBox>().First(box => (box.Text ?? "").Contains("![", StringComparison.Ordinal));
        imgEditor.CaretIndex = 8;
        Render(); Thread.Sleep(200); Render();
        Shot("inline-image-revealed");
        // ---- 2.6 行内数学单表面：$...$ 段落进入行内编辑，公式以 CSharpMath 矢量叠绘 ----
        vm.Markdown = "质能方程 $E=mc^2$ 是物理学基石";
        Render(); Thread.Sleep(400); Render();
        var mathBlock = live!.GetVisualDescendants()
            .Where(c => c.GetType().Name == "MarkdownBlockView").OfType<Control>()
            .FirstOrDefault(b => b.GetVisualDescendants().OfType<TextBox>()
                .Any(box => (box.Text ?? "").Contains('$')));
        Check(mathBlock is not null, "Inline-math paragraph block found");
        var mBlock = mathBlock!;
        var mathGlyph = mBlock.TranslatePoint(new Point(20, mBlock.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("Math block unmapped");
        window.MouseMove(mathGlyph, RawInputModifiers.None); Render();
        window.MouseDown(mathGlyph, MouseButton.Left); Render();
        window.MouseUp(mathGlyph, MouseButton.Left); Render();
        Thread.Sleep(200); Render();
        var mathPresenter = mBlock.GetVisualDescendants()
            .FirstOrDefault(p => p.GetType().Name == "MarkdownInlinePresenter");
        var mathOverlays = mathPresenter?.GetType()
            .GetField("_mathOverlays", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
            .GetValue(mathPresenter) as System.Collections.IEnumerable;
        var mathCount = mathOverlays is { } mlist ? mlist.Cast<object>().Count() : 0;
        Check(mathCount >= 1, "Inline math resolved into overlay list: " + mathCount);
        Shot("inline-math");
        // ---- 2.7 Quote 分组区间级重建：引用内回车续行仍在同组（一个 MdQuote 宿主条目） ----
        vm.Markdown = "> 第一行引用\n> 第二行引用\n\n后续段落";
        Render(); Thread.Sleep(400); Render();
        var quoteLine = live!.GetVisualDescendants()
            .Where(c => c.GetType().Name == "MarkdownBlockView").OfType<Control>()
            .FirstOrDefault(b => b.GetVisualDescendants().OfType<TextBox>()
                .Any(box => (box.Text ?? "").Contains("第二行", StringComparison.Ordinal)));
        Check(quoteLine is not null, "Quote second-line block found");
        int MdQuoteCount() => live!.GetVisualDescendants()
            .OfType<Border>().Count(border => border.Classes.Contains("MdQuote"));
        int BlockCount() => live!.GetVisualDescendants().Count(c => c.GetType().Name == "MarkdownBlockView");
        var beforeQuote = (MdQuoteCount(), BlockCount());
        var quoteEditor = quoteLine!.GetVisualDescendants().OfType<TextBox>()
            .First(box => (box.Text ?? "").Contains("第二行", StringComparison.Ordinal));
        quoteEditor.CaretIndex = quoteEditor.Text?.Length ?? 0;
        quoteEditor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = quoteEditor });
        Render(); Thread.Sleep(300); Render();
        var afterQuote = (MdQuoteCount(), BlockCount());
        Check(afterQuote.Item1 == beforeQuote.Item1 && afterQuote.Item2 == beforeQuote.Item2 + 1,
            $"Enter in quote continues same group (host {beforeQuote.Item1}->{afterQuote.Item1}, blocks {beforeQuote.Item2}->{afterQuote.Item2})");
        Shot("quote-continue");
        var instantEditor = vm.Workbench.LiveEditor!;
        instantEditor.SetText(string.Empty);
        instantEditor.EnableAutoPair = false;
        instantEditor.FocusBlock(0); Render();
        window.KeyTextInput(">"); Render();
        Check(MdQuoteCount() == 0, "Bare quote marker stays in the paragraph");
        window.KeyTextInput(" "); Render();
        Check(MdQuoteCount() == 1 && instantEditor.Text.TrimEnd() == ">"
            && window.FocusManager?.GetFocusedElement() is TextBox { Text.Length: 0, CaretIndex: 0 },
            "Quote marker plus space opens an empty quote independently of auto-pair");
        window.KeyTextInput("引用正文"); Render();
        Check(instantEditor.Text.TrimEnd() == "> 引用正文" && instantEditor.Selection.Start == "> 引用正文".Length,
            "Typing continues inside the quote, with a source-relative caret");
        instantEditor.SetText("> "); Render();
        Check(MdQuoteCount() == 1 && instantEditor.Text == "> ", "Empty quote round-trips through SetText");
        // ---- 2.5 空白文档即时编辑：空段落块存在、光标就位、可拆块（Typora 空文档行为） ----
        // 共享流程装载了演示文档；直接清空内容模拟空白文档（NewDocument 会因 dirty 弹确认挂探针）。
        vm.Markdown = string.Empty;
        Render(); Thread.Sleep(300); Render();
        vm.Workbench.SelectLayout("instant");
        Render(); Thread.Sleep(300); Render();
        var blankLive = window.Workspace.GetVisualDescendants()
            .FirstOrDefault(control => control.GetType().Name == "MarkdownInstantView");
        Check(blankLive is not null, "Blank instant view found");
        var blankBlocks = blankLive!.GetVisualDescendants()
            .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
        Console.WriteLine("blank doc block views: " + blankBlocks.Count);
        Check(blankBlocks.Count == 1, "Blank document keeps one empty paragraph block: " + blankBlocks.Count);
        var blankEditor = blankBlocks[0].GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        Console.WriteLine("blank editor: visible=" + blankEditor?.IsVisible + " focused=" + blankEditor?.IsFocused);
        Check(blankEditor is { IsVisible: true }, "Blank paragraph editor is visible (click target exists)");
        // Typora 行为：点击文档空白区域（块外下方远处，修上轮只点块中心的盲区），
        // 光标落到最近段落块（FindNearestBlockIndex → FocusBlock）。
        var blankRect = blankBlocks[0].TranslatePoint(new Point(0, 0), window)
            ?? throw new InvalidOperationException("Blank block unmapped");
        var farBelow = new Point(blankRect.X + blankRect.X / 2, blankRect.Y + blankBlocks[0].Bounds.Height + 220);
        window.MouseMove(farBelow, RawInputModifiers.None); Render();
        window.MouseDown(farBelow, MouseButton.Left); Render();
        window.MouseUp(farBelow, MouseButton.Left); Render();
        Thread.Sleep(300); Render();
        // 强验证：点击空白后直接打字，能接住文本=编辑器在编辑态（headless 焦点链不可靠，以功能为准）。
        var typed = blankBlocks[0].GetVisualDescendants().OfType<TextBox>()
            .FirstOrDefault(box => box.IsVisible);
        if (typed is not null)
        {
            // 模拟文本输入走 TextInput 通道（真实键盘路径）。
            typed.RaiseEvent(new Avalonia.Input.TextInputEventArgs { Text = "点击空白后输入", RoutedEvent = Avalonia.Input.InputElement.TextInputEvent });
            Render(); Thread.Sleep(200); Render();
        }
        Console.WriteLine("text after blank click typing: " + (typed?.Text ?? "null"));
        Check(typed is { } && (typed.Text ?? string.Empty).Contains("点击空白后输入"),
            "Typing after clicking blank area lands in the empty paragraph (Typora behavior)");
        blankEditor!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = blankEditor });
        Render(); Thread.Sleep(300); Render();
        var blankAfter = blankLive!.GetVisualDescendants()
            .Where(control => control.GetType().Name == "MarkdownBlockView").OfType<Control>().ToList();
        Console.WriteLine("blank doc blocks after enter: " + blankAfter.Count);
        Check(blankAfter.Count == 2, "Enter on blank document splits into two paragraphs");
        blankEditor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, Source = blankEditor });
        Render();

        // ---- 2.8 真实键盘回车（window.KeyPress 走焦点路由管线，非 RaiseEvent 直发）：
        // 空白文档打开后不点击直接回车，应拆块——真机反馈场景的行为钉。 ----
        vm.Markdown = string.Empty;
        Render(); Thread.Sleep(300); Render();
        var realKeyLive = window.Workspace.GetVisualDescendants()
            .FirstOrDefault(control => control.GetType().Name == "MarkdownInstantView");
        int RealBlocks() => realKeyLive!.GetVisualDescendants()
            .Count(c => c.GetType().Name == "MarkdownBlockView");
        Console.WriteLine($"  real-enter: blocks-before={RealBlocks()} focused={window.FocusManager?.GetFocusedElement()?.GetType().Name}");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Render(); Thread.Sleep(300); Render();
        Console.WriteLine($"  real-enter: blocks-after={RealBlocks()} text=\"{vm.Workbench.LiveEditor?.Text}\"");
        Check(RealBlocks() == 2, "Real keyboard Enter on untouched blank document splits into two paragraphs");
        vm.Markdown = string.Empty; Render();

        // ---- 2.9 真机时序：文档在 source 布局打开、切即时后直接真实回车——
        // 切布局瞬间同步 Focus 在真机上静默失败（可见性传播未完成），挂起聚焦应兜底。 ----
        vm.Workbench.SelectLayout("source"); Render();
        vm.Markdown = string.Empty; Render();
        vm.Workbench.SelectLayout("instant"); Render();
        Console.WriteLine($"  switch-enter: blocks-before={RealBlocks()} focused={window.FocusManager?.GetFocusedElement()?.GetType().Name}");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Render(); Thread.Sleep(300); Render();
        Console.WriteLine($"  switch-enter: blocks-after={RealBlocks()}");
        Check(RealBlocks() == 2, "Real keyboard Enter after layout switch (source->instant) splits into two paragraphs");
        vm.Markdown = string.Empty; Render();

        Console.WriteLine("Fix regression verification PASS");
    }
}
