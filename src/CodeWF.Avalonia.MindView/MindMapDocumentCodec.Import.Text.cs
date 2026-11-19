using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeWF.Avalonia.MindView.I18n;

namespace CodeWF.Avalonia.MindView;

public static partial class MindMapDocumentCodec
{
    private static MindMapNode FromJson(string json, string? filePath)
    {
        using var document = JsonDocument.Parse(json);
        var rootElement = document.RootElement;
        if (rootElement.ValueKind == JsonValueKind.Object
            && rootElement.TryGetProperty("rootTopic", out var xmindRootTopic))
        {
            return FromXMindJsonTopic(xmindRootTopic);
        }

        if (rootElement.ValueKind == JsonValueKind.Array
            && rootElement.EnumerateArray().FirstOrDefault().ValueKind == JsonValueKind.Object
            && rootElement.EnumerateArray().First().TryGetProperty("rootTopic", out var arrayRootTopic))
        {
            return FromXMindJsonTopic(arrayRootTopic);
        }

        return FromJsonElement(rootElement, GetFileTitle(filePath, "JSON"));
    }

    private static MindMapNode FromJsonElement(JsonElement element, string fallbackTitle)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("root", out var root))
        {
            return FromJsonElement(root, fallbackTitle);
        }

        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("data", out var data)
            && TryGetKnownJsonTitle(data) is not null)
        {
            element = data;
        }

        var node = new MindMapNode(TryGetKnownJsonTitle(element) ?? fallbackTitle)
        {
            Note = TryGetKnownJsonNote(element) ?? string.Empty
        };

        foreach (var child in EnumerateJsonChildren(element).Take(200))
        {
            node.Children.Add(FromJsonElement(child, string.Empty));
        }

        if (node.Children.Count == 0 && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject().Take(80))
            {
                if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    node.Children.Add(new MindMapNode($"{property.Name}: {CleanText(property.Value.ToString())}"));
                }
            }
        }
        else if (node.Children.Count == 0 && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray().Take(200))
            {
                node.Children.Add(FromJsonElement(child, string.Empty));
            }
        }

        return node;
    }

    private static string? TryGetKnownJsonTitle(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return CleanText(element.GetString());
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "title", "text", "name", "topic", "label", "value" })
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return CleanText(value.GetString());
            }
        }

        if (element.TryGetProperty("data", out var data))
        {
            return TryGetKnownJsonTitle(data);
        }

        return null;
    }

    private static string? TryGetKnownJsonNote(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "note", "notes", "description", "desc", "memo" })
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return CleanText(value.GetString());
            }
        }

        return null;
    }

    private static IEnumerable<JsonElement> EnumerateJsonChildren(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var name in new[] { "children", "topics", "nodes", "items", "subtopics" })
        {
            if (!element.TryGetProperty(name, out var children))
            {
                continue;
            }

            if (children.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in children.EnumerateArray())
                {
                    yield return child;
                }
            }
            else if (children.ValueKind == JsonValueKind.Object)
            {
                if (children.TryGetProperty("attached", out var attached) && attached.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in attached.EnumerateArray())
                    {
                        yield return child;
                    }
                }

                if (children.TryGetProperty("children", out var nested) && nested.ValueKind == JsonValueKind.Array)
                {
                    foreach (var child in nested.EnumerateArray())
                    {
                        yield return child;
                    }
                }
            }
        }

        if (element.TryGetProperty("data", out var data))
        {
            foreach (var child in EnumerateJsonChildren(data))
            {
                yield return child;
            }
        }
    }

    private static MindMapNode FromYaml(string yaml, string? filePath)
    {
        return FromIndentedLines(SplitLines(yaml), GetFileTitle(filePath, "YAML"));
    }

    private static MindMapNode FromPlainText(string text, string? filePath)
    {
        return FromIndentedLines(SplitLines(text), GetFileTitle(filePath, "TXT"));
    }

    private static MindMapNode FromIndentedLines(IEnumerable<string> lines, string fallbackTitle)
    {
        MindMapNode? root = null;
        var stack = new Stack<(int Indent, MindMapNode Node)>();
        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var trimmed = rawLine.Trim();
            if (trimmed.StartsWith('#'))
            {
                continue;
            }

            var indent = rawLine.TakeWhile(char.IsWhiteSpace).Sum(ch => ch == '\t' ? 4 : 1);
            var title = CleanText(Regex.Replace(trimmed, @"^[-*+]\s+|^\d+[.)]\s+", string.Empty));
            while (stack.Count > 0 && stack.Peek().Indent >= indent)
            {
                stack.Pop();
            }

            var node = new MindMapNode(title);
            if (root is null)
            {
                root = node;
            }
            else if (stack.Count == 0)
            {
                root.Children.Add(node);
            }
            else
            {
                stack.Peek().Node.Children.Add(node);
            }

            stack.Push((indent, node));
        }

        return root ?? new MindMapNode(fallbackTitle);
    }

    private static MindMapNode FromCsv(string csv, string? filePath)
    {
        var rows = SplitLines(csv)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(ParseCsvLine)
            .Where(row => row.Count > 0)
            .ToList();
        var root = new MindMapNode(GetFileTitle(filePath, "CSV"));
        if (rows.Count == 0)
        {
            return root;
        }

        var headers = rows[0];
        foreach (var row in rows.Skip(1).Take(200))
        {
            var title = row.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? string.Empty;
            var node = new MindMapNode(CleanText(title));
            node.Note = string.Join(
                Environment.NewLine,
                row.Select((value, index) =>
                    $"{(index < headers.Count ? headers[index] : FormatResource(MindViewL.CsvColumnTitle, "Column {0}", index + 1))}: {value}"));
            root.Children.Add(node);
        }

        return root;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                result.Add(builder.ToString().Trim());
                builder.Clear();
                continue;
            }

            builder.Append(ch);
        }

        result.Add(builder.ToString().Trim());
        return result;
    }

    private static MindMapNode FromHtml(string html, string? filePath)
    {
        var matches = Regex.Matches(
            html,
            @"<h([1-6])[^>]*>(.*?)</h\1>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matches.Count == 0)
        {
            return FromPlainText(StripHtml(html), filePath);
        }

        var root = new MindMapNode(GetFileTitle(filePath, "HTML"));
        var stack = new Dictionary<int, MindMapNode> { [0] = root };
        foreach (Match match in matches)
        {
            var level = int.Parse(match.Groups[1].Value);
            var node = new MindMapNode(CleanText(match.Groups[2].Value));
            var parentLevel = level - 1;
            while (parentLevel > 0 && !stack.ContainsKey(parentLevel))
            {
                parentLevel--;
            }

            stack[parentLevel].Children.Add(node);
            stack[level] = node;
            foreach (var stale in stack.Keys.Where(key => key > level).ToList())
            {
                stack.Remove(stale);
            }
        }

        return root.Children.Count == 1 ? root.Children[0] : root;
    }

    private static MindMapNode FromSvg(string svg, string? filePath)
    {
        var document = XDocument.Parse(svg);
        var root = new MindMapNode(GetFileTitle(filePath, "SVG"));
        foreach (var text in document
                     .Descendants()
                     .Where(element => element.Name.LocalName is "title" or "desc" or "text")
                     .Select(element => CleanText(element.Value))
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Take(80))
        {
            root.Children.Add(new MindMapNode(text));
        }

        return root.Children.Count == 0 ? CreateMetadataNode(MindMapFileFormat.Svg, filePath) : root;
    }
}
