using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

// 处理帮助菜单入口；Shell 只协调菜单动作，文档和网站打开细节收口到帮助服务。
public sealed class ShellHelpViewModel
{
    private readonly IHelpService _helpService;
    private readonly ShellDialogsViewModel _dialogs;
    private readonly IShellStatusPublisher _statusPublisher;
    private readonly IAppLocalizer _localizer;

    public ShellHelpViewModel(
        IHelpService helpService,
        ShellDialogsViewModel dialogs,
        IShellStatusPublisher statusPublisher,
        IAppLocalizer localizer)
    {
        _helpService = helpService;
        _dialogs = dialogs;
        _statusPublisher = statusPublisher;
        _localizer = localizer;
    }

    public async Task OpenHelpTopic(string? topic)
    {
        try
        {
            switch (topic)
            {
                case "changelog":
                    await _helpService.ShowLocalizedDocumentWindowAsync(
                        _localizer.Get(VexL.Changelog),
                        "CHANGELOG",
                        _localizer.Culture.Name);
                    _statusPublisher.PublishResource(VexL.StatusOpenedChangelog);
                    break;
                case "thanks":
                    await _helpService.ShowDocumentWindowAsync(_localizer.Get(VexL.Thanks), "Thanks.md");
                    _statusPublisher.PublishResource(VexL.StatusOpenedAcknowledgements);
                    break;
                case "website":
                    await _helpService.OpenWebsiteAsync();
                    break;
                case "feedback":
                    await _helpService.OpenFeedbackAsync();
                    break;
                case "about":
                    await _helpService.ShowAboutWindowAsync();
                    _statusPublisher.PublishResource(VexL.StatusAboutVex);
                    break;
                default:
                    _statusPublisher.PublishResourceFormat(VexL.StatusHelpQueuedFormat, topic ?? "Help");
                    break;
            }
        }
        catch (Exception exception)
        {
            _dialogs.ShowError(VexL.ErrorMessageCannotOpenHelpFormat, exception, GetHelpTopicDisplayName(topic));
        }
    }

    private string GetHelpTopicDisplayName(string? topic)
    {
        return topic switch
        {
            "changelog" => _localizer.Get(VexL.Changelog),
            "thanks" => _localizer.Get(VexL.Thanks),
            "website" => _localizer.Get(VexL.Website),
            "feedback" => _localizer.Get(VexL.Feedback),
            "about" => _localizer.Get(VexL.About),
            _ => topic ?? "Help"
        };
    }
}
