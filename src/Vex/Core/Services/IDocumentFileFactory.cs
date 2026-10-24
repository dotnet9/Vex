using Vex.Core.Models;

namespace Vex.Core.Services;

public interface IDocumentFileFactory
{
    DocumentFile Create(string path, string? workspaceRoot = null);

    string GetModifiedText(string path);

    /// <summary>仅在列表可见或提示打开时读取摘要；默认实现兼容轻量验证替身。</summary>
    string ReadPreview(string path) => string.Empty;
}
