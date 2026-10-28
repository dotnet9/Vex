using Avalonia.Controls;

using Microsoft.Extensions.DependencyInjection;
using Vex.Modules.Shell.ViewModels;
namespace Vex.Modules.Shell.Views;

public partial class ShellStatusBarView : UserControl
{
    public ShellStatusBarView()
    {
        InitializeComponent();
        // 原 ViewModelLocator 反射装配改为构造自取容器单例。
        DataContext = App.Services.GetRequiredService<ShellStatusBarViewModel>();
    }
}
