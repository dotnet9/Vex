using CodeWF.Avalonia.Controls.Helpers;
using System.ComponentModel;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using CodeWF.Toolkit.Lang;
using CodeWF.Avalonia.Controls.Controls;
using Vex.Controls;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

public sealed class ShellAppearanceMenuView : UserControl
{
    private ShellAppearanceViewModel? _vm;
    private readonly Grid _palettes = new() { ColumnDefinitions = new("*,*,*"), ColumnSpacing = 6, RowSpacing = 6, Margin = new Thickness(2), ClipToBounds = false }; // Margin 容纳选中 Ring 外扩，防边缘裁切
    private readonly Dictionary<PaletteOption, (Button Button, TextBlock Check, Border Ring)> _paletteButtons = [];
    private readonly Dictionary<string, Button> _modeButtons = [];
    private readonly Dictionary<string, Button> _densityButtons = [];
    private readonly Dictionary<LanguageOption, (Button Button, Border Chip, TextBlock ChipText, TextBlock Name, TextBlock Check)> _languageRows = [];
    private Button? _addCard;
    private ScrollViewer? _scroll;
    public Button AppearanceButton { get; private set; } = null!;
    public Button LanguageButton { get; private set; } = null!;
    public const double MenuWidth = 430;
    /// <summary>原型 *{box-sizing:border-box}：430 是含 14 内距与 1px 发丝描边的外框宽，
    /// 内容面板按 430 − 2×(14+1) 收口，弹层才不会被内容宽顶成 432。</summary>
    private const double MenuInset = 15;
    /// <summary>语言菜单沿用 248 菜单标准外框（workspace-chrome.css:6 --title-menu-width）。</summary>
    public const double LanguageMenuWidth = 248;

    /// <summary>原型 .density-options button.on 与卡片选中环共用的强调色派生点。</summary>
    private static readonly string[] StatusDots = ["#DC2626", "#15803D", "#B45309", "#4338CA"];
    private static readonly string[] DensityKeys = ["compact", "normal", "relaxed"];

