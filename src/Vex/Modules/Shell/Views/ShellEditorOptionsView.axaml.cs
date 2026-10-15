using Avalonia.Controls;

namespace Vex.Modules.Shell.Views;

public partial class ShellEditorOptionsView : UserControl
{
    public ShellEditorOptionsView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => MethodHint.IsVisible = Bounds.Width >= 740;
    }
}
