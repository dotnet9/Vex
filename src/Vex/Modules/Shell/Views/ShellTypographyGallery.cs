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

/// <summary>正文排版弹层（对齐原型 #typography-panel）：标题 + 渲染密度分段条 + 4 列 3 行真实排版预览卡。</summary>
public sealed class ShellTypographyGallery : UserControl
{
    private MainWindowViewModel? _vm;
    private readonly UniformGrid _cards = new() { Columns = 4, RowSpacing = 8, ColumnSpacing = 8 };
    private readonly Dictionary<TypographyOption, (Button Button, Border Ring, Border Badge, TextBlock BadgeText)> _items = [];
    private readonly Dictionary<string, Button> _densityPills = [];
    private readonly StackPanel _header = new() { Spacing = 0 };
    private TopLevel? _window;
    private ScrollViewer? _scroll;
    private bool _typographyDialogBusy;

    public ShellTypographyGallery()
    {
        DataContextChanged += (_, _) => Attach();
        AttachedToVisualTree += (_, _) => { Attach(); _window = TopLevel.GetTopLevel(this); if (_window is not null) _window.SizeChanged += WindowSizeChanged; Resize(); };
        DetachedFromVisualTree += (_, _) => { if (_vm is not null) { _vm.Appearance.PropertyChanged -= Changed; _vm.Workbench.PropertyChanged -= WorkbenchChanged; } I18nManager.Instance.CultureChanged -= CultureChanged; if (_window is not null) _window.SizeChanged -= WindowSizeChanged; _vm = null; _window = null; };
        Navigation(_cards, () => 4);
    }

    private void WindowSizeChanged(object? sender, SizeChangedEventArgs e) => Resize();

