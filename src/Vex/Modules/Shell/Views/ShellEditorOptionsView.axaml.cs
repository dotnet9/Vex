using Avalonia.Controls;

namespace Vex.Modules.Shell.Views;

public partial class ShellEditorOptionsView : UserControl
{
    public ShellEditorOptionsView()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            EditingHint.Opacity = Bounds.Width >= 900 ? 1 : 0;
            Classes.Set("compact-formatting", Bounds.Width < 850);
        };
    }
}
