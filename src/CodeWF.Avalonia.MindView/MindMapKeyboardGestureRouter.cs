using global::Avalonia.Input;

namespace CodeWF.Avalonia.MindView;

public static class MindMapKeyboardGestureRouter
{
    public static MindMapKeyboardAction ResolveTitleAction(
        Key key,
        KeyModifiers modifiers,
        bool isTitleEmpty,
        MindMapKeyboardTabBehavior tabBehavior = MindMapKeyboardTabBehavior.Hierarchy)
    {
        if (IsEnterKey(key))
        {
            return MindMapKeyboardAction.AddFromEnter;
        }

        if (key == Key.Tab)
        {
            return ResolveTabAction(modifiers, tabBehavior);
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            if (key == Key.Up)
            {
                return MindMapKeyboardAction.MoveUp;
            }

            if (key == Key.Down)
            {
                return MindMapKeyboardAction.MoveDown;
            }
        }

        return IsDeleteKey(key) && isTitleEmpty
            ? MindMapKeyboardAction.DeleteEmptyTitle
            : MindMapKeyboardAction.None;
    }

    public static MindMapKeyboardAction ResolveFrameAction(
        Key key,
        KeyModifiers modifiers,
        MindMapKeyboardTabBehavior tabBehavior = MindMapKeyboardTabBehavior.Hierarchy)
    {
        if (IsDeleteKey(key))
        {
            return MindMapKeyboardAction.DeleteSelected;
        }

        if (IsEnterKey(key))
        {
            return MindMapKeyboardAction.AddFromEnter;
        }

        if (key == Key.Tab)
        {
            return ResolveTabAction(modifiers, tabBehavior);
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            if (key == Key.Up)
            {
                return MindMapKeyboardAction.MoveUp;
            }

            if (key == Key.Down)
            {
                return MindMapKeyboardAction.MoveDown;
            }
        }

        return MindMapKeyboardAction.None;
    }

    public static MindMapKeyboardAction ResolveNoteAction(Key key, bool isNoteEmpty)
    {
        return IsDeleteKey(key) && isNoteEmpty
            ? MindMapKeyboardAction.DeleteEmptyNote
            : MindMapKeyboardAction.None;
    }

    private static MindMapKeyboardAction ResolveTabAction(
        KeyModifiers modifiers,
        MindMapKeyboardTabBehavior tabBehavior)
    {
        if (tabBehavior == MindMapKeyboardTabBehavior.AddChild)
        {
            return MindMapKeyboardAction.AddChildFromTab;
        }

        return modifiers.HasFlag(KeyModifiers.Shift)
            ? MindMapKeyboardAction.Promote
            : MindMapKeyboardAction.Demote;
    }

    public static bool IsEnterKey(Key key)
    {
        return key is Key.Enter or Key.Return;
    }

    public static bool IsDeleteKey(Key key)
    {
        return key is Key.Delete or Key.Back;
    }
}
