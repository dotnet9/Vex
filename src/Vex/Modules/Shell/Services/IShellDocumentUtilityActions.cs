using Vex.Core.Models;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Services;

public interface IShellDocumentUtilityActions
{
    void ShowProperties(ShellDocumentInfoViewModel documentInfo);

    Task ExportAsync(DocumentSnapshot document, string markdown, string? format);

    Task CopyHtmlAsync(DocumentSnapshot document, string markdown, string? target);

    Task PrintAsync(DocumentSnapshot document, string markdown);


    void WordCount(ShellDocumentInfoViewModel documentInfo);

    /// <summary>
    /// 把拖入的本地图片复制到文档所在目录的 assets/ 下，返回插入 Markdown 的相对路径；
    /// 文档未保存或无目录时返回 null。
    /// </summary>
    string? CopyImageToAssets(string? documentPath, string imagePath);

}