    public ShellAppearanceMenuView()
    {
        Navigation(_palettes, () => 3);
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => Attach();
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) _vm.PropertyChanged -= Changed; I18nManager.Instance.CultureChanged -= CultureChanged; _vm = null; };
    }

    private void Attach()
    {
        var vm = (DataContext as MainWindowViewModel)?.Appearance;
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null) _vm.PropertyChanged -= Changed;
        I18nManager.Instance.CultureChanged -= CultureChanged;
        _vm = vm;
        if (vm is null) return;
        vm.PropertyChanged += Changed; I18nManager.Instance.CultureChanged += CultureChanged;
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
        foreach (var (key, button) in _modeButtons) SegmentedPanel.SetIsActive(button, key == _vm.SelectedTheme?.Key);
        foreach (var (key, button) in _densityButtons) SegmentedPanel.SetIsActive(button, key == _vm.SelectedDensity);
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
    }

    // 原型 .appearance-choices strong：12px/15 行高的分节标题。
    /// <summary>按当前主题解析画刷资源，供选中态着色（主题/配色切换会触发 Build 或 Changed 重放）。</summary>
    private IBrush? Brush(string key) => this.TryFindResource(key, out var value) ? value as IBrush : null;

    private static TextBlock SectionTitle(string text, double top = 6) => new() { Text = text, FontSize = 12, FontWeight = FontWeight.SemiBold, LineHeight = 15, Margin = new Thickness(0, top, 0, 0) };

    /// <summary>原型 .density-options.vex-segmented（workspace-chrome.css:330）：外观菜单里界面密度与明暗模式两行
    /// 用的是同一枚标准分段控件——gap 6、button{flex:1} 等宽，轨道、滑动指示块、选中字色字重全部走 Vex.Controls
    /// 的共享配方，宿主不再自画选中底色。</summary>
    private Border SegmentRow(string? automationPrefix, Action<Button, string> register, params (string Text, Action Action, string Id)[] items)
    {
        var segments = new SegmentedPanel { Spacing = 6, FillItems = true };
        foreach (var item in items)
        {
            var button = new Button { Content = item.Text, Classes = { "density-pill" }, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) => item.Action();
            Avalonia.Automation.AutomationProperties.SetName(button, item.Text);
            if (automationPrefix is not null) Avalonia.Automation.AutomationProperties.SetAutomationId(button, automationPrefix + item.Text);
            segments.Children.Add(button);
            register(button, item.Id);
        }
        return new Border { Classes = { "segmented-track", "density-options" }, Child = segments, Margin = new Thickness(0, 6, 0, 2) };
    }

    private void Build()
    {
        if (_vm is null) return;
        _paletteButtons.Clear(); _modeButtons.Clear(); _densityButtons.Clear(); _languageRows.Clear(); _palettes.Children.Clear();
        AppearanceButton = Button("", () => { });
        // 原型 #vex-appearance-menu>summary：只留 15px 对比度图标（i-contrast：描线圆 + 实心右半），
        // 内距 7/3、4px 圆角；弹出层打开时 accent-soft 底 + 强调色图标（title-picker-open）。
        AppearanceButton.Classes.Add("tb-glyph");
        // 用户提供的新图标（主题色.svg，1024 视图框面性双色），整体填充前景色随状态换。
        // 用户提供的新图标（24 基准预缩放坐标）：主题色两笔合入 GeometryGroup 统一均匀适配，
        // 地球单路径；填充前景色随状态换（glyph-fill）。
        var contrastGeometry = new Avalonia.Media.GeometryGroup { Children =
        {
            Geometry.Parse("M12 22.5C6.2 22.5 1.5 17.8 1.5 12S6.2 1.5 12 1.5 22.5 6.2 22.5 12c0 1.9 -2.1 2.3 -4 2.7 -1.3 0.3 -2.6 0.5 -3.1 1.2 -0.7 0.8 -0.8 2 -0.9 3.1 -0.2 1.6 -0.3 3.5 -2.5 3.5zm0 -20c-5.2 0 -9.5 4.3 -9.5 9.5s4.3 9.5 9.5 9.5c1.1 0 1.3 -0.8 1.5 -2.6 0.1 -1.3 0.2 -2.7 1.1 -3.7 0.8 -0.9 2.3 -1.2 3.7 -1.5 1.9 -0.4 3.2 -0.7 3.2 -1.7 0 -5.2 -4.3 -9.5 -9.5 -9.5z"),
            Geometry.Parse("M14 9c-1.1 0 -2 -0.9 -2 -2s0.9 -2 2 -2 2 0.9 2 2 -0.9 2 -2 2zm0 -3c-0.6 0 -1 0.4 -1 1s0.4 1 1 1 1 -0.4 1 -1 -0.4 -1 -1 -1zM8 11c-1.1 0 -2 -0.9 -2 -2s0.9 -2 2 -2 2 0.9 2 2 -0.9 2 -2 2zm0 -3c-0.6 0 -1 0.4 -1 1s0.4 1 1 1 1 -0.4 1 -1 -0.4 -1 -1 -1zM8.5 17.5c-1.1 0 -2 -0.9 -2 -2s0.9 -2 2 -2 2 0.9 2 2 -0.9 2 -2 2zm0 -3c-0.6 0 -1 0.4 -1 1 0 0.5 0.4 1 1 1s1 -0.5 1 -1 -0.4 -1 -1 -1z")
        }};
        AppearanceButton.Content = new Avalonia.Controls.Shapes.Path
        {
            Width = 14, Height = 14, Stretch = Stretch.Uniform,
            Data = contrastGeometry, Classes = { "glyph-fill" }
        };
        AppearanceButton.MinHeight = 0; AppearanceButton.MinWidth = 0;
        AppearanceButton.Padding = new Thickness(4); AppearanceButton.CornerRadius = new CornerRadius(4);
        AppearanceButton.Name = "TitleBarAppearanceButton";
        ToolTipHelper.SetText(AppearanceButton, Text("Appearance"));
        Avalonia.Automation.AutomationProperties.SetName(AppearanceButton, Text("Appearance"));
        // 原型 .studio-language summary：纯图标钮，悬停 hover 底。
        LanguageButton = Button("", () => { });
        LanguageButton.Classes.Add("tb-glyph");
        LanguageButton.Content = new Avalonia.Controls.Shapes.Path
        {
            Width = 14, Height = 14, Stretch = Stretch.Uniform,
            Data = Geometry.Parse("M16.48 13.56l1.54 4.31H14.92l1.56 -4.31zm-1.31 -3.09L10.36 23.04h2.68l1.1 -3.02h4.67L19.9 23.04h2.68L17.77 10.47H15.16zM11 11.66c1.38 -1.63 2.43 -3.51 3.07 -5.55h2.42v-2.15H10.52a13.62 13.62 0 0 0 -1.5 -2.51l-0.12 -0.15 -2.18 0.79 0.26 0.29c0.43 0.49 0.83 1.02 1.17 1.58H2.18v2.15h2.44c0.65 2.12 1.78 4.05 3.31 5.66 -1.72 1.32 -3.71 2.27 -5.82 2.77l-0.35 0.09 1.16 1.91 0.19 -0.05c2.33 -0.63 4.5 -1.75 6.36 -3.3 0.79 0.63 1.63 1.2 2.52 1.7l0.9 -1.96c-0.66 -0.38 -1.29 -0.79 -1.89 -1.25m-1.55 -1.37a12.14 12.14 0 0 1 -2.7 -4.18h5.08c-0.52 1.53 -1.33 2.95 -2.38 4.18"), Classes = { "glyph-fill" }
        };
        LanguageButton.Width = 24; LanguageButton.Height = 24; LanguageButton.MinHeight = 0; LanguageButton.MinWidth = 0;
        LanguageButton.Padding = new Thickness(0); LanguageButton.CornerRadius = new CornerRadius(0);
        LanguageButton.Name = "TitleBarLanguageButton";
        ToolTipHelper.SetText(LanguageButton, Text("TitleLanguage"));
        Avalonia.Automation.AutomationProperties.SetName(LanguageButton, Text("TitleLanguage"));
        var densityRow = SegmentRow("Density_", (button, id) => _densityButtons[id] = button,
            (Text("DensityCompact"), () => _vm.SelectedDensity = "compact", "compact"),
            (Text("DensityNormal"), () => _vm.SelectedDensity = "normal", "normal"),
            (Text("DensityRelaxed"), () => _vm.SelectedDensity = "relaxed", "relaxed"));
        // 原型 theme.js：明暗模式行与密度行同为 .density-options.vex-segmented，无独立样式。
        var modeRow = SegmentRow("Mode_", (button, id) => _modeButtons[id] = button, _vm.ThemeOptions
            .OrderBy(m => m.Key == "light" ? 0 : m.Key == "dark" ? 1 : 2)
            .Select(mode => (Text(mode.Key switch { "light" => "ThemeLight", "dark" => "ThemeDark", _ => "ThemeSystem" }),
                new Action(() => _vm.SelectedTheme = mode), mode.Key))
            .ToArray());
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
        _palettes.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", (ordered.Length + 3) / 3 + 1)));
        _palettes.Children.Add(_addCard);
        var content = new StackPanel { Width = MenuWidth - 2 * MenuInset, Margin = default };
        content.Children.Add(SectionTitle(Text("AppearanceDensityTitle"), 4));
        content.Children.Add(densityRow);
        content.Children.Add(SectionTitle(Text("AppearanceModeTitle")));
        content.Children.Add(modeRow);
        content.Children.Add(SectionTitle(Text("WBTheme")));
        _palettes.Margin = new Thickness(0, 10, 0, 0);
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
        // 语言菜单保持 248px 菜单标准（外观面板 430px 属内容面板）：248 − 2×(6 内距 + 1 描边)。
        var languages = new StackPanel { Width = LanguageMenuWidth - 14, Spacing = 2 };
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
            button.Content = grid; button.Height = 36; button.Padding = new Thickness(10, 6);
            button.CornerRadius = new CornerRadius(10);
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.HorizontalAlignment = HorizontalAlignment.Stretch; languages.Children.Add(button);
            _languageRows[language] = (button, chip, chipText, name, check);
        }
        Navigation(languages, () => 1);
        var languageFlyout = new Flyout { Content = languages, Placement = PlacementMode.BottomEdgeAlignedRight };
        languageFlyout.FlyoutPresenterClasses.Add("design-language-picker"); LanguageButton.Flyout = languageFlyout;
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { AppearanceButton, LanguageButton } };
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
        // 原型口径：配色卡只留名称行（不加粗），用途说明行移除。
        var name = Label(palette.Name, 13); name.FontWeight = FontWeight.Normal; name.TextWrapping = TextWrapping.NoWrap; name.LineHeight = 19; labels.Children.Add(name);
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
        // 原型 .studio-palettes button::after：勾选钉在卡片右下角（right/bottom 5px），11px 常规字重强调色。
        var check = new TextBlock { Text = "✓", FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 5, 5) };
        Resource(check, TextBlock.ForegroundProperty, this, "VexAccentBrush");
        root.Children.Add(check);
        // 原型选中态 = accent 描边 + 外扩 1px 强调色环，由 .palette-card-on 与 Ring 覆层呈现。
        var paletteKey = palette.Key;
        if (paletteKey?.StartsWith("user:", StringComparison.Ordinal) == true)
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 4, 0) };
            var edit = new Button { Classes = { "wb-quiet" }, Padding = new Thickness(4, 1), MinHeight = 22, FontSize = 12, Content = "✎" };
            ToolTipHelper.SetText(edit, Text("EditCustomPalette"));
            edit.Click += (_, _) => OpenPaletteDialog(palette);
            var remove = new Button { Classes = { "wb-quiet" }, Padding = new Thickness(4, 1), MinHeight = 22, FontSize = 12, Content = "×" };
            ToolTipHelper.SetText(remove, Text("DeleteCustomPalette"));
            remove.Click += (_, _) => _vm?.RemoveCustomPalette(paletteKey);
            tools.Children.Add(edit); tools.Children.Add(remove);
            root.Children.Add(tools);
        }
        var card = new Button { Classes = { "palette-card" }, Content = root, Height = 92, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Name = "Palette_" + (paletteKey ?? "unknown") };
        Avalonia.Automation.AutomationProperties.SetName(card, palette.Name + "，" + palette.Purpose);
        Avalonia.Automation.AutomationProperties.SetAutomationId(card, "PaletteCard_" + paletteKey);
        card.Click += (_, _) => { if (_vm is not null) _vm.SelectedPalette = palette; };
        // 原型选中环 box-shadow:0 0 0 1px var(--accent)：Button 无 BoxShadow 属性，用外扩 1px 的描边 Border 覆层等效呈现。
        var ring = new Border { BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Margin = new Thickness(-1), IsHitTestVisible = false, IsVisible = false };
        Resource(ring, Border.BorderBrushProperty, this, "VexAccentBrush");
        var host = new Grid { Margin = new Thickness(2), ClipToBounds = false }; // 余量容纳 Ring 的 -1.5 外扩，避免边缘裁切
        host.Children.Add(card); host.Children.Add(ring);
        _paletteButtons[palette] = (card, check, ring);
        return host;
    }

    private Button BuildAddCard()
    {
        var dash = new Rectangle
        {
            StrokeThickness = 1, RadiusX = 6, RadiusY = 6, Fill = Brushes.Transparent,
            StrokeDashArray = new AvaloniaList<double> { 4, 3 }, IsHitTestVisible = false
        };
        Resource(dash, Shape.StrokeProperty, this, "VexPanelBorderBrush");
        // 原型 .palette-add：1px dashed --border、--vex-radius-sm(6px) 圆角，悬停整块换成强调色。
        dash.Classes.Add("palette-add-dash");
        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 5 };
        content.Children.Add(new TextBlock { Text = "＋", FontSize = 26, LineHeight = 26, HorizontalAlignment = HorizontalAlignment.Center });
        var label = Label(Text("AddCustomPalette"), 10); label.HorizontalAlignment = HorizontalAlignment.Center; label.Classes.Add("muted");
        content.Children.Add(label);
        var panel = new Panel();
        panel.Children.Add(dash); panel.Children.Add(content);
        var card = new Button { Classes = { "palette-add-card" }, Content = panel, Height = 92, VerticalAlignment = VerticalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Name = "AddCustomPalette" };
        card.Click += (_, _) => OpenPaletteDialog(null);
        ToolTipHelper.SetText(card, Text("AddCustomPalette"));
        return card;
    }
}
