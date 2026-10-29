using System.ComponentModel;
using Avalonia.Controls;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellWorkflowNotice : UserControl
{
    private ShellDocumentProtection? _protection;

    public ShellWorkflowNotice()
    {
        InitializeComponent();
        // 危险态类名走接线而非 Classes 绑定：编译绑定对 Classes 索引器在本仓验证中不生效（探测记录于 ui-verification）。
        DataContextChanged += (_, _) =>
        {
            if (_protection is not null) _protection.PropertyChanged -= OnProtectionPropertyChanged;
            _protection = (DataContext as MainWindowViewModel)?.Protection;
            if (_protection is not null) _protection.PropertyChanged += OnProtectionPropertyChanged;
            ApplyErrorClass();
        };
    }

    private void OnProtectionPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ApplyErrorClass();

    private void ApplyErrorClass()
    {
        if (Content is not Border border) return;
        if (_protection?.HasErrorNotice == true) border.Classes.Add("error");
        else border.Classes.Remove("error");
    }
}
