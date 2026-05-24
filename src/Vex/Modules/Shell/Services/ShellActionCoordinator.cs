using System.Diagnostics;
using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public sealed class ShellActionCoordinator
{
    private readonly MainWindowViewModel _shell;

    public ShellActionCoordinator(MainWindowViewModel shell)
    {
        _shell = shell;
        CodeWF.EventBus.EventBus.Default.Subscribe(this);
    }

    [EventHandler]
    public void ApplyShellAction(ShellActionCommand command)
    {
        // 菜单控件只发布用户意图，这里统一转回 Shell 文档流程，避免 View 直接耦合主窗口。
        _ = HandleShellActionAsync(command);
    }

    private async Task HandleShellActionAsync(ShellActionCommand command)
    {
        try
        {
            switch (command.Action)
            {
                case ShellActionKind.NewDocument:
                    await _shell.NewDocument();
                    break;
                case ShellActionKind.NewWindow:
                    OpenNewWindow();
                    break;
                case ShellActionKind.Open:
                    await _shell.OpenAsync();
                    break;
                case ShellActionKind.OpenFolder:
                    await _shell.OpenFolderAsync();
                    break;
                case ShellActionKind.QuickOpen:
                    await _shell.QuickOpenAsync();
                    break;
                case ShellActionKind.OpenRecentDocument:
                    await OpenRecentDocumentAsync(command.Parameter);
                    break;
                case ShellActionKind.ReopenWithEncoding:
                    await _shell.ReopenWithEncodingAsync(command.Parameter);
                    break;
                case ShellActionKind.Save:
                    await _shell.SaveAsync();
                    break;
                case ShellActionKind.SaveAs:
                    await _shell.SaveAsAsync();
                    break;
                case ShellActionKind.SaveAll:
                    await _shell.SaveAllAsync();
                    break;
                case ShellActionKind.ShowProperties:
                    _shell.ShowProperties();
                    break;
                case ShellActionKind.OpenFileLocation:
                    await _shell.OpenFileLocationAsync();
                    break;
                case ShellActionKind.Delete:
                    await _shell.DeleteAsync();
                    break;
                case ShellActionKind.Export:
                    await _shell.Export(command.Parameter);
                    break;
                case ShellActionKind.CopyHtml:
                    await _shell.CopyHtml(command.Parameter);
                    break;
                case ShellActionKind.Print:
                    await _shell.Print();
                    break;
                case ShellActionKind.CloseDocument:
                    await _shell.CloseDocument();
                    break;
                case ShellActionKind.ShowFindPanel:
                    _shell.ShowFindPanel();
                    break;
                case ShellActionKind.ShowReplacePanel:
                    _shell.ShowReplacePanel();
                    break;
                case ShellActionKind.WordCount:
                    _shell.WordCount();
                    break;
            }
        }
        catch (Exception exception)
        {
            var messageKey = command.Action == ShellActionKind.NewWindow
                ? VexL.ErrorMessageCannotStartNewWindow
                : VexL.ErrorMessageActionFailed;
            _shell.Dialogs.ShowError(messageKey, exception);
        }
    }

    private Task OpenRecentDocumentAsync(string? indexText)
    {
        return int.TryParse(indexText, out var index)
            ? _shell.OpenRecentDocumentAsync(index)
            : Task.CompletedTask;
    }

    private static void OpenNewWindow()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = true });
        }
    }
}
