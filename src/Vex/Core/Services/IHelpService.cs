namespace Vex.Core.Services;

public interface IHelpService
{
    Task OpenWebsiteAsync();

    Task OpenFeedbackAsync();

    Task OpenDocumentAsync(string fileName);

    Task OpenLocalizedDocumentAsync(string documentName, string cultureName);

    Task ShowDocumentWindowAsync(string title, string fileName);

    Task ShowLocalizedDocumentWindowAsync(string title, string documentName, string cultureName);

    Task ShowAboutWindowAsync();
}
