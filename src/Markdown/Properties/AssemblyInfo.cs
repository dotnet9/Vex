using System.Runtime.CompilerServices;

using global::Avalonia.Metadata;

[assembly: XmlnsPrefix("https://codewf.com", "markdown")]

// 渲染引擎（CodeWF.Avalonia.Markdown.Lite）
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Controls")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Rendering")]

// 能力包（图片 / 数学 / Mermaid）与编辑器控件同属本包
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Images")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.MathRendering")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Mermaid")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Editor")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Editor.Controls")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Editor.Services")]
[assembly: XmlnsDefinition("https://codewf.com", "CodeWF.Avalonia.Markdown.Themes")]

[assembly: InternalsVisibleTo("CodeWF.Avalonia.Content.Tests")]
