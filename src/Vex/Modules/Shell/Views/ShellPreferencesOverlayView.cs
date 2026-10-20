using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
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
    private readonly ListBox _navigation = new() { Width = 164, BorderThickness = new Thickness(0) };
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly TextBlock _title = new() { FontSize = 17, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _saved = new() { Classes = { "muted" }, FontSize = 11.5 };
    private readonly Border _card;
    private Window? _owner;
    private readonly Button _done = new() { Classes = { "primary" } };
    private readonly (string Id, string Key)[] _categories =
    [ ("general", VexL.PrefGeneral), ("editor", VexL.PrefEditor), ("markdown", "Markdown"),
      ("images", VexL.PrefImages), ("appearance", VexL.PrefAppearance), ("export", VexL.Export), ("mcp", "MCP") ];

    public ShellPreferencesOverlayView(ShellPreferencesViewModel model, IAppLocalizer localizer, string category)
    {
        _model = model; _localizer = localizer; DataContext = model;
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 16 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_title);
        var close = new Button { Content = "×", Classes = { "wb-quiet" }, FontSize = 21, Padding = new Thickness(8, 0) };
        close.Click += (_, _) => model.Close(); Grid.SetColumn(close, 1); header.Children.Add(close); grid.Children.Add(header);
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 20 };
        body.Children.Add(_navigation); Grid.SetColumn(_scroll, 1); body.Children.Add(_scroll);
        Grid.SetRow(body, 1); grid.Children.Add(body);
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _saved.VerticalAlignment = VerticalAlignment.Center; footer.Children.Add(_saved);
        _done.Click += (_, _) => model.Close(); Grid.SetColumn(_done, 1); footer.Children.Add(_done);
        Grid.SetRow(footer, 2); grid.Children.Add(footer);
        _card = new Border { Classes = { "dialog-card" }, Width = 850, Height = 610, Child = grid };
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
        _card.MaxWidth = Math.Max(320, window.Bounds.Width - 56);
        _card.MaxHeight = Math.Max(260, window.Bounds.Height - 56);
        _navigation.Width = window.Bounds.Width < 720 ? 120 : 164;
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
        _title.Text = L(VexL.Preferences); _saved.Text = L(VexL.PrefSaved); _done.Content = L(VexL.Close);
        _navigation.ItemsSource = _categories.Select(c => new ListBoxItem { Content = L(c.Key), Padding = new Thickness(12, 10) }).ToArray();
        _navigation.SelectedIndex = selected;
    }
    private void ShowCategory()
    {
        _saved.Text = L(SelectedCategory == "mcp" ? VexL.PrefMcpSave : VexL.PrefSaved);
        var page = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 8, 0) };
        _scroll.Content = page; _scroll.Offset = default;
        var p = _model.Preferences;
        switch (SelectedCategory)
        {
            case "general":
                Choice(page, VexL.PrefLanguage, _model.Appearance, nameof(_model.Appearance.SelectedLanguage), _model.Appearance.LanguageOptions.Select(x => (x.DisplayName, (object)x)));
                Toggle(page, VexL.PrefRestore, p, nameof(p.RestoreWorkspace));
                Toggle(page, VexL.PrefAutoSave, p, nameof(p.AutoSave));
                Number(page, VexL.PrefSaveDelay, p, nameof(p.AutoSaveDelay), 2, 60);
                Toggle(page, VexL.PrefRecovery, p, nameof(p.RecoveryDrafts));
                Choice(page, VexL.PrefEncoding, p, nameof(p.DefaultEncoding), [("UTF-8", "utf-8"), ("UTF-8 BOM", "utf-8-bom"), ("GB18030", "GB18030")]);
                Choice(page, VexL.PrefNewline, p, nameof(p.DefaultNewline), [(L(VexL.PrefSystemFont), "system"), ("LF", "LF"), ("CRLF", "CRLF")]);
                break;
            case "editor":
                Choice(page, VexL.PrefBodyFont, p, nameof(p.BodyFont), [(L(VexL.PrefSystemFont), "system"), (L(VexL.PrefSerifFont), "serif"), (L(VexL.PrefMonoFont), "mono")]);
                Number(page, VexL.PrefBodySize, p, nameof(p.BodyFontSize), 12, 24);
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
                var colors = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var palette in _model.Appearance.PaletteOptions)
                {
                    var swatch = new Button { Width = 28, Height = 28, Margin = new Thickness(0, 4, 8, 4), Background = palette.Swatch, CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(2), BorderBrush = _model.Appearance.SelectedPalette == palette ? palette.Swatch : Brushes.Transparent };
                    ToolTip.SetTip(swatch, L(palette.NameKey)); swatch.Click += (_, _) => _model.Appearance.SelectedPalette = palette;
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
    private void Heading(StackPanel page, string label) => page.Children.Add(new TextBlock { Text = L(label), FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 20, 0, 8) });
    private void Row(StackPanel page, string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 18, MinHeight = 50 };
        var text = new TextBlock { Text = L(label), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8) };
        row.Children.Add(text); control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control, 1); row.Children.Add(control); page.Children.Add(row);
    }
    private void Toggle(StackPanel page, string label, object source, string property)
    {
        var toggle = new ToggleSwitch { Name = property, OnContent = null, OffContent = null, MinWidth = 44 };
        toggle.Bind(ToggleSwitch.IsCheckedProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, toggle);
    }
    private void Number(StackPanel page, string label, object source, string property, decimal minimum, decimal maximum, decimal step = 1)
    {
        var number = new NumericUpDown { Name = property, Minimum = minimum, Maximum = maximum, Increment = step, Width = 104, FormatString = "0.#" };
        number.Bind(NumericUpDown.ValueProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, number);
    }
    private void Text(StackPanel page, string label, object source, string property)
    {
        page.Children.Add(new TextBlock { Text = L(label), Classes = { "muted" }, Margin = new Thickness(0, 14, 0, 5), TextWrapping = TextWrapping.Wrap });
        var text = new TextBox { Name = property }; text.Bind(TextBox.TextProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); page.Children.Add(text);
    }
    private void Choice(StackPanel page, string label, object source, string property, IEnumerable<(string Label, object Value)> choices)
    {
        var combo = new ComboBox { Name = property, MaxWidth = 200, MinWidth = 130, ItemsSource = choices.Select(x => new ChoiceItem(x.Label, x.Value)).ToArray(),
            SelectedValueBinding = new Binding(nameof(ChoiceItem.Value)), ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<ChoiceItem>((x, _) => new TextBlock { Text = x?.Label }) };
        combo.Bind(ComboBox.SelectedValueProperty, new Binding(property) { Source = source, Mode = BindingMode.TwoWay }); Row(page, label, combo);
    }
    private sealed record ChoiceItem(string Label, object Value);
}
