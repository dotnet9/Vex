namespace Vex.Core.Models;

public sealed record RecentDocument(string Path)
{
    public string FileName => System.IO.Path.GetFileName(Path);

    public string FolderName => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path)) ?? string.Empty;

    /// <summary>菜单项显示「文件名（含扩展名）- 父目录」；完整路径由悬停 ToolTip 呈现（评审定版 2026-11-10）。</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(FolderName)
        ? FileName
        : $"{FileName} - {FolderName}";
}
