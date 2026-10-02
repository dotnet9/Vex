using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellRenameFileOverlayView : UserControl
{
    private ShellDialogsViewModel? _dialogs;

    public ShellRenameFileOverlayView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => UnsubscribeDialogs();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UnsubscribeDialogs();
        if (DataContext is MainWindowViewModel main)
        {
            _dialogs = main.Dialogs;
            _dialogs.PropertyChanged += OnDialogsPropertyChanged;
        }
    }

    private void UnsubscribeDialogs()
    {
        if (_dialogs is not null)
        {
            _dialogs.PropertyChanged -= OnDialogsPropertyChanged;
            _dialogs = null;
        }
    }

    private void OnDialogsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 弹窗每次唤起：聚焦输入框并只预选主文件名（扩展名不选中），对应原型升级点①。
        if (e.PropertyName != nameof(ShellDialogsViewModel.IsRenameFilePanelVisible)
            || _dialogs is not { IsRenameFilePanelVisible: true })
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            RenameFileNameTextBox.Focus();
            var name = _dialogs?.RenameFileName ?? string.Empty;
            var dot = name.LastIndexOf('.');
            RenameFileNameTextBox.SelectionStart = 0;
            RenameFileNameTextBox.SelectionEnd = dot > 0 ? dot : name.Length;
        });
    }
}
