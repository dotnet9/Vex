namespace Vex.Core.Services;

public interface IHelpService
{
    Task OpenWebsiteAsync();

    Task OpenFeedbackAsync();

    Task OpenDocumentAsync(string fileName);

    Task ShowDocumentWindowAsync(string title, string fileName);

    Task ShowAboutWindowAsync();

    Task ShowUpdatesAsync();
}
