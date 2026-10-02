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
        string confirmText,
        string rememberChoiceText)
        : this()
    {
        DataContext = new McpOperationConfirmationWindowModel(title, message, toolName, target, summary, cancelText, confirmText, rememberChoiceText);
    }

    public bool Confirmed { get; private set; }

    public bool RememberChoice => DataContext is McpOperationConfirmationWindowModel model && model.RememberChoice;

    private void Cancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close(false);
    }

    private void Confirm_OnClick(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
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
    string ConfirmText,
    string RememberChoiceText)
{
    public bool RememberChoice { get; set; }
}
