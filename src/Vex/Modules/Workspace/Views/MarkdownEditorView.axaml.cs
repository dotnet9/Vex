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
        AttachedToVisualTree += (_, _) =>
        {
            // 挂载后 ActualThemeVariant 才有真实值，主题字典资源此时才可解析——重新应用编辑器配色。
            ConfigureEditorVisuals();
            AttachEditorController();
        };
        DetachedFromVisualTree += (_, _) => ViewModel?.DetachEditor(MarkdownEditor);
    }

    private void ConfigureEditorVisuals()
    {
        MarkdownEditor.Options.HighlightCurrentLine = true;
        MarkdownEditor.TextArea.TextView.Margin = new Thickness(6, 0, 0, 0);
        MarkdownEditor.TextArea.TextView.CurrentLineBackground = GetBrush("VexEditorCurrentLineBackgroundBrush", Brushes.Transparent);
        MarkdownEditor.TextArea.TextView.CurrentLineBorder = new Pen(GetBrush("VexEditorCurrentLineBorderBrush", Brushes.Transparent), 1);
        ApplyThemedSyntaxHighlighting();
    }

    // Markdown 语法着色跟随主题（对应原型 --md-code-fg / --md-quote 与标题用强调色），
    // 颜色资源缺失时保持 AvaloniaEdit 内置配色；六套主题的取值都在 AppPalette 中维护。
    // 图片语法用独立的 VexImageBrush，与链接（VexLinkBrush）区分，对齐常规 Markdown 编辑器的源码样式。
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
        SetHighlightingForeground(definition, "Image", GetBrush("VexImageBrush"));
        // LineBreak 内置浅灰背景在暗色主题刺眼，换成主题分隔色。
        SetHighlightingBackground(definition, "LineBreak", GetBrush("VexSplitterBrush"));
        ApplyCodeHighlightingColors();
        MarkdownEditor.TextArea.TextView.Redraw();
    }

    // Markdown 缩进代码块通过 ruleSet 导入复用 C# 着色，内置配色（Green 注释、Blue 关键字等）
    // 在暗色编辑器背景上不可读，映射到主题语义色。Vex 只编辑 Markdown，直接改共享的 C# 定义无副作用。
    private static readonly IReadOnlyDictionary<string, string> CodeColorBrushKeys = new Dictionary<string, string>
    {
        ["Comment"] = "VexQuoteBrush",
        ["Preprocessor"] = "VexQuoteBrush",
        ["String"] = "VexWarningBrush",
        ["Char"] = "VexWarningBrush",
        ["StringInterpolation"] = "VexCodeFgBrush",
        ["NumberLiteral"] = "VexWarningBrush",
        ["Keywords"] = "VexLinkBrush",
        ["GotoKeywords"] = "VexLinkBrush",
        ["ValueTypeKeywords"] = "VexDangerBrush",
        ["ReferenceTypeKeywords"] = "VexDangerBrush",
        ["NullOrValueKeywords"] = "VexDangerBrush",
        ["MethodCall"] = "VexCodeFgBrush",
    };

    private void ApplyCodeHighlightingColors()
    {
        if (HighlightingManager.Instance.GetDefinition("C#") is not { } codeDefinition)
        {
            return;
        }

        foreach (var (colorName, brushKey) in CodeColorBrushKeys)
        {
            SetHighlightingForeground(codeDefinition, colorName, GetBrush(brushKey));
        }
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

    private static void SetHighlightingBackground(
        IHighlightingDefinition definition,
        string colorName,
        IBrush? background)
    {
        if (background is null)
        {
            return;
        }

        var color = definition.NamedHighlightingColors.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, colorName, StringComparison.OrdinalIgnoreCase));
        if (color is null)
        {
            return;
        }

        if (background is ISolidColorBrush solid)
        {
            color.Background = new SimpleHighlightingBrush(solid.Color);
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

    private IBrush? GetBrush(string key, IBrush? fallback = null)
    {
        // 必须走 Application 级查找：控件级 TryGetResource 只查自身 Resources（恒为空），
        // 主题色永远解析失败。此前回退透明曾把 Link 高亮变成"隐形文字"。
        // 传入 ActualThemeVariant 以匹配 AppPalette 的 ThemeDictionaries（六套主题各自键值）。
        IBrush? result = fallback;
        if (Application.Current is { } app
            && app.TryGetResource(key, ActualThemeVariant, out var resource)
            && resource is IBrush brush)
        {
            result = brush;
        }

        return result;
    }
}
