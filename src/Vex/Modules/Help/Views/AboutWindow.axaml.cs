using System.Diagnostics;
using System.Reflection;
using Avalonia.Input;
using CodeWF.Tools.Extensions;
using Ursa.Controls;

namespace Vex.Modules.Help.Views;

public partial class AboutWindow : UrsaWindow
{
    private const string WebsiteUrl = "https://codewf.com";

    public AboutWindow()
    {
        InitializeComponent();
        InitializeAssemblyInfo();
    }

    private void InitializeAssemblyInfo()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(App).Assembly;
        VersionText.Text = assembly.InformationalVersion()
            ?? assembly.FileVersion()
            ?? assembly.Version()
            ?? "-";
        CompileTimeText.Text = assembly.CompileTime()?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    }

    private void WebsiteLink_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        Process.Start(new ProcessStartInfo(WebsiteUrl) { UseShellExecute = true });
        e.Handled = true;
    }
}
