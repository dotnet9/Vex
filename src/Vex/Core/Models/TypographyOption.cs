namespace Vex.Core.Models;

public sealed record TypographyOption(string Name, string? Key)
{
    public string NameKey => "Vex.VexL.Typography" + Key;
}
