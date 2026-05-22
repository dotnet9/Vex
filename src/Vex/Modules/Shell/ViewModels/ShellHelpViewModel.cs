using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

// 处理帮助菜单入口；主 Shell 只负责组合，不直接关心外部文档和网站打开细节。
public sealed class ShellHelpViewModel
{
    private readonly IHelpService _helpService;
    private readonly ShellDialogsViewModel _dialogs;
    private readonly IShellStatusPublisher _statusPublisher;

    public ShellHelpViewModel(
        IHelpService helpService,
        ShellDialogsViewModel dialogs,
        IShellStatusPublisher statusPublisher)
    {
        _helpService = helpService;
        _dialogs = dialogs;
        _statusPublisher = statusPublisher;
    }

    public async Task OpenHelpTopic(string? topic)
    {
        switch (topic)
        {
            case "changelog":
                await _helpService.OpenDocumentAsync("CHANGELOG.zh-CN.md");
                _statusPublisher.PublishResource(VexL.StatusOpenedChangelog);
                break;
            case "quick-start":
                await _helpService.OpenDocumentAsync("QuickStart.zh-CN.md");
                _statusPublisher.PublishResource(VexL.StatusOpenedQuickStart);
                break;
            case "thanks":
                await _helpService.OpenDocumentAsync("ACKNOWLEDGEMENTS.zh-CN.md");
                _statusPublisher.PublishResource(VexL.StatusOpenedAcknowledgements);
                break;
            case "website":
                await _helpService.OpenWebsiteAsync();
                break;
            case "feedback":
                await _helpService.OpenFeedbackAsync();
                break;
            case "about":
                // 关于面板属于 Shell 浮层，具体显示状态交给 ShellDialogsViewModel 维护。
                _dialogs.ShowAboutPanel();
                _statusPublisher.PublishResource(VexL.StatusAboutVex);
                break;
            default:
                _statusPublisher.PublishResourceFormat(VexL.StatusHelpQueuedFormat, topic ?? "Help");
                break;
        }
    }
}
