using Vex.Core.Messaging;

namespace Vex.Core.Services;

public interface IWorkspaceDocumentState
{
    string Markdown { get; }

    string? FilePath { get; }

    /// <summary>正文或文件切换时触发（与消息总线同源），供需要区分「用户输入」与「文档载入」的订阅者使用。</summary>
    event EventHandler<MarkdownDocumentChangedCommand>? DocumentChanged;

    void UpdateDocument(string markdown, string? filePath);
}
