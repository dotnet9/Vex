namespace Vex.Modules.Shell.ViewModels;

public sealed partial class ShellAppearanceViewModel
{
    /// <summary>UI 密度令牌（规则 §6 显式值表）：字号/控件高/间距/圆角写入应用资源供全控件消费。</summary>
    private void ApplyUiDensity(string density)
    {
        if (global::Avalonia.Application.Current is not { } app) return;
        var (caption, body, subtitle, title, display, hSm, hMd, hLg, padSm, padMd, padLg, rowH, icon) = density switch
        {
            "compact" => (11.0, 12.0, 14.0, 17.0, 22.0, 24.0, 28.0, 34.0, 4.0, 8.0, 12.0, 26.0, 14.0),
            "relaxed" => (13.0, 15.0, 17.0, 21.0, 28.0, 32.0, 36.0, 44.0, 8.0, 12.0, 18.0, 36.0, 18.0),
            _ => (12.0, 14.0, 16.0, 20.0, 26.0, 28.0, 32.0, 38.0, 6.0, 10.0, 14.0, 30.0, 16.0),
        };
        // 离线计算整档参数，再用 Avalonia 原生批量接口通知 DynamicResource 一次。
        var resources = new Avalonia.Controls.ResourceDictionary();
        ApplyChromeDensity(resources, density);
        resources["VexDensityFontSizeCaption"] = caption;
        resources["VexDensityFontSizeBody"] = body;
        // 原型多处取 calc(var(--fs-body) - 1px)（MCP 二级页签、设置里的分段行），单列一档令牌免得各处写死 13。
        resources["VexDensityFontSizeBodySm"] = body - 1;
        resources["VexDensityFontSizeSubtitle"] = subtitle;
        resources["VexDensityFontSizeTitle"] = title;
        resources["VexDensityFontSizeDisplay"] = display;
        resources["VexDensityControlHeightSm"] = hSm;
        resources["VexDensityControlHeightMd"] = hMd;
        resources["VexDensityControlHeightLg"] = hLg;
        resources["VexDensityPaddingSm"] = padSm;
        resources["VexDensityPaddingMd"] = padMd;
        resources["VexDensityPaddingLg"] = padLg;
        // 圆角令牌必须以 CornerRadius 装箱：样式里的 DynamicResource 直传属性存储，不做 double 隐式转换，
        // 否则 VexZoomDock 模板的气球圆角会在装载时报 Unable to cast Double to CornerRadius。
        // 圆角只由 RadiusMode 决定，密度切换不再改变形状。
        resources["VexDensityCornerRadiusSm"] = new Avalonia.CornerRadius(6 * _radiusScale);
        resources["VexDensityCornerRadiusMd"] = new Avalonia.CornerRadius(8 * _radiusScale);
        resources["VexDensityCornerRadiusLg"] = new Avalonia.CornerRadius(10 * _radiusScale);
        resources["VexDensityCornerRadiusPill"] = new Avalonia.CornerRadius(999 * _radiusScale);
        // 原型另有两档不吃密度、只吃 --radius-scale：--vex-radius-xs 基线 4（代码块、图标按钮、设置左栏），
        // --vex-radius-popover 基线 16（偏好浮层这类整窗弹层）。
        resources["VexDensityCornerRadiusXs"] = new Avalonia.CornerRadius(4 * _radiusScale);
        resources["VexDensityCornerRadiusPopover"] = new Avalonia.CornerRadius(16 * _radiusScale);
        resources["VexDensityRowHeight"] = rowH;
        resources["VexDensityIconSize"] = icon;
        // 缩放坞（原型 .vex-zoomdock）几何：坞高=控件中档、内缩=pad-sm 减 1px 描边、条宽=坞高×1.875、
        // 还原钮=坞高减 9、其左分隔线再减 8（原型 top/bottom 各 4）、图标=密度图标加 2。
        resources["VexZoomDockHeight"] = hMd;
        // 原型 .statusbar-zoom .vex-zoomdock 的 height:calc(var(--vex-status-h) - 8px)，--vex-status-h 就是控件小档。
        resources["VexZoomDockStatusHeight"] = hSm - 8;
        resources["VexZoomDockPadding"] = new Avalonia.Thickness(padSm - 1, 0);
        resources["VexZoomDockGap"] = padSm;
        resources["VexZoomDockSliderWidth"] = hMd * 1.875;
        resources["VexZoomDockResetHeight"] = hMd - 9;
        resources["VexZoomDockDividerHeight"] = hMd - 17;
        resources["VexZoomDockIconSize"] = icon + 2;
        // 分段控件（原型 .vex-segmented）几何：--seg-scale 就是 control-h-sm ÷ 28（design/assets/prototype/segmented.css:4-6），
        // 各宿主的基准高与内距都乘它，切「界面密度」时轨道、指示块和文字一起缩，不需要逐处改数值。
        var segScale = hSm / 28;
        resources["VexSegmentedScale"] = segScale;
        // 原型 .vex-segmented{--seg-inset:calc(var(--pad-sm)/2)}（segmented.css:7）：轨道内缩随密度三档走 2/3/4，
        // 指示块贴着同一内缩落位（thumb top/bottom），所以只改轨道 Padding 就两者一起缩。
        resources["VexSegmentedInset"] = new Avalonia.Thickness(padSm / 2);
        resources["VexSegmentedHeightView"] = 26 * segScale;
        resources["VexSegmentedPaddingView"] = new Avalonia.Thickness(14, 3 * segScale);
        resources["VexSegmentedPaddingMcp"] = new Avalonia.Thickness(10, 4 * segScale);
        resources["VexSegmentedHeightDensity"] = 22 * segScale;
        resources["VexSegmentedPaddingDensity"] = new Avalonia.Thickness(10, 2 * segScale);
        resources["VexSegmentedHeightRenderDensity"] = 24 * segScale;
        // 原型 .vex-pref-segmented > button：4×密度/6 内距、正文减 1 号字（prototype.css:215）。
        resources["VexSegmentedPaddingPref"] = new Avalonia.Thickness(6, 4 * segScale);

        ((Avalonia.Controls.ResourceDictionary)app.Resources).SetItems(resources);
    }