    /// <summary>
    /// 原型卡片预览固定 15px 基准（渲染密度不作用于卡片：原型 .type-document 取 --writing-size 正常档），
    /// 标题/引用按真实排版渲染，说明行单独降到 12/18。
    /// 三行 150.53 卡 + 8 间隙 + 44 新增条 = 543，落在弹层 680 - 标题 30 - 密度行 52 - 内距 36 的滚动区内。
    /// </summary>
    private const double CardHeight = 150.5;

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
        Width = Math.Min(720, (_window?.ClientSize.Width ?? 1280) - 24);
        var anchor = (_window as MainWindow)?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "TypographyTarget" && b.IsEffectivelyVisible);
        var top = anchor?.TranslatePoint(new Point(0, anchor.Bounds.Height + 8), _window!)?.Y ?? 126;
        var parentTop = _window is not null ? this.GetVisualParent()?.TranslatePoint(default, _window)?.Y ?? 0 : 0;
        // 原型 .typography-popover：right:18px 相对工作区（= 窗体右侧 24px），宽 720 含 1px 描边。
        Margin = new Thickness(12, Math.Max(0, top - parentTop), 23, 12);
        Height = Math.Max(140, Math.Min(680, (_window?.ClientSize.Height ?? 820) - top - 12));
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
        if (_header.Parent is Panel oldHeaderHost) oldHeaderHost.Children.Remove(_header);
        if (_scroll is not null) _scroll.Content = null;
        // 网格与新增条同挂在重建的 body 下，摘掉旧父级才能再次入树。
        if (_cards.Parent is Panel oldBody) oldBody.Children.Remove(_cards);
        _items.Clear(); _densityPills.Clear(); _cards.Children.Clear(); _header.Children.Clear();

        var titleRow = new Grid { ColumnDefinitions = new("*,Auto") };
        var title = Label(Text("WBTypographyTitle"), 15); title.FontWeight = FontWeight.SemiBold; title.VerticalAlignment = VerticalAlignment.Center; titleRow.Children.Add(title);
        var close = new Button { Classes = { "vex-dismiss" }, Width = 30, Height = 30 };
        Avalonia.Automation.AutomationProperties.SetName(close, Text("Close"));
        close.Click += (_, _) => { _vm.Workbench.ShowTypography = false; (_window as MainWindow)?.FindControl<ShellEditorOptionsView>("EditorOptions")?.FindControl<Button>("TypographyTarget")?.Focus(); };
        Grid.SetColumn(close, 1); titleRow.Children.Add(close);
        _header.Children.Add(titleRow);

        // 渲染密度：原型 .typography-density .density-options.vex-segmented（max-width 320、gap 6、button{flex:1}、
        // min-height 24×密度）——轨道、滑动指示块、选中字色走 Vex.Controls 共享配方，宿主只管宽度上限。
        var densityRow = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = 10 };
        var densityLabel = new TextBlock { Text = Text("RenderDensityTitle"), FontSize = 13, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        Resource(densityLabel, TextBlock.ForegroundProperty, this, "VexMutedBrush"); densityRow.Children.Add(densityLabel);
        var segments = new SegmentedPanel { Spacing = 6, FillItems = true };
        foreach (var (key, labelKey) in new[] { ("compact", "DensityCompact"), ("normal", "DensityNormal"), ("relaxed", "DensityRelaxed") })
        {
            var pill = new Button { Classes = { "density-pill", "render-density-pill" }, Content = Text(labelKey), HorizontalContentAlignment = HorizontalAlignment.Center };
            Avalonia.Automation.AutomationProperties.SetName(pill, Text(labelKey));
            var captured = key;
            pill.Click += (_, _) => _vm.Appearance.MarkdownDensity = captured;
            _densityPills[key] = pill; segments.Children.Add(pill);
        }
        var pills = new Border { Classes = { "segmented-track", "density-options" }, Child = segments, Width = 320, MaxWidth = 320, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(pills, 1);
        densityRow.Children.Add(pills);
        var densityFrame = new Border { Child = densityRow, BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(2, 9, 2, 11) };
        Resource(densityFrame, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        _header.Children.Add(densityFrame);

        foreach (var option in _vm.Appearance.TypographyOptions) _cards.Children.Add(BuildCard(option));

        // 原型 .typography-add 用 grid-column:1/-1 横跨整行；UniformGrid 不能跨列，故把新增条挂在网格下方同宽。
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(_cards);
        body.Children.Add(BuildAddCard());

        // 右侧预留约 15px 滚动条槽（原型 scrollbar-gutter:stable），保证四列卡宽 ~161。
        _scroll = new ScrollViewer { Content = body, Padding = new Thickness(2, 2, 15, 2), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalAlignment = VerticalAlignment.Stretch };
        var grid = new Grid { RowDefinitions = new("Auto,*") };
        grid.Children.Add(_header); Grid.SetRow(_scroll, 1); grid.Children.Add(_scroll);
        // 原型 .typography-popover 无投影（appearance.css:2），回退 wb-popover 默认淡影；
        // 此前叠加的加浓双投影会在左下圆角缺口里把状态胶囊圆头染暗，看起来像缺角。
        var border = new Border { Classes = { "wb-popover", "flat" }, Child = grid, Padding = new Thickness(18), CornerRadius = new CornerRadius(16) };
        Content = border;
        Resize(); Changed(null, new(""));
    }

    /// <summary>卡片即预览：以该排版真实样式渲染「## 名称 / 说明 / 引用」（原型已去掉 ### 行，窄卡才装得下三行）。</summary>
    private Control BuildCard(TypographyOption option)
    {
        var name = Text(option.NameKey);
        var purpose = Text(option.PurposeKey);
        var quote = Text("AboutShortSlogan");
        var preview = new MarkdownViewer
        {
            Markdown = $"## {name}\n\n{purpose}\n\n> {quote}",
            TypographyTheme = option.Key,
            FontWeight = FontWeight.Normal,
            TypographySize = MarkdownTypographySizes.Normal,
            IsHitTestVisible = false,
            Margin = new Thickness(11, 6),
            DocumentBottomPadding = 0,
            EnableVirtualization = false
        };
        // 原型窄卡把说明降成标签字号并压掉块间距（首块无上外边距、h2 下 5px、段落 0 0 7px），三行卡片才装得下。
        // 标题级别的边距由渲染器按 CodeWFMarkdownHeading{N}Margin 资源取值（属性只作兜底），
        // 所以这里直接覆盖 H2 的级别资源：上边距 0（原型 >:first-child{margin-top:0}）+ 下 5。
        preview.Resources["CodeWFMarkdownHeading2Margin"] = new Thickness(0, 0, 0, 5);
        preview.HeadingMargin = new Thickness(0, 0, 0, 5);
        preview.ParagraphMargin = new Thickness(0, 0, 0, 7);
        preview.ParagraphFontSize = 12;
        preview.ParagraphLineHeight = 18;
        preview.BlockSpacing = 0;
        // 原型窄卡把引用再收紧一档：#typography-panel .type-document blockquote{margin-top:0;padding:6px 9px}。
        // 引用的内距与外边距走库里的动态资源键，弹层内按卡片覆盖，不改全局排版契约。
        preview.Resources["CodeWFMarkdownQuotePadding"] = new Thickness(9, 6, 9, 6);
        preview.Resources["CodeWFMarkdownQuoteMargin"] = new Thickness(0);
        EventHandler clamp = null!;
        var clamped = false;
        clamp = (_, _) =>
        {
            if (clamped) return;
            clamped = ClampPreviewLines(preview);
            if (clamped) preview.LayoutUpdated -= clamp;
        };
        preview.LayoutUpdated += clamp;
        // 原型 .type-swatch：不圆角、不投影，只与卡片顶边隔 1px 描线，白纸铺满卡片由卡片圆角裁切。
        var paper = new Border { Child = preview, ClipToBounds = true, BorderThickness = new Thickness(0, 1, 0, 0), CornerRadius = new CornerRadius(0) };
        Resource(paper, Border.BackgroundProperty, this, "VexDocumentPaperBrush");
        Resource(paper, Border.BorderBrushProperty, this, "VexPanelBorderBrush");
        var badgeText = new TextBlock { FontSize = 9, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White };
        var badge = new Border
        {
            Child = badgeText,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 5, 5, 0), Padding = new Thickness(4, 2),
            CornerRadius = new CornerRadius(4), IsHitTestVisible = false
        };
        Resource(badge, Border.BackgroundProperty, this, "VexAccentBrush");
        // 原型选中态：box-shadow 0 0 0 1.5px accent，环在卡片边缘外侧，半径比卡片多一份描边厚度。
        var ring = new Border { BorderThickness = new Thickness(1.5), Margin = new Thickness(-1.5), IsHitTestVisible = false };
        ring.Bind(Border.CornerRadiusProperty, CardRadius(1.5));
        Resource(ring, Border.BorderBrushProperty, this, "VexAccentBrush");
        var content = new Panel { Children = { paper, ring, badge } };
        // 原型：排版卡是流程终点，应用后稍候自动收起让出正文看效果（scheduleCollapse）。
        var button = Button("", () => { _vm!.Appearance.SelectedTypography = option; ScheduleCollapse(); });
        button.Name = "Typography_" + option.Key;
        button.Content = content; button.Padding = default;
        button.MinHeight = CardHeight; button.Height = CardHeight;
        button.ClipToBounds = false;
        button.VerticalAlignment = VerticalAlignment.Stretch; button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Stretch;
        // 原型 workspace-chrome 最终档：无框、--panel 底、--vex-radius-lg 圆角 + 轻阴影（阴影见 Button.typography-card）。
        button.BorderThickness = new Thickness(0);
        button.Classes.Add("typography-card");
        Resource(button, TemplatedControl.CornerRadiusProperty, this, "VexDensityCornerRadiusLg");
        Resource(button, TemplatedControl.BackgroundProperty, this, "VexPanelBackgroundBrush");
        _items[option] = (button, ring, badge, badgeText);
        if (option.Key?.StartsWith("user:", StringComparison.Ordinal) == true)
        {
            var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 8, 8, 0), IsVisible = false };
            var captured = option.Key!;
            // 原型 .typography-card-tools 是 ✎ 编辑 + × 删除两枚，悬浮才显出；只给删除会让用户改不了已建的排版。
            var edit = new Button { Classes = { "wb-quiet" }, Width = 22, Height = 22, Padding = default, FontSize = 12, Content = "✎" };
            ToolTip.SetTip(edit, Text("EditCustomTypography"));
            edit.Click += (_, _) => OpenTypographyDialog(captured);
            var remove = new Button { Classes = { "wb-quiet" }, Width = 22, Height = 22, Padding = default, FontSize = 12, Content = "×" };
            ToolTip.SetTip(remove, Text("DeleteCustomTypography"));
            remove.Click += (_, _) => _vm!.Appearance.RemoveCustomTypography(captured);
            tools.Children.Add(edit);
            tools.Children.Add(remove);
            ((Panel)button.Content!).Children.Add(tools);
            button.PointerEntered += (_, _) => { if (button.Content is Panel p) foreach (var c in p.Children.OfType<StackPanel>()) c.IsVisible = true; };
            button.PointerExited += (_, _) => { if (button.Content is Panel p) foreach (var c in p.Children.OfType<StackPanel>()) c.IsVisible = false; };
        }
        return button;
    }

    /// <summary>
    /// 原型窄卡用 -webkit-line-clamp 截断（标题、说明各 1 行，引用 2 行），MarkdownViewer 自己排版文本，
    /// 只能在块渲染出来后按块类型回填 MaxLines，否则换行会把引用挤出卡片。返回是否三类块都已截断。
    /// </summary>
    private static bool ClampPreviewLines(MarkdownViewer preview)
    {
        var seen = 0;
        foreach (var block in preview.GetVisualDescendants().OfType<TextBlock>())
        {
            if (block.GetType() == typeof(TextBlock)) continue; // 渲染器的空占位文本块
            var ancestors = block.GetVisualAncestors().OfType<StyledElement>().ToArray();
            bool Class(string name) => ancestors.Any(element => element.Classes.Contains(name));
            // 标题 1 行、引用 2 行、其余（说明段落）1 行，与原型 #typography-panel 的 line-clamp 一致。
            var maxLines = Class("MdHeadingBorder") ? 1 : Class("MdQuote") ? 2 : 1;
            seen |= Class("MdHeadingBorder") ? 1 : Class("MdQuote") ? 4 : 2;
            if (block.MaxLines != maxLines) block.MaxLines = maxLines;
            if (block.TextTrimming != TextTrimming.CharacterEllipsis) block.TextTrimming = TextTrimming.CharacterEllipsis;
        }
        return seen == 7;
    }

    /// <summary>整行虚线「＋ 新增排版」条（原型 .typography-add：44 高、横向排布、占满网格宽度）。</summary>
    /// <summary>原型 #typography-panel 的卡片圆角档位（--vex-radius-lg，随密度与圆角缩放）；
    /// 选中环是外扩 1.5px 的 box-shadow，半径要在卡片档位上再加一份描边厚度。</summary>
    private IObservable<object?> CardRadius(double outset = 0) =>
        this.GetResourceObservable("VexDensityCornerRadiusLg").Select(value =>
            value is CornerRadius radius ? new CornerRadius(radius.TopLeft + outset) : (object?)value);

    private Control BuildAddCard()
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var plus = new TextBlock { Text = "＋", FontSize = 18, LineHeight = 18, VerticalAlignment = VerticalAlignment.Center };
        // 按钮主题会给继承文本加 SemiBold；原型 .typography-add 文字为常规字重，显式归位。
        var addLabel = new TextBlock { Text = Text("AddCustomTypography"), FontSize = 13, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
        Resource(plus, TextBlock.ForegroundProperty, this, "VexMutedBrush"); Resource(addLabel, TextBlock.ForegroundProperty, this, "VexMutedBrush");
        stack.Children.Add(plus);
        stack.Children.Add(addLabel);
        // 虚线框画在卡片边缘本身（原型 .typography-add：1px dashed --border，圆角同样取 --vex-radius-lg）。
        var dashRadius = CardRadius().Select(value => (object?)(value is CornerRadius radius ? radius.TopLeft : 10.0));
        var dash = new Avalonia.Controls.Shapes.Rectangle { StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 4, 3 }, StrokeThickness = 1, IsHitTestVisible = false };
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
        Resource(card, TemplatedControl.CornerRadiusProperty, this, "VexDensityCornerRadiusLg");
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
