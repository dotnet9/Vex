using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using CodeWF.Toolkit.Lang;
using Vex.Core.Models;
using Vex.Controls;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace Vex.Modules.Shell.Views;

public sealed class ShellAppearanceMenuView : UserControl
{
    private const double PaletteCardHeight = 92;
    private ShellAppearanceViewModel? _vm;
    private MainWindowViewModel? _shell;
    private readonly Grid _palettes = new() { ColumnDefinitions = new("*,*,*"), ColumnSpacing = 6, RowSpacing = 6 };
    private readonly Dictionary<PaletteOption, (Button Button, TextBlock Check, Border Ring)> _paletteButtons = [];
    private readonly Dictionary<ThemeOption, Button> _modeButtons = [];
    private readonly Dictionary<string, Button> _densityButtons = [];
    private readonly Dictionary<LanguageOption, (Button Button, Border Chip, TextBlock ChipText, TextBlock Name, TextBlock Check)> _languageRows = [];
    private Button? _addCard;
    private ToggleButton? _outlineButton;
    private ShapePath? _outlineIcon;
    private ScrollViewer? _scroll;
    public Button AppearanceButton { get; private set; } = null!;
    public Button LanguageButton { get; private set; } = null!;
    public ToggleButton OutlineButton => _outlineButton ?? throw new InvalidOperationException("Outline button is not built yet");
    public const double MenuWidth = 430;

    /// <summary>原型 .density-options button.on 与卡片选中环共用的强调色派生点。</summary>
    private static readonly string[] StatusDots = ["#DC2626", "#15803D", "#B45309", "#4338CA"];
    private static readonly string[] DensityKeys = ["compact", "normal", "relaxed"];

