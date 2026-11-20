using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public interface IShellDocumentUtilityActions
{

    Task<bool> ExportAsync(DocumentSnapshot document, string markdown, string? format);

    Task CopyHtmlAsync(DocumentSnapshot document, string markdown, string? target);

    Task PrintAsync(DocumentSnapshot document, string markdown);



    /// <summary>
    /// 按图片偏好复制资源并返回 Markdown 路径；未保存文档使用绝对文件 URI。
    /// </summary>
    string? CopyImageToAssets(string? documentPath, string imagePath);

    /// <summary>
    /// 剪贴板位图（截图等）按图片偏好写入资源目录并返回 Markdown 路径；
    /// 无文档路径或字节数为空返回 null。
    /// </summary>
    string? CopyImageBytesToAssets(string? documentPath, byte[] bytes, string extension);

}
