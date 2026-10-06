namespace Vex.Core.Messaging;

/// <summary>拖入本地图片文件：由工作区把图片复制到当前文档的 assets 目录并插入 Markdown。</summary>
public sealed class ShellImageDroppedCommand : CodeWF.EventBus.Command
{
    public ShellImageDroppedCommand(string sourcePath)
    {
        SourcePath = sourcePath;
    }

    public string SourcePath { get; }
}
