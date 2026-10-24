using Avalonia.Controls;
using CodeWF.Toolkit.EventBus;
using CodeWF.Avalonia.MindView.Markdown;
using Vex.Core.Messaging;
using Vex.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellAuxiliaryPanelView : UserControl
{
    public ShellAuxiliaryPanelView()
    {
        InitializeComponent();
        CollapseOutlineIcon.Data = MenuIconCatalog.Get("panel-right-close");
        AttachedToVisualTree += (_, _) => EventBus.Default.Subscribe(this);
        DetachedFromVisualTree += (_, _) => EventBus.Default.Unsubscribe(this);
    }
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
