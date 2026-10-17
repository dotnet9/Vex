using Avalonia;
using Avalonia.Controls;
using System.Reactive.Linq;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using CodeWF.Avalonia.MindView.Markdown;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using Vex.Modules.Shell.ViewModels;
using Vex.Core.Models;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownMindMapHost : UserControl
{
    public Control GuideSettingsTarget => SettingsTarget;
    private ShellWorkbenchViewModel? _workbench;
    private bool _refreshingInspector;
    private int? _inspectedStart;
    private int? _renamingStart;
    private (bool Paragraphs, bool Lists, bool Code, bool Tables)? _savedVisibility;
    public MarkdownMindMapHost()
    {
        InitializeComponent();
        Map.NodeSelected += (_, node) => { if (_workbench?.IsPresenting == true) return; FlushInspector(); if (DataContext is MainWindowViewModel vm) { vm.Workbench.ShowMapSettings = false; vm.Workbench.SelectedNode = node; } };
        Map.SourceRequested += (_, node) =>
        {
            if (_workbench?.IsPresenting == true)
            {
                var section = _workbench.PresentationSections.LastOrDefault(section => section.Start <= node.SourceStart) ?? _workbench.PresentationSections.FirstOrDefault();
                if (section is not null) _workbench.NavigatePresentation(section.Index);
            }
            else (DataContext as MainWindowViewModel)?.Workbench.LocateSource(node);
        };
        Map.ChapterActionRequested += (_, action) => { if (_workbench is null) return; _workbench.SelectedNode = action.Node; if (action.Action == "rename") BeginRename(action.Node); else ExecuteChapter(action.Action); };
        Map.ChapterMoveRequested += (_, move) => { FlushInspector(); _workbench?.MoveChapter(move.Node.SourceStart, move.Target.SourceStart, move.Position); };
        Map.ZoomChanged += (_, _) => ZoomLabel.Content = $"{Map.Zoom:P0}";
        Map.VisibleNodesChanged += (_, _) => { if (DataContext is MainWindowViewModel vm) vm.Workbench.VisibleNodeCount = Map.VisibleNodeCount; };
        DataContextChanged += (_, _) => AttachWorkbench();
        AttachedToVisualTree += (_, _) => { EventBus.Default.Subscribe(this); AttachWorkbench(); };
        DetachedFromVisualTree += (_, _) => { EventBus.Default.Unsubscribe(this); if (_workbench is not null) _workbench.PropertyChanged -= WorkbenchChanged; _workbench = null; };
        SizeChanged += (_, _) =>
        {
            MapTitle.IsVisible = false; MapHint.IsVisible = Bounds.Width >= 480 && _workbench?.IsPresenting != true;
            MapHint.MaxWidth = Math.Max(0, Bounds.Width - 180);
            var wrap = Bounds.Width < 490;
            Grid.SetRow(MapToolsRight, wrap ? 1 : 0); Grid.SetColumn(MapToolsRight, wrap ? 0 : 1);
            Grid.SetColumnSpan(MapToolsRight, wrap ? 2 : 1);
            NodeInspector.MaxHeight = Math.Max(100, Bounds.Height - 130); SettingsTarget.MaxHeight = Math.Max(100, Bounds.Height - 74);
        };
        Map.Bind(CodeWF.Avalonia.MindView.Controls.MarkdownMindMapView.AccentColorProperty, this.GetResourceObservable("StudioAccentColor").Select(r => r is Color color ? color : Colors.MediumPurple));
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
        Map.IsChapterEditingEnabled = _workbench?.IsPresenting != true;
        RefreshInspector();
        RefreshHeadingsOnly();
    }
    private void WorkbenchChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.IsPresenting))
        { MapHint.IsVisible = Bounds.Width >= 480 && _workbench?.IsPresenting != true; Map.IsChapterEditingEnabled = _workbench?.IsPresenting != true; }
        if (e.PropertyName == nameof(ShellWorkbenchViewModel.SelectedNode))
        { Map.SelectNode(_workbench?.SelectedNode?.Id); RefreshInspector(); }
        if (e.PropertyName is nameof(ShellWorkbenchViewModel.ShowParagraphs) or nameof(ShellWorkbenchViewModel.ShowLists) or nameof(ShellWorkbenchViewModel.ShowCode) or nameof(ShellWorkbenchViewModel.ShowTables))
            RefreshHeadingsOnly();
    }
    private void RefreshInspector()
    {
        _refreshingInspector = true;
        try { _inspectedStart = _workbench?.SelectedNode?.SourceStart; ChapterTitleInput.Text = _workbench?.ChapterTitle; ChapterBodyInput.Text = _workbench?.ChapterBody; }
        finally { _refreshingInspector = false; }
    }
    public void FlushInspector()
    {
        if (_refreshingInspector || _workbench is not { IsChapterSelected: true } state || state.SelectedNode?.SourceStart != _inspectedStart) return;
        var title = ChapterTitleInput.Text; var body = ChapterBodyInput.Text;
        var titleChanged = title is not null && title != state.ChapterTitle;
        var bodyChanged = body is not null && body != state.ChapterBody;
        if (titleChanged) state.CommitChapterTitle(title!);
        if (bodyChanged) state.CommitChapterBody(body!);
    }
    private void TitleLostFocus(object? sender, RoutedEventArgs e) => FlushInspector();
    private void BodyLostFocus(object? sender, RoutedEventArgs e) => FlushInspector();
    private void TitleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { FlushInspector(); Map.Focus(); e.Handled = true; }
        if (e.Key == Key.Escape) { RefreshInspector(); Map.Focus(); e.Handled = true; }
    }
    private void ChapterClick(object? sender, RoutedEventArgs e) { if (sender is Button { Tag: string command }) ExecuteChapter(command); }
    private void ExecuteChapter(string command)
    {
        FlushInspector();
        if (command == "delete")
        {
            if (_workbench?.IsChapterSelected != true || ReferenceEquals(_workbench.SelectedNode, _workbench.Root)) return;
            DeleteChapterName.Text = _workbench.SelectedTitle; KeepChapterContent.IsChecked = false;
            ChapterDeleteLayer.IsVisible = true; CancelDeleteChapter.Focus(); return;
        }
        _workbench?.ExecuteChapter(command);
        if (command is "child" or "sibling") Dispatcher.UIThread.Post(() => ChapterTitleInput.Focus(), DispatcherPriority.Background);
    }
    private void BeginRename(MarkdownMindMapNode node)
    {
        if (node.Kind != MarkdownMindMapNodeKind.Heading || Map.GetNodeBounds(node.Id) is not { } rect) return;
        _renamingStart = node.SourceStart; InlineChapterTitle.Text = _workbench?.ChapterTitle;
        InlineChapterTitle.Width = Math.Max(130, rect.Width - 16);
        Canvas.SetLeft(InlineChapterTitle, Math.Clamp(rect.X + 8, 4, Math.Max(4, Map.Bounds.Width - InlineChapterTitle.Width - 4)));
        Canvas.SetTop(InlineChapterTitle, Math.Clamp(rect.Center.Y - 15, 4, Math.Max(4, Map.Bounds.Height - 34)));
        RenameLayer.IsVisible = true; InlineChapterTitle.Focus(); InlineChapterTitle.SelectAll();
    }
    private void EndRename(bool commit)
    {
        var start = _renamingStart; _renamingStart = null;
        if (commit && start is not null && _workbench?.SelectedNode?.SourceStart == start) _workbench.CommitChapterTitle(InlineChapterTitle.Text ?? "");
        RenameLayer.IsVisible = false;
    }
    private void InlineTitleKeyDown(object? sender, KeyEventArgs e)
    { if (e.Key is Key.Enter or Key.Escape) { EndRename(e.Key == Key.Enter); Map.Focus(); e.Handled = true; } }
    private void InlineTitleLostFocus(object? sender, RoutedEventArgs e) => EndRename(true);
    public bool HandleEscape()
    {
        if (RenameLayer.IsVisible) { EndRename(false); Map.Focus(); return true; }
        if (ChapterDeleteLayer.IsVisible) { ChapterDeleteLayer.IsVisible = false; Map.Focus(); return true; }
        return false;
    }
    private void CancelDeleteClick(object? sender, RoutedEventArgs e) { ChapterDeleteLayer.IsVisible = false; Map.Focus(); }
    private void ConfirmDeleteClick(object? sender, RoutedEventArgs e) { _workbench?.DeleteChapter(KeepChapterContent.IsChecked == true); ChapterDeleteLayer.IsVisible = false; Map.Focus(); }
    private void HeadingsOnlyClick(object? sender, RoutedEventArgs e)
    {
        if (_workbench is not { } state) return;
        var only = HeadingsOnlyButton.IsChecked == true;
        if (only) { _savedVisibility = (state.ShowParagraphs, state.ShowLists, state.ShowCode, state.ShowTables); state.ShowParagraphs = state.ShowLists = state.ShowCode = state.ShowTables = false; }
        else { var saved = _savedVisibility ?? (true, true, false, false); state.ShowParagraphs = saved.Item1; state.ShowLists = saved.Item2; state.ShowCode = saved.Item3; state.ShowTables = saved.Item4; }
    }
    private void RefreshHeadingsOnly()
    {
        if (_workbench is { } state) HeadingsOnlyButton.IsChecked = !state.ShowParagraphs && !state.ShowLists && !state.ShowCode && !state.ShowTables;
    }
    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (_workbench?.IsMapVisible != true || _workbench.Root is null) return;
        var nodes = Flatten(_workbench.Root);
        var node = nodes.Where(n => n.Kind == MarkdownMindMapNodeKind.Heading && n.SourceLine <= command.Line).OrderByDescending(n => n.SourceLine).FirstOrDefault() ?? _workbench.Root;
        Map.RevealNode(node.Id);
        static IEnumerable<MarkdownMindMapNode> Flatten(MarkdownMindMapNode n) { yield return n; foreach (var c in n.Children) foreach (var d in Flatten(c)) yield return d; }
    }
    private void ZoomOutClick(object? sender, RoutedEventArgs e) => Map.ZoomOut();
    public void ZoomPresentation(bool increase) { if (increase) Map.ZoomIn(); else Map.ZoomOut(); }
    private void ZoomInClick(object? sender, RoutedEventArgs e) => Map.ZoomIn();
    private void ResetZoomClick(object? sender, RoutedEventArgs e) => Map.ResetZoom();
    private void FitClick(object? sender, RoutedEventArgs e) => Map.Fit();
    private void ExpandClick(object? sender, RoutedEventArgs e) => Map.ExpandAll();
    private void CollapseClick(object? sender, RoutedEventArgs e)
    {
        if (HasCollapsed(Map.Root)) Map.ExpandAll(); else Map.CollapseAll();
        CollapseBranchesButton.Content = WorkbenchText.Get(HasCollapsed(Map.Root) ? "WBExpand" : "WBCollapse");
        static bool HasCollapsed(MarkdownMindMapNode? node) => node is not null && (node.IsCollapsed || node.Children.Any(HasCollapsed));
    }
}
