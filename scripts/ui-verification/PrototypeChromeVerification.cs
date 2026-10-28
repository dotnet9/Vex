using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Messaging;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class PrototypeChromeVerification
{
    // 断言失败时只说「正文变了」不够用：把第一处不同的偏移量查出来，才能一眼看出是按键漏进了编辑器还是别的路径。
    private static string FirstDifference(string before, string after)
    {
        var index = 0;
        while (index < before.Length && index < after.Length && before[index] == after[index]) index++;
        return index >= before.Length && index >= after.Length ? "equal"
            : $"{index} ({before[Math.Min(index, before.Length - 1)]}->{after[Math.Min(index, after.Length - 1)]})";
    }
    private static string Preview(string text) => "\"" + text.Replace("\n", "\\n").Replace("\r", "\\r")[..Math.Min(48, text.Length)] + "\"";
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var documentState = app.Resolve<IWorkspaceDocumentState>();
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        Check(!vm.Layout.IsOutlineVisible, "Auxiliary panel starts closed for a new workspace");
        app.Resolve<IWorkspaceDocumentState>().UpdateDocument("# 在文字之间，理清思路\n\n一篇文章，从一个想法开始。这里保留完整的原文。\n\n## 写作，让想法有迹可循\n\n先记下观察，再整理线索。\n\n> 好的工具，让注意力回到内容本身。\n\n| API | 说明 |\n| --- | --- |\n| Read | 读取文档 |", null);
        // 种文档是直接喂 IWorkspaceDocumentState 的，而 vm.Markdown 只跟编辑器事件；不补这一拍，
        // 基准里留着开场的 577 字演示文档，后面任何「正文没变」的断言比的都是那份陈旧镜像。
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new MarkdownTextChangedCommand(
            documentState.Markdown, 1, 1, documentState.Markdown.Count(c => c == '\n') + 1));
        Dispatcher.UIThread.RunJobs();
        var original = vm.Markdown; var history = vm.Workbench.History.Capture();
        var layoutButton = window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")!.FindControl<Button>("LayoutButton")!;
        var formats = window.FindControl<ShellEditorOptionsView>("EditorOptions")!;
        foreach (var size in new[] { new Size(800,480), new Size(1024,768), new Size(1440,1000) })
        {
            window.Width = size.Width; window.Height = size.Height; Render();
            foreach (var layout in WorkbenchLayoutOption.All)
            {
                vm.Workbench.SelectLayout(layout.Id); Render();
                Check(vm.Workbench.LayoutName == layout.Name && vm.Markdown == original, "Current layout and document stay synchronized: " + size.Width + "/" + layout.Id);
            }
            vm.Workbench.SelectLayout("instant"); Render();
            // 这一圈在 800/1024 进 three 时会按原型自动收起文件导航（workspace.js 的让位规则）。
            // 后面的浮层几何全都按展开态量，不抬回来就会被这次收起一起带偏，所以此处显式复原并等动画走完。
            vm.Layout.IsSidebarVisible = true; LayoutSettle.Run(); Render();
            var statusChrome = window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")!;
            var viewSwitch = statusChrome.FindControl<Border>("ViewSwitch")!;
            var sourceSegment = statusChrome.FindControl<Button>("SourceSegment")!;
            var readSegment = statusChrome.FindControl<Button>("ReadSegment")!;
            var mapSegment = statusChrome.FindControl<Button>("MapSegment")!;
            // 原型 .document-statusbar：整条状态栏退役为悬浮胶囊，水平居中于正文列；
            // 底距不再写死，圆心按令牌钉在正文列底部轴线上（列内 20，即窗底上方 30，与折叠钮、缩放坞同轴）。
            var documentColumn = (Control)statusChrome.Parent!;
            var pillOrigin = statusChrome.TranslatePoint(default, documentColumn)!.Value;
            Check(Math.Abs(documentColumn.Bounds.Height - pillOrigin.Y - statusChrome.Bounds.Height / 2 - 20) < 1
                && Math.Abs(pillOrigin.X + statusChrome.Bounds.Width / 2 - documentColumn.Bounds.Width / 2) < 2
                && statusChrome.Bounds.Height is > 30 and <= 44
                && viewSwitch.Bounds.Width < statusChrome.Bounds.Width,
                $"Status bar floats as a centered pill on the bottom axis at {size.Width}: origin={pillOrigin} size={statusChrome.Bounds.Size} capsule={viewSwitch.Bounds.Size}");
            Check(!sourceSegment.Classes.Contains("active") && !readSegment.Classes.Contains("active") && !mapSegment.Classes.Contains("active"),
                "Combined layouts keep every status segment inactive");
            sourceSegment.Command!.Execute(null); Render();
            Check(vm.Workbench.WorkspaceLayout == "source" && sourceSegment.Classes.Contains("active") && !readSegment.Classes.Contains("active") && !mapSegment.Classes.Contains("active"),
                "Source segment selects the pure source view and highlights itself");
            mapSegment.Command!.Execute(null); Render();
            Check(vm.Workbench.WorkspaceLayout == "map" && mapSegment.Classes.Contains("active"), "Map segment selects the mind map view");
            readSegment.Command!.Execute(null); Render();
            Check(vm.Workbench.WorkspaceLayout == "read" && readSegment.Classes.Contains("active"), "Read segment selects the reading view");
            vm.Workbench.SelectLayout("instant"); Render();
            var formatting = formats.FindControl<Ursa.Controls.ToolBar>("FormattingToolbar")!;
            var origin = formatting.TranslatePoint(default, formats);
            Check(origin is not null && origin.Value.X >= 0 && formatting.Bounds.Width <= formats.Bounds.Width,
                $"Formatting toolbar stays inside the document at {size.Width}: origin={origin?.ToString() ?? "null"} toolbar={formatting.Bounds} host={formats.Bounds}");
            // 原型 .pane-collapse：折叠钮从状态栏迁到块角——展开贴在所属块内侧下角（内缩 8px），折叠贴画布窗边；
            // 底距不写死：列内 8 + 列底到窗底 10 = 圆心钉在窗底上方 30 的公共轴线上。
            var columns = window.FindControl<Grid>("WorkspaceColumns")!;
            void CheckCollapse(string name, string blockName, bool open, bool left)
            {
                var button = window.FindControl<Button>(name)!;
                var block = window.FindControl<Border>(blockName)!;
                var spot = button.TranslatePoint(default, columns)!.Value;
                var expectedX = open
                    ? block.TranslatePoint(new Point(left ? block.Bounds.Width - 8 - 24 : 8, 0), columns)!.Value.X
                    : left ? 0 : columns.Bounds.Width - 24;
                Check(button.Bounds.Width == 24 && button.Bounds.Height == 24
                    && Math.Abs(spot.X - expectedX) < 1
                    && Math.Abs(columns.Bounds.Height - spot.Y - 24 - 8) < 1,
                    $"{name} docks at the {(open ? "block corner" : "window edge")} at {size.Width}: x={spot.X} expected={expectedX} y={spot.Y} bounds={button.Bounds.Size} height={columns.Bounds.Height}");
            }
            CheckCollapse("SidebarCollapseButton", "SidebarGuideTarget", vm.Layout.ShowSidebar, true);
            CheckCollapse("OutlineCollapseButton", "OutlineGuideTarget", vm.Layout.ShowOutlineSidebar, false);
            // 列宽逐帧插值：动画没走完就量几何，读到的是起点。
            void Settle() { LayoutSettle.Run(); Render(); }
            vm.Layout.IsOutlineVisible = true; Settle();
            CheckCollapse("OutlineCollapseButton", "OutlineGuideTarget", vm.Layout.ShowOutlineSidebar, false);
            vm.Layout.IsOutlineVisible = false; Settle();
            CheckCollapse("OutlineCollapseButton", "OutlineGuideTarget", vm.Layout.ShowOutlineSidebar, false);
            window.CaptureRenderedFrame()?.Save(Path.Combine(output, "workspace-" + size.Width + ".png"));
            vm.Workbench.ShowLayouts = true; Render();
            var gallery = window.FindControl<WorkspaceLayoutGallery>("LayoutGallery")!;
            var choices = gallery.GetVisualDescendants().OfType<Button>().Where(b => b.Tag is string id && WorkbenchLayoutOption.All.Any(o => o.Id == id)).ToArray();
            Check(choices.Length == 8 && gallery.Bounds.Height <= window.ClientSize.Height - 120, "All eight layouts fit the available panel at " + size.Width);
            choices[0].Focus(); window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
            Check(vm.Workbench.WorkspaceLayout == "instant", "Browsing layouts does not apply a choice");
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Render();
            Check(!vm.Workbench.ShowLayouts && layoutButton.IsFocused, "Escape closes layout panel and returns focus");
        }
        // 原型 d2be540：文件列表快捷钮移到搜索框下方（右对齐、无分隔线），查找条独立成正文列的首个块。
        window.Width = 1440; window.Height = 1000; Render();
        var filesView = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        var filterBox = filesView.GetVisualDescendants().OfType<Border>().First(b => Math.Abs(b.Bounds.Height - 32) < 1);
        var filesToolbar = filesView.FindControl<Border>("FilesToolbar")!;
        var toolButtons = filesToolbar.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("files-tool")).ToArray();
        var toolbarTop = filesToolbar.TranslatePoint(default, filesView)!.Value.Y;
        var filterBottom = filterBox.TranslatePoint(new Point(0, filterBox.Bounds.Height), filesView)!.Value.Y;
        var toolbarRight = filesToolbar.TranslatePoint(new Point(filesToolbar.Bounds.Width - 12, 0), filesView)!.Value.X;
        Check(toolButtons.Length == 3 && toolButtons.All(b => b.Bounds.Height == 26)
            && Math.Abs(toolbarTop - filterBottom - 6) < 1 && filesToolbar.BorderThickness.Bottom == 0
            && Math.Abs(toolButtons.Max(b => b.TranslatePoint(new Point(b.Bounds.Width, 0), filesView)!.Value.X) - toolbarRight) < 1,
            $"File tools dock right under the filter as a borderless right-aligned row: top={toolbarTop:0.#}, filter bottom={filterBottom:0.#}, right={toolbarRight:0.#}");
        var fileTree = filesView.GetVisualDescendants().OfType<TreeView>().First();
        Check(Math.Abs(fileTree.TranslatePoint(default, filesView)!.Value.Y - (toolbarTop + filesToolbar.Bounds.Height)) < 1,
            "The list starts where the tool row ends instead of above it");
        vm.FindBar.ShowFindPanel(); Render();
        var findStrip = window.GetVisualDescendants().OfType<ShellFindBarView>().Single().FindControl<Border>("FindStrip")!;
        var viewColumn = window.FindControl<Grid>("DocumentColumn")!;
        var stripOrigin = findStrip.TranslatePoint(default, viewColumn)!.Value;
        var viewBlock = viewColumn.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("workspace-block") && !ReferenceEquals(b, findStrip));
        Check(findStrip.Classes.Contains("workspace-block") && findStrip.BorderThickness == new Thickness(1)
            && findStrip.CornerRadius.TopLeft == 10 && Math.Abs(findStrip.Bounds.Width - viewColumn.Bounds.Width) < 1
            && Math.Abs(stripOrigin.X) < 1 && Math.Abs(stripOrigin.Y) < 1,
            $"Find bar is the document column's own rounded block: origin={stripOrigin} size={findStrip.Bounds.Size} radius={findStrip.CornerRadius.TopLeft}");
        // 原型 .find-strip（workspace-chrome.css:578）整条 46px：内距 7×12 + 输入框 30 + 上下 1px 描边，
        // 动作钮 .btn.sm 26、关闭钮 .icon-button 30×30。三个开关是原型条上没有的实现侧补充，
        // 度量借原型自己的 .opt-toggle（theme.css:542：28 高、min-width 30）。
        var findInputs = findStrip.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsVisible).ToArray();
        var findActions = findStrip.GetVisualDescendants().OfType<Control>()
            .Where(c => (c is Button || c is ToggleButton) && c.Classes.Contains("find-action") && c.IsVisible).ToArray();
        var findToggles = findActions.OfType<ToggleButton>().ToArray();
        var findClose = findStrip.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("find-close"));
        Check(Math.Abs(findStrip.Bounds.Height - 46) < 1 && findInputs.All(t => t.Bounds.Height == 30) && findInputs.Length > 0
            && findActions.Except(findToggles).All(b => b.Bounds.Height == 26)
            && findToggles.Length == 3 && findToggles.All(t => t.Bounds.Height == 28 && t.Bounds.Width >= 30)
            && findClose.Bounds.Width == 30 && findClose.Bounds.Height == 30,
            $"Find strip keeps the prototype's 46px row: strip={findStrip.Bounds.Height:0.#}, inputs={string.Join(",", findInputs.Select(t => t.Bounds.Height.ToString("0.#")))}, actions={string.Join(",", findActions.Select(b => b.Bounds.Height.ToString("0.#")))}, close={findClose.Bounds.Size}");
        Check(Math.Abs(viewBlock.TranslatePoint(default, viewColumn)!.Value.Y - (stripOrigin.Y + findStrip.Bounds.Height + 4)) < 1,
            $"The view block sits 4px below the find block: strip bottom={stripOrigin.Y + findStrip.Bounds.Height:0.#}, block top={viewBlock.TranslatePoint(default, viewColumn)!.Value.Y:0.#}");
        // 新键只改了 Language.cs 而没落进 I18n/*.json（或产物里留着旧副本）时，按钮会直接把资源键印在脸上。
        var stripText = findStrip.GetVisualDescendants().OfType<Control>().Where(c => c.IsVisible)
            .Select(c => (c as ContentControl)?.Content?.ToString() ?? (c as TextBlock)?.Text ?? string.Empty)
            .Where(text => text.Length > 0).ToArray();
        Check(!stripText.Any(text => text.Contains("Vex.VexL.", StringComparison.Ordinal)),
            "Every visible find-strip caption is localized: " + string.Join("/", stripText));
        // 原型 .find-strip 的动作顺序是「上一个 → 下一个 → 全部替换」，关闭钮收在最右；实现侧补的三个开关收在关闭钮之前。
        var prevButton = findStrip.FindControl<Button>("FindPrevButton")!;
        var nextButton = findStrip.FindControl<Button>("FindNextButton")!;
        Check(prevButton.IsVisible && nextButton.IsVisible && prevButton.Bounds.Right <= nextButton.Bounds.Left
            && findActions.All(a => a.Bounds.Right <= findClose.Bounds.Right),
            $"Find strip keeps the prototype's order with three option toggles and the close button last: prev={prevButton.Bounds.X:0.#}, next={nextButton.Bounds.X:0.#}, toggles={findToggles.Length}");
        // 原型 .find-strip 在 ≤850px 时 flex-wrap，同时 .input{min-width:0} 让输入框先收缩。
        // 实现侧的展开高度由 ContentExpansionAnimator 的固定 ExpandedSize 决定，换不了行，
        // 所以窄窗只能靠收缩把整条留在块内——查找态和替换态都不许有控件被挤出圆角块外。
        var wideSize = (window.Width, window.Height);
        window.Width = 800; window.Height = 480; Render();
        void CheckNarrowStripFits(string mode)
        {
            var right = findStrip.GetVisualDescendants().OfType<Control>()
                .Where(control => control.IsVisible && (control is Button || control is ToggleButton || control is TextBox))
                .Select(control => control.TranslatePoint(new Point(control.Bounds.Width, 0), findStrip)!.Value.X)
                .DefaultIfEmpty(0).Max();
            Check(right <= findStrip.Bounds.Width - 11 && Math.Abs(findStrip.Bounds.Height - 46) < 1,
                $"Find strip keeps every control inside the block at 800px ({mode}): right={right:0.#}, strip={findStrip.Bounds.Width:0.#}x{findStrip.Bounds.Height:0.#}");
        }
        CheckNarrowStripFits("find");
        vm.FindBar.ShowReplacePanel(); Render();
        CheckNarrowStripFits("replace");
        vm.FindBar.ShowFindPanel(); Render();
        window.Width = wideSize.Width; window.Height = wideSize.Height; Render();
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "find-strip.png"));
        vm.FindBar.CloseFindPanel(); Render();
        Check(Math.Abs(viewBlock.TranslatePoint(default, viewColumn)!.Value.Y - stripOrigin.Y) < 1,
            "Closing the find bar returns the view block to the column top");
        // 原型 workbench.js 的 find-input：Enter 下一个、Shift+Enter 上一个。既断言按键发出的动作，
        // 也断言真实编辑器的徽章随步进移动——向后一步必须落回向前一步刚离开的那处命中。
        var historyBeforeFind = vm.Workbench.History.Capture();
        // 取正文基准要用 documentState，不能用 vm.Markdown：验证是直接喂 IWorkspaceDocumentState 种文档的，
        // vm.Markdown 只是编辑器的滞后镜像，第一次真实编辑器事件（查找步进就会触发）才把它追平。
        // 漏进正文的按键仍会被抓到——编辑器把新文本发回 VM，VM 随即 PublishWorkspaceDocumentState。
        var contentBeforeFind = documentState.Markdown;
        EditorSearchCommand? sent = null;
        CodeWF.Toolkit.EventBus.EventBus.Default.Subscribe<EditorSearchCommand>(command => sent = command);
        vm.FindBar.ShowFindPanel(); Render();
        vm.FindBar.SearchText = "想法";
        Thread.Sleep(260); Render(); // 先让 180ms 计数防抖落定，之后徽章只由方向步进改变。
        var findInput = findStrip.GetVisualDescendants().OfType<TextBox>().First(t => t.Name == "FindTextBox");
        findInput.Focus();
        sent = null;
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Render();
        var firstBadge = vm.FindBar.SearchResultText;
        Check(sent?.Action == EditorSearchAction.FindNext, "Enter publishes a forward find");
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null); Render();
        Check(sent?.Action == EditorSearchAction.FindNext && vm.FindBar.SearchResultText != firstBadge,
            $"Two forward steps land on two different matches: {firstBadge} then {vm.FindBar.SearchResultText}");
        sent = null;
        window.KeyPress(Key.Enter, RawInputModifiers.Shift, PhysicalKey.Enter, null); Render();
        Check(sent?.Action == EditorSearchAction.FindPrev, "Shift+Enter publishes a backward find");
        Check(vm.FindBar.SearchResultText == firstBadge,
            $"Stepping back returns to the match the forward step left: expected {firstBadge}, got {vm.FindBar.SearchResultText}");
        // Enter 必须留在查找框里：焦点一旦跑进正文，下一次回车就是在文档里插换行。
        Check(findInput.IsFocused, "Enter keeps the caret in the find box instead of the editor");
        sent = null;
        var prevPoint = prevButton.TranslatePoint(new Point(prevButton.Bounds.Width / 2, prevButton.Bounds.Height / 2), window)!.Value;
        window.MouseDown(prevPoint, MouseButton.Left); window.MouseUp(prevPoint, MouseButton.Left); Render();
        Check(sent?.Action == EditorSearchAction.FindPrev && vm.FindBar.SearchResultText != firstBadge,
            $"The previous-match button drives the same backward find: {vm.FindBar.SearchResultText}");
        vm.FindBar.IsRegex = true; vm.FindBar.IsWholeWord = true; Render();
        Check(findToggles.Count(t => t.IsChecked == true) == 2, "The three option toggles bind both ways to the search flags");
        // 定位只移动选区：正文、脏标记与撤销栈都不许动，Enter 也必须留在查找框里（焦点跑进正文会插入换行）。
        var historyAfterFind = vm.Workbench.History.Capture();
        Check(documentState.Markdown == contentBeforeFind
            && historyAfterFind.Undo.SequenceEqual(historyBeforeFind.Undo)
            && historyAfterFind.Current == historyBeforeFind.Current
            && historyAfterFind.Redo.SequenceEqual(historyBeforeFind.Redo),
            $"Stepping through matches leaves the document and undo history untouched: markdown {contentBeforeFind.Length}->{documentState.Markdown.Length} at {FirstDifference(contentBeforeFind, documentState.Markdown)} "
            + $"[{Preview(contentBeforeFind)} vs {Preview(documentState.Markdown)}], "
            + $"undo {historyBeforeFind.Undo.Length}->{historyAfterFind.Undo.Length}, redo {historyBeforeFind.Redo.Length}->{historyAfterFind.Redo.Length}, "
            + $"current {historyBeforeFind.Current.Length}->{historyAfterFind.Current.Length}");
        vm.FindBar.IsRegex = false; vm.FindBar.IsWholeWord = false; vm.FindBar.SearchText = string.Empty;
        vm.FindBar.CloseFindPanel(); Render();
        // 工具条钉住：默认钉住为占位行；取消钉住回到悬浮静默，并随偏好写入配置。
        var pins = vm.Workbench.Preferences;
        var editorOptions = window.FindControl<ShellEditorOptionsView>("EditorOptions")!;
        var editOptionsBorder = editorOptions.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("edit-options"));
        Check(editOptionsBorder.Classes.Contains("pinned") && Grid.GetRow(editorOptions) == 0
            && editorOptions.GetVisualAncestors().OfType<Border>().Any(b => b.Classes.Contains("workspace-block")),
            "Pinned editor toolbar docks as the editing block's own first row");
        pins.PinEditorToolbar = false; Render();
        Check(!editOptionsBorder.Classes.Contains("pinned") && Grid.GetRow(editorOptions) == 1,
            "Unpinned editor toolbar floats over the content without taking a row");
        pins.PinEditorToolbar = true; Render();
        vm.Workbench.SelectLayout("source-render"); Render();
        var readingHeader = window.WorkspaceViews.ReadingHeader;
        var pinToggle = window.WorkspaceViews.ReadingPin!;
        var mapBar = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!.FindControl<Border>("MapEditingToolbar")!;
        Check(pinToggle.IsChecked == true && pinToggle.GetVisualDescendants().OfType<Viewbox>().First(v => v.Classes.Contains("pin-on")).IsVisible,
            "Pin button shows the pinned icon by default");
        pins.PinReadingToolbar = false; pins.PinMapToolbar = false; Render();
        Check(Grid.GetRow(readingHeader) == 1 && readingHeader.Classes.Contains("floating"), "Unpinned reading header floats above content");
        Check(Grid.GetRow(mapBar) == 1 && mapBar.Classes.Contains("floating"), "Unpinned map toolbar floats above content");
        Check(pinToggle.GetVisualDescendants().OfType<Viewbox>().First(v => v.Classes.Contains("pin-off")).IsVisible,
            "Unpinned pin button swaps to the tilted icon");
        Check(app.Resolve<IAppSettingsStore>().Current.PinReadingToolbar == false && app.Resolve<IAppSettingsStore>().Current.PinMapToolbar == false,
            "Pin states persist to settings");
        pins.PinReadingToolbar = true; pins.PinMapToolbar = true; Render();
        Check(Grid.GetRow(readingHeader) == 0 && !readingHeader.Classes.Contains("floating") && Grid.GetRow(mapBar) == 0 && !mapBar.Classes.Contains("floating"),
            "Re-pinning docks both toolbars back into the layout");
        // 演示缩放坞：一条拖动条两用（正文 1.0–2.5），还原钮回到进入演示时的字号（原型 deck footer 的 fit 语义）。
        vm.Workbench.StartPresentation(); Render();
        var deckDock = window.FindControl<Vex.Controls.VexZoomDock>("PresentationDeckDock")!;
        Check(Math.Abs(deckDock.Minimum - 1) < .001 && Math.Abs(deckDock.Maximum - 2.5) < .001 && Math.Abs(deckDock.Step - .1) < .001,
            "Deck dock keeps the reading range 1.0 to 2.5 in tenth steps");
        var initialZoom = vm.Workbench.PresentationZoom;
        deckDock.Value = 2.5; Render();
        Check(Math.Abs(vm.Workbench.PresentationZoom - 2.5) < 0.001 && vm.Workbench.PresentationZoomText == "250%", "Deck dock raises presentation zoom to 250 percent");
        deckDock.Value = 1; Render();
        var deckReset = deckDock.GetVisualDescendants().OfType<Button>().First(b => b.Name == "PART_Reset");
        var deckResetPoint = deckReset.TranslatePoint(new Point(deckReset.Bounds.Width / 2, deckReset.Bounds.Height / 2), window)!.Value;
        window.MouseDown(deckResetPoint, MouseButton.Left); window.MouseUp(deckResetPoint, MouseButton.Left); Render();
        Check(vm.Workbench.PresentationZoom == initialZoom, "Deck dock reset returns to the presentation's initial zoom");
        vm.Workbench.ShowPresentationMap(); Render();
        Check(Math.Abs(deckDock.Minimum - .15) < .001 && Math.Abs(deckDock.Maximum - 3) < .001 && Math.Abs(deckDock.Step - .05) < .001,
            "Deck dock swaps to the map range 0.15 to 3 in twentieth steps");
        vm.Workbench.StopPresentation(); Render();
        Check(app.Resolve<IAppSettingsStore>().Current.PresentationScale == initialZoom, "Presentation zoom persists the restored initial scale");
        vm.Workbench.SelectLayout("instant"); Render();
        // 自定义配色：注册→进目录并选中→持久化；删除后回退默认配色。
        var customKey = vm.Appearance.RegisterCustomPalette("", "陶土暖纸", "验证用配色",
            "#FFFFFF", "#F5F1EA", "#26201A", "#B4642D", "#12100E", "#1C1917", "#E8E1D8", "#D8973C");
        Check(customKey.StartsWith("user:") && vm.Appearance.PaletteOptions.Count == 10 && vm.Appearance.SelectedPalette!.Key == customKey,
            "Custom palette registers, joins the catalog and becomes selected");
        Check((app.Resolve<IAppSettingsStore>().Current.CustomPalettes ?? "").Contains(customKey), "Custom palette persists to settings");
        Check(vm.Appearance.RemoveCustomPalette(customKey) && vm.Appearance.PaletteOptions.Count == 9 && vm.Appearance.SelectedPalette!.Key == "indigo",
            "Removing the custom palette falls back to the default palette");
        // 导图布局与样式：六种布局切换不改变节点数；样式与布局随设置持久化。
        var mapHost = window.FindControl<Vex.Modules.Workspace.Views.MarkdownMindMapHost>("MindMapGuideTarget")!;
        var map = mapHost.FindControl<CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView>("Map")!;
        vm.Workbench.SelectLayout("map"); Render();
        var beforeCount = map.RealizedNodeCount;
        foreach (var layout in ShellWorkbenchViewModel.MindMapLayoutKeys)
        {
            vm.Workbench.MindMapLayout = layout; Render();
            var bounds = map.RealizedNodeCount;
            Check(bounds >= beforeCount - 1, "Layout switch keeps nodes realized: " + layout);
        }
        Check(vm.Workbench.MindMapLayout == "fishbone", "Last layout stays selected");
        foreach (var style in ShellWorkbenchViewModel.MindMapStyleKeys)
        {
            vm.Workbench.MindMapStyle = style; Render();
        }
        Check(app.Resolve<IAppSettingsStore>().Current.MindMapLayout == "fishbone" && app.Resolve<IAppSettingsStore>().Current.MindMapStyle == "mono",
            "Map layout and style persist to settings");
        vm.Workbench.MindMapLayout = "right"; vm.Workbench.MindMapStyle = "quiet"; Render();
        // 表格工具：阅读视图的渲染表格可提取并还原 Markdown / CSV。
        vm.Workbench.SelectLayout("read"); Render();
        var preview = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
        var previewScroll = preview.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (previewScroll is not null) { previewScroll.Offset = new Vector(0, previewScroll.Extent.Height); Render(); previewScroll.Offset = new Vector(0, previewScroll.Extent.Height); Render(); }
        var tableContainer = window.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("MdTableContainer"));
        Check(tableContainer is not null, "Reading view renders table containers");
        var extracted = Vex.Modules.Workspace.Views.TableTools.Extract(tableContainer!);
        Check(extracted.Length >= 2 && extracted[0][0] == "API", "Table tools extract rendered cells");
        Check(Vex.Modules.Workspace.Views.TableTools.ToMarkdown(extracted).StartsWith("| API | 说明 |"), "Table tools rebuild markdown");
        Check(Vex.Modules.Workspace.Views.TableTools.ToCsv(extracted).Contains("Read,读取文档"), "Table tools build csv");
        previewScroll!.Offset = default; Render();
        // 原型 makeHandle()：视图分栏把手即 4px 间隙，支持拖拽与方向键，按「预设 : 轴向」持久化。
        var surface = window.FindControl<Grid>("ViewSurface")!;
        var columnSplit = surface.GetVisualChildren().OfType<GridSplitter>().Single(split => split.ResizeDirection == GridResizeDirection.Columns);
        var rowSplit = surface.GetVisualChildren().OfType<GridSplitter>().Single(split => split.ResizeDirection == GridResizeDirection.Rows);
        Check(Avalonia.Automation.AutomationProperties.GetName(columnSplit) == "调整左右视图宽度"
            && Avalonia.Automation.AutomationProperties.GetName(rowSplit) == "调整上下视图高度",
            "View split handles carry the prototype resize labels");
        Border Block(string pane) => window.FindControl<Control>(pane)!
            .GetVisualAncestors().OfType<Border>().First(block => block.Classes.Contains("workspace-block"));
        vm.Workbench.SelectLayout("source-render"); Render();
        columnSplit.Focus();
        window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); Render();
        Check(Math.Abs(vm.Layout.SplitRatio("source-render:h") - .55) < .001, "Right arrow widens the left view by one 5% step");
        window.KeyPress(Key.Home, RawInputModifiers.None, PhysicalKey.Home, null); Render();
        window.KeyPress(Key.End, RawInputModifiers.None, PhysicalKey.End, null); Render();
        Check(Math.Abs(vm.Layout.SplitRatio("source-render:h") - ShellWindowLayoutViewModel.SplitRatioMax) < .001, "End clamps the split at the 80% ceiling");
        var keyboardSplit = Block("EditorGuideTarget").Bounds.Width
            / (Block("EditorGuideTarget").Bounds.Width + Block("PreviewGuideTarget").Bounds.Width);
        Check(Math.Abs(keyboardSplit - .8) < .02, $"Keyboard resize reaches the block geometry: ratio={keyboardSplit:0.###}");
        Check((app.Resolve<IAppSettingsStore>().Current.ViewSplitRatios ?? "").Contains("source-render:h=0.8"),
            "Keyboard resize persists the ratio per layout preset");
        var splitSettings = (MemorySettings)app.Resolve<IAppSettingsStore>();
        var splitWrites = splitSettings.UpdateCount;
        var handlePoint = columnSplit.TranslatePoint(new Point(columnSplit.Bounds.Width / 2, columnSplit.Bounds.Height / 2), window)!.Value;
        var dropPoint = surface.TranslatePoint(new Point(surface.Bounds.Width * .35, surface.Bounds.Height / 2), window)!.Value;
        window.MouseDown(handlePoint, MouseButton.Left);
        for (var step = 1; step <= 5; step++)
        {
            window.MouseMove(handlePoint + (dropPoint - handlePoint) * step / 5, RawInputModifiers.LeftMouseButton); Render();
        }
        Check(splitSettings.UpdateCount == splitWrites, "Dragging the view split writes no settings while the pointer moves");
        window.MouseUp(dropPoint, MouseButton.Left); Render();
        Check(Math.Abs(vm.Layout.SplitRatio("source-render:h") - .35) < .03
            && splitSettings.UpdateCount == splitWrites + 1,
            $"Dropping the handle persists one final ratio: {vm.Layout.SplitRatio("source-render:h"):0.###}");
        var dragSplit = Block("EditorGuideTarget").Bounds.Width
            / (Block("EditorGuideTarget").Bounds.Width + Block("PreviewGuideTarget").Bounds.Width);
        Check(Math.Abs(dragSplit - vm.Layout.SplitRatio("source-render:h")) < .02,
            $"Block widths follow the dragged ratio: blocks={dragSplit:0.###} stored={vm.Layout.SplitRatio("source-render:h"):0.###}");
        vm.Workbench.SelectLayout("read"); Render();
        vm.Workbench.SelectLayout("source-render"); Render();
        Check(Math.Abs(vm.Layout.SplitRatio("source-render:h") - .35) < .03
            && Math.Abs(Block("EditorGuideTarget").Bounds.Width / (Block("EditorGuideTarget").Bounds.Width + Block("PreviewGuideTarget").Bounds.Width) - .35) < .03,
            "Leaving and returning to the preset restores its own split ratio");
        vm.Workbench.SelectLayout("three"); Render();
        Check(rowSplit.IsVisible && columnSplit.IsVisible, "Three-pane layout shows both split handles");
        rowSplit.Focus();
        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null); Render();
        var mapBlock = Block("MindMapGuideTarget").Bounds.Height;
        var topBlock = Block("EditorGuideTarget").Bounds.Height;
        Check(Math.Abs(vm.Layout.SplitRatio("three:v") - .55) < .001 && Math.Abs(topBlock / (topBlock + mapBlock) - .55) < .02
            && Math.Abs(vm.Layout.SplitRatio("source-render:h") - .35) < .03,
            $"Vertical split is a separate ratio from the horizontal one: v={vm.Layout.SplitRatio("three:v")} h={vm.Layout.SplitRatio("source-render:h")} blocks={topBlock:0}x{mapBlock:0}");
        vm.Workbench.SelectLayout("instant"); Render();
        Check(!columnSplit.IsVisible && !rowSplit.IsVisible, "A single view hides both split handles");
        // 自定义排版：注册→目录出现并可选中→持久化；删除回退简约。
        var typeKey = vm.Appearance.RegisterCustomTypography("", "暖纸手记", "#B4642D", "#D8973C", "#8A5A2B", "#C89A6B", "serif", 2.1, "tint");
        Check(typeKey.StartsWith("user:") && vm.Appearance.TypographyOptions.Count == 13, "Custom typography joins the catalog");
        vm.Appearance.SelectedTypography = vm.Appearance.TypographyOptions.First(option => option.Key == typeKey); Render();
        Check((app.Resolve<IAppSettingsStore>().Current.CustomTypographies ?? "").Contains(typeKey), "Custom typography persists to settings");
        // 原型 #typography-dialog 的第三件选择器：装饰档位要落进 H2 的渲染资源，并且能按档名原样读回（编辑浮层靠它回填）。
        var customType = CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Find(typeKey)!;
        Check(vm.Appearance.HeadingDecoration(typeKey) == "tint"
                && customType.Headings[1].Tint > 0 && customType.Headings[1].Radius == 4 && customType.Headings[1].Border == "0,0,0,0",
            $"H2 decoration reaches the heading style and reads back as the tier name: tint={customType.Headings[1].Tint}, radius={customType.Headings[1].Radius}");
        Check((app.Resolve<IAppSettingsStore>().Current.CustomTypographies ?? "").Contains("|tint"), "H2 decoration tier is persisted with the record");
        var typedAgain = vm.Appearance.RegisterCustomTypography(typeKey, "暖纸手记", "#B4642D", "#D8973C", "#8A5A2B", "#C89A6B", "sans", 1.9, "underline");
        Check(typedAgain == typeKey && vm.Appearance.TypographyOptions.Count == 13
                && CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Find(typeKey)!.Headings[1].Border == "0,0,0,2",
            "Re-registering the same key edits that typography instead of adding a second one");
        Check(vm.Appearance.RemoveCustomTypography(typeKey) && vm.Appearance.TypographyOptions.Count == 12 && vm.Appearance.SelectedTypography!.Key == "Simple",
            "Removing the custom typography falls back to Simple");
        // 导入：HTML 走共享转换器；docx 解包 OOXML 读取标题与段落。
        var htmlMarkdown = Vex.Modules.Workspace.Services.MarkdownImportService.ConvertHtml("<h1>导入标题</h1><p>导入段落文本</p>");
        Check(htmlMarkdown.Contains("# 导入标题") && htmlMarkdown.Contains("导入段落文本"), "HTML import converts headings and paragraphs");
        var docxBuffer = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(docxBuffer, System.IO.Compression.ZipArchiveMode.Create, true))
        using (var entry = zip.CreateEntry("word/document.xml").Open())
        {
            var xml = "<?xml version=\"1.0\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">"
                + "<w:p><w:pPr><w:pStyle w:val=\"Heading1\"/></w:pPr><w:r><w:t>导入标题</w:t></w:r></w:p>"
                + "<w:p><w:r><w:t>正文段落</w:t></w:r></w:p></w:document>";
            var bytes = System.Text.Encoding.UTF8.GetBytes(xml);
            entry.Write(bytes, 0, bytes.Length);
        }
        var docxMarkdown = Vex.Modules.Workspace.Services.MarkdownImportService.ConvertDocx(docxBuffer.ToArray());
        Check(docxMarkdown.Contains("# 导入标题") && docxMarkdown.Contains("正文段落"), "Word import reads headings and text");
        foreach (var culture in vm.Appearance.LanguageOptions)
        {
            vm.Appearance.SelectedLanguage = culture; Render();
            foreach (var name in new[] { "FileMenuItem", "InsertMenuItem", "HelpMenuItem" })
            {
                var item = window.FindControl<ShellTitleMenuView>("TitleMenuView")!.FindControl<MenuItem>(name)!;
                item.IsSubMenuOpen = true; Render();
                var popup = item.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
                Check(popup?.Child is { Bounds.Width: 248 }, "Title menu has a 248px panel: " + culture.CultureName + "/" + name);
                item.IsSubMenuOpen = false; Render();
            }
        }
        Check(vm.Markdown == original && vm.Workbench.History.Capture().Undo.SequenceEqual(history.Undo), "Layout and language changes preserve text and undo history");
        // 演示退出：正文可能在退出那一刻仍不可见（回到即时视图），全文必须当场回落，
        // 否则残留的演示章节会在下一次显露时直接渲染出来——阅读块只剩首个标题。
        var previewViewModel = (Vex.Modules.Workspace.ViewModels.MarkdownPreviewViewModel)preview.DataContext!;
        int RenderedBlocks(string className) => preview.GetVisualDescendants().OfType<Border>().Count(b => b.Classes.Contains(className));
        vm.Workbench.SelectLayout("instant"); Render();
        Check(previewViewModel.Markdown == documentState.Markdown && RenderedBlocks("MdTableContainer") == 1,
            "Reading pane keeps the full document while hidden in the instant view");
        vm.Workbench.StartPresentation(); Render();
        Check(previewViewModel.Markdown.Length < documentState.Markdown.Length && vm.Workbench.IsPresenting,
            "Presentation swaps the pane to a single section");
        vm.Workbench.NextPresentation(); Render();
        vm.Workbench.StopPresentation(); Render();
        Check(!vm.Workbench.IsPresenting && !vm.Workbench.IsPreviewVisible, "Exiting the presentation hides the section pane again");
        Check(previewViewModel.Markdown == documentState.Markdown,
            "Exiting the presentation restores the whole document even while the pane stays hidden");
        vm.Workbench.SelectLayout("read"); Render();
        Check(RenderedBlocks("MdTableContainer") == 1 && RenderedBlocks("MdHeadingBorder") >= 2,
            "Reading view after the presentation renders the tail blocks, not the last section");
        NarrowWindowThreeViewCollapse(app, window, vm, Render, Check);
    }

    /// <summary>
    /// 原型 workspace.js 的 setLayout：进三视图时窗口窄于 1250 就替用户收起文件导航并发一条提示。
    /// 这里同时钉住两件容易写坏的事——自动收起不许进偏好，宽窗不许动用户的导航。
    /// </summary>
    private static void NarrowWindowThreeViewCollapse(VerificationVexApp app, MainWindow window, MainWindowViewModel vm,
        Action Render, Action<bool, string> Check)
    {
        var localizer = app.Resolve<Vex.Core.Services.IAppLocalizer>();
        var settings = app.Resolve<Vex.Core.Services.IAppSettingsStore>();
        var toastText = localizer.Get(Vex.VexL.ToastFileNavCollapsed);
        var sidebarBefore = vm.Layout.IsSidebarVisible;
        var sizeBefore = (window.Width, window.Height);
        var persistedBefore = settings.Current.IsSidebarVisible;

        vm.Layout.IsSidebarVisible = true; Render();
        window.Width = 1200; window.Height = 800; Render();
        vm.Workbench.SelectLayout("three"); Render(); LayoutSettle.Run(); Render();
        var toastHost = window.GetVisualDescendants().OfType<ContentControl>()
            .FirstOrDefault(c => (c.Content as string)?.Contains(toastText) == true
                || c.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.Contains(toastText) == true));
        Check(!vm.Layout.IsSidebarVisible, "Entering the three-view layout below 1250px collapses the file navigation (prototype workspace.js)");
        Check(toastHost is not null, "The collapse tells the user how to get the navigation back");
        Check(settings.Current.IsSidebarVisible == true,
            "The automatic collapse is a layout concession, not a preference: the saved sidebar choice stays untouched");
        window.Width = 1440; Render();
        // 原型只会在窄窗让位时收起，从不替用户展开：先把上一次收起的导航手动打开，再看宽窗会不会动它。
        vm.Layout.IsSidebarVisible = true; LayoutSettle.Run(); Render();
        vm.Workbench.SelectLayout("instant"); vm.Workbench.SelectLayout("three"); Render();
        Check(vm.Layout.IsSidebarVisible, "A window wide enough for the three views keeps the file navigation open");
        window.Width = 1200; Render();
        vm.Workbench.SelectLayout("instant"); vm.Workbench.SelectLayout("source"); Render();
        Check(vm.Layout.IsSidebarVisible, "Layouts other than the three-view one never collapse the navigation on a narrow window");

        vm.Layout.IsSidebarVisible = sidebarBefore;
        settings.Update(s => s with { IsSidebarVisible = persistedBefore });
        (window.Width, window.Height) = sizeBefore; Render();
    }
}
