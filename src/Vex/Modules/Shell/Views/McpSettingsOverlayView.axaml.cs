using Avalonia.Controls;

namespace Vex.Modules.Shell.Views;

public partial class McpSettingsOverlayView : UserControl
{
    public McpSettingsOverlayView(bool embedded) : this()
    {
        if (!embedded || Content is not Border border || border.Child is not Grid grid) return;
        border.Classes.Remove("dialog-card"); border.Width = double.NaN; border.MaxHeight = double.PositiveInfinity;
        grid.Children[0].IsVisible = false; grid.RowDefinitions[0].Height = new Avalonia.Controls.GridLength(0);
        if (grid.Children[2] is StackPanel footer) footer.Children[0].IsVisible = false;
    }

    public McpSettingsOverlayView()
    {
        InitializeComponent();
    }
}
