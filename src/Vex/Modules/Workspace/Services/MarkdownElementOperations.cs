using Vex.Core.Messaging;

namespace Vex.Modules.Workspace.Services;

public sealed record ParsedTable(string[][] Rows, string[] Align);

public sealed record TableHit(int Start, int Length, string Raw, int Row, int Column);

public sealed record ChangedTable(string Raw, int Row, int Column);

/// <summary>
/// 源码与即时渲染共用的块级文本操作：表格结构（Typora 式 20 项）、段落转换与块定位。
/// 解析与序列化语义对齐原型 VexDocumentOps（分隔行校验、单元格转义与反引号串感知、列宽按内容补齐）。
/// </summary>
public static class MarkdownElementOperations
{
    public static ParsedTable? ParseTable(string raw)
    {
        var lines = raw.Replace("\r\n", "\n").Trim('\n').Split('\n');
        if (lines.Length < 2) return null;
        var head = SplitCells(lines[0]);
        var separators = SplitCells(lines[1]);
        if (head.Count == 0 || separators.Count != head.Count
            || separators.Any(cell => !System.Text.RegularExpressions.Regex.IsMatch(cell, "^:?-{3,}:?$")))
            return null;
        var rows = new List<string[]> { head.ToArray() };
        rows.AddRange(lines.Skip(2).Select(line => head.Select((_, i) => SplitCells(line).ElementAtOrDefault(i) ?? "").ToArray()));
        var align = separators.Select(cell => cell.StartsWith(':')
            ? (cell.EndsWith(':') ? "center" : "left")
            : (cell.EndsWith(':') ? "right" : "")).ToArray();
        return new ParsedTable([.. rows], align);
    }

    public static string SerializeTable(ParsedTable table)
    {
        var separators = table.Align.Select(align => align switch
        {
            "left" => ":---",
            "center" => ":---:",
            "right" => "---:",
            _ => "---",
        }).ToArray();
        var rows = new List<string[]> { table.Rows[0], separators };
        rows.AddRange(table.Rows.Skip(1));
        var widths = separators.Select((_, i) => Math.Max(3, rows.Max(row => (row.ElementAtOrDefault(i) ?? "").Length))).ToArray();
        return string.Join("\n", rows.Select(row => "| " + string.Join(" | ",
            widths.Select((width, i) => (row.ElementAtOrDefault(i) ?? "").PadRight(width))) + " |"));
    }

    public static bool CanTable(ParsedTable table, MarkdownTableAction action, int row, int column)
    {
        if (row < 0 || row >= table.Rows.Length || column < 0 || column >= table.Align.Length) return false;
        return action switch
        {
            MarkdownTableAction.RowAbove or MarkdownTableAction.RowDelete => row > 0,
            MarkdownTableAction.RowUp => row > 1,
            MarkdownTableAction.RowDown => row > 0 && row < table.Rows.Length - 1,
            MarkdownTableAction.ColLeft => column > 0,
            MarkdownTableAction.ColRight => column < table.Align.Length - 1,
            MarkdownTableAction.ColDelete => table.Align.Length > 1,
            _ => true,
        };
    }

    public static ChangedTable? ChangeTable(ParsedTable table, MarkdownTableAction action, int row, int column)
    {
        if (!CanTable(table, action, row, column)) return null;
        var rows = table.Rows.Select(current => current.ToArray()).ToArray();
        var align = aligns();
        string[] EmptyRow() => align.Select(_ => "").ToArray();
        string[][] mutated = rows;
        switch (action)
        {
            case MarkdownTableAction.RowAbove or MarkdownTableAction.RowBelow:
                if (action == MarkdownTableAction.RowBelow) row++;
                mutated = rows.Take(row).Concat([EmptyRow()]).Concat(rows.Skip(row)).ToArray();
                break;
            case MarkdownTableAction.RowDelete:
                mutated = rows.Where((_, index) => index != row).ToArray();
                row = Math.Min(row, mutated.Length - 1);
                break;
            case MarkdownTableAction.RowUp or MarkdownTableAction.RowDown:
                {
                    var next = action == MarkdownTableAction.RowUp ? row - 1 : row + 1;
                    (mutated[row], mutated[next]) = (mutated[next], mutated[row]);
                    row = next;
                    break;
                }
            case MarkdownTableAction.ColBefore or MarkdownTableAction.ColAfter:
                {
                    if (action == MarkdownTableAction.ColAfter) column++;
                    mutated = rows.Select(current => current.Take(column).Concat([""]).Concat(current.Skip(column)).ToArray()).ToArray();
                    align = align.Take(column).Concat([""]).Concat(align.Skip(column)).ToArray();
                    break;
                }
            case MarkdownTableAction.ColDelete:
                mutated = rows.Select(current => current.Where((_, index) => index != column).ToArray()).ToArray();
                align = align.Where((_, index) => index != column).ToArray();
                column = Math.Min(column, align.Length - 1);
                break;
            case MarkdownTableAction.ColLeft or MarkdownTableAction.ColRight:
                {
                    var next = action == MarkdownTableAction.ColLeft ? column - 1 : column + 1;
                    foreach (var current in mutated) (current[column], current[next]) = (current[next], current[column]);
                    (align[column], align[next]) = (align[next], align[column]);
                    column = next;
                    break;
                }
            case MarkdownTableAction.AlignLeft: align[column] = "left"; break;
            case MarkdownTableAction.AlignCenter: align[column] = "center"; break;
            case MarkdownTableAction.AlignRight: align[column] = "right"; break;
        }
        return new ChangedTable(SerializeTable(new ParsedTable(mutated, align)), row, column);

        string[] aligns() => table.Align.ToArray();
    }

