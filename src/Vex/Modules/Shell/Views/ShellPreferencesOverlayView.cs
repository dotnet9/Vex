using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>复用应用中的实际设置模型，按桌面原型提供分类导航和即时设置。</summary>
public sealed class ShellPreferencesOverlayView : UserControl
{
    private readonly ShellPreferencesViewModel _model;
    private readonly IAppLocalizer _localizer;
    private readonly ListBox _navigation = new() { Classes = { "preferences-navigation" }, Width = 148, Padding = new Thickness(10, 18), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    private readonly ScrollViewer _scroll = new() { Padding = new Thickness(20, 22), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly TextBlock _title = new() { FontSize = 14, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _saved = new() { Classes = { "muted" }, FontSize = 12 };
    private readonly Border _card;
    private Window? _owner;
    private readonly Button _done = new() { FontSize = 12, MinHeight = 32, Padding = new Thickness(12, 6), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
    private readonly (string Id, string Key)[] _categories =
    [ ("general", VexL.PrefGeneral), ("editor", VexL.PrefEditor), ("markdown", "Markdown"),
      ("images", VexL.PrefImages), ("appearance", VexL.PrefAppearance), ("export", VexL.Export), ("mcp", "MCP") ];

    public ShellPreferencesOverlayView(ShellPreferencesViewModel model, IAppLocalizer localizer, string category)
    {
        _model = model; _localizer = localizer; DataContext = model;
        BindResource(this, ForegroundProperty, "VexValueBrush");
        var grid = new Grid { RowDefinitions = new RowDefinitions("36,*,54") };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Margin = new Thickness(16, 0, 0, 0);
        header.Children.Add(_title);
        var close = new Button { Classes = { "vex-dismiss" }, Width = 46, Height = 32 };
        Avalonia.Automation.AutomationProperties.SetName(close, L(VexL.Close));
        close.Click += (_, _) => model.Close(); Grid.SetColumn(close, 1); header.Children.Add(close);
        grid.Children.Add(Frame(header, new Thickness(0, 0, 0, 1)));
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var navigationFrame = Frame(_navigation, new Thickness(0, 0, 1, 0));
        BindResource(navigationFrame, Border.BackgroundProperty, "VexShellBackgroundBrush");
        body.Children.Add(navigationFrame); Grid.SetColumn(_scroll, 1); body.Children.Add(_scroll);
        Grid.SetRow(body, 1); grid.Children.Add(body);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var dot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
        BindResource(dot, Border.BackgroundProperty, "VexAccentBrush"); status.Children.Add(dot); status.Children.Add(_saved); footer.Children.Add(status);
        _done.Click += (_, _) => model.Close(); Grid.SetColumn(_done, 1); footer.Children.Add(_done);
        BindResource(_done, Button.BorderBrushProperty, "VexPanelBorderBrush"); BindResource(_done, Button.BackgroundProperty, "VexShellBackgroundBrush");
        var footerFrame = Frame(footer, new Thickness(0, 1, 0, 0)); footerFrame.Padding = new Thickness(18, 10);
        Grid.SetRow(footerFrame, 2); grid.Children.Add(footerFrame);
        _card = new Border { Classes = { "dialog-card" }, Width = 675, Height = 700, Padding = default, CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = grid };
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
        _title.Text = L(VexL.Preferences); _saved.Text = L(VexL.PrefSaved); _done.Content = L("Vex.VexL.PrefDone");
        string[] icons = ["M7 2H15L20 7V22H4V2Z M14 2V8H20", "M12 4H4V20H20V12 M18 2L22 6L12 16L8 17L9 13Z", "M8 7L3 12L8 17 M16 7L21 12L16 17 M14 5L10 19", "M3 3H21V21H3Z M3 17L9 10L14 15L17 12L21 17 M15 7H15.1", "M12 3A9 9 0 1 0 21 12H16A3 3 0 0 1 13 9 M8 7H8.1 M6 12H6.1 M10 17H10.1", "M12 3V15 M7 10L12 15L17 10 M4 16V21H20V16", "M4 5L10 11L4 17 M12 19H21"];
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
        _saved.Text = L(SelectedCategory == "mcp" ? VexL.PrefMcpSave : VexL.PrefSaved);
        var page = new StackPanel { Spacing = 0 };
        _scroll.Content = page; _scroll.Offset = default;
        var p = _model.Preferences;
        switch (SelectedCategory)
        {
            case "general":
                Choice(page, VexL.PrefLanguage, _model.Appearance, nameof(_model.Appearance.SelectedLanguage), _model.Appearance.LanguageOptions.Select(x => (x.DisplayName, (object)x)));
                Choice(page, "Vex.VexL.PrefStartup", p, nameof(p.RestoreWorkspace), [(L("Vex.VexL.PrefEmptyWorkspace"), false), (L("Vex.VexL.PrefRecentWorkspace"), true)]);
                Heading(page, "Vex.VexL.PrefSaveRecovery");
                Toggle(page, VexL.PrefAutoSave, p, nameof(p.AutoSave), "Vex.VexL.PrefAutoSaveNote");
                Choice(page, VexL.PrefSaveDelay, p, nameof(p.AutoSaveDelay), [("2 " + L("Vex.VexL.PrefSeconds"), 2), ("5 " + L("Vex.VexL.PrefSeconds"), 5), ("10 " + L("Vex.VexL.PrefSeconds"), 10)]);
                Toggle(page, VexL.PrefRecovery, p, nameof(p.RecoveryDrafts), "Vex.VexL.PrefRecoveryNote");
                Heading(page, "Vex.VexL.PrefNewDefaults");
                Choice(page, VexL.PrefEncoding, p, nameof(p.DefaultEncoding), [("UTF-8", "utf-8"), ("UTF-8 BOM", "utf-8-bom"), ("GB18030", "GB18030")]);
                Choice(page, VexL.PrefNewline, p, nameof(p.DefaultNewline), [(L(VexL.PrefSystemFont), "system"), ("LF", "LF"), ("CRLF", "CRLF")]);
                break;
            case "editor":
                Choice(page, VexL.PrefBodyFont, p, nameof(p.BodyFont), [(L(VexL.PrefSystemFont), "system"), (L(VexL.PrefSerifFont), "serif"), (L(VexL.PrefMonoFont), "mono")]);
                Number(page, VexL.PrefBodySize, p, nameof(p.BodyFontSize), 16, 24);
                Number(page, VexL.PrefCodeSize, p, nameof(p.CodeFontSize), 11, 20);
                Number(page, VexL.PrefIndent, p, nameof(p.IndentSize), 2, 8);
                Toggle(page, VexL.MenuAutoPair, _model.Display, nameof(_model.Display.EnableAutoPair));
                Toggle(page, VexL.PrefWrap, p, nameof(p.CodeWrap));
                Toggle(page, VexL.ShowLineNumbers, _model.Display, nameof(_model.Display.ShowLineNumbers));
                Toggle(page, VexL.MenuFocusMode, _model.Layout, nameof(_model.Layout.IsFocusMode));
                Toggle(page, VexL.MenuTypewriterMode, _model.Layout, nameof(_model.Layout.IsTypewriterMode));
                break;
            case "markdown":
                Toggle(page, VexL.PrefMath, p, nameof(p.SyntaxMath)); Toggle(page, VexL.PrefChemistry, p, nameof(p.SyntaxChemistry));
                Toggle(page, VexL.PrefDiagrams, p, nameof(p.SyntaxDiagrams)); Toggle(page, VexL.PrefSubscript, p, nameof(p.SyntaxSubscript));
                Toggle(page, VexL.PrefHighlight, p, nameof(p.SyntaxHighlight)); Toggle(page, VexL.PrefAutoLinks, p, nameof(p.SyntaxAutoLinks));
                Toggle(page, VexL.PrefSoftBreaks, p, nameof(p.SyntaxSoftBreaks));
                break;
            case "images":
                Toggle(page, VexL.PrefImageCopy, p, nameof(p.ImageCopy)); Toggle(page, VexL.PrefImageRelative, p, nameof(p.ImageRelative));
                Text(page, VexL.PrefImageFolder, p, nameof(p.ImageFolder));
                break;
            case "appearance":
                Choice(page, VexL.WBTheme, _model.Appearance, nameof(_model.Appearance.SelectedTheme), _model.Appearance.ThemeOptions.Select(x => (L(x.Key == "light" ? VexL.ThemeLight : x.Key == "dark" ? VexL.ThemeDark : VexL.ThemeSystem), (object)x)));
                var colors = new UniformGrid { Columns = 8, Width = 224 };
                foreach (var palette in _model.Appearance.PaletteOptions)
                {
                    var swatch = new Button { Width = 22, Height = 22, MinHeight = 0, MinWidth = 0, Padding = default, Margin = new Thickness(3), Background = palette.Swatch, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(2), BorderBrush = _model.Appearance.SelectedPalette == palette ? palette.Swatch : Brushes.Transparent, Tag = palette };
                    ToolTip.SetTip(swatch, L(palette.NameKey)); swatch.Click += (_, _) =>
                    {
                        _model.Appearance.SelectedPalette = palette;
                        foreach (var button in colors.Children.OfType<Button>()) button.BorderBrush = Equals(button.Tag, palette) ? palette.Swatch : Brushes.Transparent;
                    };
                    colors.Children.Add(swatch);
                }
                Row(page, VexL.WBTheme, colors);
                Choice(page, VexL.WBTypography, _model.Appearance, nameof(_model.Appearance.SelectedTypography), _model.Appearance.TypographyOptions.Select(x => (L(x.NameKey), (object)x)));
                Toggle(page, VexL.CompactLayout, _model.Appearance, nameof(_model.Appearance.IsCompactLayout));
                Number(page, VexL.PrefZoom, p, nameof(p.ContentZoom), 50, 200, 10);
                Toggle(page, VexL.PrefWordCount, p, nameof(p.WordCountVisible));
                Toggle(page, VexL.MenuMotion, _model.Layout, nameof(_model.Layout.EnableMotion));
                Heading(page, VexL.PrefWatermark);
                Toggle(page, VexL.PrefWatermarkPreview, p, nameof(p.WatermarkPreview));
                Toggle(page, VexL.PrefWatermarkExport, p, nameof(p.WatermarkExport));
                Text(page, VexL.PrefWatermarkText, p, nameof(p.WatermarkText));
                Number(page, VexL.PrefWatermarkOpacity, p, nameof(p.WatermarkOpacity), 0, 50);
                break;
            case "export":
                Choice(page, VexL.PrefExportFormat, p, nameof(p.ExportFormat), [("HTML", "HTML"), ("PDF", "PDF"), ("Markdown", "Markdown"), ("PNG", "PNG"), ("Word", "Word")]);
                Text(page, VexL.PrefExportFolder, p, nameof(p.ExportFolder));
                Toggle(page, VexL.PrefExportOpen, p, nameof(p.ExportOpen)); Toggle(page, VexL.PrefHtmlStyle, p, nameof(p.HtmlStyle));
                Toggle(page, VexL.PrefWatermarkExport, p, nameof(p.WatermarkExport));
                Heading(page, "PDF");
                Choice(page, VexL.PrefPdfSize, p, nameof(p.PdfSize), [("A4", "A4"), ("Letter", "Letter"), ("A5", "A5")]);
                Number(page, VexL.PrefPdfMargin, p, nameof(p.PdfMargin), 10, 30);
                var export = new Button { Content = L(VexL.Export), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 16, 0, 0) };
                export.Click += (_, _) => CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new Vex.Core.Messaging.ShellActionCommand(Vex.Core.Messaging.ShellActionKind.Export, p.ExportFormat));
                page.Children.Add(export);
                break;
            case "mcp":
                var tabs = new TabControl { Name = "McpTabs" };
                tabs.Items.Add(new TabItem { Header = L(VexL.PrefMcpServer), Content = new McpSettingsOverlayView(embedded: true) { DataContext = _model.Mcp } });
                var clients = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12) };
                clients.Children.Add(new TextBlock { Text = L(VexL.PrefMcpClientHint), TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
                var config = new TextBox { Name = "McpClientConfiguration", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontFamily = VexFonts.Code, MinHeight = 240 };
                config.Bind(TextBox.TextProperty, new Binding(nameof(_model.Mcp.ClientConfiguration)) { Source = _model.Mcp });
                clients.Children.Add(config);
                var copy = new Button { Content = L(VexL.PrefMcpCopyConfig), HorizontalAlignment = HorizontalAlignment.Left };
                copy.Click += async (_, _) => await _model.Mcp.CopyClientConfigurationAsync(); clients.Children.Add(copy);
                tabs.Items.Add(new TabItem { Header = L(VexL.PrefMcpClients), Content = clients });
                tabs.Items.Add(new TabItem { Header = L(VexL.McpAudit), Content = new McpAuditOverlayView(_model.Audit, L(VexL.McpAudit), L(VexL.RefreshPreview), L(VexL.McpAuditEmpty), embedded: true) });
                page.Children.Add(tabs);
                break;
        }
    }
    private void BindResource(AvaloniaObject target, AvaloniaProperty property, string key) => DesignSelectionControls.Resource(target, property, this, key);
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
    private void Row(StackPanel page, string label, Control control, string? note = null)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 24, MinHeight = 33 };
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = L(label), FontSize = 14, LineHeight = 21, TextWrapping = TextWrapping.Wrap });
        if (note is not null) labels.Children.Add(new TextBlock { Text = L(note), FontSize = 12, LineHeight = 19.2, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, Classes = { "muted" } });
        row.Children.Add(labels); control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control, 1); row.Children.Add(control);
        var frame = Frame(row, new Thickness(0, 0, 0, 1)); frame.Padding = new Thickness(0, 12); page.Children.Add(frame);
    }
    private void Toggle(StackPanel page, string label, object source, string property, string? note = null)
    {
        var toggle = new ToggleSwitch { Name = property, Classes = { "pref-toggle" }, OnContent = null, OffContent = null, MinWidth = 34 };
        toggle.Bind(ToggleSwitch.IsCheckedProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, toggle, note);
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
