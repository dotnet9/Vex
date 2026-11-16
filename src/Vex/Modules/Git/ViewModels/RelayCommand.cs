using System.Windows.Input;

namespace Vex.Modules.Git.ViewModels;

/// <summary>Git 面板的轻量同步命令：ReactiveCommand 的 Execute 依赖调度器，无调度泵的环境
/// （验证夹具）不落地；桌面绑定只需要同步 ICommand——直发直执行。</summary>
public sealed class RelayCommand<T>(Action<T?> execute, Func<T?, bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke(Cast(parameter)) ?? true;

    public void Execute(object? parameter) => execute(Cast(parameter));

    private static T? Cast(object? parameter) => parameter is T value ? value : default;
}
