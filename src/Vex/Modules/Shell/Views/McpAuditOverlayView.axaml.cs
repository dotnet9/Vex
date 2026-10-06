using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Shell.Services;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.Views;

public partial class McpAuditOverlayView : UserControl
{
    private readonly IMcpOperationAuditService? _auditService;

    public McpAuditOverlayView()
    {
        InitializeComponent();
    }

    public McpAuditOverlayView(
        IMcpOperationAuditService auditService,
        string title,
        string refreshText,
        string emptyText)
        : this()
    {
        _auditService = auditService;
        TitleText.Text = title;
        RefreshButton.Content = refreshText;
        EmptyTextBlock.Text = emptyText;
        LoadRecords();
    }

    private void LoadRecords()
    {
        var records = _auditService?.GetRecent() ?? [];
        RecordsItemsControl.ItemsSource = records;
        EmptyTextBlock.IsVisible = records.Count == 0;
    }

    private void Refresh_OnClick(object? sender, RoutedEventArgs e) => LoadRecords();

    private void Close_OnClick(object? sender, RoutedEventArgs e) => ShellOverlayHost.Dismiss(this);
}
