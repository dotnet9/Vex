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
/// Mermaid 图表编辑器（对应原型 diagram-editor）：双击阅读视图中的图表，
/// 源码 + 实时预览 + 语法回退提示；保存经 MarkdownTextChangedCommand 写回正文（可撤销）。
/// </summary>
public static class DiagramEditor
{
    private const string FenceMarker = "```mermaid";

    /// <summary>给阅读视图挂上图表双击编辑；重复调用安全。</summary>
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
            ToolTip.SetTip(border, "双击编辑图表源码");
            border.DoubleTapped += (_, _) => OpenEditor(root, fenceIndex);
        }
    }

    private static readonly AttachedProperty<bool> AttachedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("DiagramEditorAttached", typeof(DiagramEditor));

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

    private static void OpenEditor(Control root, int fenceIndex)
    {
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
            MinHeight = 180,
            TextWrapping = TextWrapping.NoWrap,
        };
        var previewHost = new ContentControl { MinHeight = 120 };
        var status = new TextBlock { FontSize = 11.5, Opacity = 0.75, Margin = new Thickness(0, 6, 0, 0) };
        var apply = new Button { Content = "保存并写回", Classes = { "primary" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        var cancel = new Button { Content = "取消", Classes = { "wb-quiet" }, MinHeight = 30, Padding = new Thickness(14, 4) };
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(cancel);
        actions.Children.Add(apply);

        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(2) };
        stack.Children.Add(new TextBlock { Text = "编辑 Mermaid 图表", FontSize = 15, FontWeight = FontWeight.SemiBold });
        stack.Children.Add(input);
        stack.Children.Add(new TextBlock { Text = "实时预览", FontSize = 12, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
        stack.Children.Add(previewHost);
        stack.Children.Add(status);
        stack.Children.Add(actions);
        var scroll = new ScrollViewer { Content = stack, MaxHeight = 560, MaxWidth = 760, MinWidth = 520, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };

        DispatcherTimer? previewTimer = null;
        void RenderPreview()
        {
            previewHost.Content = new MarkdownViewer
            {
                Markdown = "```mermaid\n" + input.Text.TrimEnd() + "\n```",
                Width = 700,
            };
            status.Text = "预览由渲染器出图；语法有误时保留源码显示。";
        }
        input.PropertyChanged += (_, args) =>
        {
            if (args.Property != TextBox.TextProperty) return;
            previewTimer?.Stop();
            previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            previewTimer.Tick += (_, _) => { previewTimer?.Stop(); RenderPreview(); };
            previewTimer.Start();
        };
        RenderPreview();

        var finished = new TaskCompletionSource<bool>();
        apply.Click += (_, _) => finished.TrySetResult(true);
        cancel.Click += (_, _) => finished.TrySetResult(false);
        _ = Ursa.Controls.OverlayDialog.ShowCustomAsync<bool>(scroll, new object(),
            options: new Ursa.Controls.OverlayDialogOptions
            {
                CanLightDismiss = true, CanDragMove = false, CanResize = true, IsCloseButtonVisible = false,
            });
        _ = finished.Task.ContinueWith(task =>
        {
            if (!task.Result) return;
            Dispatcher.UIThread.Post(() =>
                EventBus.Default.Publish(new MarkdownTextChangedCommand(
                    ReplaceFence(markdown, fenceIndex, input.Text), 1, 1, 1)));
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
}
