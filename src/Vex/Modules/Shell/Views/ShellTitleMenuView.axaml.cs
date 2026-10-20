using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Vex.Modules.Shell.Views;

public partial class ShellTitleMenuView : UserControl
{
    public ShellTitleMenuView()
    {
        InitializeComponent();
        foreach (var item in this.GetLogicalDescendants().OfType<MenuItem>())
            item.SubmenuOpened += (_, _) =>
            {
                var popup = item.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
                if (popup?.Child is not { } panel) return;
                panel.Width = ShellAppearanceMenuView.MenuWidth;
                panel.MaxHeight = Math.Max(120, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 820) - 56);
            };
    }

    public event EventHandler? BeginGuideRequested;

    public MenuItem FileMenuTarget => FileMenuItem;

    private void BeginGuideMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginGuideRequested?.Invoke(this, EventArgs.Empty);
    }
}
