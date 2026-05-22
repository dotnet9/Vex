using System.Runtime.CompilerServices;
using CodeWF.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellStatusViewModel : ReactiveObject
{
    private string _statusText = "Ready";
    private int _caretLine = 1;
    private int _caretColumn = 1;

    public ShellStatusViewModel(IEventBus eventBus)
    {
        eventBus.Subscribe(this);
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
        StatusText = command.Message;
    }

    [EventHandler]
    public void ApplyMarkdownTextChanged(MarkdownTextChangedCommand command)
    {
        CaretLine = command.CaretLine;
        CaretColumn = command.CaretColumn;
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
