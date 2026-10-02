using Avalonia.Interactivity;
using Ursa.Controls;

namespace Vex.Modules.Mcp.Views;

public partial class McpOperationConfirmationWindow : UrsaWindow
{
    public McpOperationConfirmationWindow()
    {
        InitializeComponent();
    }

    public McpOperationConfirmationWindow(
        string title,
        string message,
        string toolName,
        string target,
        string summary,
        string cancelText,
        string confirmText)
        : this()
    {
        DataContext = new McpOperationConfirmationWindowModel(title, message, toolName, target, summary, cancelText, confirmText);
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Confirm_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }
}

public sealed record McpOperationConfirmationWindowModel(
    string Title,
    string Message,
    string ToolName,
    string Target,
    string Summary,
    string CancelText,
    string ConfirmText);
