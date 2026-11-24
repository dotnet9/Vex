using Avalonia.Controls;

namespace Vex.Modules.Git.Views;

/// <summary>删除分支确认弹层（GitHub Desktop delete-branch-dialog 口径）。交互全走绑定。</summary>
public partial class GitBranchDeleteConfirmView : UserControl
{
    public GitBranchDeleteConfirmView()
    {
        InitializeComponent();
    }
}
