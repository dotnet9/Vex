using Avalonia;
using CodeWF.Avalonia.Markdown.Core.Blocks;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Threading;
using CodeWF.Avalonia.Markdown.Themes;
using CodeWF.Toolkit.Lang;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
/// <summary>所见即所得视图里一个表格块的编辑视图（每个单元格就地编辑）。</summary>
internal sealed class MarkdownTableView : UserControl
{
    private readonly Grid _grid = new global::CodeWF.Avalonia.Markdown.Rendering.MarkdownTableGrid();
    private readonly List<List<MarkdownBlockView>> _cells = [];
    private readonly MarkdownBlockModel _model;
    private readonly MarkdownBlockViewOptions _options;

    private static readonly string[] MenuResourceKeys =
    [
        MarkdownL.EditorTableAddRow, MarkdownL.EditorTableRemoveLastRow,
        MarkdownL.EditorTableAddColumn, MarkdownL.EditorTableRemoveLastColumn
    ];

    public MarkdownTableView(MarkdownBlockModel model, MarkdownBlockViewOptions options)
    {
        _model = model;
        _options = options;
        Resources.MergedDictionaries.Add(MarkdownThemes.CreateTypographyResources(options.TypographyTheme, options.TypographySize));
        _grid.Classes.Add("MdTable");
        Rebuild();
        var container = new Border
        {
            Child = _grid
        };
        container.SetValue(Border.BorderThicknessProperty, new Thickness(1), global::Avalonia.Data.BindingPriority.Style);
        container.Bind(Border.CornerRadiusProperty, this.GetResourceObservable("CodeWFMarkdownTableCornerRadius"), global::Avalonia.Data.BindingPriority.Style);
        container.Classes.Add("MdTableContainer");
        container.SetValue(Border.BorderBrushProperty, _options.SeparatorBrush, global::Avalonia.Data.BindingPriority.Style);
        container.SetValue(Control.HorizontalAlignmentProperty, HorizontalAlignment.Left, global::Avalonia.Data.BindingPriority.Style);
        Content = container;
        var menu = new ContextMenu();
        for (var action = 0; action < MenuResourceKeys.Length; action++)
        {
            var capturedAction = action;
            var item = new MenuItem { Header = MarkdownLocalization.GetResource(MenuResourceKeys[action]) };
            item.Click += (_, _) => StructureChangeRequested?.Invoke(this, capturedAction);
            menu.Items.Add(item);
        }
        if (options.IsDefaultContextMenuEnabled) ContextMenu = menu;
        AttachedToVisualTree += (_, _) =>
        {
            I18nManager.Instance.CultureChanged += OnLocalizationChanged;
            I18nManager.Instance.ResourcesChanged += OnLocalizationChanged;
            UpdateMenuHeaders();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            I18nManager.Instance.CultureChanged -= OnLocalizationChanged;
            I18nManager.Instance.ResourcesChanged -= OnLocalizationChanged;
        };
    }

