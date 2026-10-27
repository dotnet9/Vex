using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vex.Controls;
using Vex.Core.Motion;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>复用应用中的实际设置模型，按桌面原型提供分类导航和即时设置。</summary>
public sealed class ShellPreferencesOverlayView : UserControl
{
    private readonly ShellPreferencesViewModel _model;
    private readonly IAppLocalizer _localizer;
    private readonly ListBox _navigation = new() { Classes = { "preferences-navigation" }, Width = 148, Padding = new Thickness(10, 18), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    // 原型两栏背景：左栏 --panel、右栏 --bg，浅深主题下有明暗差。浏览器侧是覆盖式滚动条，
    // 截图不占位；原生用 Hidden 保持同宽内容列（滚轮/触摸板仍可滚动）。
    private readonly ScrollViewer _scroll = new() { Padding = new Thickness(24, 26), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden };
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _card;
    private Window? _owner;
    // MCP 分类下「服务/客户端/审计」页签的选择：整页会随语言重建，所以选择记在视图上。
    private int _mcpTab;
    private readonly (string Id, string Key)[] _categories =
    [ ("general", VexL.PrefGeneral), ("editor", VexL.PrefEditor),
      ("images", VexL.PrefImages), ("appearance", VexL.PrefAppearance), ("export", VexL.Export), ("mcp", "MCP") ];

    public ShellPreferencesOverlayView(ShellPreferencesViewModel model, IAppLocalizer localizer, string category)
    {
        _model = model; _localizer = localizer; DataContext = model;
        BindResource(this, ForegroundProperty, "VexValueBrush");
        var grid = new Grid { RowDefinitions = new RowDefinitions("36,*") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Margin = new Thickness(16, 0, 0, 0);
        header.Children.Add(_title);
        var close = new Button { Classes = { "vex-dismiss" }, Width = 46, Height = 32 };
        Avalonia.Automation.AutomationProperties.SetName(close, L(VexL.Close));
        close.Click += (_, _) => model.Close(); Grid.SetColumn(close, 1); header.Children.Add(close);
        grid.Children.Add(Frame(header, new Thickness(0, 0, 0, 1)));
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var navigationFrame = Frame(_navigation, new Thickness(0, 0, 1, 0));
        BindResource(navigationFrame, Border.BackgroundProperty, "VexPanelBackgroundBrush");
        BindResource(_scroll, BackgroundProperty, "VexShellBackgroundBrush");
        body.Children.Add(navigationFrame); Grid.SetColumn(_scroll, 1); body.Children.Add(_scroll);
        Grid.SetRow(body, 1); grid.Children.Add(body);
        // 改值即生效：去掉「设置已保存 / 完成」页脚（对应原型偏好浮层）。
        // 宿主挂在 CodeWFWindow.OverlayContent 整窗浮层槽（含标题条），与原型
        // .vex-settings-host 同几何：卡片直接在整窗内居中，无需负边距补偿。
        _card = new Border { Classes = { "dialog-card" }, Width = 675, Height = 700, Padding = new Thickness(0), CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = grid };
        Content = _card;
        _navigation.SelectionChanged += (_, _) => ShowCategory();
        RefreshLabels();
        _navigation.SelectedIndex = Math.Max(0, Array.FindIndex(_categories, c => c.Id == category));
        AttachedToVisualTree += (_, _) =>
        {
            _localizer.CultureChanged += CultureChanged;
            _owner = TopLevel.GetTopLevel(this) as Window;
            if (_owner is not null) _owner.SizeChanged += OwnerSizeChanged;
            ResizeCard();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _localizer.CultureChanged -= CultureChanged;
            if (_owner is not null) _owner.SizeChanged -= OwnerSizeChanged;
            _owner = null;
        };
        SizeChanged += (_, _) => ResizeCard();
    }

    public string SelectedCategory => _categories[Math.Max(0, _navigation.SelectedIndex)].Id;
    public void SelectCategory(string category) => _navigation.SelectedIndex = Math.Max(0, Array.FindIndex(_categories, c => c.Id == category));
    private void OwnerSizeChanged(object? sender, SizeChangedEventArgs args) => ResizeCard();
    private void ResizeCard()
    {
        if (TopLevel.GetTopLevel(this) is not Window window) return;
        _card.MaxWidth = Math.Max(320, window.Bounds.Width - 48);
        _card.MaxHeight = Math.Max(260, window.Bounds.Height - 48);
        _navigation.Width = 148;
    }
    private void CultureChanged(object? sender, EventArgs args)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) { Avalonia.Threading.Dispatcher.UIThread.Post(() => CultureChanged(sender, args)); return; }
        RefreshLabels(); ShowCategory();
    }
    private string L(string key) => key.Contains('.') ? _localizer.Get(key) : key;
    private void RefreshLabels()
    {
        var selected = Math.Max(0, _navigation.SelectedIndex);
        _title.Text = L(VexL.PreferencesTitle);
        // 字形与 _categories 逐项对应：文件、编辑、图片、调色板、导出（向上箭头，同原型 i-export）、终端。
        string[] icons = ["M7 2H15L20 7V22H4V2Z M14 2V8H20", "M12 4H4V20H20V12 M18 2L22 6L12 16L8 17L9 13Z", "M3 5H21V19H3Z M8 11A1.5 1.5 0 1 0 8 11.01 M5 17L10 12L13 15L16 12L19 15", "M12 3a9 9 0 0 0 0 18h1.5a2 2 0 0 0 0-4H12a2 2 0 0 1 0-4h3.5a5.5 5.5 0 0 0 0-11H12Z M7.5 9h.1 M11 6.5h.1 M16 7.5h.1", "M21 15V19A2 2 0 0 1 19 21H5A2 2 0 0 1 3 19V15 M17 8L12 3L7 8 M12 3V15", "M5 7L10 12L5 17 M13 17H20"];
        _navigation.ItemsSource = _categories.Select((c, i) =>
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("17,*"), ColumnSpacing = 10 };
            var icon = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(icons[i]), Width = 17, Height = 17, Stretch = Stretch.Uniform, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center };
            icon.Bind(Avalonia.Controls.Shapes.Path.StrokeProperty, _navigation.GetResourceObservable("VexMutedBrush")); row.Children.Add(icon);
            var label = new TextBlock { Text = L(c.Key), FontSize = 14, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(label, 1); row.Children.Add(label);
            return new ListBoxItem { Content = row, MinHeight = 40, Padding = new Thickness(12, 9), Margin = new Thickness(0, 0, 0, 3), CornerRadius = new CornerRadius(5), HorizontalContentAlignment = HorizontalAlignment.Stretch };
        }).ToArray();
        _navigation.SelectedIndex = selected;
    }
    private void ShowCategory()
    {
        var page = new StackPanel { Spacing = 0 };
        _scroll.Content = page; _scroll.Offset = default;
        var p = _model.Preferences;
        switch (SelectedCategory)
        {
            case "general":
                // 界面语言入口收敛到标题栏语言菜单，偏好「通用与文件」不再重复放置。
                Choice(page, "Vex.VexL.PrefStartup", p, nameof(p.RestoreWorkspace), [(L("Vex.VexL.PrefEmptyWorkspace"), false), (L("Vex.VexL.PrefRecentWorkspace"), true)]);
                Toggle(page, VexL.PrefAutoSave, p, nameof(p.AutoSave));
                Choice(page, VexL.PrefSaveDelay, p, nameof(p.AutoSaveDelay), [("2 " + L("Vex.VexL.PrefSeconds"), 2), ("5 " + L("Vex.VexL.PrefSeconds"), 5), ("10 " + L("Vex.VexL.PrefSeconds"), 10)]);
                Toggle(page, VexL.PrefRecovery, p, nameof(p.RecoveryDrafts));
                Heading(page, "Vex.VexL.PrefNewDefaults");
                Choice(page, VexL.PrefEncoding, p, nameof(p.DefaultEncoding), [("UTF-8", "utf-8"), ("UTF-8 BOM", "utf-8-bom"), ("GB18030", "GB18030")]);
                Choice(page, VexL.PrefNewline, p, nameof(p.DefaultNewline), [(L(VexL.PrefSystemFont), "system"), ("LF", "LF"), ("CRLF", "CRLF")]);
                break;
            case "editor":
                // 对齐原型 editor 页：仅 编辑区字体、缩进 与「代码与源码」组（自动换行、源码行号）。
                Choice(page, VexL.PrefBodyFont, p, nameof(p.BodyFont), [(L(VexL.PrefSystemFont), "system"), (L(VexL.PrefSerifFont), "serif"), (L(VexL.PrefMonoFont), "mono")]);
                Choice(page, VexL.PrefIndent, p, nameof(p.IndentSize), [(LF("Vex.VexL.PrefSpaceUnit", 2), 2), (LF("Vex.VexL.PrefSpaceUnit", 4), 4), (LF("Vex.VexL.PrefSpaceUnit", 8), 8)]);
                Heading(page, "Vex.VexL.PrefCodeAndSource");
                Toggle(page, VexL.PrefWrap, p, nameof(p.CodeWrap));
                Toggle(page, "Vex.VexL.PrefSourceLineNumbers", _model.Display, nameof(_model.Display.ShowLineNumbers));
                break;
            case "images":
                Toggle(page, VexL.PrefImageCopy, p, nameof(p.ImageCopy)); Toggle(page, VexL.PrefImageRelative, p, nameof(p.ImageRelative));
                FolderRow(page, VexL.PrefImageFolder, p, nameof(p.ImageFolder));
                break;
            case "appearance":
                // 对应原型外观页：界面动效 + 界面圆角两个三态分段控件，再是完整水印参数与实时样例；
                // 配色与排版入口在标题栏「外观」，水印开关只在视图菜单出现一处，偏好页不重复放置。
                Segmented(page, VexL.MenuMotion, _model.Layout, nameof(_model.Layout.MotionMode),
                    () => _model.Layout.MotionMode, value => _model.Layout.MotionMode = value,
                    [(MotionModes.Full, L("Vex.VexL.MotionFull")), (MotionModes.Reduced, L("Vex.VexL.MotionReduced")), (MotionModes.Off, L("Vex.VexL.MotionOff"))],
                    "Vex.VexL.MotionModeNote");
                Segmented(page, "Vex.VexL.PrefCornerRadius", _model.Appearance, nameof(_model.Appearance.RadiusMode),
                    () => _model.Appearance.RadiusMode, value => _model.Appearance.RadiusMode = value,
                    [("square", L("Vex.VexL.RadiusSquare")), ("compact", L("Vex.VexL.RadiusCompact")), ("standard", L("Vex.VexL.RadiusStandard"))],
                    "Vex.VexL.RadiusModeNote");
                Heading(page, VexL.PrefWatermark);
                Text(page, VexL.PrefWatermarkText, p, nameof(p.WatermarkText));
                Range(page, VexL.PrefWatermarkFontSize, p, nameof(p.WatermarkFontSize), 12, 36, " px");
                ColorChoice(page, VexL.PrefWatermarkColor, p);
                Choice(page, VexL.PrefWatermarkWeight, p, nameof(p.WatermarkWeight), [(L("Vex.VexL.PrefWeightLight"), 300), (L("Vex.VexL.PrefWeightRegular"), 400), (L("Vex.VexL.PrefWeightBold"), 700)]);
                Range(page, VexL.PrefWatermarkOpacity, p, nameof(p.WatermarkOpacity), 0, 50, "%");
                page.Children.Add(BuildWatermarkSample());
                break;
            case "export":
                // 对齐原型 export 页；水印「所见即所得」——文档显示水印导出即包含，不再单设开关。
                Toggle(page, VexL.PrefExportOpen, p, nameof(p.ExportOpen));
                Heading(page, "PDF");
                Choice(page, VexL.PrefPdfSize, p, nameof(p.PdfSize), [("A4", "A4"), ("Letter", "Letter"), ("A5", "A5")]);
                Range(page, VexL.PrefPdfMargin, p, nameof(p.PdfMargin), 10, 30, " mm");
                Heading(page, "HTML");
                Toggle(page, VexL.PrefHtmlStyle, p, nameof(p.HtmlStyle));
                break;
            case "mcp":
                // 对齐原型 .vex-mcp-tabs：它就是 .vex-segmented 的一份宿主——轨道、滑动指示块、选中字色字重
                // 全走共享配方，宿主只给 gap 4、button{flex:1} 等宽与 28×密度的基准高；页签与内容之间留 16。
                var mcpRoot = new StackPanel { Spacing = 16 };
                var mcpHost = new ContentControl();
                var tabTrack = new Border { Classes = { "vex-segmented", "mcp-tabs" } };
                var tabBar = new VexSegmentedPanel { Spacing = 4, FillItems = true };
                tabTrack.Child = tabBar;
                var mcpPeers = new List<Button>();
                Control BuildServer() => new McpSettingsOverlayView(embedded: true) { DataContext = _model.Mcp };
                Control BuildClients() => BuildMcpClientsPage();
                Control BuildAudit() => new StackPanel { Spacing = 0, Children = { BuildMcpStatusRow(), new McpAuditOverlayView(_model.Audit, L(VexL.McpAudit), L(VexL.McpAuditRefresh), L(VexL.McpAuditEmpty), embedded: true) } };
                (string Name, string Label, Func<Control> Build)[] tabs =
                [
                    ("McpTabServer", L(VexL.PrefMcpServer), BuildServer),
                    ("McpTabClients", L(VexL.PrefMcpClients), BuildClients),
                    ("McpTabAudit", L(VexL.McpTabAudit), BuildAudit),
                ];
                void SelectTab(int index)
                {
                    _mcpTab = index;
                    for (var i = 0; i < mcpPeers.Count; i++)
                        VexSegmentedPanel.SetIsActive(mcpPeers[i], i == index);
                    mcpHost.Content = tabs[index].Build();
                }
                for (var i = 0; i < tabs.Length; i++)
                {
                    var index = i;
                    var tab = new Button { Name = tabs[i].Name, Classes = { "vex-segment", "mcp-tab" }, Content = tabs[i].Label };
                    tab.Click += (_, _) => SelectTab(index);
                    mcpPeers.Add(tab); tabBar.Children.Add(tab);
                }
                // 换语言会整页重建，页签选择属于用户状态，得跟着复原而不是退回第一个。
                SelectTab(_mcpTab);
                mcpRoot.Children.Add(tabTrack); mcpRoot.Children.Add(mcpHost);
                page.Children.Add(mcpRoot);
                break;
        }
    }
    private void BindResource(AvaloniaObject target, AvaloniaProperty property, string key) => DesignSelectionControls.Resource(target, property, this, key);
    private string LF(string key, params object?[] args) => _localizer.Format(key, args);

    /// <summary>服务状态行（原型 .vex-mcp-status）：三个 MCP 子页共用，绿灯与红字跟着模型属性走。</summary>
    private Control BuildMcpStatusRow()
    {
        var mcp = _model.Mcp;
        var dot = new Avalonia.Controls.Shapes.Ellipse { Classes = { "mcp-dot" } };
        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new Binding(nameof(McpSettingsViewModel.StatusText)) { Source = mcp });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Classes = { "mcp-status" } };
        row.Children.Add(dot); row.Children.Add(text);
        void Sync()
        {
            dot.Classes.Set("on", mcp.IsStatusRunning);
            row.Classes.Set("error", mcp.IsStatusError);
        }
        // 样式类只能命令式改，所以订阅跟着视觉树挂卸：切换页签重建页面时不会留下旧行的订阅。
        void Changed(object? sender, PropertyChangedEventArgs args) => Sync();
        row.AttachedToVisualTree += (_, _) => { mcp.PropertyChanged += Changed; Sync(); };
        row.DetachedFromVisualTree += (_, _) => mcp.PropertyChanged -= Changed;
        return row;
    }

    /// <summary>配置正文块（原型 pre.vex-code：12 号等宽、可整段选中复制）。</summary>
    private Border BuildMcpCode(string? name, string property, object source, string? visibleProperty = null)
    {
        var text = new SelectableTextBlock { Name = name ?? string.Empty };
        text.Bind(TextBlock.TextProperty, new Binding(property) { Source = source });
        var block = new Border { Classes = { "mcp-code" }, Child = text };
        if (visibleProperty is not null) block.Bind(IsVisibleProperty, new Binding(visibleProperty) { Source = source });
        return block;
    }

    /// <summary>客户端页（对齐原型 .vex-mcp-tabs.vex-mcp-subtabs.vex-segmented）：状态行 + 与一级同语言的胶囊分段 + 配置正文。</summary>
    private Control BuildMcpClientsPage()
    {
        var page = new StackPanel { Spacing = 0 };
        var host = new ContentControl();
        var subTrack = new Border { Classes = { "vex-segmented", "mcp-tabs", "mcp-subtabs" } };
        var bar = new VexSegmentedPanel { Spacing = 4, FillItems = true };
        subTrack.Child = bar;
        var peers = new List<Button>();
        void SelectTab(Button selected, Func<Control> build)
        {
            foreach (var peer in peers)
                VexSegmentedPanel.SetIsActive(peer, ReferenceEquals(peer, selected));
            host.Content = build();
        }
        foreach (var (name, label, build) in new (string, string, Func<Control>)[]
        {
            ("McpSubHttp", L(VexL.McpHttpConfig), BuildMcpHttpPane),
            ("McpSubStdio", L(VexL.McpStdioBridge), BuildMcpStdioPane),
        })
        {
            var tab = new Button { Name = name, Classes = { "vex-segment", "mcp-tab", "mcp-subtab" }, Content = label };
            tab.Click += (_, _) => SelectTab(tab, build);
            peers.Add(tab); bar.Children.Add(tab);
        }
        SelectTab(peers[0], BuildMcpHttpPane);
        page.Children.Add(BuildMcpStatusRow());
        page.Children.Add(subTrack);
        page.Children.Add(host);
        return page;
    }
    private Control BuildMcpHttpPane()
    {
        var mcp = _model.Mcp;
        var pane = new StackPanel { Spacing = 0 };
        pane.Children.Add(BuildMcpCode("McpClientConfiguration", nameof(McpSettingsViewModel.ClientConfiguration), mcp));
        var actions = new StackPanel { Classes = { "mcp-actions" } };
        var copy = new Button { Classes = { "mcp-pill" }, Content = L(VexL.PrefMcpCopyConfig) };
        copy.Click += async (_, _) => await mcp.CopyClientConfigurationAsync();
        var test = new Button { Classes = { "mcp-pill", "primary" }, Content = L(VexL.McpTestConnection) };
        test.Click += async (_, _) => await mcp.TestConnectionAsync();
        actions.Children.Add(copy); actions.Children.Add(test);
        pane.Children.Add(actions);
        // 原型 #vex-test-result：连接结果原地回显，不另弹提示。
        var result = new TextBlock { FontSize = 13, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
        result.Bind(TextBlock.TextProperty, new Binding(nameof(McpSettingsViewModel.TestResult)) { Source = mcp });
        result.Bind(IsVisibleProperty, new Binding(nameof(McpSettingsViewModel.HasTestResult)) { Source = mcp });
        pane.Children.Add(result);
        return pane;
    }
    private Control BuildMcpStdioPane()
    {
        var mcp = _model.Mcp;
        var pane = new StackPanel { Spacing = 0 };
        pane.Children.Add(BuildMcpCode("McpStdioConfiguration", nameof(McpSettingsViewModel.StdioConfiguration), mcp));
        var actions = new StackPanel { Classes = { "mcp-actions" } };
        var read = new Button { Classes = { "mcp-pill" }, Content = L(VexL.McpReadDocument) };
        read.Click += async (_, _) => await mcp.ReadDocumentAsync();
        var write = new Button { Classes = { "mcp-pill" }, Content = L(VexL.McpWriteDocument) };
        write.Click += async (_, _) => await mcp.RequestDocumentEditAsync();
        actions.Children.Add(read); actions.Children.Add(write);
        pane.Children.Add(actions);
        // 请求区（原型 #vex-mcp-request）：正文预览走 .vex-code，提示文字按校验条/正文两种语言渲染。
        var error = new TextBlock { FontSize = 12, LineHeight = 19.2, Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap };
        BindResource(error, TextBlock.ForegroundProperty, "VexDangerBrush");
        error.Bind(TextBlock.TextProperty, new Binding(nameof(McpSettingsViewModel.RequestMessage)) { Source = mcp });
        error.Bind(IsVisibleProperty, new Binding(nameof(McpSettingsViewModel.HasRequestError)) { Source = mcp });
        pane.Children.Add(error);
        var note = new TextBlock { FontSize = 13, Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap };
        note.Bind(TextBlock.TextProperty, new Binding(nameof(McpSettingsViewModel.RequestMessage)) { Source = mcp });
        note.Bind(IsVisibleProperty, new Binding(nameof(McpSettingsViewModel.HasRequestNote)) { Source = mcp });
        pane.Children.Add(note);
        pane.Children.Add(BuildMcpCode(null, nameof(McpSettingsViewModel.RequestCode), mcp, nameof(McpSettingsViewModel.HasRequestCode)));
        return pane;
    }

    /// <summary>目录字段行：输入框 + 「…」浏览按钮（资源目录、授权目录等）。路径可能很长，输入框占满剩余宽度。</summary>
    private void FolderRow(StackPanel page, string label, object source, string property)
    {
        var text = new TextBox { Name = property, MinWidth = 240, Height = 34, FontSize = 14, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Stretch };
        BindResource(text, TextBox.BorderBrushProperty, "VexPanelBorderBrush"); BindResource(text, TextBox.BackgroundProperty, "VexShellBackgroundBrush");
        text.Bind(TextBox.TextProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay });
        var browse = new Button { Content = "…", Width = 34, Height = 34, Padding = default, CornerRadius = new CornerRadius(5), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        BindResource(browse, Button.BorderBrushProperty, "VexPanelBorderBrush"); BindResource(browse, Button.BackgroundProperty, "VexShellBackgroundBrush");
        browse.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not { } owner) return;
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { AllowMultiple = false, Title = L(label) });
            if (folders.FirstOrDefault() is { } selected && Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(selected) is { } folder)
                source.GetType().GetProperty(property)?.SetValue(source, folder);
        };
        var panel = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        panel.Children.Add(text); Grid.SetColumn(browse, 1); panel.Children.Add(browse);
        Row(page, label, panel, fill: true);
    }
    private Border Frame(Control child, Thickness border)
    {
        var frame = new Border { Child = child, BorderThickness = border };
        BindResource(frame, Border.BorderBrushProperty, "VexPanelBorderBrush"); return frame;
    }
    private void Heading(StackPanel page, string label)
    {
        var text = new TextBlock { Text = L(label), FontSize = 12, LineHeight = 18, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 24, 0, 8) };
        BindResource(text, TextBlock.ForegroundProperty, "VexMutedBrush"); page.Children.Add(text);
    }
    private void Row(StackPanel page, string label, Control control, string? note = null, bool fill = false)
    {
        // fill = 控件吃满剩余宽度（路径类输入框），否则按内容自适应并靠右。
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions(fill ? "Auto,*" : "*,Auto"), ColumnSpacing = 24, MinHeight = 33 };
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = L(label), FontSize = 14, LineHeight = 21, TextWrapping = TextWrapping.Wrap });
        if (note is not null) labels.Children.Add(new TextBlock { Text = L(note), FontSize = 12, LineHeight = 19.2, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
        row.Children.Add(labels); control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control, 1); row.Children.Add(control);
        var frame = Frame(row, new Thickness(0, 0, 0, 1)); frame.Padding = new Thickness(0, 12); page.Children.Add(frame);
    }
    private void Toggle(StackPanel page, string label, object source, string property, string? note = null)
    {
        var toggle = new ToggleButton { Name = property, Classes = { "pref-switch" } };
        toggle.Bind(ToggleButton.IsCheckedProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, toggle, note);
    }

    /// <summary>
    /// 原型 .vex-pref-segmented（preferences.js 的 segmented()）：互斥项一律用共享分段控件呈现，
    /// 轨道固定 196 等分（prototype.css:214-215），段高走 --control-h-sm、内距与字号走密度令牌。
    /// 值改变既可能来自这里，也可能来自标题栏菜单，所以选中态订阅回模型，且随视觉树挂卸避免换页后留下旧订阅。
    /// </summary>
    private void Segmented(StackPanel page, string label, INotifyPropertyChanged source, string property,
        Func<string> get, Action<string> set, (string Value, string Label)[] options, string? note = null)
    {
        var bar = new VexSegmentedPanel { FillItems = true };
        var track = new Border { Classes = { "vex-segmented", "pref-segmented" }, Width = 196, Child = bar };
        var buttons = options.Select(option =>
        {
            var button = new Button { Classes = { "vex-segment", "pref-segment" }, Content = option.Label };
            button.Click += (_, _) => set(option.Value);
            bar.Children.Add(button);
            return (Option: option, Button: button);
        }).ToArray();
        void Sync()
        {
            var current = get();
            foreach (var (option, button) in buttons)
                VexSegmentedPanel.SetIsActive(button, option.Value == current);
        }
        void Changed(object? sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == property) Sync();
        }
        Sync();
        track.AttachedToVisualTree += (_, _) => { source.PropertyChanged += Changed; Sync(); };
        track.DetachedFromVisualTree += (_, _) => source.PropertyChanged -= Changed;
        Row(page, label, track, note);
    }
    private void Range(StackPanel page, string label, object source, string property, double minimum, double maximum, string suffix)
    {
        var slider = new Slider { Classes = { "pref-range" }, Minimum = minimum, Maximum = maximum, Width = 135, VerticalAlignment = VerticalAlignment.Center };
        slider.Bind(Slider.ValueProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay });
        var value = new TextBlock { FontSize = 13, VerticalAlignment = VerticalAlignment.Center, MinWidth = 44, TextAlignment = TextAlignment.Right };
        value.Bind(TextBlock.TextProperty, new Binding(property) { Source = source, Converter = RangeValueConverter.Instance, ConverterParameter = suffix });
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(slider); panel.Children.Add(value);
        Row(page, label, panel);
    }

    private sealed class RangeValueConverter : IValueConverter
    {
        public static readonly RangeValueConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is null) return null;
            var number = value is IFormattable formattable ? formattable.ToString("0", culture) : value.ToString();
            return number + (parameter as string ?? "");
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => BindingOperations.DoNothing;
    }
    private void ColorChoice(StackPanel page, string label, Vex.Core.Services.EditorPreferences preferences)
    {
        var fill = new Border { Height = 18, Width = 164, CornerRadius = new CornerRadius(4) };
        fill.Bind(Border.BackgroundProperty, new Binding(nameof(Vex.Core.Services.EditorPreferences.WatermarkColor)) { Source = preferences, Converter = HexToBrushConverter.Instance });
        var swatch = new Button { Width = 180, Height = 26, Padding = default, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), Content = fill };
        BindResource(swatch, Button.BorderBrushProperty, "VexPanelBorderBrush");
        var presets = new UniformGrid { Columns = 8, Width = 224, Margin = new Thickness(8) };
        foreach (var hex in new[] { "#24766B", "#2E5851", "#3A6EA5", "#4C6FE8", "#7457E8", "#C2417B", "#B3261E", "#D96C2C", "#8A6D3B", "#5F5347", "#6B7280", "#374151", "#111111", "#E8E4DA", "#88B04B", "#2477A8" })
        {
            var item = new Button { Width = 22, Height = 22, Margin = new Thickness(2), CornerRadius = new CornerRadius(11), Padding = default, BorderThickness = new Thickness(0), Background = new SolidColorBrush(Color.Parse(hex)), Tag = hex };
            ToolTip.SetTip(item, hex);
            item.Click += (_, _) => preferences.WatermarkColor = hex;
            presets.Children.Add(item);
        }
        // 对齐原型 type=color：预设色板之上提供完整颜色选择器（光谱 + 分量）。
        var pickerPanel = new StackPanel { Margin = new Thickness(8) };
        var picker = new ColorPicker { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
        try { picker.Color = Color.Parse(preferences.WatermarkColor); } catch { /* 默认色即可 */ }
        picker.ColorChanged += (_, args) => preferences.WatermarkColor = args.NewColor.ToString();
        pickerPanel.Children.Add(picker);
        presets.Margin = new Thickness(8, 4, 8, 8);
        pickerPanel.Children.Add(presets);
        swatch.Flyout = new Flyout { Content = pickerPanel, Placement = PlacementMode.BottomEdgeAlignedLeft };
        Row(page, label, swatch);
    }
    /// <summary>水印实时样例：标语 + 按当前参数叠加的水印（对应原型外观页底部样例卡）。</summary>
    private Control BuildWatermarkSample()
    {
        var p = _model.Preferences;
        // 原型 .vex-watermark-sample：strong 固定 17px/500，span 12px muted，列间距 10。
        var slogan = new TextBlock { Text = L(VexL.AboutShortSlogan), FontSize = 17, FontWeight = FontWeight.Medium, TextWrapping = TextWrapping.Wrap };
        BindResource(slogan, TextBlock.ForegroundProperty, "VexEditorForegroundBrush");
        var byline = new TextBlock { Text = "维刻 Vex", FontSize = 12 };
        BindResource(byline, TextBlock.ForegroundProperty, "VexMutedBrush");
        var content = new StackPanel { Margin = new Thickness(20, 26), Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(slogan); content.Children.Add(byline);
        var watermark = new Vex.Modules.Workspace.Views.WatermarkOverlay();
        watermark.Bind(Vex.Modules.Workspace.Views.WatermarkOverlay.TextProperty, new Binding(nameof(Vex.Core.Services.EditorPreferences.WatermarkText)) { Source = p });
        watermark.Bind(Vex.Modules.Workspace.Views.WatermarkOverlay.StrengthProperty, new Binding(nameof(Vex.Core.Services.EditorPreferences.WatermarkOpacity)) { Source = p });
        watermark.Bind(Vex.Modules.Workspace.Views.WatermarkOverlay.FontSizeProperty, new Binding(nameof(Vex.Core.Services.EditorPreferences.WatermarkFontSize)) { Source = p });
        watermark.Bind(Vex.Modules.Workspace.Views.WatermarkOverlay.HexInkProperty, new Binding(nameof(Vex.Core.Services.EditorPreferences.WatermarkColor)) { Source = p });
        watermark.Bind(Vex.Modules.Workspace.Views.WatermarkOverlay.WeightValueProperty, new Binding(nameof(Vex.Core.Services.EditorPreferences.WatermarkWeight)) { Source = p });
        var host = new Grid { MinHeight = 140, ClipToBounds = true };
        host.Children.Add(content); host.Children.Add(watermark);
        var frame = Frame(host, new Thickness(1));
        frame.CornerRadius = new CornerRadius(5); frame.Margin = new Thickness(0, 18, 0, 0);
        BindResource(frame, Border.BackgroundProperty, "VexDocumentPaperBrush");
        return frame;
    }

    private sealed class HexToBrushConverter : IValueConverter
    {
        public static readonly HexToBrushConverter Instance = new();
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            try { return value is string hex ? new SolidColorBrush(Color.Parse(hex)) : null; }
            catch { return null; }
        }
        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => BindingOperations.DoNothing;
    }
    private void Number(StackPanel page, string label, object source, string property, decimal minimum, decimal maximum, decimal step = 1)
    {
        var number = new NumericUpDown { Name = property, Minimum = minimum, Maximum = maximum, Increment = step, Width = 104, FormatString = "0.#" };
        number.Bind(NumericUpDown.ValueProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, number);
    }
    private void Text(StackPanel page, string label, object source, string property)
    {
        var text = new TextBox { Name = property, Width = 180, Height = 34, FontSize = 14, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1) };
        BindResource(text, TextBox.BorderBrushProperty, "VexPanelBorderBrush"); BindResource(text, TextBox.BackgroundProperty, "VexShellBackgroundBrush");
        text.Bind(TextBox.TextProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, text);
    }
    private void Choice(StackPanel page, string label, object source, string property, IEnumerable<(string Label, object Value)> choices)
    {
        var items = choices.Select(x => new ChoiceItem(x.Label, x.Value)).ToArray();
        var combo = new ComboBox { Name = property, Width = 180, Height = 34, FontSize = 14, Padding = new Thickness(9, 5), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), ItemsSource = items,
            ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ChoiceItem>((x, _) => new TextBlock { Text = x?.Label, FontSize = 14 }) };
        BindResource(combo, ComboBox.BorderBrushProperty, "VexPanelBorderBrush"); BindResource(combo, ComboBox.BackgroundProperty, "VexShellBackgroundBrush");
        // 显式映射选项，避免 NativeAOT 对私有记录的反射路径及初始化空选区写回设置。
        combo.Bind(ComboBox.SelectedItemProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay, Converter = new ChoiceSelectionConverter(items) }); Row(page, label, combo);
    }
    private sealed record ChoiceItem(string Label, object Value);
    private sealed class ChoiceSelectionConverter(ChoiceItem[] items) : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            items.FirstOrDefault(item => Equals(item.Value, value));
        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
            value is ChoiceItem item ? item.Value : BindingOperations.DoNothing;
    }
}
