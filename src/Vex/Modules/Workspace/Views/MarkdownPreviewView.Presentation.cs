using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using CodeWF.Avalonia.Markdown.Themes;

namespace Vex.Modules.Workspace.Views;

public partial class MarkdownPreviewView
{
    private (string? Theme, double Size, ThemeVariant Variant)? _presentationTypography;
    private readonly Dictionary<object, object?> _savedPresentationResources = [];
    private readonly HashSet<object> _presentationResourceKeys = [];

    private void ApplyPresentationTypography(bool presenting)
    {
        if (!presenting)
        {
            if (_presentationTypography is null) return;
            foreach (var key in _presentationResourceKeys) PreviewMarkdownViewer.Resources.Remove(key);
            foreach (var entry in _savedPresentationResources) PreviewMarkdownViewer.Resources[entry.Key] = entry.Value;
            _presentationResourceKeys.Clear();
            _savedPresentationResources.Clear();
            _presentationTypography = null;
            return;
        }

        var profile = (_viewModel!.TypographyTheme, _viewModel.BodyFontSize, PreviewMarkdownViewer.ActualThemeVariant);
        if (_presentationTypography == profile) return;
        var article = ArticleTypographyResolver.Find(profile.TypographyTheme);
        if (article is null) return;
        var factor = profile.BodyFontSize / article.BodySize;
        var resources = article.CreateResources();
        // deck 使用文章原始排版；professional.css 的阅读标题装饰只属于普通工作台。
        foreach (var entry in resources.ToArray())
        {
            resources[entry.Key] = entry.Value switch
            {
                double value => value * factor,
                Thickness value when entry.Key is string key && (key.EndsWith("Margin") || key.EndsWith("Padding")) =>
                    new Thickness(value.Left * factor, value.Top * factor, value.Right * factor, value.Bottom * factor),
                _ => entry.Value
            };
        }
        for (var i = 0; i < article.Headings.Length; i++)
        {
            var prefix = $"CodeWFMarkdownHeading{i + 1}";
            var heading = article.Headings[i];
            if (resources.TryGetResource(prefix + "Background", profile.ActualThemeVariant, out var background))
                resources[prefix + "Background"] = background;
            if (resources.TryGetResource(prefix + "BorderBrush", profile.ActualThemeVariant, out var border))
                resources[prefix + "BorderBrush"] = border;
            if (resources.TryGetResource(prefix + "Foreground", profile.ActualThemeVariant, out var foreground))
                resources[prefix + "Foreground"] = foreground;
            if (heading.Foreground.Contains("--fg)", StringComparison.Ordinal))
                resources[prefix + "Foreground"] = PreviewMarkdownViewer.FindResource("VexEditorForegroundBrush");
            var margin = (Thickness)resources[prefix + "Margin"]!;
            resources[$"VexReadHeading{i + 1}AfterQuoteMargin"] = new Thickness(
                margin.Left, Math.Max(0, Thickness.Parse(heading.Margin).Top * factor
                    - Thickness.Parse(article.Quote.Margin).Bottom * factor), margin.Right, margin.Bottom);
        }
        var original = PreviewMarkdownViewer.Resources.ToDictionary(entry => entry.Key, entry => entry.Value);
        foreach (var entry in resources)
        {
            if (_presentationResourceKeys.Add(entry.Key) && original.TryGetValue(entry.Key, out var previous))
                _savedPresentationResources[entry.Key] = previous;
            PreviewMarkdownViewer.Resources[entry.Key] = entry.Value;
        }
        _presentationTypography = profile;
    }
}
