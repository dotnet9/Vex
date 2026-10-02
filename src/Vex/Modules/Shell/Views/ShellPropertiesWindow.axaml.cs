using Avalonia.Controls;
using Avalonia.Input.Platform;
using Vex.Modules.Shell.ViewModels;
using Ursa.Controls;

namespace Vex.Modules.Shell.Views;

public partial class ShellPropertiesWindow : UrsaWindow
{
    public ShellPropertiesWindow()
    {
        InitializeComponent();
    }

    public ShellPropertiesWindow(object dataContext)
        : this()
    {
        DataContext = dataContext;
    }

    private async void CopyPath_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ShellDocumentInfoViewModel info ||
            info.CurrentFilePath is not { Length: > 0 } path)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is not IClipboard clipboard)
        {
            return;
        }

        await clipboard.SetTextAsync(path);
    }
}
