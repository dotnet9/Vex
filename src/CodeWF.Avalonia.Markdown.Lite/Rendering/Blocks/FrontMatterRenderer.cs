using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Rendering;

/// <summary>
/// Front matter（FR-16）：文档顶部的 YAML 元数据块渲染为 GitHub 风格的两列
/// 键值表（左键右值，行分隔线，列表值渲染为徽章），文本可选中复制。键值按
/// 「key: value」与「- item」逐行解析，无法解析的行归入上一键的原始展示；
/// 样式经 CodeWFMarkdownFrontMatter* 资源键随主题解析。源码/即时编辑保持
/// 原样 YAML 文本，不受此渲染影响。
/// </summary>
internal sealed class FrontMatterRenderer : IMarkdownBlockRenderer
{
    private static readonly System.Text.RegularExpressions.Regex KeyLine =
        new("^(?<key>[A-Za-z][\\w-]*)\\s*:\\s*(?<value>.*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public Control? TryRender(Block block, string? sourceMarkdown, IMarkdownRenderContext context)
    {
        if (block is not YamlFrontMatterBlock yaml)
        {
            return null;
        }

        var entries = ParseEntries(yaml.Lines.ToString().Replace("\r\n", "\n").Split('\n'));

        var border = new Border();
        context.AddMarkdownClass(border, MarkdownStyleKeys.Quote);
        border.Classes.Add("MdFrontMatter");
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBackgroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196")) { Opacity = 0.08 }));

        var label = new TextBlock
        {
            Text = MarkdownLocalization.GetResource(MarkdownL.FrontMatterTitle),
            FontWeight = FontWeight.SemiBold,
            FontSize = context.ParagraphFontSize * 0.85,
            Margin = new Thickness(12, 10, 12, 6),
        };
        label.Bind(TextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));

        var grid = new Grid
        {
            ColumnDefinitions = ColumnDefinitions.Parse("Auto,*"),
            Margin = new Thickness(0, 0, 0, 6),
        };
        for (var i = 0; i < entries.Count; i++)
        {
            var (key, value, items) = entries[i];
            var isLast = i == entries.Count - 1;

            var keyCell = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, isLast ? 0 : 1),
                Padding = new Thickness(14, 7, 14, 7),
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            keyCell.Bind(Border.BorderBrushProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));
            var keyText = new global::Avalonia.Controls.SelectableTextBlock
            {
                Text = key,
                FontWeight = FontWeight.Medium,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
            };
            keyText.Bind(global::Avalonia.Controls.SelectableTextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterForegroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#5B625E"))));
            keyCell.Child = keyText;
            Grid.SetColumn(keyCell, 0);
            Grid.SetRow(keyCell, i);
            grid.Children.Add(keyCell);

            var valueCell = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
                Padding = new Thickness(14, 7, 14, 7),
            };
            valueCell.Bind(Border.BorderBrushProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));
            if (items.Count > 0)
            {
                var badges = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var item in items)
                {
                    var badge = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 2, 8, 2),
                        Margin = new Thickness(0, 1, 6, 1),
                        BorderThickness = new Thickness(1),
                    };
                    badge.Bind(Border.BorderBrushProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterBorderBrush", v => v ?? new SolidColorBrush(Color.Parse("#8A9196"))));
                    var badgeText = new global::Avalonia.Controls.SelectableTextBlock { Text = item };
                    badgeText.Bind(global::Avalonia.Controls.SelectableTextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterForegroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#5B625E"))));
                    badge.Child = badgeText;
                    badges.Children.Add(badge);
                }

                valueCell.Child = badges;
            }
            else
            {
                var valueText = new global::Avalonia.Controls.SelectableTextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
                valueText.Bind(global::Avalonia.Controls.SelectableTextBlock.ForegroundProperty, border.GetResourceObservable("CodeWFMarkdownFrontMatterForegroundBrush", v => v ?? new SolidColorBrush(Color.Parse("#5B625E"))));
                valueCell.Child = valueText;
            }

            Grid.SetColumn(valueCell, 1);
            Grid.SetRow(valueCell, i);
            grid.Children.Add(valueCell);
            grid.RowDefinitions.Add(new RowDefinition());
        }

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(label);
        stack.Children.Add(grid);
        border.Child = stack;
        return border;
    }

    private static List<(string Key, string Value, List<string> Items)> ParseEntries(string[] lines)
    {
        var entries = new List<(string Key, string Value, List<string> Items)>();
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.Trim() == "---")
            {
                continue;
            }

            var match = KeyLine.Match(line);
            if (match.Success)
            {
                var value = match.Groups["value"].Value.Trim();
                value = value.Length >= 2 && (value.StartsWith('"') && value.EndsWith('"') || value.StartsWith('\'') && value.EndsWith('\''))
                    ? value[1..^1]
                    : value;
                entries.Add((match.Groups["key"].Value, value, []));
            }
            else if (line.TrimStart().StartsWith("- ") && entries.Count > 0)
            {
                var item = line.TrimStart()[2..].Trim().Trim('"');
                entries[^1].Items.Add(item);
            }
            else if (entries.Count > 0)
            {
                // 无法解析的行归入上一键原样展示，避免内容凭空消失。
                var last = entries[^1];
                last.Value = string.IsNullOrEmpty(last.Value) ? line.Trim() : $"{last.Value}\n{line.Trim()}";
                entries[^1] = last;
            }
        }

        return entries;
    }
}
