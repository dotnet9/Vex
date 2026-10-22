using CodeWF.Avalonia.Controls.Themes;
using Avalonia.Media;

namespace Vex.Core.Models;

public sealed record ThemeOption(string Key, StudioThemeMode Mode);

/// <summary>界面配色选项（规则 §13 注册表驱动：内置九套 + 用户配色，名称/描述为注册表直出文案）。</summary>
public sealed record PaletteOption(
    string Key, string Name, string Purpose, bool IsRecommended, IBrush Swatch, StudioPaletteDefinition Definition);
