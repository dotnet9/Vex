using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Avalonia.Markdown.Themes;
using CodeWF.Avalonia.Controls.Controls;
using CodeWF.Toolkit.Lang;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

/// <summary>正文排版弹层（对齐原型 #typography-panel）：标题 + 渲染密度分段条 + 3 列真实排版预览卡。</summary>
public sealed class ShellTypographyGallery : UserControl
{
    private MainWindowViewModel? _vm;
    private readonly UniformGrid _cards = new() { Columns = 3, RowSpacing = 10, ColumnSpacing = 10 };
    private readonly Dictionary<TypographyOption, (Button Button, Border Ring, Border Badge, TextBlock BadgeText, MarkdownViewer Preview)> _items = [];
    private readonly Dictionary<string, Button> _densityPills = [];
    private readonly StackPanel _header = new() { Spacing = 0 };
    private TopLevel? _window;
    private ScrollViewer? _scroll;

    public ShellTypographyGallery()
    {
        // UserControl 默认裁剪子内容。阴影画在弹层边界外，裁剪后看起来就像没有阴影。
        ClipToBounds = false;
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { Attach(); _window = TopLevel.GetTopLevel(this); if (_window is not null) _window.SizeChanged += WindowSizeChanged; Resize(); };
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) { _vm.Appearance.PropertyChanged -= Changed; _vm.Workbench.PropertyChanged -= WorkbenchChanged; } I18nManager.Instance.CultureChanged -= CultureChanged; if (_window is not null) _window.SizeChanged -= WindowSizeChanged; _vm = null; _window = null; };
        Navigation(_cards, () => 3);
    }

    private void WindowSizeChanged(object? sender, SizeChangedEventArgs e) => Resize();

    private void Attach()
    {
        var vm = DataContext as MainWindowViewModel;
        if (ReferenceEquals(vm, _vm)) return;
        if (_vm is not null) { _vm.Appearance.PropertyChanged -= Changed; _vm.Workbench.PropertyChanged -= WorkbenchChanged; }
        I18nManager.Instance.CultureChanged -= CultureChanged;
        _vm = vm;
        if (vm is null) return;
        vm.Appearance.PropertyChanged += Changed; vm.Workbench.PropertyChanged += WorkbenchChanged; I18nManager.Instance.CultureChanged += CultureChanged; Build();
    }

    private void WorkbenchChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ShellWorkbenchViewModel.ShowTypography) && _vm?.Workbench.ShowTypography == true) Resize(); }

    private void CultureChanged(object? sender, EventArgs e)
    {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (_vm is not null) Build(); });
            return;
        }
        Build();
    }

    private void Resize()
    {
        Width = Math.Min(880, (_window?.ClientSize.Width ?? 1280) - 36);
        var anchor = (_window as MainWindow)?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "TypographyTarget" && b.IsEffectivelyVisible);
        var top = anchor?.TranslatePoint(new Point(0, anchor.Bounds.Height + 14), _window!)?.Y ?? 132;
        var parentTop = _window is not null ? this.GetVisualParent()?.TranslatePoint(default, _window)?.Y ?? 0 : 0;
        Margin = new Thickness(18, Math.Max(0, top - parentTop), 18, 18);
        Height = Math.Max(140, Math.Min(680, (_window?.ClientSize.Height ?? 820) - top - 18));
    }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        if (e.PropertyName is nameof(ShellAppearanceViewModel.MarkdownDensity) or null or "")
            foreach (var (key, pill) in _densityPills) PaintDensity(pill, key == _vm.Appearance.MarkdownDensity);
        foreach (var (option, item) in _items)
        {
            var selected = option == _vm.Appearance.SelectedTypography;
            item.Ring.IsVisible = selected;
            var badgeText = selected ? "✓ " + Text("ChoiceCurrent") : option.IsRecommended ? Text("ChoiceRecommended") : "";
            item.BadgeText.Text = badgeText; item.Badge.IsVisible = badgeText.Length > 0;
            item.Preview.TypographySize = _vm.Appearance.CurrentTypographySize;
        }
    }

    private void PaintDensity(Button pill, bool on)
    {
        pill.Classes.Set("appearance-pill-on", on);
        pill.Background = on ? null! : Brushes.Transparent;
    }

    private void Build()
    {
        if (_vm is null) return;
        // 重建时字段级容器仍挂在上一版树上，先摘除再入新树。
        if (_header.Parent is Panel oldHeaderHost) oldHeaderHost.Children.Remove(_header);
        if (_scroll is not null) _scroll.Content = null;
        _items.Clear(); _densityPills.Clear(); _cards.Children.Clear(); _header.Children.Clear();

        var titleRow = new Grid { ColumnDefinitions = new("*,Auto") };
        var title = Label(Text("WBTypographyTitle"), 15); title.FontWeight = FontWeight.Normal; title.VerticalAlignment = VerticalAlignment.Center; titleRow.Children.Add(title);
        var close = new Button { Classes = { "vex-dismiss" } };
        Avalonia.Automation.AutomationProperties.SetName(close, Text("Close"));
        close.Click += (_, _) => { _vm.Workbench.ShowTypography = false; (_window as MainWindow)?.FindControl<ShellEditorOptionsView>("EditorOptions")?.FindControl<Button>("TypographyTarget")?.Focus(); };
        Grid.SetColumn(close, 1); titleRow.Children.Add(close);
        _header.Children.Add(titleRow);

        // 三枚等宽胶囊（flex:1）：从排版弹层内容左侧对齐。
        var densityRow = new Grid { ColumnDefinitions = new("*"), Height = 52 };
        var pills = new UniformGrid { Columns = 3, Width = 320, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        foreach (var (key, labelKey) in new[] { ("compact", "DensityCompact"), ("normal", "DensityNormal"), ("relaxed", "DensityRelaxed") })
        {
            var pill = new Button { Classes = { "appearance-pill", "typography-density" }, Content = Text(labelKey), FontWeight = FontWeight.Normal, HorizontalAlignment = HorizontalAlignment.Stretch };
            var captured = key;
            pill.Click += (_, _) => _vm.Appearance.MarkdownDensity = captured;
            _densityPills[key] = pill; pills.Children.Add(pill);
        }
        densityRow.Children.Add(pills);
        var densityFrame = new Border { Child = densityRow, BorderThickness = new Thickness(0, 0, 0, 1) };
        Resource(densityFrame, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        _header.Children.Add(densityFrame);

        foreach (var option in _vm.Appearance.TypographyOptions) _cards.Children.Add(BuildCard(option));
        _cards.Children.Add(BuildAddCard());

        // 右侧预留约 15px 滚动条槽（原型 scrollbar-gutter:stable），保证三列卡宽 269。
        _scroll = new ScrollViewer { Content = _cards, Padding = new Thickness(2, 2, 15, 2), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalAlignment = VerticalAlignment.Stretch };
        var grid = new Grid { RowDefinitions = new("Auto,*") };
        grid.Children.Add(_header); Grid.SetRow(_scroll, 1); grid.Children.Add(_scroll);
        // 圆角、边框和阴影交给 Border.wb-popover，与底部布局弹层保持同一套样式。
        var border = new Border { Classes = { "wb-popover" }, Child = grid, Padding = new Thickness(18) };
        Resource(border, Border.BackgroundProperty, this, "VexElevatedBackgroundBrush");
        Content = border;
        Resize(); Changed(null, new(""));
    }

    /// <summary>卡片即预览：名称用普通字重，正文按该排版真实样式渲染说明、三级标题和引用。</summary>
    private Control BuildCard(TypographyOption option)
    {
        var preview = new MarkdownViewer
        {
            Markdown = $"{Text(option.PurposeKey)}\n\n### 维刻 Vex\n\n> {Text("AboutShortSlogan")}",
            TypographyTheme = option.Key,
            FontWeight = FontWeight.Normal,
            TypographySize = MarkdownTypographySizes.Compact,
            IsHitTestVisible = false,
            Margin = new Thickness(14, 2, 14, 12),
            DocumentBottomPadding = 0,
            EnableVirtualization = false
        };
        var name = new TextBlock
        {
            Text = Text(option.NameKey),
            FontSize = 13,
            FontWeight = FontWeight.Normal,
            Margin = new Thickness(14, 10, 14, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Resource(name, TextBlock.ForegroundProperty, this, "VexValueBrush");
        var paper = new Border { Child = new StackPanel { Children = { name, preview } }, ClipToBounds = true, CornerRadius = new CornerRadius(10) };
        Resource(paper, Border.BackgroundProperty, this, "VexDocumentPaperBrush");
        var badgeText = new TextBlock { FontSize = 9, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White };
        var badge = new Border
        {
            Child = badgeText,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 5, 0), Padding = new Thickness(4, 2),
            CornerRadius = new CornerRadius(4), IsHitTestVisible = false
        };
        Resource(badge, Border.BackgroundProperty, this, "VexAccentBrush");
        // 原型选中态：box-shadow 0 0 0 1.5px accent，环在卡片边缘外侧。
        var ring = new Border { BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(11.5), Margin = new Thickness(-1.5), IsHitTestVisible = false };
        Resource(ring, Border.BorderBrushProperty, this, "VexAccentBrush");
        var content = new Panel { Children = { paper, ring, badge } };
        var button = Button("", () => _vm!.Appearance.SelectedTypography = option);
        button.Name = "Typography_" + option.Key;
        button.Content = content; button.Padding = default; button.MinHeight = 262; button.Height = 262;
        button.ClipToBounds = false;
        button.VerticalAlignment = VerticalAlignment.Stretch; button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
        button.BorderThickness = new Thickness(0); button.CornerRadius = new CornerRadius(10);
        Resource(button, TemplatedControl.BackgroundProperty, this, "VexPanelBackgroundBrush");
        _items[option] = (button, ring, badge, badgeText, preview);
        if (option.Key?.StartsWith("user:", StringComparison.Ordinal) == true)
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 8, 0), IsVisible = false };
            var captured = option.Key!;
            var remove = new Button { Classes = { "wb-quiet" }, Width = 22, Height = 22, Padding = default, FontSize = 12, Content = "×" };
            ToolTip.SetTip(remove, Text("DeleteCustomTypography"));
            remove.Click += (_, _) => _vm!.Appearance.RemoveCustomTypography(captured);
            tools.Children.Add(remove);
            ((Panel)button.Content!).Children.Add(tools);
            button.PointerEntered += (_, _) => { if (button.Content is Panel p) foreach (var c in p.Children.OfType<StackPanel>()) c.IsVisible = true; };
            button.PointerExited += (_, _) => { if (button.Content is Panel p) foreach (var c in p.Children.OfType<StackPanel>()) c.IsVisible = false; };
        }
        return button;
    }

    /// <summary>虚线「＋ 新增排版」整卡（原型 .typography-add：262 高、虚线框、居中）。</summary>
    private Control BuildAddCard()
    {
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };
        var plus = new TextBlock { Text = "＋", FontSize = 34, LineHeight = 34, HorizontalAlignment = HorizontalAlignment.Center };
        var addLabel = new TextBlock { Text = Text("AddCustomTypography"), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center };
        Resource(plus, TextBlock.ForegroundProperty, this, "VexMutedBrush"); Resource(addLabel, TextBlock.ForegroundProperty, this, "VexMutedBrush");
        stack.Children.Add(plus);
        stack.Children.Add(addLabel);
        var dash = new Avalonia.Controls.Shapes.Rectangle { StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }, RadiusX = 10, RadiusY = 10, StrokeThickness = 1, IsHitTestVisible = false };
        Resource(dash, Avalonia.Controls.Shapes.Shape.StrokeProperty, this, "VexPanelBorderBrush");
        var card = Button("", OpenTypographyDialog);
        card.Name = "AddCustomTypography";
        card.Content = new Panel { Children = { dash, stack } };
        card.Padding = default; card.MinHeight = 262; card.Height = 262;
        card.HorizontalContentAlignment = HorizontalAlignment.Stretch; card.VerticalContentAlignment = VerticalAlignment.Stretch;
        card.HorizontalAlignment = HorizontalAlignment.Stretch; card.VerticalAlignment = VerticalAlignment.Stretch;
        card.BorderThickness = new Thickness(0); card.CornerRadius = new CornerRadius(10);
        card.Classes.Add("palette-add-card");
        return card;
    }

    async void OpenTypographyDialog()
    {
        if (_vm is null) return;
        var model = new ShellTypographyEditModel(null);
        var applied = await Ursa.Controls.OverlayDialog.ShowCustomAsync<bool>(new ShellTypographyEditView(model), model,
            options: new Ursa.Controls.OverlayDialogOptions { CanLightDismiss = true, CanDragMove = false, CanResize = false, IsCloseButtonVisible = false });
        if (!applied || model.Result is not { } result) return;
        _vm.Appearance.RegisterCustomTypography("", result.Name, result.AccentLight, result.AccentDark, result.DecoLight, result.DecoDark, result.Font, result.Leading);
        Build();
    }
}
