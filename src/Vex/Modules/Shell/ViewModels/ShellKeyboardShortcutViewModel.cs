using Avalonia.Input;
using ReactiveUI;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellKeyboardShortcutViewModel : ReactiveObject
{
    public ShellKeyboardShortcutViewModel(
        ShellFindBarViewModel findBar,
        ShellWindowLayoutViewModel layout)
    {
        FindBar = findBar;
        Layout = layout;
    }

    public ShellFindBarViewModel FindBar { get; }

    public ShellWindowLayoutViewModel Layout { get; }

    public bool HandleKeyDown(Key key, KeyModifiers keyModifiers)
    {
        var hasControl = keyModifiers.HasFlag(KeyModifiers.Control);
        var hasShift = keyModifiers.HasFlag(KeyModifiers.Shift);
        var hasAlt = keyModifiers.HasFlag(KeyModifiers.Alt);

        // 窗口级快捷键只做意图路由，文件 I/O 与未保存确认继续由 ShellActionCoordinator 统一处理。
        if ((hasControl || keyModifiers.HasFlag(KeyModifiers.Meta)) && key == Key.OemComma)
            return PublishShellAction(ShellActionKind.ShowPreferences);
        if (hasControl && hasShift && key == Key.N) return PublishShellAction(ShellActionKind.NewWindow);

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
            return PublishShellAction(ShellActionKind.QuickOpen);
        }

        if (hasControl && hasShift && key == Key.P) return PublishShellAction(ShellActionKind.Print);

        // 评审修订（原型 C5）：补齐格式快捷键（Typora 惯例）——链接/行内代码/删除线/高亮/上下标，
        // 经 EditorActionCommand 走既有分发（工具条、菜单与 MCP 工具同一通道）。
        if (hasControl && !hasAlt && !hasShift && key == Key.K)
            return PublishEditorAction(EditorActionKind.Link);
        if (hasControl && hasShift && key == Key.OemTilde)
            return PublishEditorAction(EditorActionKind.InlineCode);
        if (hasControl && hasShift && key is Key.D5 or Key.OemBackslash)
            return PublishEditorAction(EditorActionKind.Strikethrough);
        if (hasControl && hasShift && key == Key.H)
            return PublishEditorAction(EditorActionKind.Highlight);
        if (hasControl && hasShift && key is Key.OemPlus or Key.Add)
            return PublishEditorAction(EditorActionKind.Superscript);
        if (hasControl && hasShift && key is Key.OemMinus or Key.Subtract)
            return PublishEditorAction(EditorActionKind.Subscript);

        if (key == Key.F8 && keyModifiers == KeyModifiers.None) { Layout.ToggleFocusMode(); return true; }

        if (hasControl && !hasShift && key == Key.W)
        {
            return PublishShellAction(ShellActionKind.CloseDocument);
        }

        if (key == Key.F11)
        {
            Layout.ToggleFullScreen();
            return true;
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
            if (hasShift)
            {
                FindBar.FindPrev();
            }
            else
            {
                FindBar.FindNext();
            }

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
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellActionCommand(action));
        return true;
    }

    private bool PublishEditorAction(EditorActionKind action)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorActionCommand(action));
        return true;
    }

}
