using CodeWF.Avalonia.Controls.Helpers;
using System.ComponentModel;
using System.Reactive.Linq;
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
using Vex.Controls;
using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;
using static Vex.Modules.Shell.Views.DesignSelectionControls;

namespace Vex.Modules.Shell.Views;

/// <summary>正文排版弹层（对齐原型 #typography-panel）：渲染密度分段条 + 4 列真实排版预览卡，弹层自身不带标题。</summary>
public sealed class ShellTypographyGallery : UserControl
{
    private MainWindowViewModel? _vm;
    private readonly Grid _cards = new() { Classes = { "typography-cards" }, ColumnDefinitions = new("*,*,*,*"), RowSpacing = 8, ColumnSpacing = 8 };
    private readonly Dictionary<TypographyOption, (Button Button, Border Ring, Border Badge, TextBlock BadgeText)> _items = [];
    private readonly Dictionary<string, Button> _densityPills = [];
    private TopLevel? _window;
    private ScrollViewer? _scroll;
    private bool _typographyDialogBusy;

    public ShellTypographyGallery()
    {
        ClipToBounds = false;
        UseLayoutRounding = false;
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { Attach(); _window = TopLevel.GetTopLevel(this); if (_window is not null) _window.SizeChanged += WindowSizeChanged; Resize(); };
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) { _vm.Appearance.PropertyChanged -= Changed; _vm.Workbench.PropertyChanged -= WorkbenchChanged; } I18nManager.Instance.CultureChanged -= CultureChanged; if (_window is not null) _window.SizeChanged -= WindowSizeChanged; _vm = null; _window = null; };
        Navigation(_cards, () => 4);
    }

    // SizeChanged 在布局中途触发：此刻锚点已按新尺寸落位而 ClientSize 仍是旧值，
    // 当场重算会把高度钉在旧窗口的量程上，等布局走完再读一次才是最终几何。
    private void WindowSizeChanged(object? sender, SizeChangedEventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(Resize);

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
        // 原型 typography.js:183 position()：宽 720（含 1px 描边），左沿贴触发钮右沿、顶边 = 钮底 + 8，
        // 左右各留 8 夹回宿主，高度按宿主剩余空间封顶 680。换算全部在宿主坐标系里做，弹层才不跟窗口内距较劲。
        var host = this.GetVisualParent();
        // 首次 Build 早于布局：宿主 Bounds 还是 0，Width 会收到负值，所以未测量的量程退回窗口可视宽。
        var hostWidth = host is { Bounds.Width: > 0 } ? host.Bounds.Width : _window?.ClientSize.Width ?? 1280;
        var hostHeight = host is { Bounds.Height: > 0 } ? host.Bounds.Height : _window?.ClientSize.Height ?? 820;
        Width = Math.Min(720, hostWidth - 16);
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        var anchor = (_window as MainWindow)?.WorkspaceViews.ActiveTypographyTrigger;
        // TranslatePoint 收的是控件自己的局部坐标，Bounds.Right/Bottom 含父级偏移，直接拿来当局部点会多算一个自身位置。
        var corner = anchor is not null && host is not null
            ? anchor.TranslatePoint(new Point(anchor.Bounds.Width, anchor.Bounds.Height + 8), host)
            : null;
        var left = corner is null ? 12 : Math.Max(8, Math.Min(hostWidth - Width - 8, corner.Value.X - Width));
        var top = corner is null ? 126 : Math.Max(8, corner.Value.Y);
        Margin = new Thickness(left, top, 0, 0);
        Height = Math.Max(140, Math.Min(680, hostHeight - top - 12));
    }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        // 目录一变就整块重建：原型 renderGrid() 跟着每次增删改走。宿主里增删来自三条路径
        // （浮层新增、卡片删除、启动恢复），只让浮层那条负责重建会让删掉的卡片继续留在开着的弹层里。
        if (e.PropertyName == nameof(ShellAppearanceViewModel.TypographyOptions)) { Build(); return; }
        if (e.PropertyName is nameof(ShellAppearanceViewModel.MarkdownDensity) or null or "")
            foreach (var (key, pill) in _densityPills) PaintDensity(pill, key == _vm.Appearance.MarkdownDensity);
        foreach (var (option, item) in _items)
        {
            var selected = option == _vm.Appearance.SelectedTypography;
            item.Ring.IsVisible = selected;
            item.Button.Classes.Set("typography-card-on", selected);
            var badgeText = selected ? "✓ " + Text("ChoiceCurrent") : option.IsRecommended ? Text("ChoiceRecommended") : "";
            item.BadgeText.Text = badgeText; item.Badge.IsVisible = badgeText.Length > 0;
        }
    }

    private static void PaintDensity(Button pill, bool on) => SegmentedPanel.SetIsActive(pill, on);

    /// <summary>选卡应用后 650ms 自动收起；编辑/删除/新增的对话框开着时不收（点工具也会冒泡到卡片）。</summary>
    private void ScheduleCollapse() => Avalonia.Threading.DispatcherTimer.RunOnce(() =>
    {
        if (_vm is { Workbench.ShowTypography: true } && !_typographyDialogBusy) _vm.Workbench.ShowTypography = false;
    }, TimeSpan.FromMilliseconds(650));

    private void Build()
    {
        if (_vm is null) return;
        // 重建时字段级容器仍挂在上一版树上，先摘除再入新树。
        if (_scroll is not null) _scroll.Content = null;
        // 网格与新增条同挂在重建的 body 下，摘掉旧父级才能再次入树。
        if (_cards.Parent is Panel oldBody) oldBody.Children.Remove(_cards);
        _items.Clear(); _densityPills.Clear(); _cards.Children.Clear(); _cards.RowDefinitions.Clear();

        // 渲染密度：原型 .typography-density 里只有 .density-options.vex-segmented 一枚轨道（320×30，
        // 距行顶 11、距发丝线 10），标题与「渲染密度」四个字都不存在——作用域只写进轨道的可访问名，
        // 对应原型 aria-label="Markdown 渲染密度"。轨道、滑动指示块、选中字色走 Vex.Controls 共享配方。
        var segments = new SegmentedPanel { Spacing = 6, FillItems = true };
        foreach (var (key, labelKey) in new[] { ("compact", "DensityCompact"), ("normal", "DensityNormal"), ("relaxed", "DensityRelaxed") })
        {
            var pill = new Button { Classes = { "density-pill", "render-density-pill" }, Content = Text(labelKey), HorizontalContentAlignment = HorizontalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(pill, Text(labelKey));
            var captured = key;
            pill.Click += (_, _) => _vm.Appearance.MarkdownDensity = captured;
            _densityPills[key] = pill; segments.Children.Add(pill);
        }
        var track = new Border { Classes = { "segmented-track", "density-options" }, Child = segments, Margin = new Thickness(0, 9, 0, 0), Width = 320, MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        Avalonia.Automation.AutomationProperties.SetName(track, Text("RenderDensityTitle"));
        var densityFrame = new Border { Classes = { "typography-density" }, Child = track, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(2, 2, 2, 10) };
        Resource(densityFrame, Border.BorderBrushProperty, this, "VexPanelBorderBrush");

        foreach (var option in _vm.Appearance.TypographyOptions)
        {
            var index = _cards.Children.Count;
            if (index % 4 == 0) _cards.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var card = BuildCard(option);
            Grid.SetRow(card, index / 4); Grid.SetColumn(card, index % 4);
            _cards.Children.Add(card);
        }

        // 新增条横跨整行；卡片各行按真实预览的内容高度排列。
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(_cards);
        body.Children.Add(BuildAddCard());

        // 右侧预留约 15px 滚动条槽（原型 scrollbar-gutter:stable），保证四列卡宽 ~161。
        _scroll = new ScrollViewer { Classes = { "typography-grid" }, Content = body, Padding = new Thickness(2, 2, 14, 2), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalAlignment = VerticalAlignment.Stretch };
        var layout = new Grid { RowDefinitions = new("Auto,*") };
        layout.Children.Add(densityFrame); Grid.SetRow(_scroll, 1); layout.Children.Add(_scroll);
        // 原型 .typography-popover 实测吃浮层一档（14 内距、8 圆角、--shadow-pop 双层），
        // 面/描边/圆角/投影都在 wb-popover 里，宿主只给内距。
        var border = new Border { Classes = { "wb-popover" }, Child = layout, Padding = new Thickness(14) };
        Content = border;
        Resize(); Changed(null, new(""));
    }

    /// <summary>卡片即预览：以该排版真实样式渲染「## 名称 / 说明 / 引用」（原型已去掉 ### 行，窄卡才装得下三行）。</summary>
    private Control BuildCard(TypographyOption option)
    {
        var name = Text(option.NameKey);
        var purpose = Text(option.PurposeKey);
        var quote = Text("AboutShortSlogan");
        var preview = new TypographyCardPreview(option.Key, name, purpose, quote);
        // Button 的矩形裁切不会裁掉纸面的四角；纸面自身沿卡片圆角裁切。
        var paper = new Border { Child = preview, ClipToBounds = true, BorderThickness = new Thickness(0, 1, 0, 0) };
        paper.Bind(Border.CornerRadiusProperty, CardRadius());
        Resource(paper, Border.BackgroundProperty, this, "VexDocumentPaperBrush");
        Resource(paper, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        // 原型 .palette-badge：600 10px/1、2px 4px 内距、--vex-radius-xs 圆角、距卡片右上角各 6。
        var badgeText = new TextBlock
        {
            FontSize = 10, LineHeight = 10, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White,
            FontFamily = new FontFamily("Segoe UI Symbol, Microsoft YaHei UI")
        };
        var badge = new Border
        {
            Child = badgeText,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 6, 0), Padding = new Thickness(4, 2),
            IsHitTestVisible = false
        };
        badge.Bind(Border.CornerRadiusProperty, this.GetResourceObservable("VexDensityCornerRadiusXs"));
        Resource(badge, Border.BackgroundProperty, this, "VexAccentBrush");
        // 原型选中态：accent 描边 + inset 0 0 0 1px accent，环压在卡片边缘内侧。
        var ring = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false };
        ring.Bind(Border.CornerRadiusProperty, CardRadius(-1));
        Resource(ring, Border.BorderBrushProperty, this, "VexAccentBrush");
        var content = new Panel { Children = { paper, ring, badge } };
        // 原型：排版卡是流程终点，应用后稍候自动收起让出正文看效果（scheduleCollapse）。
        var button = Button("", () => { _vm!.Appearance.SelectedTypography = option; ScheduleCollapse(); });
        button.Name = "Typography_" + option.Key;
        button.Content = content; button.Padding = default; button.FontSize = 10; button.FontWeight = FontWeight.Normal;
        button.MinHeight = 0;
        button.ClipToBounds = true;
        button.VerticalAlignment = VerticalAlignment.Stretch; button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
        // 原型实测：1px --border 描边、--surface 底、--vex-radius-md 圆角，box-shadow:none（描边色见 Button.typography-card）。
        button.BorderThickness = new Thickness(1);
        button.Classes.Add("typography-card");
        Resource(button, TemplatedControl.CornerRadiusProperty, this, "VexDensityCornerRadiusMd");
        Resource(button, TemplatedControl.BackgroundProperty, this, "VexDocumentPaperBrush");
        _items[option] = (button, ring, badge, badgeText);
        ring.IsVisible = option == _vm!.Appearance.SelectedTypography;
        button.Classes.Set("typography-card-on", ring.IsVisible);
        if (option.Key?.StartsWith("user:", StringComparison.Ordinal) == true)
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 8, 0), IsVisible = false };
            var captured = option.Key!;
            // 原型 .typography-card-tools 是 ✎ 编辑 + × 删除两枚，悬浮才显出；只给删除会让用户改不了已建的排版。
            var edit = new Button { Classes = { "wb-quiet" }, Width = 22, Height = 22, Padding = default, FontSize = 12, Content = "✎" };
            ToolTipHelper.SetText(edit, Text("EditCustomTypography"));
            edit.Click += (_, _) => OpenTypographyDialog(captured);
            var remove = new Button { Classes = { "wb-quiet" }, Width = 22, Height = 22, Padding = default, FontSize = 12, Content = "×" };
            ToolTipHelper.SetText(remove, Text("DeleteCustomTypography"));
            remove.Click += (_, _) => _vm!.Appearance.RemoveCustomTypography(captured);
            tools.Children.Add(edit);
            tools.Children.Add(remove);
            ((Panel)button.Content!).Children.Add(tools);
            button.PointerEntered += (_, _) => { if (button.Content is Panel p) foreach (var c in p.Children.OfType<StackPanel>()) c.IsVisible = true; };
            button.PointerExited += (_, _) => { if (button.Content is Panel p) foreach (var c in p.Children.OfType<StackPanel>()) c.IsVisible = false; };
        }
        return button;
    }

    /// <summary>原型 #typography-panel 的卡片圆角档位（--vex-radius-md，随密度与圆角缩放）；
    /// 选中环压在描边内侧，半径与卡片同档。</summary>
    private IObservable<object?> CardRadius(double outset = 0) =>
        this.GetResourceObservable("VexDensityCornerRadiusMd").Select(value =>
            value is CornerRadius radius ? new CornerRadius(radius.TopLeft + outset) : (object?)value);

    /// <summary>整行虚线「＋ 新增排版」条（原型 .typography-add：44 高、横向排布、占满网格宽度）。</summary>
    private Control BuildAddCard()
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        // 原型 span{font-size:18px;line-height:1} + small{font-size:12px}。两行文字都不写前景，
        // 交给 Button.palette-add-card 的 muted 与 :hover 的 accent 继承，整条才能一起变色（按钮主题会加 SemiBold，字重显式归位）。
        var plus = new TextBlock { Text = "＋", FontSize = 18, LineHeight = 18, VerticalAlignment = VerticalAlignment.Center };
        var addLabel = new TextBlock { Text = Text("AddCustomTypography"), FontSize = 12, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(plus);
        stack.Children.Add(addLabel);
        // 虚线框画在卡片边缘本身（原型 .typography-add：1px dashed --border，圆角取 --vex-radius-md）。
        var dashRadius = CardRadius().Select(value => (object?)(value is CornerRadius radius ? radius.TopLeft : 8.0));
        var dash = new Avalonia.Controls.Shapes.Rectangle { StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }, StrokeThickness = 1, IsHitTestVisible = false };
        // 悬浮换色的样式选择器按类名认这圈虚线（WorkbenchStyles 的 palette-add-card:pointerover）。
        dash.Classes.Add("palette-add-dash");
        dash.Bind(Avalonia.Controls.Shapes.Rectangle.RadiusXProperty, dashRadius);
        dash.Bind(Avalonia.Controls.Shapes.Rectangle.RadiusYProperty, dashRadius);
        Resource(dash, Avalonia.Controls.Shapes.Shape.StrokeProperty, this, "VexPanelBorderBrush");
        var card = Button("", () => OpenTypographyDialog());
        card.Name = "AddCustomTypography";
        card.Content = new Panel { Children = { dash, stack } };
        card.Padding = default; card.MinHeight = 44; card.Height = 44;
        card.MaxHeight = 44;
        card.HorizontalAlignment = HorizontalAlignment.Stretch; card.VerticalAlignment = VerticalAlignment.Top;
        card.HorizontalContentAlignment = HorizontalAlignment.Stretch; card.VerticalContentAlignment = VerticalAlignment.Stretch;
        card.BorderThickness = new Thickness(0);
        Resource(card, TemplatedControl.CornerRadiusProperty, this, "VexDensityCornerRadiusMd");
        card.Classes.Add("palette-add-card");
        return card;
    }

    /// <summary>新增（editingKey 为空）或编辑既有用户排版：编辑先把当前值回填到浮层，保存时沿用同一 key。</summary>
    async void OpenTypographyDialog(string? editingKey = null)
    {
        if (_vm is null) return;
        _typographyDialogBusy = true;
        try
        {
            var existing = editingKey is null ? null : CodeWF.Avalonia.Markdown.Themes.ArticleTypographyResolver.Find(editingKey);
            var model = new ShellTypographyEditModel(editingKey, existing?.Name ?? "");
            if (existing is not null)
            {
                model.AccentLight = existing.LightText;
                model.AccentDark = existing.DarkText;
                model.DecoLight = existing.LightDecoration;
                model.DecoDark = existing.DarkDecoration;
                model.Font = existing.Font;
                model.Leading = existing.Leading;
                model.HeadingDecoration = _vm.Appearance.HeadingDecoration(editingKey!);
            }
            var applied = await Ursa.Controls.OverlayDialog.ShowCustomAsync<bool>(new ShellTypographyEditView(model), model,
                options: new Ursa.Controls.OverlayDialogOptions { CanLightDismiss = true, CanDragMove = false, CanResize = false, IsCloseButtonVisible = false });
            if (!applied || model.Result is not { } result) return;
            _vm.Appearance.RegisterCustomTypography(editingKey ?? "", result.Name, result.AccentLight, result.AccentDark,
                result.DecoLight, result.DecoDark, result.Font, result.Leading, result.HeadingDecoration);
            // 目录变化由 Changed 统一触发重建，这里不再手工 Build 一次。
        }
        finally { _typographyDialogBusy = false; }
    }
}
