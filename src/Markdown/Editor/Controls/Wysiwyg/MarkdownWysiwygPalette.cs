using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

/// <summary>
/// 所见即所得视图的字体常量。颜色不在这里：由 <see cref="MarkdownLiveEditorView"/> 按宿主
/// 令牌（<c>CodeWFMarkdownEditor*Brush</c>）解析成 <see cref="MarkdownBlockViewOptions"/> 注入块视图。
/// </summary>
public static class MarkdownWysiwygPalette
{
    public static FontFamily CodeFontFamily { get; } = new("Cascadia Mono, Consolas, Microsoft YaHei UI");

    /// <summary>选区浮动条字号档位（对齐原型）。</summary>
    public static readonly double[] SpanSizes = [12, 14, 16, 18, 22, 28];

    /// <summary>Semi 色板 9 色相（浅色模式）：文字色用中深档、背景色用浅档。</summary>
    public static readonly (string Hue, string[] Hexes)[] SpanSemiHues =
    [
        ("红", ["FEF2ED", "FEDDD2", "FDB7A5", "FB9078", "FA664C", "F93920", "D52515", "B2140C", "8E0805", "6A0103"]),
        ("橙", ["FFF8EA", "FEEECC", "FED998", "FDC165", "FDA633", "FC8800", "D26700", "A84A00", "7E3100", "541D00"]),
        ("琥珀", ["FEFBEB", "FCF5CE", "F9E89E", "F6D86F", "F3C641", "F0B114", "C88A0F", "A0660A", "784606", "502B03"]),
        ("黄", ["FFFDEA", "FEFBCB", "FDF398", "FCE865", "FBDA32", "FAC800", "D0AA00", "A78B00", "7D6A00", "534800"]),
        ("绿", ["ECF7EC", "D0F0D1", "A4E0A7", "7DD182", "5AC262", "3BB346", "30953B", "25772F", "1B5924", "113C18"]),
        ("青", ["E5F7F8", "C2EFF0", "8ADDE2", "58CBD3", "2CB8C5", "05A4B6", "038698", "016979", "004D5B", "00323D"]),
        ("蓝", ["EAF5FF", "CBE7FE", "98CDFD", "65B2FC", "3295FB", "0064FA", "0062D6", "004FB3", "003D8F", "002C6B"]),
        ("紫", ["F3EDF9", "E2D1F4", "C4A7E9", "A67FDD", "885BD2", "6A3AC7", "572FB3", "46259E", "361C8A", "281475"]),
        ("灰", ["F9F9F9", "E6E8EA", "C6CACD", "A7ABB0", "888D92", "6B7075", "555B61", "41464C", "2E3238", "1C1F23"]),
    ];

    /// <summary>文字色档位（中深→深，纸面可读）。</summary>
    public static (string Name, string Hex)[] SpanForegroundShades { get; } =
        [.. SpanSemiHues.SelectMany(hue => new[] { 4, 5, 6, 7 }.Select(shade => (hue.Hue + shade, "#" + hue.Hexes[shade])))];

    /// <summary>背景色档位（极浅→浅，不抢字）。</summary>
    public static (string Name, string Hex)[] SpanBackgroundShades { get; } =
        [.. SpanSemiHues.SelectMany(hue => new[] { 0, 1, 2, 3 }.Select(shade => (hue.Hue + shade, "#" + hue.Hexes[shade])))];

    public static Color ParseHex(string hex)
    {
        var value = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(value.Substring(0, 2), 16),
            Convert.ToByte(value.Substring(2, 2), 16),
            Convert.ToByte(value.Substring(4, 2), 16));
    }
}
