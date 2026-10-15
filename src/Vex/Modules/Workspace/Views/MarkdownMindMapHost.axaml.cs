using Avalonia;
using Avalonia.Controls;
using System.Reactive.Linq;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.MindView.Markdown;
using Avalonia.Interactivity;
using Avalonia.Media;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownMindMapHost : UserControl
{
    public Control GuideSettingsTarget => SettingsTarget;
    private ShellWorkbenchViewModel? _workbench;
    public MarkdownMindMapHost()
    {
        InitializeComponent();
        Map.NodeSelected += (_, node) => { if (DataContext is MainWindowViewModel vm) { vm.Workbench.ShowMapSettings = false; vm.Workbench.SelectedNode = node; } };
        Map.SourceRequested += (_, node) => (DataContext as MainWindowViewModel)?.Workbench.LocateSource(node);
        Map.ZoomChanged += (_, _) => ZoomLabel.Content = $"{Map.Zoom:P0}";
        Map.VisibleNodesChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Workbench.VisibleNodeCount = Map.VisibleNodeCount; };
        DataContextChanged += (_, _) => AttachWorkbench();
        AttachedToVisualTree += (_, _) => { EventBus.Default.Subscribe(this); AttachWorkbench(); };
        DetachedFromVisualTree += (_, _) => { EventBus.Default.Unsubscribe(this); if (_workbench is not null) _workbench.PropertyChanged -= WorkbenchChanged; _workbench = null; };
        SizeChanged += (_, _) => { MapTitle.IsVisible = Bounds.Width >= 720; MapHint.IsVisible = Bounds.Width >= 850; NodeInspector.MaxHeight = Math.Max(100, Bounds.Height - 130); SettingsTarget.MaxHeight = Math.Max(100, Bounds.Height - 74); };
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.AccentColorProperty, this.GetResourceObservable("VexAccentBrush").Select(r => r is ISolidColorBrush b ? b.Color : Colors.MediumPurple));
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.SuccessColorProperty, this.GetResourceObservable("VexSuccessBrush").Select(r => r is ISolidColorBrush b ? b.Color : Colors.SeaGreen));
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.WarningColorProperty, this.GetResourceObservable("VexWarningBrush").Select(r => r is ISolidColorBrush b ? b.Color : Colors.Orange));
        PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsVisible" && IsVisible)
                Avalonia.Threading.Dispatcher.UIThread.Post(Map.EnsureViewport, Avalonia.Threading.DispatcherPriority.Render);
        };
    }
    private void AttachWorkbench()
    {
        if (_workbench is not null) _workbench.PropertyChanged -= WorkbenchChanged;
        _workbench = (DataContext as MainWindowViewModel)?.Workbench;
        if (_workbench is not null) _workbench.PropertyChanged += WorkbenchChanged;
    }
    private void WorkbenchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ShellWorkbenchViewModel.SelectedNode) && _workbench?.SelectedNode is null) Map.ClearSelection(); }
    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_workbench?.IsMindMap != true || _workbench.Root is null) return;
        var nodes = Flatten(_workbench.Root);
        var node = nodes.Where(n => n.Kind == MarkdownMindMapNodeKind.Heading && n.SourceLine <= command.Line).OrderByDescending(n => n.SourceLine).FirstOrDefault() ?? _workbench.Root;
        Map.RevealNode(node.Id);
        static IEnumerable<MarkdownMindMapNode> Flatten(MarkdownMindMapNode n) { yield return n; foreach (var c in n.Children) foreach (var d in Flatten(c)) yield return d; }
    }
    private void ZoomOutClick(object? sender, RoutedEventArgs e) => Map.ZoomOut();
    private void ZoomInClick(object? sender, RoutedEventArgs e) => Map.ZoomIn();
    private void ResetZoomClick(object? sender, RoutedEventArgs e) => Map.ResetZoom();
    private void FitClick(object? sender, RoutedEventArgs e) => Map.Fit();
    private void ExpandClick(object? sender, RoutedEventArgs e) => Map.ExpandAll();
    private void CollapseClick(object? sender, RoutedEventArgs e) => Map.CollapseAll();
}