    private void OnLocalizationChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess()) UpdateMenuHeaders();
        else Dispatcher.UIThread.Post(UpdateMenuHeaders);
    }

    private void UpdateMenuHeaders()
    {
        if (ContextMenu is null) return;
        for (var index = 0; index < MenuResourceKeys.Length; index++)
        {
            if (ContextMenu.Items[index] is MenuItem item)
                item.Header = MarkdownLocalization.GetResource(MenuResourceKeys[index]);
        }
    }

    /// <summary>单元格内容变化回调。</summary>
    public Action<MarkdownTableView>? CellsChanged { get; set; }
    public Action<MarkdownBlockView>? CellEditingStarted { get; set; }
    public Action? CellSelectionChanged { get; set; }
    private MarkdownBlockView? _activeCell;
    internal MarkdownBlockView? ActiveCell => _activeCell;
    internal MarkdownBlockView? FirstCell => _cells.SelectMany(row => row).FirstOrDefault();

    /// <summary>请求增删行列（参数为 0=加行 1=删行 2=加列 3=删列）。</summary>
    public Action<MarkdownTableView, int>? StructureChangeRequested { get; set; }

    public MarkdownBlockModel Model => _model;

    public void RefreshFromModel() => Rebuild();

    private void Rebuild()
    {
        _grid.Children.Clear();
        _grid.ColumnDefinitions.Clear();
        _grid.RowDefinitions.Clear();
        _cells.Clear();
        _activeCell = null;

        var rows = _model.Cells;
        var columnCount = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
        if (columnCount == 0)
        {
            return;
        }

        for (var column = 0; column < columnCount; column++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        for (var row = 0; row < rows.Count; row++)
        {
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var rowBoxes = new List<MarkdownBlockView>();
            for (var column = 0; column < columnCount; column++)
            {
                var value = column < rows[row].Count ? rows[row][column] : string.Empty;
                var isHeader = row == 0 && _model.HasHeader;
                var cellModel = new MarkdownBlockModel { Kind = MarkdownBlockKind.Paragraph, Text = value,
                    HeadingLevel = isHeader ? 6 : 0 };
                var box = new MarkdownBlockView(cellModel, false, _options, isTableCell: true)
                {
                    RunFontSize = Resource("CodeWFMarkdownTableCellFontSize", _options.FontSize),
                    RunLineHeight = Resource("CodeWFMarkdownTableCellLineHeight", double.NaN)
                };
                var capturedRow = row;
                var capturedColumn = column;
                box.TextCommitted = (_, text) => CommitCell(capturedRow, capturedColumn, text);
                box.EditingStarted = cell =>
                {
                    if (_activeCell != cell) _activeCell?.CommitAndRender();
                    _activeCell = cell;
                    CellEditingStarted?.Invoke(cell);
                };
                box.EditorSelectionChanged = () => CellSelectionChanged?.Invoke();
                box.SplitRequested = (_, _) => MoveFocus(capturedRow + 1, capturedColumn);
                box.NavigateRequested = direction =>
                {
                    var next = capturedRow * columnCount + capturedColumn + direction;
                    MoveFocus(next / columnCount, next % columnCount);
                };
                var cell = new Border { Child = box };
                cell.Bind(Border.BorderThicknessProperty, this.GetResourceObservable("CodeWFMarkdownTableCellBorderThickness"), global::Avalonia.Data.BindingPriority.Style);
                cell.Bind(Border.PaddingProperty, this.GetResourceObservable("CodeWFMarkdownTableCellPadding"), global::Avalonia.Data.BindingPriority.Style);
                cell.Bind(Control.MinHeightProperty, this.GetResourceObservable("CodeWFMarkdownTableCellMinHeight"), global::Avalonia.Data.BindingPriority.Style);
                cell.Classes.Add(isHeader ? "MdTableHeaderCell" : "MdTableCell");
                if (!isHeader && (row - (_model.HasHeader ? 1 : 0)) % 2 == 1) cell.Classes.Add("MdTableAlternateCell");
                if (isHeader) cell.Background = _options.TableHeaderBackgroundBrush ?? _options.CodeBackgroundBrush;
                cell.SetValue(Border.BorderBrushProperty, _options.SeparatorBrush, global::Avalonia.Data.BindingPriority.Style);
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                _grid.Children.Add(cell);
                rowBoxes.Add(box);
            }

            _cells.Add(rowBoxes);
        }
    }

    private double Resource(string key, double fallback) =>
        Resources.TryGetResource(key, ActualThemeVariant, out var value) && value is double number ? number : fallback;

    private void CommitCell(int row, int column, string value)
    {
        if (_model.Cells.Count <= row || _model.Cells[row].Count <= column)
        {
            return;
        }

        if (string.Equals(_model.Cells[row][column], value, StringComparison.Ordinal))
        {
            return;
        }

        _model.Cells[row][column] = value;
        CellsChanged?.Invoke(this);
    }

    private void MoveFocus(int row, int column)
    {
        if (row >= 0 && row < _cells.Count && column >= 0 && column < _cells[row].Count)
        {
            _cells[row][column].BeginEditAtEnd();
        }
    }
}
