using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Services;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class TypewriterScrollTests
{
    [Theory]
    [InlineData(100, 200, 0)]
    [InlineData(500, 200, 400)]
    [InlineData(40, 200, 0)]
    [InlineData(100, 0, 0)]
    public void GetTargetOffset_UsesContentRelativeCaret(double caret, double viewport, double expected)
    {
        Assert.Equal(expected, TypewriterScroll.GetTargetOffset(caret, viewport));
    }

    [Collection("AvaloniaPlatform")]
    public sealed class SourceEditorTests(AvaloniaPlatformFixture platform)
    {
        [Fact]
        public void SourceCaretRectangle_UsesContentCoordinates()
        {
            platform.Run(() =>
            {
                Application.Current!.Resources["FontFamilyNormal"] = FontFamily.Default;
                Application.Current.Resources["ContentControlThemeFontFamily"] = FontFamily.Default;
                Application.Current.Resources["ControlContentThemeFontFamily"] = FontFamily.Default;
                Application.Current.Resources["FontSizeNormal"] = 14d;
                Application.Current.Resources["ControlContentThemeFontSize"] = 14d;
                Application.Current.Resources["FontWeightNormal"] = FontWeight.Normal;
                Application.Current.Styles.Add(new global::Avalonia.Markup.Xaml.Styling.StyleInclude(
                    new Uri("avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml"))
                {
                    Source = new Uri("avares://AvaloniaEdit/Themes/Simple/AvaloniaEdit.xaml")
                });
                var view = new MarkdownEditorView();
                var window = new Window { Width = 600, Height = 400, Content = view };
                window.Show();
                try
                {
                    var lines = Enumerable.Range(0, 200).Select(index => $"第 {index} 行正文").ToArray();
                    view.Text = string.Join('\n', lines);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var editor = view.Editor;
                    editor.CaretOffset = lines.Take(30).Sum(line => line.Length + 1);
                    editor.TextArea.TextView.EnsureVisualLines();
                    window.UpdateLayout();
                    var caret = editor.TextArea.Caret.CalculateCaretRectangle();
                    var lineTop = editor.TextArea.TextView.GetVisualTopByDocumentLine(editor.TextArea.Caret.Line);
                    Assert.Equal(lineTop, caret.Top, 3);
                    Assert.Equal(Math.Max(0, caret.Center.Y - editor.Bounds.Height / 2),
                        TypewriterScroll.GetTargetOffset(caret.Center.Y, editor.Bounds.Height), 3);
                }
                finally
                {
                    window.Close();
                }
            });
        }
    }
}
