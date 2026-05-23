using System.Diagnostics;
using Avalonia.Input;
using Ursa.Controls;

namespace Vex.Modules.Help.Views;

public partial class AboutWindow : UrsaWindow
{
    private const string WebsiteUrl = "https://codewf.com";

    public AboutWindow()
    {
        InitializeComponent();
    }

    private void WebsiteLink_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true });
        e.Handled = true;
    }
}
