using Avalonia.Media;

using CodeWF.Avalonia.Markdown.Shared.Rendering;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public class MarkdownSpanStyleTests
{
    [Theory]
    [InlineData("#c92a2a", 201, 42, 42)]
    [InlineData("#C92A2A", 201, 42, 42)]
    [InlineData("#fff", 255, 255, 255)]
    [InlineData("red", 255, 0, 0)]
    [InlineData("rgb(201, 42, 42)", 201, 42, 42)]
    public void TryGetColor_parses_supported_values(string value, byte r, byte g, byte b)
    {
        var color = MarkdownSpanStyle.TryGetColor(value);
        Assert.NotNull(color);
        Assert.Equal(r, color!.Value.R);
        Assert.Equal(g, color!.Value.G);
        Assert.Equal(b, color!.Value.B);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("")]
    [InlineData(null)]
    public void TryGetColor_rejects_invalid_values(string? value)
    {
        Assert.Null(MarkdownSpanStyle.TryGetColor(value));
    }

    [Theory]
    [InlineData("18px", 18)]
    [InlineData("86px", 64)] // clamp 上限
    [InlineData("2px", 8)]   // clamp 下限
    public void TryGetFontSize_clamps_to_range(string value, double expected)
    {
        Assert.Equal(expected, MarkdownSpanStyle.TryGetFontSize(value));
    }

    [Theory]
    [InlineData("18")]
    [InlineData("1.5em")]
    [InlineData("big")]
    public void TryGetFontSize_rejects_non_px(string value)
    {
        Assert.Null(MarkdownSpanStyle.TryGetFontSize(value));
    }

    [Fact]
    public void FromDeclarations_keeps_whitelist_only()
    {
        var style = MarkdownSpanStyle.FromDeclarations("color:#c92a2a; font-size:18px; margin:4px; behavior:url(x)");
        Assert.NotNull(style);
        Assert.Equal("#c92a2a", style!.Foreground);
        Assert.Null(style.Background);
        Assert.Equal(18, style.FontSize);
    }

    [Fact]
    public void FromDeclarations_returns_null_when_nothing_valid()
    {
        Assert.Null(MarkdownSpanStyle.FromDeclarations("margin:4px"));
        Assert.Null(MarkdownSpanStyle.FromDeclarations(null));
    }

    [Fact]
    public void Over_inner_wins_and_inherits_outer()
    {
        var outer = new MarkdownSpanStyle(Foreground: "#111111", Background: "#EEEEEE");
        var inner = outer.Over(new MarkdownSpanStyle(Foreground: "#222222"));
        Assert.Equal("#222222", inner.Foreground);
        Assert.Equal("#EEEEEE", inner.Background);
    }

    [Fact]
    public void EnsureReadable_keeps_readable_author_color()
    {
        var authored = Color.FromRgb(0xD5, 0x25, 0x15);
        Assert.Equal(authored, MarkdownSpanStyle.EnsureReadable(authored, null, Colors.Black));
    }

    [Fact]
    public void EnsureReadable_drops_low_contrast_color_on_inferred_dark_surface()
    {
        // 亮色文字 → 推断暗色主题底（#1E1E1E）→ 对比不足时弃用（模拟暗色主题下的浅灰字）。
        var authored = Color.FromRgb(0x3A, 0x3A, 0x3A);
        var surface = Color.FromRgb(0x1E, 0x1E, 0x1E);
        var fallback = Colors.White;
        var result = MarkdownSpanStyle.EnsureReadable(authored, surface, fallback);
        Assert.Equal(fallback, result);
    }

    [Fact]
    public void EnsureReadable_uses_span_background_for_contrast()
    {
        // 深红文字垫深红底：对背景对比不足 → 回退；垫浅黄底：可读 → 保留。
        var authored = Color.FromRgb(0xB2, 0x14, 0x0C);
        var darkRed = Color.FromRgb(0x8E, 0x08, 0x05);
        Assert.Equal(Colors.Black, MarkdownSpanStyle.EnsureReadable(authored, darkRed, Colors.Black));
        var paleYellow = Color.FromRgb(0xFF, 0xF3, 0xBF);
        Assert.Equal(authored, MarkdownSpanStyle.EnsureReadable(authored, paleYellow, Colors.Black));
    }
}
