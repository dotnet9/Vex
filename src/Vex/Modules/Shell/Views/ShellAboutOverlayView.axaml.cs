using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CodeWF.Tools.Extensions;
using CodeWF.Tools.UpdateChecking;
using Lang.Avalonia;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.Views;

public partial class ShellAboutOverlayView : UserControl
{
    private const string WebsiteUrl = "https://codewf.com";
    private static readonly UpdateChecker UpdateChecker = new("dotnet9", "Vex");
    private bool _checkingUpdate;

    public ShellAboutOverlayView()
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

    private async void CheckUpdateLink_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_checkingUpdate)
        {
            return;
        }

        _checkingUpdate = true;
        CheckUpdateResultText.Text = I18nManager.Instance.GetResource(VexL.AboutCheckingUpdate);
        e.Handled = true;
        try
        {
            Version? current = VersionUtil.Parse(VersionText.Text);
            UpdateCheckResult result = await UpdateChecker.CheckAsync(current ?? new Version(0, 0, 0));
            if (!result.Succeeded)
            {
                CheckUpdateResultText.Text = string.Format(
                    I18nManager.Instance.GetResource(VexL.AboutUpdateFailed) ?? "{0}", result.Error);
                return;
            }

            if (result.Update is { } update)
            {
                CheckUpdateResultText.Text = string.Format(
                    I18nManager.Instance.GetResource(VexL.AboutUpdateAvailable) ?? "{0}", update.Tag);
                Process.Start(new ProcessStartInfo(update.PageUrl) { UseShellExecute = true });
                return;
            }

            CheckUpdateResultText.Text = I18nManager.Instance.GetResource(VexL.AboutUpToDate);
        }
        catch (Exception exception)
        {
            CheckUpdateResultText.Text = string.Format(
                I18nManager.Instance.GetResource(VexL.AboutUpdateFailed) ?? "{0}", exception.Message);
        }
        finally
        {
            _checkingUpdate = false;
        }
    }

    private void Close_OnClick(object? sender, RoutedEventArgs e) => ShellOverlayHost.Dismiss(this);
}