    private static void ApplyChromeDensity(Avalonia.Controls.ResourceDictionary resources, string density)
    {
        var step = density switch { "compact" => -1, "relaxed" => 1, _ => 0 };
        double Size(double normal, double delta = 4) => normal + step * delta;
        double Space(double normal) => Math.Round(normal * (1 + step * .2), 1);
        void Set(string key, object value) => resources["VexUi" + key] = value;
        void Pad(string key, double x, double y) => Set(key, new Avalonia.Thickness(Space(x), Space(y)));

        Set("HeaderHeight", Size(36));
        Set("ControlHeight", Size(28));
        Set("SmallButtonHeight", Size(26, 2));
        Set("SelectionButtonHeight", Size(30));
        Set("FieldHeight", Size(32));
        Set("ListRowHeight", Size(28));
        Set("FolderRowHeight", Size(24, 2));
        Set("MenuRowHeight", Size(32));
        Set("NavigationHeight", Size(48, 6));
        Set("NavigationBottomHeight", Size(40, 4));
        Set("FormatIconSize", Size(18, 1));
        Set("FindHeight", Size(45, 6));
        Set("FindFieldHeight", Size(30));
        Set("DialogButtonHeight", Size(36));
        Set("PreferencesRowHeight", Size(53, 8));
        Set("PreferencesNavigationHeight", Size(40));
        Set("MenuFontSize", Size(14, 0));
        Set("SmallFontSize", Size(11, 0.5));
        Set("OutlineFontSize", Size(12, 1));
        Set("OutlineNumberFontSize", Size(11, 1));
        Set("OutlineLineHeight", Size(19.2, 1.6));
        Set("SmallLineHeight", Size(17.6, .8));
        Set("CaptionLineHeight", Size(19.2, 1.6));
        // 库大纲面板（MarkdownOutlinePanel）的 OutlinePanel* 尺寸契约键随密度一起换档。
        Set("OutlinePanelTitleFontSize", Size(12, 1));
        Set("OutlinePanelNumberFontSize", Size(11, 1));
        Set("OutlinePanelTitleLineHeight", Size(19.2, 1.6));
        Set("OutlinePanelNumberLineHeight", Size(17.6, .8));
        Set("OutlinePanelRowMinHeight", Size(32));
        Set("OutlinePanelRowPadding", new Avalonia.Thickness(0, Space(6), Space(6), Space(6)));
        Set("BodyLineHeight", Size(21, 1.5));
        foreach (var gap in new[] { 4, 6, 7, 8, 16, 24 })
            Set("Gap" + gap, Space(gap));
        Pad("HeaderPadding", 12, 0);
        Pad("SearchPadding", 10, 0);
        Pad("ListPadding", 8, 0);
        Pad("TitleMenuPadding", 10, 0);
        Pad("FolderPadding", 4, 0);
        Pad("MenuPadding", 10, 5);
        Pad("MenuInset", 6, 6);
        Pad("FieldPadding", 9, 5);
        Pad("DialogPadding", 22, 22);
        Pad("DialogActionPadding", 16, 8);
        Pad("ActionPadding", 16, 5);
        Pad("SelectionPadding", 8, 5);
        Pad("PreferencesContentPadding", 20, 20);
        Pad("PreferencesRowPadding", 0, 10);
        Pad("PreferencesNavigationPadding", 12, 9);
        Pad("ToolbarButtonPadding", 8, 4);
        Pad("FindPadding", 12, 7);
        Pad("FindActionPadding", 12, 2);
        Pad("FindFieldPadding", 10, 5);
        Pad("PanelPadding", 14, 14);
        Pad("MapToolPadding", 7, 4);
        Set("MapOptionMargin", new Avalonia.Thickness(0, 0, 0, Space(11)));
        Pad("MapHeaderPadding", 10, 4);
        Pad("LanguagePadding", 10, 6);
        Pad("CardPadding", 10, 10);
        Set("OutlinePadding", new Avalonia.Thickness(0, Space(6), Space(6), Space(6)));
        Set("NavigationPadding", new Avalonia.Thickness(0, Space(10), 0, 0));
        Set("ToolbarItemMargin", new Avalonia.Thickness(0, 0, Space(4), 0));
        Set("MenuSeparatorMargin", new Avalonia.Thickness(4, Space(4)));
        Set("ZoomPadding", new Avalonia.Thickness(Space(5), 0));
        Set("ZoomSliderWidth", Size(72, 8));
        Set("ZoomResetHeight", Size(22, 2));
    }
}
