using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using Vex.Modules.Workspace.Services;

namespace Vex.Modules.Shell.Views;

public partial class ShellInsertTableOverlayView : UserControl
{
    public ShellInsertTableOverlayView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            TableRows.Focus();
            TableRows.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.SelectAll();
        }, DispatcherPriority.Loaded);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Model?.Close(); e.Handled = true; }
            else if (e.Key == Key.Enter && Model?.IsValid == true) { Model.Confirm(); e.Handled = true; }
        };
    }

    private ShellInsertTableModel? Model => DataContext as ShellInsertTableModel;
    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Model?.Close();
    private void Insert_OnClick(object? sender, RoutedEventArgs e) => Model?.Confirm();
}

public sealed class ShellInsertTableModel : ReactiveObject, Irihi.Avalonia.Shared.Contracts.IDialogContext
{
    private decimal? _rows = 3;
    private decimal? _columns = 3;
    public decimal? Rows { get => _rows; set { this.RaiseAndSetIfChanged(ref _rows, value); this.RaisePropertyChanged(nameof(IsValid)); } }
    public decimal? Columns { get => _columns; set { this.RaiseAndSetIfChanged(ref _columns, value); this.RaisePropertyChanged(nameof(IsValid)); } }
    public bool IsValid => Rows is >= 1 and <= 100 && Columns is >= 1 and <= 20
        && Rows == decimal.Truncate(Rows.Value) && Columns == decimal.Truncate(Columns.Value);
    public event EventHandler<object?>? RequestClose;
    public void Confirm() { if (IsValid) RequestClose?.Invoke(this, new TableDimensions((int)Rows!.Value, (int)Columns!.Value)); }
    public void Close() => RequestClose?.Invoke(this, null);
}
