using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

/// <summary>
/// 文档切换广播（文件 ↔ 文件真实切换时发布；正文编辑不发布）。
/// 阅读视图、即时编辑与源码编辑器订阅它，把滚动条与光标复位到顶部。
/// </summary>
public sealed class MarkdownDocumentSwitchedCommand : Command
{
    public MarkdownDocumentSwitchedCommand(string fileName) => FileName = fileName;

    public string FileName { get; }
}
