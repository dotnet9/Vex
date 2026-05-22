namespace Vex.Core.Services;

public interface IHelpService
{
    Task OpenWebsiteAsync();

    Task OpenFeedbackAsync();

    Task OpenDocumentAsync(string fileName);
}
