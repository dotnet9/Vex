namespace Vex.Core.Models;

public sealed record TypographyOption(string Name, string? Key)
{
    public string SwatchColor => Key switch
    {
        "OrangeHeart" => "#F59E7F", "ColorfulPurple" or "RosePurple" => "#9574CD",
        "TenderGreen" or "Verdant" or "CuteGreen" => "#62B78A", "RedScarlet" => "#E78294",
        "InkBlack" or "GeekBlack" => "#7C7C87", "Yamabuki" => "#D6AD54", "LanQing" => "#5AB5B4", _ => "#8E9DFB"
    };
    public string NameKey => "Vex.VexL.Typography" + Key;
}
