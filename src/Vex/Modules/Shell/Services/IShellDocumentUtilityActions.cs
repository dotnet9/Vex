using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public interface IShellDocumentUtilityActions
{
    void ShowProperties(ShellDocumentInfoViewModel documentInfo);

    Task<bool> ExportAsync(DocumentSnapshot document, string markdown, string? format);

    Task CopyHtmlAsync(DocumentSnapshot document, string markdown, string? target);

    Task PrintAsync(DocumentSnapshot document, string markdown);


    void WordCount(ShellDocumentInfoViewModel documentInfo);

    /// <summary>
    /// 按图片偏好复制资源并返回 Markdown 路径；未保存文档使用绝对文件 URI。
    /// </summary>
    string? CopyImageToAssets(string? documentPath, string imagePath);

}
