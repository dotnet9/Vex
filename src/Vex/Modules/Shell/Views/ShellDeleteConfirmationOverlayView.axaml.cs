using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Vex.Modules.Shell.Views;

public partial class ShellDeleteConfirmationOverlayView : UserControl
{
    public ShellDeleteConfirmationOverlayView()
    {
        InitializeComponent();
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Delete_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void Close(bool confirmed)
    {
        if (DataContext is ShellDeleteConfirmationModel model)
        {
            model.Complete(confirmed);
        }
    }
}

/// <summary>
/// 删除确认浮层状态：<see cref="Complete"/> 的结果即浮层返回值，
/// 取消/关闭（Ursa 走 <see cref="Irihi.Avalonia.Shared.Contracts.IDialogContext.Close"/>）视为未确认。
/// </summary>
public sealed class ShellDeleteConfirmationModel : Irihi.Avalonia.Shared.Contracts.IDialogContext
{
    public ShellDeleteConfirmationModel(string confirmationText, string warningText, string deletePath)
    {
        ConfirmationText = confirmationText;
        WarningText = warningText;
        DeletePath = deletePath;
    }

    public string ConfirmationText { get; }

    public string WarningText { get; }

    public string DeletePath { get; }

    public event EventHandler<object?>? RequestClose;

    public void Complete(bool confirmed) => RequestClose?.Invoke(this, confirmed);

    public void Close() => Complete(false);
}
