using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CodeWF.Toolkit.Lang;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellTitleMenuView : UserControl
{
    public ShellTitleMenuView()
    {
        InitializeComponent();
        foreach (var item in ((Menu)Content!).Items.OfType<MenuItem>()) Configure(item, true);
        AttachedToVisualTree += (_, _) => I18nManager.Instance.CultureChanged += CultureChanged;
        DetachedFromVisualTree += (_, _) => I18nManager.Instance.CultureChanged -= CultureChanged;
    }

    /// <summary>原型设计准则：一级与级联菜单统一 248px（外观面板 430px 属内容面板，不共用）。</summary>
    private const double MenuWidth = 248;

    private void Configure(MenuItem item, bool topLevel = false)
    {
        if (!topLevel) item.Classes.Add("wrap-entry");
        item.SubmenuOpened += (_, _) =>
            {
                var popup = item.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
                if (popup?.Child is not { } panel) return;
                panel.Width = MenuWidth;
                panel.MaxHeight = Math.Max(120, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 820) - 56);
            };
        foreach (var child in item.Items.OfType<MenuItem>()) Configure(child);
    }

    private void CultureChanged(object? sender, EventArgs e)
    {
        // 界面文案由 i18n 标记扩展自动刷新；保留事件位以对齐其余 Shell 视图的文化刷新节奏。
    }

    public event EventHandler? BeginGuideRequested;

    public MenuItem FileMenuTarget => FileMenuItem;

    private void BeginGuideMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginGuideRequested?.Invoke(this, EventArgs.Empty);
    }
}
