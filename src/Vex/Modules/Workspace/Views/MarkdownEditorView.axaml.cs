using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using Vex.Modules.Workspace.ViewModels;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownEditorView : UserControl
{
    public MarkdownEditorView()
    {
        InitializeComponent();
        MarkdownEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("MarkDown");
        ConfigureEditorVisuals();
        ActualThemeVariantChanged += (_, _) => ConfigureEditorVisuals();
        MarkdownEditor.AddHandler(InputElement.KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += (_, _) => AttachEditorController();
        AttachedToVisualTree += (_, _) => AttachEditorController();
        DetachedFromVisualTree += (_, _) => ViewModel?.DetachEditor(MarkdownEditor);
    }

    private void ConfigureEditorVisuals()
    {
        MarkdownEditor.Options.HighlightCurrentLine = true;
        MarkdownEditor.TextArea.TextView.Margin = new Thickness(6, 0, 0, 0);
        MarkdownEditor.TextArea.TextView.CurrentLineBackground = GetBrush("VexEditorCurrentLineBackgroundBrush");
        MarkdownEditor.TextArea.TextView.CurrentLineBorder = new Pen(GetBrush("VexEditorCurrentLineBorderBrush"), 1);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && IsPlainPasteGesture(e.KeyModifiers))
        {
            e.Handled = true;
            ViewModel?.Paste();
            return;
        }

        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = ViewModel?.HandleEditorKeyDown(e.Key, e.KeyModifiers) == true;
            return;
        }

        if (e.Key != Key.Tab)
        {
            return;
        }

        // Tab/Shift+Tab 只发布编辑动作，缩进文本如何变化由 Workspace 控制器统一维护。
        e.Handled = true;
        ViewModel?.HandleEditorKeyDown(e.Key, e.KeyModifiers);
    }

    private static bool IsPlainPasteGesture(KeyModifiers modifiers)
    {
        return modifiers is KeyModifiers.Control or KeyModifiers.Meta;
    }

    private void AttachEditorController()
    {
        ViewModel?.AttachEditor(MarkdownEditor);
    }

    private MarkdownEditorViewModel? ViewModel => DataContext as MarkdownEditorViewModel;

    private IBrush GetBrush(string key)
    {
        return this.TryGetResource(key, ActualThemeVariant, out var resource) && resource is IBrush brush
            ? brush
            : Brushes.Transparent;
    }
}
