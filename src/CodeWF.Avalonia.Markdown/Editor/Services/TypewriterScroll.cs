namespace CodeWF.Avalonia.Markdown.Editor.Services;

internal static class TypewriterScroll
{
    public static double GetTargetOffset(double caretCenterInContent, double viewportHeight) =>
        viewportHeight <= 0 ? 0 : Math.Max(0, caretCenterInContent - viewportHeight / 2);
}
