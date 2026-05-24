using Avalonia.Input;
using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellKeyboardShortcutViewModel : ReactiveObject
{
    public ShellKeyboardShortcutViewModel(
        ShellDialogsViewModel dialogs,
        ShellEditorDisplayViewModel editorDisplay,
        ShellFindBarViewModel findBar,
        ShellWindowLayoutViewModel layout)
    {
        Dialogs = dialogs;
        EditorDisplay = editorDisplay;
        FindBar = findBar;
        Layout = layout;
    }

    public ShellDialogsViewModel Dialogs { get; }

    public ShellEditorDisplayViewModel EditorDisplay { get; }

    public ShellFindBarViewModel FindBar { get; }

    public ShellWindowLayoutViewModel Layout { get; }

    public bool HandleKeyDown(Key key, KeyModifiers keyModifiers)
    {
        var hasControl = keyModifiers.HasFlag(KeyModifiers.Control);
        var hasShift = keyModifiers.HasFlag(KeyModifiers.Shift);
        var hasAlt = keyModifiers.HasFlag(KeyModifiers.Alt);

        // 窗口级快捷键只做意图路由，文件 I/O 与未保存确认继续由 ShellActionCoordinator 统一处理。
        if (hasControl && !hasShift && key == Key.N)
        {
            return PublishShellAction(ShellActionKind.NewDocument);
        }

        if (hasControl && !hasShift && key == Key.O)
        {
            return PublishShellAction(ShellActionKind.Open);
        }

        if (hasControl && !hasShift && key == Key.S)
        {
            return PublishShellAction(ShellActionKind.Save);
        }

        if (hasControl && hasShift && key == Key.S)
        {
            return PublishShellAction(ShellActionKind.SaveAs);
        }

        if (hasControl && !hasShift && key == Key.P)
        {
            return PublishShellAction(ShellActionKind.Print);
        }

        if (hasControl && !hasShift && key == Key.W)
        {
            return PublishShellAction(ShellActionKind.CloseDocument);
        }

        if (hasControl && !hasShift && IsZoomInKey(key))
        {
            EditorDisplay.ZoomIn();
            return true;
        }

        if (hasControl && !hasShift && IsZoomOutKey(key))
        {
            EditorDisplay.ZoomOut();
            return true;
        }

        if (hasControl && !hasShift && IsActualSizeKey(key))
        {
            EditorDisplay.ActualSize();
            return true;
        }

        if (key == Key.F11)
        {
            Layout.ToggleFullScreen();
            return true;
        }

        if (hasAlt && key == Key.Enter)
        {
            return PublishShellAction(ShellActionKind.ShowProperties);
        }

        if (hasControl && key == Key.F)
        {
            FindBar.ShowFindPanel();
            return true;
        }

        if (hasControl && key == Key.H)
        {
            FindBar.ShowReplacePanel();
            return true;
        }

        if (key == Key.F3)
        {
            FindBar.FindNext();
            return true;
        }

        if (key == Key.Escape && Dialogs.CloseFloatingPanel())
        {
            return true;
        }

        if (key == Key.Escape && FindBar.IsVisible)
        {
            FindBar.CloseFindPanel();
            return true;
        }

        return false;
    }

    private bool PublishShellAction(ShellActionKind action)
    {
        CodeWF.EventBus.EventBus.Default.Publish(new ShellActionCommand(action));
        return true;
    }

    private static bool IsZoomInKey(Key key)
    {
        return key is Key.OemPlus or Key.Add;
    }

    private static bool IsZoomOutKey(Key key)
    {
        return key is Key.OemMinus or Key.Subtract;
    }

    private static bool IsActualSizeKey(Key key)
    {
        return key is Key.D0 or Key.NumPad0;
    }
}
