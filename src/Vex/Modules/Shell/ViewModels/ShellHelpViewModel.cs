using CodeWF.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

// 处理帮助菜单入口；主 Shell 只负责组合，不直接关心外部文档和网站打开细节。
public sealed class ShellHelpViewModel
{
    private readonly IHelpService _helpService;
    private readonly ShellDialogsViewModel _dialogs;
    private readonly IEventBus _eventBus;

    public ShellHelpViewModel(IHelpService helpService, ShellDialogsViewModel dialogs, IEventBus eventBus)
    {
        _helpService = helpService;
        _dialogs = dialogs;
        _eventBus = eventBus;
    }

    public async Task OpenHelpTopic(string? topic)
    {
        switch (topic)
        {
            case "changelog":
                await _helpService.OpenDocumentAsync("CHANGELOG.zh-CN.md");
                SetStatus("Opened changelog.");
                break;
            case "quick-start":
                await _helpService.OpenDocumentAsync("QuickStart.zh-CN.md");
                SetStatus("Opened quick start.");
                break;
            case "thanks":
                await _helpService.OpenDocumentAsync("ACKNOWLEDGEMENTS.zh-CN.md");
                SetStatus("Opened acknowledgements.");
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
                SetStatus("About Vex.");
                break;
            default:
                SetStatus($"{topic ?? "Help"} is queued for implementation.");
                break;
        }
    }

    private void SetStatus(string message)
    {
        _eventBus.Publish(new WorkspaceStatusChangedCommand(message));
    }
}
