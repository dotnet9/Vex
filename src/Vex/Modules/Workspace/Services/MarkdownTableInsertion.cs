namespace Vex.Modules.Workspace.Services;

public sealed record TableDimensions(int Rows, int Columns);

/// <summary>行数包含表头；分隔行属于 Markdown 语法，不计入行数。</summary>
public static class MarkdownTableInsertion
{
    public static string Create(TableDimensions size, Func<int, string> columnTitle)
    {
        if (size.Rows is < 1 or > 100 || size.Columns is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(size));
        var lines = new List<string>
        {
            "| " + string.Join(" | ", Enumerable.Range(1, size.Columns).Select(columnTitle)) + " |",
            "| " + string.Join(" | ", Enumerable.Repeat("---", size.Columns)) + " |"
        };
        for (var row = 1; row < size.Rows; row++)
            lines.Add("| " + string.Join(" | ", Enumerable.Repeat("", size.Columns)) + " |");
        return string.Join('\n', lines);
    }

    public static string AtSelection(string document, int start, int length, string table)
    {
        var before = document[..start];
        var after = document[(start + length)..];
        var newline = document.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var prefix = before.Length == 0 || before.EndsWith(newline + newline, StringComparison.Ordinal) ? ""
            : before.EndsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
        var suffix = after.StartsWith(newline + newline, StringComparison.Ordinal) ? ""
            : after.StartsWith(newline, StringComparison.Ordinal) ? newline : newline + newline;
        return prefix + table.ReplaceLineEndings(newline) + suffix;
    }
}
