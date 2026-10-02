using Avalonia.Interactivity;
using Ursa.Controls;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.Views;

public partial class McpAuditWindow : UrsaWindow
{
    private readonly IMcpOperationAuditService _auditService;
    private readonly string _emptyText;

    public McpAuditWindow()
    {
        InitializeComponent();
    }

    public McpAuditWindow(
        IMcpOperationAuditService auditService,
        string title,
        string refreshText,
        string emptyText)
        : this()
    {
        _auditService = auditService;
        _emptyText = emptyText;
        Title = title;
        RefreshButton.Content = refreshText;
        EmptyTextBlock.Text = emptyText;
        LoadRecords();
    }

    private void LoadRecords()
    {
        var records = _auditService.GetRecent();
        RecordsItemsControl.ItemsSource = records;
        EmptyTextBlock.IsVisible = records.Count == 0;
    }

    private void Refresh_OnClick(object? sender, RoutedEventArgs e)
    {
        LoadRecords();
    }
}
