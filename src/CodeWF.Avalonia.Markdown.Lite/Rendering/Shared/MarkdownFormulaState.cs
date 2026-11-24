using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

internal sealed class MarkdownFormulaState : AvaloniaObject
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<MarkdownFormulaState, Control, string?>("Text");

    public static readonly AttachedProperty<IBrush?> SearchBrushProperty =
        AvaloniaProperty.RegisterAttached<MarkdownFormulaState, Control, IBrush?>("SearchBrush");

    public static readonly AttachedProperty<int> SelectionStartProperty =
        AvaloniaProperty.RegisterAttached<MarkdownFormulaState, Control, int>("SelectionStart", -1);

    public static readonly AttachedProperty<int> SelectionEndProperty =
        AvaloniaProperty.RegisterAttached<MarkdownFormulaState, Control, int>("SelectionEnd", -1);

    public static string? GetText(Control control) => control.GetValue(TextProperty);
    public static void SetText(Control control, string? value) => control.SetValue(TextProperty, value);
    public static IBrush? GetSearchBrush(Control control) => control.GetValue(SearchBrushProperty);
    public static void SetSearchBrush(Control control, IBrush? value) => control.SetValue(SearchBrushProperty, value);
    public static int GetSelectionStart(Control control) => control.GetValue(SelectionStartProperty);
    public static void SetSelectionStart(Control control, int value) => control.SetValue(SelectionStartProperty, value);
    public static int GetSelectionEnd(Control control) => control.GetValue(SelectionEndProperty);
    public static void SetSelectionEnd(Control control, int value) => control.SetValue(SelectionEndProperty, value);

    public static void ClearSelection(Control control)
    {
        control.SetValue(SelectionStartProperty, -1);
        control.SetValue(SelectionEndProperty, -1);
    }
}
