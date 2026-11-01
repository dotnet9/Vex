using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace Vex.Modules.Workspace.Views;

/// <summary>窗内大表格预览：按桌面窗口限宽，数据行由 DataGrid 虚拟化。</summary>
public partial class TablePreviewView : UserControl, IDialogContext
{
    private TopLevel? _window;
    public event EventHandler<object?>? RequestClose;

    public TablePreviewView() : this(Array.Empty<IReadOnlyList<string>>()) { }

    public TablePreviewView(IReadOnlyList<IReadOnlyList<string>> rows, bool hasHeader = true)
    {
        InitializeComponent();
        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        for (var column = 0; column < columns; column++)
            PreviewTable.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock { Text = hasHeader && column < rows[0].Count ? rows[0][column] : (column + 1).ToString(), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                Binding = new Binding($"[{column}]"), Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        PreviewTable.ItemsSource = rows.Skip(hasHeader ? 1 : 0)
            .Select(row => Enumerable.Range(0, columns).Select(column => column < row.Count ? row[column] : string.Empty).ToArray()).ToArray();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        AttachedToVisualTree += (_, _) =>
        {
            _window = TopLevel.GetTopLevel(this);
            if (_window is null) return;
            _window.SizeChanged += OnWindowSizeChanged;
            ResizeToWindow();
            Dispatcher.UIThread.Post(() => ClosePreview.Focus(), DispatcherPriority.Loaded);
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (_window is not null) _window.SizeChanged -= OnWindowSizeChanged;
            _window = null;
        };
        SizeChanged += (_, _) => Vex.Controls.OverlayDialogLayout.CenterAfterLayout(this);
    }

    public static Task<bool> Show(IReadOnlyList<IReadOnlyList<string>> rows, Control owner, bool hasHeader = true)
    {
        var view = new TablePreviewView(rows, hasHeader);
        if (TopLevel.GetTopLevel(owner) is Window window)
        {
            view.DocumentName.Text = window.Title;
            view.PreviewCard.Width = Math.Min(1200, window.ClientSize.Width - 64);
            view.PreviewCard.MaxHeight = window.ClientSize.Height - 64;
        }
        return OverlayDialog.ShowCustomAsync<bool>(view, view, options: new OverlayDialogOptions
        {
            CanLightDismiss = true, CanDragMove = false, CanResize = false, IsCloseButtonVisible = false,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
    }

    private void ResizeToWindow()
    {
        if (_window is null) return;
        PreviewCard.Width = Math.Min(1200, Math.Max(200, _window.ClientSize.Width - 64));
        PreviewCard.MaxHeight = Math.Max(120, _window.ClientSize.Height - 64);
        Vex.Controls.OverlayDialogLayout.CenterAfterLayout(this);
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e) => ResizeToWindow();
    private void OnClose(object? sender, RoutedEventArgs e) => Close();
    public void Close() => RequestClose?.Invoke(this, false);
}
