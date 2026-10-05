using System.Runtime.CompilerServices;
using ReactiveUI;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>
/// 重命名自定义对话框（Ursa OverlayDialog）的 UI 状态。
/// RequestClose 的参数即重命名结果：确认时为新文件名，取消/关闭为 null。
/// </summary>
public sealed class ShellRenameDialogViewModel : ReactiveObject, Irihi.Avalonia.Shared.Contracts.IDialogContext
{
    private readonly IAppLocalizer _localizer;
    private string _fileName;
    private string? _error;

    public ShellRenameDialogViewModel(IAppLocalizer localizer, string path, string fileName)
    {
        _localizer = localizer;
        Path = path;
        _fileName = fileName;
    }

    public string Path { get; }

    public string FileName
    {
        get => _fileName;
        set => SetProperty(ref _fileName, value);
    }

    public string? Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    public event EventHandler<object?>? RequestClose;

    public void Confirm()
    {
        var newName = _fileName.Trim();
        if (newName.Length == 0)
        {
            Error = _localizer.Get(VexL.RenameErrorEmpty);
            return;
        }

        if (newName.IndexOfAny(global::System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            Error = _localizer.Get(VexL.RenameErrorInvalidChars);
            return;
        }

        RequestClose?.Invoke(this, newName);
    }

    public void Cancel()
    {
        RequestClose?.Invoke(this, null);
    }

    public void Close()
    {
        Cancel();
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }
}
