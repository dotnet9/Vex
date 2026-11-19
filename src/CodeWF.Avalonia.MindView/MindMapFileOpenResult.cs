namespace CodeWF.Avalonia.MindView;

public sealed record MindMapFileOpenResult(
    string FilePath,
    MindMapFileFormat Format,
    string? TextContent,
    byte[]? BinaryContent);
