using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellStatusViewModel : ReactiveObject
{
    private readonly IAppLocalizer _localizer;
    private string _statusText;
    private int _caretLine = 1;
    private int _caretColumn = 1;
    private bool _isReadyStatus = true;

    public ShellStatusViewModel(IAppLocalizer localizer)
    {
        _localizer = localizer;
        _statusText = _localizer.Get(VexL.StatusReady);
        _localizer.CultureChanged += OnCultureChanged;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public int CaretLine
    {
        get => _caretLine;
        set => SetProperty(ref _caretLine, value);
    }

    public int CaretColumn
    {
        get => _caretColumn;
        set => SetProperty(ref _caretColumn, value);
    }

    [EventHandler]
    public void ApplyWorkspaceStatusChanged(WorkspaceStatusChangedCommand command)
    {
        _isReadyStatus = false;
        StatusText = command.Message;
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        CaretLine = command.CaretLine;
        CaretColumn = command.CaretColumn;
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (_isReadyStatus)
        {
            // 空闲状态仍显示“就绪”时才随语言切换刷新，避免覆盖刚发生的用户操作反馈。
            StatusText = _localizer.Get(VexL.StatusReady);
        }
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
