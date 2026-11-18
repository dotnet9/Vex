using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// Front matter（FR-16）：文档顶部的 YAML 元数据块渲染为置顶元数据卡
/// （键值表，非正文），源码编辑保持原样文本。键值按「key: value」逐行解析，
/// 无法解析的行原样展示；样式经 CodeWFMarkdownFrontMatter* 资源键随主题解析。
/// </summary>
internal sealed class FrontMatterRenderer : IMarkdownBlockRenderer
{
    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not YamlFrontMatterBlock yaml)
        {
            return null;
        }

        var lines = yaml.Lines.ToString().Replace("\r\n", "\n").Split('\n');

        var border = new Border();
        context.AddMarkdownClass(border, MarkdownStyleKeys.Quote);
        border.Classes.Add("MdFrontMatter");
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBackgroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196")) { Opacity = 0.08 }));

        var stack = new StackPanel { Orientation = Orientation.Vertical };

        var label = new TextBlock
        {
            Text = "元数据",
            FontWeight = FontWeight.SemiBold,
            FontSize = context.ParagraphFontSize * 0.85,
            Margin = new Thickness(0, 0, 0, 4),
        };
        label.Bind(TextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));
        stack.Children.Add(label);

        foreach (var line in lines)
        {
            if (!line.Trim().Any())
            {
                continue;
            }

            var row = new Grid { ColumnDefinitions = ColumnDefinitions.Parse("Auto,*"), Margin = new Thickness(0, 1) };
            var match = System.Text.RegularExpressions.Regex.Match(line, "^([A-Za-z][\\w-]*)\\s*:\\s*(.*)$");
            if (match.Success)
            {
                var key = new TextBlock
                {
                    Text = match.Groups[1].Value,
                    FontWeight = FontWeight.Medium,
                    Margin = new Thickness(0, 0, 16, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                key.Bind(TextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterForegroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#5B625E"))));
                var value = new TextBlock { Text = match.Groups[2].Value, TextWrapping = TextWrapping.Wrap };
                value.Bind(TextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterForegroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#5B625E"))));
                Grid.SetColumn(key, 0);
                Grid.SetColumn(value, 1);
                row.Children.Add(key);
                row.Children.Add(value);
            }
            else
            {
                var raw = new TextBlock { Text = line, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(raw, 0);
                Grid.SetColumnSpan(raw, 2);
                row.Children.Add(raw);
            }

            stack.Children.Add(row);
        }

        border.Child = stack;
        return border;
    }
}
