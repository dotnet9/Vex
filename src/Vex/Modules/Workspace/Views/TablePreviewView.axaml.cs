using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Irihi.Avalonia.Shared.Contracts;
using Ursa.Controls;

namespace Vex.Modules.Workspace.Views;

/// <summary>窗内大表格预览：常规规模用可选格网（跨行/列拖选复制），超大表由 DataGrid 虚拟化。</summary>
public partial class TablePreviewView : UserControl, IDialogContext
{
    /// <summary>可选格网的行数上限：全部单元格物化为控件，超出走 DataGrid 虚拟化路径。</summary>
    private const int SelectableRowLimit = 300;

    private TopLevel? _window;
    public event EventHandler<object?>? RequestClose;

    /// <summary>展示行（已含表头、已按列数补齐）。</summary>
    private IReadOnlyList<string[]> _rows = [];
    private bool _hasHeader;

    private readonly List<(SelectableTextBlock Cell, int Row, int Col)> _cells = [];
    private (int Row, int Col)? _anchorCell;
    private (int Row, int Col)? _activeCell;

    public TablePreviewView() : this(Array.Empty<IReadOnlyList<string>>()) { }

    public TablePreviewView(IReadOnlyList<IReadOnlyList<string>> rows, bool hasHeader = true)
    {
        InitializeComponent();
        _hasHeader = hasHeader;
        var columns = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        _rows = rows.Select(row => Enumerable.Range(0, columns)
            .Select(column => column < row.Count ? row[column] : string.Empty).ToArray()).ToArray();

        if (_rows.Count <= SelectableRowLimit)
        {
            BuildSelectableTable();
        }
        else
        {
            BuildVirtualizedTable(columns);
        }

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
        };
        KeyDown += OnPreviewCopy;
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

    // ---- 常规规模：可选格网（跨行/列拖选，Ctrl+C 复制） ----

    private void BuildSelectableTable()
    {
        var grid = SelectableTable;
        grid.ColumnDefinitions.Clear();
        grid.RowDefinitions.Clear();
        grid.Children.Clear();
        _cells.Clear();

        var columns = _rows.Count == 0 ? 0 : _rows.Max(row => row.Length);
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star),
                MinWidth = 90,
            });
        }

        for (var row = 0; row < _rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var isHeader = row == 0 && _hasHeader;
            for (var column = 0; column < _rows[row].Length; column++)
            {
                var cell = new Border
                {
                    BorderBrush = new SolidColorBrush(0x00000000),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = new SelectableTextBlock
                    {
                        Text = _rows[row][column],
                        TextWrapping = TextWrapping.Wrap,
                        Padding = new Thickness(12, 10),
                        Focusable = false,
                    },
                };
                cell.Bind(Border.BorderBrushProperty, cell.GetResourceObservable("VexPanelBorderBrush"));
                cell.Child!.Bind(TextBlock.ForegroundProperty, cell.GetResourceObservable("VexValueBrush"));
                if (isHeader)
                {
                    cell.Bind(Border.BackgroundProperty, cell.GetResourceObservable("VexDocumentTableHeaderBrush"));
                }

                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
                _cells.Add(((SelectableTextBlock)cell.Child!, row, column));
            }
        }

        // 单元格指针事件在格网层统一接管（Tunnel 先于可选文本块原生选择）：
        // 按下定锚、拖动矩形扩选，Ctrl+C 按行 Tab、跨行换行拼接复制。
        grid.AddHandler(PointerPressedEvent, OnSelectableCellPressed, RoutingStrategies.Tunnel);
        grid.PointerMoved += OnSelectablePointerMoved;
        grid.PointerReleased += OnSelectablePointerReleased;
        SelectableTableHost.IsVisible = true;
    }

    private void OnSelectableCellPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(SelectableTable).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var hit = HitCell(e.GetPosition(SelectableTable));
        if (hit is null)
        {
            return;
        }

        _anchorCell = hit;
        _activeCell = null;
        ApplySelectableSelection();
        e.Pointer.Capture(SelectableTable);
        e.Handled = true;
    }

    private void OnSelectablePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_anchorCell is null || !e.GetCurrentPoint(SelectableTable).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var hit = HitCell(e.GetPosition(SelectableTable));
        if (hit is null || _activeCell == hit && hit != _anchorCell)
        {
            return;
        }

        _activeCell = hit;
        ApplySelectableSelection();
        e.Handled = true;
    }

    private void OnSelectablePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_anchorCell is null)
        {
            return;
        }

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private (int Row, int Col)? HitCell(Point gridPoint)
    {
        foreach (var (cell, row, col) in _cells)
        {
            var origin = cell.TranslatePoint(new Point(0, 0), SelectableTable);
            if (origin is { } point && new Rect(point, cell.Bounds.Size).Contains(gridPoint))
            {
                return (row, col);
            }
        }

        return null;
    }

    private void ApplySelectableSelection()
    {
        var anchor = _anchorCell ?? (0, 0);
        var active = _activeCell ?? anchor;
        var (rowFrom, rowTo) = Normalize(anchor.Row, active.Row);
        var (colFrom, colTo) = Normalize(anchor.Col, active.Col);
        foreach (var (cell, row, col) in _cells)
        {
            var inside = row >= rowFrom && row <= rowTo && col >= colFrom && col <= colTo;
            if (inside)
            {
                cell.SelectAll();
            }
            else
            {
                cell.ClearSelection();
            }
        }
    }

    private static (int From, int To) Normalize(int a, int b) => a <= b ? (a, b) : (b, a);

    /// <summary>选区文本：行内 Tab、跨行换行；首行是否含表头随选区覆盖决定。</summary>
    private string? BuildSelectedText()
    {
        if (_anchorCell is null || SelectableTableHost.IsVisible is false)
        {
            return null;
        }

        var anchor = _anchorCell.Value;
        var active = _activeCell ?? anchor;
        var (rowFrom, rowTo) = Normalize(anchor.Row, active.Row);
        var (colFrom, colTo) = Normalize(anchor.Col, active.Col);
        var lines = new List<string>();
        for (var row = rowFrom; row <= rowTo; row++)
        {
            var cells = new List<string>();
            for (var column = colFrom; column <= colTo; column++)
            {
                cells.Add(column < _rows[row].Length ? _rows[row][column] : string.Empty);
            }

            lines.Add(string.Join("\t", cells));
        }

        return string.Join("\n", lines);
    }

    private void OnPreviewCopy(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.C || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        var text = BuildSelectedText();
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            clipboard.SetTextAsync(text);
            e.Handled = true;
        }
    }

    // ---- 超大表：DataGrid 虚拟化路径 ----

    private void BuildVirtualizedTable(int columns)
    {
        for (var column = 0; column < columns; column++)
            PreviewTable.Columns.Add(new DataGridTextColumn
            {
                Header = new TextBlock
                {
                    Text = _hasHeader && column < _rows[0].Length ? _rows[0][column] : (column + 1).ToString(),
                    TextWrapping = TextWrapping.Wrap,
                },
                Binding = new Binding($"[{column}]"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            });
        PreviewTable.ItemsSource = _rows
            .Skip(_hasHeader ? 1 : 0)
            .ToArray();
        PreviewTable.IsVisible = true;
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
