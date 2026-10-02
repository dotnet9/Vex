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
        ApplyThemedSyntaxHighlighting();
    }

    // Markdown 语法着色跟随主题（对应原型 --md-code-fg / --md-quote 与标题用强调色），
    // 颜色资源缺失时保持 AvaloniaEdit 内置配色；六套主题的取值都在 AppPalette 中维护。
    private void ApplyThemedSyntaxHighlighting()
    {
        if (MarkdownEditor.SyntaxHighlighting is not { } definition)
        {
            return;
        }

        SetHighlightingForeground(definition, "Heading", GetBrush("VexAccentBrush"));
        SetHighlightingForeground(definition, "Code", GetBrush("VexCodeFgBrush"));
        SetHighlightingForeground(definition, "BlockQuote", GetBrush("VexQuoteBrush"));
        SetHighlightingForeground(definition, "Link", GetBrush("VexLinkBrush"));
        SetHighlightingForeground(definition, "Image", GetBrush("VexLinkBrush"));
        MarkdownEditor.TextArea.TextView.Redraw();
    }

    private static void SetHighlightingForeground(
        IHighlightingDefinition definition,
        string colorName,
        IBrush? foreground)
    {
        if (foreground is null)
        {
            return;
        }

        var color = definition.NamedHighlightingColors.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, colorName, StringComparison.OrdinalIgnoreCase));
        if (color is null)
        {
            return;
        }

        if (foreground is ISolidColorBrush solid)
        {
            color.Foreground = new SimpleHighlightingBrush(solid.Color);
        }
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
