namespace Vex.Core.Services;

public interface IEditorAppearanceState
{
    event EventHandler? Changed;

    string TypographySize { get; }

    string? TypographyTheme { get; }

    void UpdateTypography(string? typographyTheme, string typographySize);
}
