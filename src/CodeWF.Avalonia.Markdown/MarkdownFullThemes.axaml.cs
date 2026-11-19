using global::Avalonia.Markup.Xaml;

namespace CodeWF.Avalonia.Markdown.Themes;

/// <summary>
/// 完整包（CodeWF.Avalonia.Markdown）的样式入口：在基类（Lite.Themes 的控件模板与 12 套排版主题）
/// 之上叠加图片外观与 AvaloniaEdit 原生编辑器模板，无需引入 Fluent / Simple 应用主题。
/// 基础包请直接使用 CodeWF.Avalonia.Markdown.Lite.Themes 的 MarkdownThemes。
/// <para>
/// <see cref="TypographyTheme"/> / <see cref="TypographySize"/> 必须在此 override：
/// 宿主工程（如 Vex）从 NuGet 引用本包时，Avalonia XAML 编译器解析样式根类型只认
/// 本程序集内可见的属性，跨程序集的继承属性解析不到（报 AVLN2000）。
/// </para>
/// </summary>
public class MarkdownFullThemes : MarkdownThemes
{
    public MarkdownFullThemes()
    {
        // 基类加载排版主题；完整包追加图片外观与编辑器模板。
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override string? TypographyTheme
    {
        get => base.TypographyTheme;
        set => base.TypographyTheme = value;
    }

    /// <inheritdoc />
    public override string? TypographySize
    {
        get => base.TypographySize;
        set => base.TypographySize = value;
    }
}
