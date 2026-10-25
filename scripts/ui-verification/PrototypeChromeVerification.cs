using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class PrototypeChromeVerification
{
    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        void Render() { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        Check(!vm.Layout.IsOutlineVisible, "Auxiliary panel starts closed for a new workspace");
        app.Resolve<IWorkspaceDocumentState>().UpdateDocument("# 在文字之间，理清思路\n\n一篇文章，从一个想法开始。这里保留完整的原文。\n\n## 写作，让想法有迹可循\n\n先记下观察，再整理线索。\n\n> 好的工具，让注意力回到内容本身。\n\n| API | 说明 |\n| --- | --- |\n| Read | 读取文档 |", null);
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
            var statusChrome = window.FindControl<ShellStatusBarView>("StatusBarGuideTarget")!;
            var viewSwitch = statusChrome.FindControl<Border>("ViewSwitch")!;
            var sourceSegment = statusChrome.FindControl<Button>("SourceSegment")!;
            var readSegment = statusChrome.FindControl<Button>("ReadSegment")!;
            var mapSegment = statusChrome.FindControl<Button>("MapSegment")!;
            // 原型 .document-statusbar：整条状态栏退役为悬浮胶囊，水平居中于正文列、底边留 16px，不再占行高。
            var documentColumn = (Control)statusChrome.Parent!;
            var pillOrigin = statusChrome.TranslatePoint(default, documentColumn)!.Value;
            Check(Math.Abs(documentColumn.Bounds.Height - pillOrigin.Y - statusChrome.Bounds.Height - 16) < 1
                && Math.Abs(pillOrigin.X + statusChrome.Bounds.Width / 2 - documentColumn.Bounds.Width / 2) < 2
                && statusChrome.Bounds.Height is > 30 and <= 44
                && viewSwitch.Bounds.Width < statusChrome.Bounds.Width,
                $"Status bar floats as a centered pill over the document column at {size.Width}: origin={pillOrigin} size={statusChrome.Bounds.Size} capsule={viewSwitch.Bounds.Size}");
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
            // 原型 .pane-collapse：折叠钮从状态栏迁到块角——展开贴在所属块内侧下角（内缩 8px），折叠贴画布窗边，底边恒 18px。
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
                    && Math.Abs(columns.Bounds.Height - spot.Y - 24 - 18) < 1,
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
        // 原型 .find-strip 整条 46px：输入框 30、.btn.sm 动作钮 26、关闭钮 30×30。
        var findInputs = findStrip.GetVisualDescendants().OfType<TextBox>().Where(t => t.IsVisible).ToArray();
        var findActions = findStrip.GetVisualDescendants().OfType<Control>()
            .Where(c => (c is Button || c is ToggleButton) && c.Classes.Contains("find-action") && c.IsVisible).ToArray();
        var findClose = findStrip.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("find-close"));
        Check(Math.Abs(findStrip.Bounds.Height - 46) < 1 && findInputs.All(t => t.Bounds.Height == 30) && findInputs.Length > 0
            && findActions.All(b => b.Bounds.Height == 26) && findActions.Length > 0
            && findClose.Bounds.Width == 30 && findClose.Bounds.Height == 30,
            $"Find strip keeps the prototype's 46px row: strip={findStrip.Bounds.Height:0.#}, inputs={string.Join(",", findInputs.Select(t => t.Bounds.Height.ToString("0.#")))}, actions={string.Join(",", findActions.Select(b => b.Bounds.Height.ToString("0.#")))}, close={findClose.Bounds.Size}");
        Check(Math.Abs(viewBlock.TranslatePoint(default, viewColumn)!.Value.Y - (stripOrigin.Y + findStrip.Bounds.Height + 4)) < 1,
            $"The view block sits 4px below the find block: strip bottom={stripOrigin.Y + findStrip.Bounds.Height:0.#}, block top={viewBlock.TranslatePoint(default, viewColumn)!.Value.Y:0.#}");
        window.CaptureRenderedFrame()?.Save(Path.Combine(output, "find-strip.png"));
        vm.FindBar.CloseFindPanel(); Render();
        Check(Math.Abs(viewBlock.TranslatePoint(default, viewColumn)!.Value.Y - stripOrigin.Y) < 1,
            "Closing the find bar returns the view block to the column top");
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
        // 演示字号拖动条：拖动实时改字号，百分比读数随动，1.0–2.5。
        vm.Workbench.StartPresentation(); Render();
        var zoomSlider = window.FindControl<Slider>("PresentationZoomSlider")!;
        zoomSlider.Value = 2.5; Render();
        Check(Math.Abs(vm.Workbench.PresentationZoom - 2.5) < 0.001 && vm.Workbench.PresentationZoomText == "250%", "Slider raises presentation zoom to 250 percent");
        zoomSlider.Value = 1; Render();
        Check(vm.Workbench.PresentationZoom == 1 && vm.Workbench.PresentationZoomText == "100%", "Slider restores presentation zoom to 100 percent");
        vm.Workbench.StopPresentation(); Render();
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
        var typeKey = vm.Appearance.RegisterCustomTypography("", "暖纸手记", "#B4642D", "#D8973C", "#8A5A2B", "#C89A6B", "serif", 2.1);
        Check(typeKey.StartsWith("user:") && vm.Appearance.TypographyOptions.Count == 13, "Custom typography joins the catalog");
        vm.Appearance.SelectedTypography = vm.Appearance.TypographyOptions.First(option => option.Key == typeKey); Render();
        Check((app.Resolve<IAppSettingsStore>().Current.CustomTypographies ?? "").Contains(typeKey), "Custom typography persists to settings");
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
    }
}
