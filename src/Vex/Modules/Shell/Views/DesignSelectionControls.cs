using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CodeWF.Toolkit.Lang;

namespace Vex.Modules.Shell.Views;

internal static class DesignSelectionControls
{
    internal static string Text(string key) => I18nManager.Instance.GetResource(key.StartsWith("Vex.") ? key : "Vex.VexL." + key);
    internal static void Resource(AvaloniaObject target, AvaloniaProperty property, StyledElement owner, string key) =>
        target.Bind(property, owner.GetResourceObservable(key));
    internal static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, FontSize = 12, Padding = new Thickness(8, 5), MinHeight = 30 };
        button.Classes.Add("wb-quiet"); button.Click += (_, _) => action(); return button;
    }
    internal static TextBlock Label(string text, double size = 12) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
    internal static void Navigation(Panel panel, Func<int> columns)
    {
        panel.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)) return;
            var buttons = panel.Children.OfType<Button>().Where(button => button.IsVisible).ToArray();
            var target = (e.Source as Visual)?.GetVisualAncestors().Prepend(e.Source as Visual).OfType<Button>().FirstOrDefault();
            var current = Array.IndexOf(buttons, target);
            if (current < 0) return;
            var next = e.Key switch { Key.Home => 0, Key.End => buttons.Length - 1, Key.Left => current - 1,
                Key.Right => current + 1, Key.Up => current - columns(), _ => current + columns() };
            buttons[Math.Clamp(next, 0, buttons.Length - 1)].Focus();
            buttons[Math.Clamp(next, 0, buttons.Length - 1)].BringIntoView();
            e.Handled = true;
        });
    }
}
