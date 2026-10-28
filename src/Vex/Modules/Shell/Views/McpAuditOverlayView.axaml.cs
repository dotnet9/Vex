using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Vex.Modules.Mcp.Models;
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
        string emptyText,
        bool embedded = false)
        : this()
    {
        _auditService = auditService;
        TitleText.Text = title;
        RefreshButton.Content = refreshText;
        EmptyTextBlock.Text = emptyText;
        LoadRecords();
        if (embedded && Content is Border border && border.Child is Grid grid)
        {
            // 偏好页里的审计就是一张表：标题行与关闭页脚都不属于原型，连同它们的行高一起收掉。
            // 偏好页正文在 ScrollViewer 里没有确定高度，表格只能按内容排，否则撑不满也滚不动。
            border.Classes.Remove("dialog-card"); border.Width = double.NaN; border.MaxHeight = double.PositiveInfinity;
            grid.RowDefinitions[0].Height = new GridLength(); grid.RowDefinitions[2].Height = new GridLength();
            grid.RowDefinitions[1].Height = GridLength.Auto;
            // 行高归零不等于间距归零：RowSpacing 仍会在两个空行之间留 12 像素，嵌入态一并收掉。
            grid.RowSpacing = 0;
            HeaderRow.IsVisible = false; FooterRow.IsVisible = false;
        }
    }

    private void LoadRecords()
    {
        // 原型 record() 用 unshift：最近一次调用排在表头下面第一行。
        var records = _auditService?.GetRecent() ?? [];
        var newestFirst = new List<McpOperationRecord>(records);
        newestFirst.Reverse();
        AuditGrid.ItemsSource = newestFirst;
        EmptyRow.IsVisible = newestFirst.Count == 0;
    }

    private void Refresh_OnClick(object? sender, RoutedEventArgs e) => LoadRecords();

    private void Close_OnClick(object? sender, RoutedEventArgs e) => ShellOverlayHost.Dismiss(this);
}
