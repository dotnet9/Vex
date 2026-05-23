namespace Vex.Core.Services;

public interface IHelpService
{
    Task OpenWebsiteAsync();

    Task OpenFeedbackAsync();

    Task OpenDocumentAsync(string fileName);

    Task OpenLocalizedDocumentAsync(string documentName, string cultureName);
}
