using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellPropertiesOverlayView : UserControl
{
    public ShellPropertiesOverlayView()
    {
        InitializeComponent();
    }

    private async void CopyPath_OnClick(object? sender, RoutedEventArgs e)
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

    private void Close_OnClick(object? sender, RoutedEventArgs e) => ShellOverlayHost.Dismiss(this);
}