    public ShellAppearanceMenuView()
    {
        Navigation(_palettes, () => 3);
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) =>
        {
            if (_vm is not null) _vm.PropertyChanged -= Changed;
            if (_shell is not null) _shell.Layout.PropertyChanged -= LayoutChanged;
            I18nManager.Instance.CultureChanged -= CultureChanged;
            _vm = null;
            _shell = null;
        };
    }

    private void Attach()
    {
        var vm = (DataContext as MainWindowViewModel)?.Appearance;
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null) _vm.PropertyChanged -= Changed;
        if (_shell is not null) _shell.Layout.PropertyChanged -= LayoutChanged;
        I18nManager.Instance.CultureChanged -= CultureChanged;
        _vm = vm;
        _shell = DataContext as MainWindowViewModel;
        if (vm is null) return;
        vm.PropertyChanged += Changed;
        if (_shell is not null) _shell.Layout.PropertyChanged += LayoutChanged;
        I18nManager.Instance.CultureChanged += CultureChanged;
        if (DataContext is MainWindowViewModel shell)
        {
            shell.Workbench.AppearanceMenuRequested -= OnAppearanceMenuRequested;
            shell.Workbench.AppearanceMenuRequested += OnAppearanceMenuRequested;
        }
        Build();
    }

    /// <summary>新增/编辑用户配色：对话框确认后注册并应用（对应原型 openPaletteDialog）。</summary>
    private async void OpenPaletteDialog(Vex.Core.Models.PaletteOption? editing)
    {
        var definition = editing?.Definition;
        string[]? seed = definition is null
            ? null
            : [definition.Light.Paper, definition.Light.Surface, definition.Light.Ink, definition.Light.Accent,
               definition.Dark.Paper, definition.Dark.Surface, definition.Dark.Ink, definition.Dark.Accent];
        var model = new ShellPaletteEditModel(editing?.Key, definition?.Name ?? "", definition?.Note ?? "", seed);
        var applied = await Ursa.Controls.OverlayDialog.ShowCustomAsync<bool>(new ShellPaletteEditView(model), model,
            options: new Ursa.Controls.OverlayDialogOptions { CanLightDismiss = true, CanDragMove = false, CanResize = false, IsCloseButtonVisible = false });
        if (_vm is null || !applied || model.Result is not { Colors.Length: >= 8 } result) return;
        var key = editing?.Key ?? "user:" + Guid.NewGuid().ToString("N")[..8];
        _vm.RegisterCustomPalette(key, result.Name, result.Note, result.Colors[0], result.Colors[1], result.Colors[2], result.Colors[3],
            result.Colors[4], result.Colors[5], result.Colors[6], result.Colors[7]);
    }

    /// <summary>弹出层开合状态回写给工作台，引导门控据此判定「外观已展开」。</summary>
    private void SetAppearanceMenuOpen(bool open)
    {
        if (DataContext is MainWindowViewModel shell) shell.Workbench.IsAppearanceMenuOpen = open;
    }

    /// <summary>引导「继续」代点开合外观弹出层。</summary>
    private void OnAppearanceMenuRequested(object? sender, EventArgs e)
    {
        var flyout = AppearanceButton.Flyout;
        if (flyout is null) return;
        if (flyout.IsOpen) flyout.Hide(); else flyout.ShowAt(AppearanceButton);
    }

    /// <summary>弹出层内容区，作为引导步骤的高亮目标。</summary>
    public Control MenuContent => _scroll ?? throw new InvalidOperationException("Appearance menu is not built yet");

    private void CultureChanged(object? sender, EventArgs e)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_vm is not null) Build(); });
            return;
        }
        Build();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        if (e.PropertyName is nameof(ShellAppearanceViewModel.PaletteOptions) or null) { Build(); return; }
        var accent = Brush("VexAccentBrush"); var soft = Brush("VexAccentSoftBrush"); var onAccent = Brush("StudioOnPrimary"); var text = Brush("VexValueBrush");
        foreach (var (option, card) in _paletteButtons)
        {
            var selected = option == _vm.SelectedPalette;
            card.Button.Classes.Set("palette-card-on", selected);
            card.Ring.IsVisible = selected;
            card.Check.IsVisible = selected;
        }
        foreach (var (option, button) in _modeButtons) button.Classes.Set("appearance-pill-on", option == _vm.SelectedTheme);
        foreach (var (key, button) in _densityButtons) button.Classes.Set("appearance-pill-on", key == _vm.SelectedDensity);
        foreach (var (option, row) in _languageRows)
        {
            var selected = option == _vm.SelectedLanguage;
            row.Button.Classes.Set("language-item-on", selected);
            row.Check.IsVisible = selected;
            row.Chip.Background = selected ? accent : soft;
            row.ChipText.Foreground = selected ? onAccent : accent;
            row.Name.Foreground = selected ? accent : text;
        }
        if (_addCard is not null) _addCard.IsVisible = !_vm.PaletteOptions.Any(option => option.Key.StartsWith("user:", StringComparison.Ordinal));
        UpdateOutlineButton();
    }

    private void LayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShellWindowLayoutViewModel.ShowOutlineSidebar) or nameof(ShellWindowLayoutViewModel.IsOutlineVisible) or null)
            UpdateOutlineButton();
    }

    private void UpdateOutlineButton()
    {
        if (_outlineButton is null || _outlineIcon is null || _shell is null) return;
        var visible = _shell.Layout.ShowOutlineSidebar;
        _outlineButton.IsChecked = visible;
        _outlineIcon.Data = MenuIconCatalog.Get(visible ? "panel-right-close" : "panel-right-open");
        var tooltip = Text(visible ? "CollapseStructure" : "Outline");
        ToolTip.SetTip(_outlineButton, tooltip);
        AutomationProperties.SetName(_outlineButton, tooltip);
    }

    // 原型 .appearance-choices strong：12px/15 行高的分节标题。
    /// <summary>按当前主题解析画刷资源，供选中态着色（主题/配色切换会触发 Build 或 Changed 重放）。</summary>
    private IBrush? Brush(string key) => this.TryFindResource(key, out var value) ? value as IBrush : null;

    private static TextBlock SectionTitle(string text, double top = 6) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, LineHeight = 15, Margin = new Thickness(0, top, 0, 0) };

    private Button Pill(string text, Action action)
    {
        var button = new Button { Content = text, Classes = { "appearance-pill" }, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => action();
        return button;
    }

    private Grid PillRow(params (string Text, Action Action, string Id)[] items)
    {
        var row = new Grid { Margin = new Thickness(0, 9, 0, 0) };
        row.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", items.Length)));
        row.ColumnSpacing = 6;
        foreach (var (index, item) in items.Select((item, index) => (index, item)))
        {
            var button = Pill(item.Text, item.Action);
            Grid.SetColumn(button, index);
            row.Children.Add(button);
            if (item.Id == "density-compact") _densityButtons["compact"] = button;
            if (item.Id == "density-normal") _densityButtons["normal"] = button;
            if (item.Id == "density-relaxed") _densityButtons["relaxed"] = button;
        }
        return row;
    }

    private void Build()
    {
        if (_vm is null) return;
        _paletteButtons.Clear(); _modeButtons.Clear(); _densityButtons.Clear(); _languageRows.Clear(); _palettes.Children.Clear();
        AppearanceButton = Button("", () => { });
        AppearanceButton.Classes.Add("titlebar-action");
        AppearanceButton.Content = new PathIcon { Width = 15, Height = 15, Data = Geometry.Parse("M8 1a7 7 0 1 0 0 14h1a2 2 0 0 0 1-3.7.8.8 0 0 1 .4-1.5H12a3 3 0 0 0 3-3C15 3.5 12 1 8 1Z M4.5 6h.1 M7 3.5h.1 M10.5 4h.1 M12 6.5h.1") };
        // 原型 .studio-title-controls：外观摘要为 56×32 / 11px / 左右 7px 的扁平按钮。
        AppearanceButton.Height = 32; AppearanceButton.MinHeight = 0; AppearanceButton.MinWidth = 0;
        AppearanceButton.Padding = new Thickness(0); AppearanceButton.CornerRadius = new CornerRadius(5);
        AppearanceButton.Name = "TitleBarAppearanceButton";
        ToolTip.SetTip(AppearanceButton, Text("Appearance"));
        AutomationProperties.SetName(AppearanceButton, Text("Appearance"));
        LanguageButton = Button("文ᴬ", () => { });
        LanguageButton.Classes.Add("titlebar-action");
        LanguageButton.Width = 32; LanguageButton.Height = 32; LanguageButton.MinHeight = 0;
        LanguageButton.Padding = new Thickness(0); LanguageButton.CornerRadius = new CornerRadius(4);
        LanguageButton.FontSize = 12;
        LanguageButton.Name = "TitleBarLanguageButton";
        ToolTip.SetTip(LanguageButton, Text("MenuLanguage"));
        AutomationProperties.SetName(LanguageButton, Text("MenuLanguage"));
        var densityRow = PillRow(
            (Text("DensityCompact"), () => _vm.SelectedDensity = "compact", "density-compact"),
            (Text("DensityNormal"), () => _vm.SelectedDensity = "normal", "density-normal"),
            (Text("DensityRelaxed"), () => _vm.SelectedDensity = "relaxed", "density-relaxed"));
        var modeRow = PillRow(_vm.ThemeOptions
            .OrderBy(m => m.Key == "light" ? 0 : m.Key == "dark" ? 1 : 2)
            .Select(mode => (Text(mode.Key switch { "light" => "ThemeLight", "dark" => "ThemeDark", _ => "ThemeSystem" }),
                new Action(() => _vm.SelectedTheme = mode), "mode-" + mode.Key))
            .ToArray());
        // PillRow 只回传 density 引用；模式按钮按列序回填字典。
        foreach (var (index, mode) in _vm.ThemeOptions.OrderBy(m => m.Key == "light" ? 0 : m.Key == "dark" ? 1 : 2)
                     .Select((option, position) => (position, option)))
            if (modeRow.Children[index] is Button button) _modeButtons[mode] = button;
        // 原型 .studio-palettes：推荐置顶的 3×3 卡片网格；自定义配色卡替换末尾「＋」块。
        var ordered = _vm.PaletteOptions.OrderBy(option => option.IsRecommended ? 0 : 1).ToArray();
        for (var index = 0; index < ordered.Length; index++)
        {
            var palette = ordered[index];
            var card = BuildPaletteCard(palette);
            Grid.SetRow(card, index / 3); Grid.SetColumn(card, index % 3);
            _palettes.Children.Add(card);
        }
        _addCard = BuildAddCard();
        Grid.SetRow(_addCard, (ordered.Length + 2) / 3); Grid.SetColumn(_addCard, 0);
        // 每行固定 92px；新增卡片紧随最后一个配色行，避免 Auto 行把虚线卡片拉高。
        var paletteRows = (ordered.Length + 2) / 3 + 1;
        _palettes.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat(PaletteCardHeight.ToString(System.Globalization.CultureInfo.InvariantCulture), paletteRows)));
        _palettes.Children.Add(_addCard);
        var content = new StackPanel { Width = MenuWidth - 36, Margin = default };
        densityRow.Margin = new Thickness(0);
        content.Children.Add(densityRow);
        modeRow.Margin = new Thickness(0, 8, 0, 0);
        content.Children.Add(modeRow);
        _palettes.Margin = new Thickness(0, 8, 0, 0);
        // 重建时 _palettes 仍挂在上一版内容面板上，先摘除再入新树。
        if (_palettes.Parent is Panel previousHost) previousHost.Children.Remove(_palettes);
        content.Children.Add(_palettes);
        _scroll = new ScrollViewer { Content = content, MaxHeight = 646, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var appearanceFlyout = new Flyout { Content = _scroll, Placement = PlacementMode.BottomEdgeAlignedRight };
        appearanceFlyout.FlyoutPresenterClasses.Add("design-title-picker");
        appearanceFlyout.Opening += (_, _) => _scroll.MaxHeight = Math.Max(140, Math.Min(646, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 820) - 114));
        appearanceFlyout.Opened += (_, _) => { SetAppearanceMenuOpen(true); AppearanceButton.Classes.Add("title-picker-open"); };
        appearanceFlyout.Closed += (_, _) => { SetAppearanceMenuOpen(false); AppearanceButton.Classes.Remove("title-picker-open"); };
        AppearanceButton.Flyout = appearanceFlyout;
        // 语言菜单保持 248px 菜单标准（外观面板 430px 属内容面板）。
        var languages = new StackPanel { Width = 236, Spacing = 2 };
        _languageRows.Clear();
        foreach (var language in _vm.LanguageOptions)
        {
            // 原型 .studio-language [role=menuitemradio]：24px 语言简写徽标 + 名称 + 右侧 ✓，
            // 选中行 accent-soft 底 + 强调色文字，徽标反白为实心强调色。
            var chipText = new TextBlock { Text = language.CultureName switch { "zh-CN" => "中", "zh-Hant" => "繁", "ja-JP" => "日", _ => "EN" }, FontSize = 11, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var chip = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(7), Child = chipText };
            var name = Label(language.CultureName switch { "zh-CN" => "简体中文", "zh-Hant" => "繁體中文", "ja-JP" => "日本語", _ => "English" }, 13); name.Margin = new Thickness(10, 0, 0, 0); name.VerticalAlignment = VerticalAlignment.Center;
            var check = Label("✓", 13); check.IsVisible = false; check.VerticalAlignment = VerticalAlignment.Center; check.Margin = new Thickness(8, 0, 0, 0);
            var grid = new Grid { ColumnDefinitions = new("Auto,*,Auto"), Children = { chip, name, check } };
            Grid.SetColumn(name, 1); Grid.SetColumn(check, 2);
            var button = Button("", () => { LanguageButton.Flyout?.Hide(); _vm.SelectedLanguage = language; });
            button.Classes.Add("language-item");
            button.Content = grid; button.Height = 38; button.Padding = new Thickness(10, 6);
            button.CornerRadius = new CornerRadius(10);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.HorizontalAlignment = HorizontalAlignment.Stretch; languages.Children.Add(button);
            _languageRows[language] = (button, chip, chipText, name, check);
        }
        Navigation(languages, () => 1);
        var languageFlyout = new Flyout { Content = languages, Placement = PlacementMode.BottomEdgeAlignedRight };
        languageFlyout.FlyoutPresenterClasses.Add("design-language-picker"); LanguageButton.Flyout = languageFlyout;
        _outlineIcon = new ShapePath { Classes = { "wb-icon", "titlebar-outline-icon" }, Width = 15, Height = 15, Stretch = Stretch.Uniform };
        _outlineButton = new ToggleButton
        {
            Content = _outlineIcon,
            Width = 30,
            Height = 28,
            MinHeight = 0,
            MinWidth = 0,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            Classes = { "titlebar-action" },
        };
        _outlineButton.Click += (_, _) => _shell?.Layout.ToggleOutline();
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { AppearanceButton, LanguageButton, _outlineButton } };
        UpdateOutlineButton();
        Changed(null, new(""));
    }

    private Control BuildPaletteCard(PaletteOption palette)
    {
        // 原型 .swatch-dots：4 列网格，点 16px、行列间距 4px（无边距），块高恰为 36px。
        var dots = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnDefinitions = new("Auto,Auto,Auto,Auto"), RowSpacing = 4, ColumnSpacing = 4, HorizontalAlignment = HorizontalAlignment.Left };
        var swatches = new[] { palette.Definition.Light.Paper, palette.Definition.Light.Surface, palette.Definition.Light.Ink, palette.Definition.Light.Accent }
            .Concat(StatusDots).ToArray();
        for (var i = 0; i < swatches.Length; i++)
        {
            var dot = new Ellipse { Width = 16, Height = 16, StrokeThickness = 1 };
            try { dot.Fill = new SolidColorBrush(Color.Parse(swatches[i])); } catch { dot.Fill = Brushes.Transparent; }
            Resource(dot, Shape.StrokeProperty, this, "VexPanelBorderBrush");
            Grid.SetRow(dot, i / 4); Grid.SetColumn(dot, i % 4);
            dots.Children.Add(dot);
        }
        var labels = new StackPanel { Spacing = 1 };
        var name = Label(palette.Name, 13); name.FontWeight = FontWeight.Normal; name.TextWrapping = TextWrapping.NoWrap; name.LineHeight = 19; labels.Children.Add(name);
        var purpose = Label(palette.Purpose); purpose.FontSize = 10; purpose.LineHeight = 14; purpose.Classes.Add("muted"); labels.Children.Add(purpose);
        var body = new StackPanel { Spacing = 6 }; body.Children.Add(dots); body.Children.Add(labels);
        // 原型 .studio-palettes button::after：✓ 固定在卡片右缘垂直居中（right 6px、14px/700 强调色），
        // 点阵用 padding-right 24 让位，这里给正文同样让出右侧空间。
        body.Margin = new Thickness(0, 0, 24, 0);
        var root = new Grid();
        root.Children.Add(body);
        if (palette.IsRecommended)
        {
            var badgeText = new TextBlock { Text = Text("ChoiceRecommended"), FontSize = 9, FontWeight = FontWeight.SemiBold };
            Resource(badgeText, TextBlock.ForegroundProperty, this, "StudioOnPrimary");
            var badge = new Border
            {
                Child = badgeText, CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 2),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 5, 0)
            };
            Resource(badge, Border.BackgroundProperty, this, "VexAccentBrush");
            root.Children.Add(badge);
        }
        var check = new TextBlock { Text = "✓", FontSize = 14, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        Resource(check, TextBlock.ForegroundProperty, this, "VexAccentBrush");
        root.Children.Add(check);
        // 原型选中态 = 透明边框 + 外扩 1.5px 强调色环，由 .palette-card-on 与 Ring 覆层呈现。
        var paletteKey = palette.Key;
        if (paletteKey?.StartsWith("user:", StringComparison.Ordinal) == true)
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 4, 0) };
            var edit = new Button { Classes = { "wb-quiet" }, Padding = new Thickness(4, 1), MinHeight = 22, FontSize = 12, Content = "✎" };
            ToolTip.SetTip(edit, Text("EditCustomPalette"));
            edit.Click += (_, _) => OpenPaletteDialog(palette);
            var remove = new Button { Classes = { "wb-quiet" }, Padding = new Thickness(4, 1), MinHeight = 22, FontSize = 12, Content = "×" };
            ToolTip.SetTip(remove, Text("DeleteCustomPalette"));
            remove.Click += (_, _) => _vm?.RemoveCustomPalette(paletteKey);
            tools.Children.Add(edit); tools.Children.Add(remove);
            root.Children.Add(tools);
        }
        var card = new Button { Classes = { "palette-card" }, Content = root, Height = PaletteCardHeight, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Name = "Palette_" + (paletteKey ?? "unknown") };
        card.Click += (_, _) => { if (_vm is not null) _vm.SelectedPalette = palette; };
        // 原型选中环 box-shadow:0 0 0 1.5px var(--accent)：Button 无 BoxShadow 属性，用外扩 1.5px 的描边 Border 覆层等效呈现。
        var ring = new Border { BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(7.5), Margin = new Thickness(-1.5), IsHitTestVisible = false, IsVisible = false };
        Resource(ring, Border.BorderBrushProperty, this, "VexAccentBrush");
        var host = new Grid { Height = PaletteCardHeight };
        host.Children.Add(card); host.Children.Add(ring);
        _paletteButtons[palette] = (card, check, ring);
        return host;
    }

    private Button BuildAddCard()
    {
        var dash = new Rectangle
        {
            StrokeThickness = 1, RadiusX = 8, RadiusY = 8, Fill = Brushes.Transparent,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 }, IsHitTestVisible = false
        };
        Resource(dash, Shape.StrokeProperty, this, "VexPanelBorderBrush");
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 5 };
        content.Children.Add(new TextBlock { Text = "＋", FontSize = 26, LineHeight = 26, HorizontalAlignment = HorizontalAlignment.Center });
        var label = Label(Text("AddCustomPalette"), 10); label.HorizontalAlignment = HorizontalAlignment.Center; label.Classes.Add("muted");
        content.Children.Add(label);
        var panel = new Panel();
        panel.Children.Add(dash); panel.Children.Add(content);
        // 内容必须纵向拉伸，否则虚线只包住居中的加号和文字，而不是整张卡片。
        var card = new Button
        {
            Classes = { "palette-add-card" }, Content = panel, Height = PaletteCardHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Name = "AddCustomPalette"
        };
        card.Click += (_, _) => OpenPaletteDialog(null);
        ToolTip.SetTip(card, Text("AddCustomPalette"));
        return card;
    }
}
