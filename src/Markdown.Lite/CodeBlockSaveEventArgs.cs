namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 代码块下载请求：宿主可在 <see cref="MarkdownViewer.CodeBlockSaveRequested" /> 中接管保存
/// （置 <see cref="Handled" /> 跳过默认的系统保存对话框）。
/// </summary>
public sealed class CodeBlockSaveEventArgs : EventArgs
{
    public CodeBlockSaveEventArgs(string code, string? language, string? path, string suggestedFileName)
    {
        Code = code;
        Language = language;
        Path = path;
        SuggestedFileName = suggestedFileName;
    }

    public string Code { get; }

    public string? Language { get; }

    /// <summary>围栏信息里的路径（没有时为 null）。</summary>
    public string? Path { get; }

    public string SuggestedFileName { get; }

    /// <summary>置 true 表示宿主已自行保存，Viewer 不再弹保存对话框。</summary>
    public bool Handled { get; set; }
}