    /// <summary>计算表格文本内第 row 行第 column 单元格的字符偏移，用于操作后把光标带回原位。</summary>
    public static int CellOffset(string tableRaw, int row, int column)
    {
        var lines = tableRaw.Replace("\r\n", "\n").Split('\n');
        if (row < 0 || row >= lines.Length) return 0;
        var lineStart = lines.Take(row).Sum(line => line.Length + 1);
        var line = lines[row];
        int SkipSpaces(int index)
        {
            while (index < line.Length && (line[index] == '|' || line[index] == ' ')) index++;
            return index;
        }
        if (column <= 0) return lineStart + SkipSpaces(0);
        var ticks = 0;
        var separators = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (current == '\\' && i + 1 < line.Length) { i++; continue; }
            if (current == '`')
            {
                var count = 1;
                while (i + count < line.Length && line[i + count] == '`') count++;
                if (ticks == 0) ticks = count;
                else if (ticks == count) ticks = 0;
                i += count - 1;
                continue;
            }
            if (current == '|' && ticks == 0 && ++separators == column)
                return Math.Min(lineStart + SkipSpaces(i + 1), lineStart + line.Length);
        }
        return lineStart + line.Length;
    }

    /// <summary>定位光标所在的表格块与其单元格；光标不在表格内时返回 null。</summary>
    public static TableHit? FindTableAt(string document, int caret)
    {        if (caret < 0 || caret > document.Length) return null;
        var newline = document.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = document.Replace("\r\n", "\n").Split('\n');
        var offsets = LineOffsets(lines, newline.Length);
        var line = Math.Min(IndexOfLine(offsets, caret), lines.Length - 1);
        if (!IsTableRow(lines[line]))
        {
            if (line + 1 < lines.Length && IsTableRow(lines[line + 1])) line++;
            else return null;
        }
        var first = line;
        while (first > 0 && IsTableRow(lines[first - 1])) first--;
        var last = line;
        while (last + 1 < lines.Length && IsTableRow(lines[last + 1])) last++;
        var start = offsets[first];
        var length = offsets[last + 1] - start - newline.Length;
        if (length <= 0 || start + length > document.Length) return null;
        var raw = document[start..(start + length)];
        if (ParseTable(raw) is not { } parsed) return null;
        var caretInTable = caret >= start && caret <= start + length;
        var cellLine = caretInTable ? line : first;
        var column = caretInTable ? Math.Clamp(caret - offsets[cellLine], 0, lines[cellLine].Length) : 0;
        // 分隔行属于语法行，不在行模型内：其下的数据行序号要再减一。
        var row = cellLine - first > 1 ? cellLine - first - 1 : cellLine - first;
        return new TableHit(start, length, raw, row, CellIndexAt(lines[cellLine], column));
    }

    /// <summary>把 raw（一个块）整体转换为目标类型；语义对齐原型 convert()。</summary>
    public static string ConvertBlock(string raw, string kind)
    {
        var lines = raw.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count == 2 && System.Text.RegularExpressions.Regex.IsMatch(lines[1], @"^\s*(=+|-+)\s*$")) lines.RemoveAt(1);
        var content = lines.Select(line => System.Text.RegularExpressions.Regex
            .Replace(line, @"^\s*(?:#{1,6}\s+|>\s?|(?:[-+*]|\d+[.)])\s+(?:\[[ xX]\]\s*)?)", "")).ToArray();
        if (kind.StartsWith("heading", StringComparison.Ordinal))
            return new string('#', kind[^1] - '0') + " " + string.Join(" ", content).Trim();
        if (kind == "paragraph") return string.Join("\n", content);
        return string.Join("\n", content.Select((line, i) => kind switch
        {
            "quote" => "> " + line,
            "list" => "- " + line,
            "ordered" => $"{i + 1}. " + line,
            "task" => "- [ ] " + line,
            _ => line,
        }));
    }

    /// <summary>定位光标所在块的字符范围：围栏感知，标题独立成块，列表项逐条，引用/表格/段落按连续行聚合。</summary>
    public static (int Start, int Length) FindBlockAt(string document, int caret)
    {
        var newline = document.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = document.Replace("\r\n", "\n").Split('\n');
        var offsets = LineOffsets(lines, newline.Length);
        var line = Math.Min(IndexOfLine(offsets, caret), lines.Length - 1);
        var kind = LineKind(lines, line);
        if (kind == LineType.Fence)
        {
            var fenceStart = line;
            while (fenceStart > 0 && LineKind(lines, fenceStart - 1) != LineType.Fence) fenceStart--;
            var fenceEnd = line;
            while (fenceEnd + 1 < lines.Length && LineKind(lines, fenceEnd + 1) != LineType.Fence) fenceEnd++;
            return RangeOf(offsets, fenceStart, Math.Min(fenceEnd + 1, lines.Length));
        }
        var first = line;
        while (first > 0 && JoinsPrevious(lines, first)) first--;
        var last = line;
        while (last + 1 < lines.Length && JoinsPrevious(lines, last + 1)) last++;
        return RangeOf(offsets, first, Math.Min(last + 1, lines.Length));
    }

    private enum LineType
    {
        Blank,
        Heading,
        TableCell,
        Fence,
        Quote,
        ListItem,
        Text,
    }

    private static LineType LineKind(string[] lines, int index)
    {
        var trimmed = lines[index].TrimStart();
        if (trimmed.Length == 0) return LineType.Blank;
        if (trimmed.StartsWith('#')) return LineType.Heading;
        if (IsTableRow(trimmed)) return LineType.TableCell;
        if (trimmed.StartsWith("```")) return LineType.Fence;
        if (trimmed.StartsWith('>')) return LineType.Quote;
        if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[-+*]\s")) return LineType.ListItem;
        return LineType.Text;
    }

    private static bool JoinsPrevious(string[] lines, int index)
    {
        var current = LineKind(lines, index);
        var previous = LineKind(lines, index - 1);
        if (current is LineType.Blank or LineType.Fence
            || previous is LineType.Blank or LineType.Fence) return false;
        if (current is LineType.Heading or LineType.ListItem) return false;
        if (current == LineType.TableCell) return previous == LineType.TableCell;
        if (current == LineType.Quote) return previous == LineType.Quote;
        return previous is LineType.Text or LineType.TableCell or LineType.Quote;
    }

    private static (int Start, int Length) RangeOf(int[] offsets, int firstLine, int lastLine) =>
        (offsets[firstLine], offsets[lastLine] - offsets[firstLine]);

    private static int[] LineOffsets(string[] lines, int newlineLength)
    {
        var offsets = new int[lines.Length + 1];
        for (var i = 0; i < lines.Length; i++) offsets[i + 1] = offsets[i] + lines[i].Length + newlineLength;
        return offsets;
    }

    private static int IndexOfLine(int[] offsets, int caret)
    {
        for (var i = offsets.Length - 1; i >= 0; i--)
            if (caret >= offsets[i]) return i;
        return 0;
    }

    private static bool IsTableRow(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 1 && trimmed.StartsWith('|') && trimmed.EndsWith('|');
    }

    /// <summary>按行内偏移求单元格序号：与 SplitCells 同一套转义/反引号规则数前面的分隔符；行首竖线不计。</summary>
    private static int CellIndexAt(string line, int column)
    {
        var ticks = 0;
        var separators = 0;
        for (var i = 0; i < line.Length && i < column; i++)
        {
            var current = line[i];
            if (current == '\\' && i + 1 < line.Length) { i++; continue; }
            if (current == '`')
            {
                var count = 1;
                while (i + count < line.Length && line[i + count] == '`') count++;
                if (ticks == 0) ticks = count;
                else if (ticks == count) ticks = 0;
                i += count - 1;
                continue;
            }
            if (current == '|' && ticks == 0) separators++;
        }
        if (line.TrimStart().StartsWith('|')) separators = Math.Max(0, separators - 1);
        return separators;
    }

    private static List<string> SplitCells(string line)
    {
        var result = new List<string>();
        var value = string.Empty;
        var ticks = 0;
        for (var i = 0; i < line.Length; i++)
        {
            var current = line[i];
            if (current == '\\' && i + 1 < line.Length)
            {
                value += current.ToString() + line[++i];
                continue;
            }
            if (current == '`')
            {
                var count = 1;
                while (i + count < line.Length && line[i + count] == '`') count++;
                if (ticks == 0) ticks = count;
                else if (ticks == count) ticks = 0;
                value += new string('`', count);
                i += count - 1;
                continue;
            }
            if (current == '|' && ticks == 0)
            {
                result.Add(value.Trim());
                value = string.Empty;
            }
            else value += current;
        }
        result.Add(value.Trim());
        var trimmedLine = line.Trim();
        if (trimmedLine.StartsWith('|') && result.Count > 0) result.RemoveAt(0);
        if (trimmedLine.EndsWith('|') && result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
        return result;
    }
}
