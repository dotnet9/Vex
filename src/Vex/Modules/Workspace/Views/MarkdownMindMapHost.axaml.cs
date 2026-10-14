using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownMindMapHost : UserControl
{
    public Control GuideSettingsTarget => SettingsTarget;
    public MarkdownMindMapHost()
    {
        InitializeComponent();
        Map.NodeSelected += (_, node) => { if (DataContext is MainWindowViewModel vm) vm.Workbench.SelectedNode = node; };
        Map.SourceRequested += (_, node) => (DataContext as MainWindowViewModel)?.Workbench.LocateSource(node);
        Map.ZoomChanged += (_, _) => ZoomLabel.Content = $"{Map.Zoom:P0}";
        ActualThemeVariantChanged += (_, _) => RefreshAccent();
        AttachedToVisualTree += (_, _) => RefreshAccent();
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsVisible" && IsVisible)
                Avalonia.Threading.Dispatcher.UIThread.Post(Map.EnsureViewport, Avalonia.Threading.DispatcherPriority.Render);
        };
    }
    private void RefreshAccent()
    {
        if (this.TryFindResource("VexAccentBrush", out var resource) && resource is ISolidColorBrush brush) Map.AccentColor = brush.Color;
    }
    private void ZoomOutClick(object? sender, RoutedEventArgs e) => Map.ZoomOut();
    private void ZoomInClick(object? sender, RoutedEventArgs e) => Map.ZoomIn();
    private void ResetZoomClick(object? sender, RoutedEventArgs e) => Map.ResetZoom();
    private void FitClick(object? sender, RoutedEventArgs e) => Map.Fit();
    private void ExpandClick(object? sender, RoutedEventArgs e) => Map.ExpandAll();
    private void CollapseClick(object? sender, RoutedEventArgs e) => Map.CollapseAll();
}
