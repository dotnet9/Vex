using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using CodeWF.Toolkit.EventBus;
using CodeWF.Avalonia.MindView.Markdown;
using Vex.Core.Messaging;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellAuxiliaryPanelView : UserControl
{
    public ShellAuxiliaryPanelView()
    {
        InitializeComponent();
        Tabs.KeyDown += (_, e) =>
        {
            if (DataContext is not MainWindowViewModel vm || e.Key is not (Key.Left or Key.Right or Key.Home or Key.End)) return;
            var index = vm.Layout.IsOutlineTab ? 0 : vm.Layout.IsInfoTab ? 1 : 2;
            index = e.Key == Key.Home ? 0 : e.Key == Key.End ? 2 : (index + (e.Key == Key.Right ? 1 : 2)) % 3;
            if (index == 0) vm.Layout.SelectOutlineTab(); else if (index == 1) vm.Layout.SelectInfoTab(); else vm.Layout.SelectChapterTab();
            ((ToggleButton)Tabs.Children[index]).Focus();
            e.Handled = true;
        };
        AttachedToVisualTree += (_, _) => EventBus.Default.Subscribe(this);
        DetachedFromVisualTree += (_, _) => EventBus.Default.Unsubscribe(this);
    }
    public void SetInspector(Control inspector) => InspectorSlot.Content = inspector;
    [EventHandler]
    public void NavigateTo(NavigateToLineCommand command)
    {
        if (DataContext is not MainWindowViewModel vm || vm.Workbench.Root is not { } root) return;
        vm.Workbench.SelectedNode = Flatten(root).Where(node => node.Kind == MarkdownMindMapNodeKind.Heading && node.SourceLine <= command.Line)
            .OrderByDescending(node => node.SourceLine).FirstOrDefault();
        static IEnumerable<MarkdownMindMapNode> Flatten(MarkdownMindMapNode node)
        { yield return node; foreach (var child in node.Children) foreach (var descendant in Flatten(child)) yield return descendant; }
    }
}
