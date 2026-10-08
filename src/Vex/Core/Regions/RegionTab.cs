using Avalonia;

namespace Vex.Core.Regions;

public static class RegionTab
{
    public static readonly AttachedProperty<string?> HeaderKeyProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("HeaderKey", typeof(RegionTab));

    public static readonly AttachedProperty<string?> IconDataProperty =
        AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>("IconData", typeof(RegionTab));

    public static string? GetIconData(AvaloniaObject element) => element.GetValue(IconDataProperty);

    public static void SetIconData(AvaloniaObject element, string? value) => element.SetValue(IconDataProperty, value);

    public static string? GetHeaderKey(AvaloniaObject element)
    {
        return element.GetValue(HeaderKeyProperty);
    }

    public static void SetHeaderKey(AvaloniaObject element, string? value)
    {
        element.SetValue(HeaderKeyProperty, value);
    }
}
