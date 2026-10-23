using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Vex.Core.Services;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Controls;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.Views;

/// <summary>
/// Mermaid 图表编辑器（对应原型 diagram-editor）：单击阅读视图中的图表块原位展开——
/// 上方源码、下方实时预览与语法提示；收起（Esc/Ctrl+Enter/点击块外）时一次性写回正文（可撤销）。
/// </summary>
public static class DiagramEditor
{
    private const string FenceMarker = "```mermaid";

    private static Border? _openBorder;
    private static Control? _openContent;
    private static Action? _closeActive;

    /// <summary>给阅读视图挂上图表原位编辑；重复调用安全。</summary>
    public static void Attach(Control root)
    {
        var index = 0;
        foreach (var border in root.GetVisualDescendants().OfType<Border>()
                     .Where(border => border.Classes.Contains("MdMermaid")))
        {
            var fenceIndex = index++;
            border.Cursor = new Cursor(StandardCursorType.Hand);
            if (border.GetValue(AttachedProperty)) continue;
            border.SetValue(AttachedProperty, true);
            ToolTip.SetTip(border, "点击编辑图表源码");
            border.Tapped += (_, _) => OpenInPlace(root, border, fenceIndex);
        }
    }

    /// <summary>原位展开某个图表块的编辑面板（供视图与验证器直接调用）。</summary>
    public static void OpenInPlace(Control root, Border border, int fenceIndex)
    {
        _closeActive?.Invoke();
        var markdown = root.GetVisualDescendants().OfType<MarkdownViewer>().FirstOrDefault()?.Markdown
            ?? string.Empty;
        var source = GetFenceSource(markdown, fenceIndex);
        if (source is null) return;

        var input = new TextBox
        {
            Text = source,
            AcceptsReturn = true,
            FontFamily = VexFonts.Code,
            FontSize = 12.5,
            MinHeight = 128,
            MaxHeight = 360,
            TextWrapping = TextWrapping.NoWrap,
        };
        var previewHost = new ContentControl { MinHeight = 120 };
        var status = new TextBlock { FontSize = 11.5, Opacity = 0.75, Margin = new Thickness(0, 6, 0, 0) };
        var surface = new StackPanel { Spacing = 6 };
        surface.Children.Add(input);
        surface.Children.Add(previewHost);
        surface.Children.Add(status);
        surface.Children.Add(new TextBlock
        {
            Text = "Esc 或点击块外完成编辑",
            FontSize = 11,
            Opacity = 0.6,
            Margin = new Thickness(0, 2, 0, 0),
        });

        DispatcherTimer? previewTimer = null;
        void RenderPreview()
        {
            previewHost.Content = new MarkdownViewer
            {
                Markdown = "```mermaid\n" + input.Text.TrimEnd() + "\n```",
            };
            status.Text = "预览由渲染器出图；语法有误时保留源码显示。";
        }
        input.PropertyChanged += (_, args) =>
        {
            if (args.Property != TextBox.TextProperty) return;
            previewTimer?.Stop();
            previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            previewTimer.Tick += (_, _) => { previewTimer?.Stop(); RenderPreview(); };
            previewTimer.Start();
        };

        void Collapse()
        {
            _closeActive = null;
            var (openBorder, openContent) = (_openBorder, _openContent);
            _openBorder = null;
            _openContent = null;
            if (openBorder.GetValue(OpenFenceIndexProperty) is { } index
                && openBorder.GetValue(OpenRootProperty) is { } openRoot
                && openBorder.GetValue(OpenMarkdownProperty) is { } original)
            {
                var edited = ReplaceFence(original, index, input.Text);
                if (edited != original)
                {
                    EventBus.Default.Publish(new MarkdownTextChangedCommand(edited, 1, 1,
                        edited.Count(character => character == '\n') + 1));
                }
            }
            if (ReferenceEquals(openBorder.Child, surface)) openBorder.Child = openContent;
            input.KeyDown -= OnInputKeyDown;
            root.PointerPressed -= OnRootPointerPressed;
        }

        void OnInputKeyDown(object? sender, KeyEventArgs args)
        {
            if (args.Key == Key.Escape || (args.Key == Key.Enter && args.KeyModifiers.HasFlag(KeyModifiers.Control)))
            {
                args.Handled = true;
                Collapse();
                border.Focus();
            }
        }

        void OnRootPointerPressed(object? sender, PointerPressedEventArgs args)
        {
            var position = args.GetPosition(surface);
            if (position.X < 0 || position.Y < 0 || position.X > surface.Bounds.Width || position.Y > surface.Bounds.Height) Collapse();
        }

        input.KeyDown += OnInputKeyDown;
        root.PointerPressed += OnRootPointerPressed;
        border.SetValue(OpenFenceIndexProperty, fenceIndex);
        border.SetValue(OpenRootProperty, root);
        border.SetValue(OpenMarkdownProperty, markdown);
        _openBorder = border;
        _openContent = border.Child;
        border.Child = surface;
        RenderPreview();
        _closeActive = Collapse;
        input.Focus();
    }

    private static readonly AttachedProperty<bool> AttachedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("DiagramEditorAttached", typeof(DiagramEditor));

    private static readonly AttachedProperty<int> OpenFenceIndexProperty =
        AvaloniaProperty.RegisterAttached<Border, int>("DiagramOpenFenceIndex", typeof(DiagramEditor));

    private static readonly AttachedProperty<Control?> OpenRootProperty =
        AvaloniaProperty.RegisterAttached<Border, Control?>("DiagramOpenRoot", typeof(DiagramEditor));

    private static readonly AttachedProperty<string?> OpenMarkdownProperty =
        AvaloniaProperty.RegisterAttached<Border, string?>("DiagramOpenMarkdown", typeof(DiagramEditor));

    /// <summary>按文档顺序取第 index 个 mermaid 围栏；越界返回 null。</summary>
    public static string? GetFenceSource(string markdown, int index)
    {
        var fences = SplitFences(markdown);
        return index >= 0 && index < fences.Count ? fences[index] : null;
    }

    /// <summary>把第 index 个 mermaid 围栏替换为新源码。</summary>
    public static string ReplaceFence(string markdown, int index, string source)
    {
        var position = 0;
        var seen = -1;
        while (position < markdown.Length)
        {
            var start = markdown.IndexOf(FenceMarker, position, StringComparison.Ordinal);
            if (start < 0) return markdown;
            seen++;
            var bodyStart = markdown.IndexOf('\n', start);
            if (bodyStart < 0) return markdown;
            var end = markdown.IndexOf("\n```", bodyStart, StringComparison.Ordinal);
            if (end < 0) return markdown;
            if (seen == index)
            {
                return markdown[..(start + FenceMarker.Length)] + "\n" + source.TrimEnd() + "\n```" + markdown[(end + 4)..];
            }
            position = end + 4;
        }
        return markdown;
    }

    private static List<string> SplitFences(string markdown)
    {
        var fences = new List<string>();
        var position = 0;
        while (position < markdown.Length)
        {
            var start = markdown.IndexOf(FenceMarker, position, StringComparison.Ordinal);
            if (start < 0) break;
            var bodyStart = markdown.IndexOf('\n', start);
            if (bodyStart < 0) break;
            var end = markdown.IndexOf("\n```", bodyStart, StringComparison.Ordinal);
            if (end < 0) break;
            fences.Add(markdown[(bodyStart + 1)..end].TrimEnd());
            position = end + 4;
        }
        return fences;
    }

}
