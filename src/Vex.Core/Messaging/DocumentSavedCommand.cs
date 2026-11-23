using CodeWF.Toolkit.EventBus;

namespace Vex.Core.Messaging;

/// <summary>文档落盘成功（保存/另存为）：文件树 Git 徽章据此触发重算（未跟踪 ↔ 修改态翻转）。</summary>
public sealed class DocumentSavedCommand : Command
{
    public DocumentSavedCommand(string filePath) => FilePath = filePath;

    public string FilePath { get; }
}
